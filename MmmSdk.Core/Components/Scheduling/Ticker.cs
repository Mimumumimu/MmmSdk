using System.Runtime.ExceptionServices;

namespace MmmSdk.Core.Components.Scheduling;

/// <summary>アプリ全体で 1 つの、定期処理の合図 (<see cref="ITicker"/>の実装)</summary>
/// <param name="timeProvider">現在時刻・タイマーの提供元</param>
/// <remarks>
/// <para>
/// タイマーは 1 つで、最初の登録で作り、破棄で止める。登録ごとの次の時刻を求め、いちばん早い時刻に、単発のタイマーを掛け直す。
/// 毎秒起きる固定のタイマーではないので、登録が毎分だけなら、1 分に 1 回しか起きない。
/// 時刻は、毎回、そのときの時計から求め直す。システム時刻の変更や、タイマーのずれ、スリープ明けに追従するため。
/// </para>
/// <para>
/// 毎分の合図 (<see cref="TickTiming.OnTheMinute"/>・<see cref="TickTiming.Staggered"/>)は、登録した分には渡さない。同じ分に 2 回渡さない。
/// 登録した直後に動かしたい処理は、<see cref="TickOptions.RunImmediately"/> を使う。
/// </para>
/// </remarks>
public sealed class Ticker(TimeProvider timeProvider) : ITicker, IDisposable
{
    /// <summary>失敗が続いたときに、次の合図までの時間を延ばす上限。仮</summary>
    /// <remarks>DB に届かない間、多数の端末が同じ間隔で読みに行き続けないようにするため。戻ったあと、この時間のうちに気づけるように、上限を置く。</remarks>
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    /// <summary><see cref="TickTiming.Interval"/> の間隔の下限。登録の間違いで、タイマーが休みなく回るのを防ぐ</summary>
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(1);

    /// <summary>状態を守るロック</summary>
    /// <remarks>登録 1 件のロック (<c>Subscription._sync</c>)と一緒に取るときは、必ずこちらを先に取る。</remarks>
    private readonly Lock _gate = new();

    /// <summary>登録されている処理</summary>
    private readonly List<Subscription> _subscriptions = [];

    /// <summary>タイマー。最初の登録の前と、破棄後は null</summary>
    private ITimer? _timer;

    /// <summary>破棄したか</summary>
    private bool _disposed;

    /// <inheritdoc />
    public ITickSubscription Subscribe(Func<DateTime, Task> handler, TickOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return Subscribe(
            async now =>
            {
                await handler(now).ConfigureAwait(false);
                return TickResult.Done;
            },
            options);
    }

    /// <inheritdoc />
    public ITickSubscription Subscribe(Func<DateTime, Task<TickResult>> handler, TickOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        options ??= new TickOptions();
        if (options.Timing == TickTiming.Interval)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(options.Interval, MinimumInterval, nameof(options));
        }

        var context = options.OnCallerContext
            ? SynchronizationContext.Current ?? throw new InvalidOperationException("UI スレッドで処理を動かすには、UI スレッドから登録してください。")
            : null;
        var now = timeProvider.GetUtcNow();
        var offset = options.Timing == TickTiming.Staggered ? TimeSpan.FromSeconds(Random.Shared.Next(1, 60)) : TimeSpan.Zero;
        var subscription = new Subscription(this, timeProvider, handler, options, context, now, offset);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _subscriptions.Add(subscription);
            _timer ??= timeProvider.CreateTimer(_ => OnTimer(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            RescheduleLocked(now);
        }

        if (options.RunImmediately)
        {
            subscription.Run();
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
            _timer?.Dispose();
            _timer = null;
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Dispose();
        }
    }

    /// <summary>タイマーを、いちばん早い時刻に掛け直す</summary>
    /// <remarks>登録の間隔を変えたとき、すぐ動かしたいときに使う。</remarks>
    private void Reschedule()
    {
        lock (_gate)
        {
            RescheduleLocked(timeProvider.GetUtcNow());
        }
    }

    /// <summary>タイマーを、いちばん早い時刻に掛け直す (<c>_gate</c> を取ったまま呼ぶ)</summary>
    /// <param name="now">現在の日時</param>
    private void RescheduleLocked(DateTimeOffset now)
    {
        if (_disposed || _timer is null)
        {
            return;
        }

        DateTimeOffset? earliest = null;
        foreach (var subscription in _subscriptions)
        {
            if (subscription.GetDue(now) is { } due && (earliest is null || due < earliest))
            {
                earliest = due;
            }
        }

        _timer.Change(earliest is { } next ? (next > now ? next - now : TimeSpan.Zero) : Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>タイマーが来たら、時刻が来た登録に合図を渡して、次の時刻に掛け直す (戻りは待たない)</summary>
    /// <remarks>タイマーが時刻より少し早く来たときは、どの登録も時刻前なので、何も渡さず、残りの時間で掛け直す。</remarks>
    private void OnTimer()
    {
        List<Subscription>? due = null;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            foreach (var subscription in _subscriptions)
            {
                if (subscription.TryTake(now))
                {
                    (due ??= []).Add(subscription);
                }
            }
            RescheduleLocked(now);
        }

        if (due is null)
        {
            return;
        }

        foreach (var subscription in due)
        {
            subscription.Run();
        }
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
        Ticker owner,
        TimeProvider timeProvider,
        Func<DateTime, Task<TickResult>> handler,
        TickOptions options,
        SynchronizationContext? context,
        DateTimeOffset registeredAt,
        TimeSpan offset) : ITickSubscription
    {
        /// <summary>状態を守るロック</summary>
        private readonly Lock _sync = new();

        /// <summary>毎分の合図で、最後に渡した分で、秒以下を切り捨てた時刻。同じ分に 2 回渡さない。登録した分から始める</summary>
        private DateTimeOffset _lastMinute = MinuteTime.TruncateToMinute(registeredAt);

        /// <summary>間隔の合図で、最後に渡した時刻 (登録した時刻から始める)</summary>
        private DateTimeOffset _lastFiredAt = registeredAt;

        /// <summary>間隔の合図の間隔</summary>
        private TimeSpan _interval = options.Interval;

        /// <summary>すぐ動かす指定があるか</summary>
        private bool _wake;

        /// <summary>続けて失敗した回数。成功すると 0 に戻る</summary>
        private int _failures;

        /// <summary>失敗が続いているとき、これより前には合図を渡さない時刻。続いていなければ null</summary>
        private DateTimeOffset? _backoffUntil;

        /// <summary>処理を動かしている最中か。最中なら、次の合図を飛ばす</summary>
        private bool _running;

        /// <summary>最後に処理を動かし始めた (動かした扱いにした)時刻。まだ無ければ null</summary>
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
        public void WakeNow()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }
                _wake = true;
            }

            owner.Reschedule();
        }

        /// <inheritdoc />
        public void SetInterval(TimeSpan interval)
        {
            if (options.Timing != TickTiming.Interval)
            {
                throw new InvalidOperationException("間隔を変えられるのは、間隔の合図 (TickTiming.Interval)の登録だけです。");
            }
            ArgumentOutOfRangeException.ThrowIfLessThan(interval, MinimumInterval, nameof(interval));

            lock (_sync)
            {
                if (_disposed || _interval == interval)
                {
                    return;
                }
                _interval = interval;
            }

            owner.Reschedule();
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
        }

        /// <summary>次に合図を渡す時刻を求める</summary>
        /// <param name="now">現在の日時</param>
        /// <returns>次に渡す時刻。解除済みなら null。<paramref name="now"/> 以前なら、すぐ渡す</returns>
        public DateTimeOffset? GetDue(DateTimeOffset now)
        {
            lock (_sync)
            {
                return DueLocked(now);
            }
        }

        /// <summary>時刻が来ていれば、渡した印を付ける</summary>
        /// <param name="now">現在の日時</param>
        /// <returns>時刻が来ていて、これから <see cref="Run"/> を呼ぶべきなら true</returns>
        /// <remarks>
        /// 処理を動かすかどうか (<see cref="TickOptions.IsActive"/> や、前の処理が続いているか)にかかわらず、印を付ける。
        /// 動かさなかったときも、次の時刻に進めるため。次の時刻に進めないと、同じ時刻でタイマーが休みなく回る。
        /// </remarks>
        public bool TryTake(DateTimeOffset now)
        {
            lock (_sync)
            {
                if (DueLocked(now) is not { } due || due > now)
                {
                    return false;
                }

                _wake = false;
                _lastFiredAt = now;
                _lastMinute = MinuteTime.TruncateToMinute(now);
                return true;
            }
        }

        /// <summary>条件を確かめて、処理を動かす (戻りは待たない)</summary>
        public async void Run()
        {
            try
            {
                if (options.IsActive is { } isActive && !isActive())
                {
                    return;
                }

                if (!TryBegin())
                {
                    return;
                }

                TickResult? result = null;
                try
                {
                    result = await InvokeAsync(timeProvider.GetLocalNow().DateTime).ConfigureAwait(false);
                }
                finally
                {
                    End(result);
                }
            }
            catch (OperationCanceledException)
            {
                // 取り消された
            }
            catch (Exception ex)
            {
                // 予想外の失敗 (バグ)は、握りつぶさず、アプリの未処理例外の受け皿へ渡す。async void の中で投げ直すと、スレッドに依らず渡るとは限らないので、別のスレッドで投げる
                ThreadPool.QueueUserWorkItem(static error => ExceptionDispatchInfo.Capture(error).Throw(), ex, preferLocal: false);
            }
        }

        /// <summary>次に合図を渡す時刻を求める (<c>_sync</c> を取ったまま呼ぶ)</summary>
        /// <param name="now">現在の日時</param>
        /// <returns>次に渡す時刻。解除済みなら null</returns>
        private DateTimeOffset? DueLocked(DateTimeOffset now)
        {
            if (_disposed)
            {
                return null;
            }

            if (_wake)
            {
                return now;
            }

            DateTimeOffset due;
            if (options.Timing == TickTiming.Interval)
            {
                // 時計を戻したときに、戻した分だけ待たされないよう、1 間隔より先にはしない
                var next = _lastFiredAt + _interval;
                due = next > now + _interval ? now + _interval : next;
            }
            else
            {
                // 毎分の合図。最後に渡した分の次の分に、ずらした秒を足す。時計を戻したときは、今の分の次の分までに縮める
                // 過ぎていれば、すぐ 1 回だけ渡す。スリープ明けに、たまった分をまとめて渡さない
                var current = MinuteTime.TruncateToMinute(now);
                var minute = _lastMinute + TimeSpan.FromMinutes(1);
                if (minute > current + TimeSpan.FromMinutes(1))
                {
                    minute = current + TimeSpan.FromMinutes(1);
                }
                due = minute + offset;
            }

            // 失敗が続いている間は、延ばした時刻より前に渡さない。時計を戻したときに、上限より先にならないようにする
            if (_backoffUntil is { } until)
            {
                var latest = now + MaxBackoff;
                var wait = until > latest ? latest : until;
                if (wait > due)
                {
                    due = wait;
                }
            }
            return due;
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

        /// <summary>処理を終えた印を付け、失敗が続いているかを覚える</summary>
        /// <param name="result">処理の結果。例外で終わったときなど、結果が無いときは null で、失敗の状態を変えない</param>
        /// <remarks>
        /// 最後に動かした時刻は、始めた時刻のまま変えない。処理が遅いときに、終わった時刻から数えると、次の合図が飛ばされて、間隔が延びるため。重なりは、最中の印で防ぐ。
        /// 失敗のたびに、次の合図までの時間を 2 倍にして、上限 (<see cref="MaxBackoff"/>)で止める。延ばした時間の半分から全部のうちのどこかに、ばらつかせる。
        /// </remarks>
        private void End(TickResult? result)
        {
            lock (_sync)
            {
                _running = false;
                switch (result)
                {
                    case TickResult.Done:
                        _failures = 0;
                        _backoffUntil = null;
                        break;
                    case TickResult.Failed:
                        _failures = Math.Min(_failures + 1, 30);
                        var basePeriod = options.Timing == TickTiming.Interval ? _interval : TimeSpan.FromMinutes(1);
                        var grown = TimeSpan.FromTicks((long)Math.Min(basePeriod.Ticks * Math.Pow(2, _failures), MaxBackoff.Ticks));
                        var delay = grown < basePeriod ? basePeriod : grown;
                        _backoffUntil = timeProvider.GetUtcNow() + delay * (0.5 + 0.5 * Random.Shared.NextDouble());
                        break;
                    default:
                        break;
                }
            }

            owner.Reschedule();
        }

        /// <summary>処理を動かす</summary>
        /// <param name="now">現在の日時</param>
        /// <returns>処理の結果を表すタスク</returns>
        private Task<TickResult> InvokeAsync(DateTime now)
        {
            if (context is null)
            {
                // スレッドプールで動かす。呼んだスレッド (UI スレッド・合図を渡すタイマー)で、処理の同期の部分を動かさない
                return Task.Run(() => handler(now));
            }

            // UI スレッドで状態を持つ処理は、登録したスレッドへ戻して動かす
            var completion = new TaskCompletionSource<TickResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Post(
                async _ =>
                {
                    try
                    {
                        completion.SetResult(await handler(now));
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
