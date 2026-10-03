namespace MmmSdk.Core.Components.Scheduling;

/// <summary>システム時刻の毎分 00 秒に、処理を呼ぶ</summary>
/// <param name="timeProvider">現在時刻・タイマーの提供元</param>
/// <remarks>
/// 開始直後に 1 回、以後は毎分 00 秒に呼ぶ。固定間隔のタイマーではなく、次の 00 秒までの残り時間をその都度計算する単発タイマーを掛け直す
/// （システム時刻の変更や、タイマーのずれに追従するため）。タイマーが 00 秒より少し早く来たときは、まだ前の分なので呼ばず、次の掛け直しに任せる
/// （同じ分に 2 回呼ばない）。呼ばれる処理は、タイマーのスレッドで動く。
/// </remarks>
public sealed class MinuteScheduler(TimeProvider timeProvider) : IDisposable
{
    /// <summary>状態を守るロック</summary>
    private readonly Lock _gate = new();

    /// <summary>タイマー。開始前・破棄後は null</summary>
    private ITimer? _timer;

    /// <summary>毎分呼ぶ処理</summary>
    private Func<DateTime, Task>? _onMinute;

    /// <summary>最後に処理を呼んだ分（秒以下を切り捨てた時刻）</summary>
    /// <remarks>タイマーが 00 秒より少し早く来たときに、同じ分を 2 回呼ばないために使う。</remarks>
    private DateTime? _lastCalledMinute;

    /// <summary>破棄したか</summary>
    private bool _disposed;

    /// <summary>始める</summary>
    /// <param name="onMinute">毎分呼ぶ処理。現在の日時を受け取る。タイマーのスレッドから呼ぶので、UI スレッドへの切り替えは渡す側で行う</param>
    /// <exception cref="InvalidOperationException">すでに開始している。</exception>
    /// <exception cref="ObjectDisposedException">破棄済み。</exception>
    public void Start(Func<DateTime, Task> onMinute)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_timer is not null)
            {
                throw new InvalidOperationException("すでに開始しています。");
            }

            _onMinute = onMinute;
            // 1 回目の呼び出しが掛け直しで _timer を使うので、代入してから動かす
            _timer = timeProvider.CreateTimer(_ => OnTick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>秒以下を切り捨てる</summary>
    /// <param name="value">日時</param>
    /// <returns>秒以下を 0 にした日時</returns>
    public static DateTime TruncateToMinute(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    /// <summary>止める</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
            _onMinute = null;
        }
    }

    /// <summary>タイマーが来たら処理を呼んで、次の 00 秒に掛け直す</summary>
    /// <remarks>処理中の想定外の例外は握りつぶさない（async void なのでアプリの未処理例外になる）。</remarks>
    private async void OnTick()
    {
        try
        {
            await CallAsync().ConfigureAwait(false);
        }
        finally
        {
            ScheduleNext();
        }
    }

    /// <summary>今の分の処理を呼ぶ（同じ分に呼んでいたら呼ばない）</summary>
    /// <returns>処理の完了を表すタスク</returns>
    private async Task CallAsync()
    {
        var now = timeProvider.GetLocalNow().DateTime;
        var minute = TruncateToMinute(now);
        Func<DateTime, Task>? onMinute;
        lock (_gate)
        {
            // 00 秒より少し早く来たときは、まだ前の分なので呼ばない（次の掛け直しで 00 秒過ぎに来る）
            if (_lastCalledMinute == minute)
            {
                return;
            }
            _lastCalledMinute = minute;
            onMinute = _onMinute;
        }

        if (onMinute is not null)
        {
            await onMinute(now).ConfigureAwait(false);
        }
    }

    /// <summary>次の 00 秒にタイマーを掛け直す</summary>
    private void ScheduleNext()
    {
        lock (_gate)
        {
            if (_disposed || _timer is null)
            {
                return;
            }

            var now = timeProvider.GetLocalNow().DateTime;
            var next = TruncateToMinute(now).AddMinutes(1);
            _timer.Change(next - now, Timeout.InfiniteTimeSpan);
        }
    }
}
