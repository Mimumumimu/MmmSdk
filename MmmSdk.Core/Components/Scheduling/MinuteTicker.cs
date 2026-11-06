using System.Runtime.ExceptionServices;

namespace MmmSdk.Core.Components.Scheduling;

/// <summary>アプリ全体で 1 つの、毎分の合図 (<see cref="IMinuteTicker"/>の実装)</summary>
/// <param name="timeProvider">現在時刻・タイマーの提供元</param>
/// <remarks>
/// タイマーは <see cref="MinuteScheduler"/> を 1 つだけ持つ。最初の登録で動き出し、破棄で止まる。
/// 登録した時点の分には、合図を渡さない。登録した直後に動かしたい処理は、<see cref="MinuteTickOptions.RunImmediately"/> を使う。
/// </remarks>
public sealed class MinuteTicker(TimeProvider timeProvider) : IMinuteTicker, IDisposable
{
    /// <summary>状態を守るロック</summary>
    private readonly Lock _gate = new();

    /// <summary>登録されている処理</summary>
    private readonly List<Subscription> _subscriptions = [];

    /// <summary>毎分の呼び出し。最初の登録の前は null</summary>
    private MinuteScheduler? _scheduler;

    /// <summary>破棄したか</summary>
    private bool _disposed;

    /// <inheritdoc />
    public IMinuteTickSubscription Subscribe(Func<DateTime, Task> handler, MinuteTickOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        options ??= new MinuteTickOptions();

        var context = options.OnCallerContext
            ? SynchronizationContext.Current ?? throw new InvalidOperationException("UI スレッドで処理を動かすには、UI スレッドから登録してください。")
            : null;
        var now = timeProvider.GetLocalNow().DateTime;
        // 登録した分には合図を渡さない。00 秒に近い登録でも、タイマーの最初の呼び出しと二重にならない
        var offset = options.Timing == MinuteTickTiming.Staggered ? TimeSpan.FromSeconds(Random.Shared.Next(1, 60)) : TimeSpan.Zero;
        var subscription = new Subscription(this, timeProvider, handler, options, context, MinuteScheduler.TruncateToMinute(now), offset);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _subscriptions.Add(subscription);
            if (_scheduler is null)
            {
                _scheduler = new MinuteScheduler(timeProvider);
                _scheduler.Start(OnMinute);
            }
        }

        if (options.RunImmediately)
        {
            subscription.Run(TimeSpan.Zero);
        }
        return subscription;
    }

    /// <summary>止める</summary>
    public void Dispose()
    {
        Subscription[] subscriptions;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            subscriptions = [.. _subscriptions];
            _subscriptions.Clear();
        }

        _scheduler?.Dispose();
        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
    }

    /// <summary>毎分、登録された全員へ合図を渡す (戻りは待たない)</summary>
    /// <param name="now">現在の日時</param>
    /// <returns>渡し終えたことを表すタスク</returns>
    private Task OnMinute(DateTime now)
    {
        var minute = MinuteScheduler.TruncateToMinute(now);
        Subscription[] subscriptions;
        lock (_gate)
        {
            subscriptions = [.. _subscriptions];
        }

        foreach (var subscription in subscriptions)
        {
            subscription.OnMinute(minute);
        }
        return Task.CompletedTask;
    }

    /// <summary>登録を外す</summary>
    /// <param name="subscription">外す登録</param>
    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }

    /// <summary>登録 1 件の状態と、処理の動かし方</summary>
    private sealed class Subscription(
        MinuteTicker owner,
        TimeProvider timeProvider,
        Func<DateTime, Task> handler,
        MinuteTickOptions options,
        SynchronizationContext? context,
        DateTime lastMinute,
        TimeSpan offset) : IMinuteTickSubscription
    {
        /// <summary>状態を守るロック</summary>
        private readonly Lock _sync = new();

        /// <summary>解除で、合図を渡す前の待ちを止める</summary>
        /// <remarks>タイマーも待ちのハンドルも持たないので、破棄はしない。破棄すると、待っている処理が、あとからトークンを取り出すときに落ちる。</remarks>
        private readonly CancellationTokenSource _cancellation = new();

        /// <summary>最後に合図を渡した分で、秒以下を切り捨てた時刻。同じ分に 2 回渡さない</summary>
        private DateTime _lastMinute = lastMinute;

        /// <summary>処理を動かしている最中か。最中なら、次の合図を飛ばす</summary>
        private bool _running;

        /// <summary>最後に処理を動かした (動かした扱いにした)時刻。まだ無ければ null</summary>
        private DateTimeOffset? _lastRanAt;

        /// <summary>解除したか</summary>
        private bool _disposed;

        /// <inheritdoc />
        public void MarkRan()
        {
            lock (_sync)
            {
                _lastRanAt = timeProvider.GetUtcNow();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _disposed = true;
            }

            owner.Remove(this);
            _cancellation.Cancel();
        }

        /// <summary>毎分の合図を受ける (同じ分や過去の分、解除後は飛ばす)</summary>
        /// <param name="minute">今の分</param>
        public void OnMinute(DateTime minute)
        {
            lock (_sync)
            {
                // 同じ分だけ飛ばす。過去の分を飛ばすと、時計を戻したときに、その分が過ぎるまで合図が止まる
                if (_disposed || minute == _lastMinute)
                {
                    return;
                }
                _lastMinute = minute;
            }

            Run(offset);
        }

        /// <summary>待ってから処理を動かす (戻りは待たない)</summary>
        /// <param name="delay">動かすまでの待ち。ずらした合図の秒</param>
        public async void Run(TimeSpan delay)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, timeProvider, _cancellation.Token).ConfigureAwait(false);
                }

                if (options.IsActive is { } isActive && !isActive())
                {
                    return;
                }

                if (!TryBegin())
                {
                    return;
                }

                try
                {
                    await InvokeAsync(timeProvider.GetLocalNow().DateTime).ConfigureAwait(false);
                }
                finally
                {
                    End();
                }
            }
            catch (OperationCanceledException)
            {
                // 解除された
            }
            catch (Exception ex)
            {
                // 予想外の失敗 (バグ)は、握りつぶさず、アプリの未処理例外の受け皿へ渡す。async void の中で投げ直すと、スレッドに依らず渡るとは限らないので、別のスレッドで投げる
                ThreadPool.QueueUserWorkItem(static error => ExceptionDispatchInfo.Capture(error).Throw(), ex, preferLocal: false);
            }
        }

        /// <summary>動かしてよければ、動かし始めた印を付ける</summary>
        /// <returns>動かしてよければ true。解除済み・前の処理が最中・直前の実行から間隔が足りないときは false</returns>
        private bool TryBegin()
        {
            lock (_sync)
            {
                if (_disposed || _running)
                {
                    return false;
                }

                var now = timeProvider.GetUtcNow();
                if (options.MinimumGap > TimeSpan.Zero && _lastRanAt is { } last && now - last < options.MinimumGap)
                {
                    return false;
                }

                _running = true;
                _lastRanAt = now;
                return true;
            }
        }

        /// <summary>処理を終えた印を付ける</summary>
        private void End()
        {
            lock (_sync)
            {
                _running = false;
                _lastRanAt = timeProvider.GetUtcNow();
            }
        }

        /// <summary>処理を動かす</summary>
        /// <param name="now">現在の日時</param>
        /// <returns>処理の完了を表すタスク</returns>
        private Task InvokeAsync(DateTime now)
        {
            if (context is null)
            {
                // スレッドプールで動かす。呼んだスレッド (UI スレッド・合図を渡すループ)で、処理の同期の部分を動かさない
                return Task.Run(() => handler(now));
            }

            // UI スレッドで状態を持つ処理は、登録したスレッドへ戻して動かす
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Post(
                async _ =>
                {
                    try
                    {
                        await handler(now);
                        completion.SetResult();
                    }
                    catch (Exception ex)
                    {
                        completion.SetException(ex);
                    }
                },
                null);
            return completion.Task;
        }
    }
}
