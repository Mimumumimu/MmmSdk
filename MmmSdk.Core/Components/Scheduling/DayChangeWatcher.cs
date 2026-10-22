namespace MmmSdk.Core.Components.Scheduling;

/// <summary>画面に出している日付と今の日付を毎分比べ、違っていたら知らせる</summary>
/// <param name="timeProvider">現在時刻・タイマーの提供元</param>
/// <remarks>
/// 「次の 0 時まであと何時間」と時間の長さで予約すると、スタンバイや時計の変更でずれたときに、日付が変わったことに気づけない。
/// そのため、予約はせず、<see cref="MinuteScheduler"/> の毎分の呼び出しで日付を比べる。ずれても、最大 1 分で追いつく。
/// 画面に出している日付は、画面を作り直したあとに <see cref="MarkShown"/> で覚えさせる。覚えている日付が無いときも、違うものとして知らせる。
/// 知らせる処理は、タイマーのスレッドで動く。違いが直るまで、つまり <see cref="MarkShown"/> されるまで、毎分知らせる。
/// </remarks>
public sealed class DayChangeWatcher(TimeProvider timeProvider) : IDisposable
{
    /// <summary>状態を守るロック</summary>
    private readonly Lock _gate = new();

    /// <summary>毎分の呼び出し</summary>
    private readonly MinuteScheduler _scheduler = new(timeProvider);

    /// <summary>画面に出している日付。まだ無ければ null</summary>
    private DateOnly? _shownDate;

    /// <summary>破棄したか</summary>
    private bool _disposed;

    /// <summary>日付が違っていたときに呼ぶ処理</summary>
    private Action? _onDayChanged;

    /// <summary>始める</summary>
    /// <param name="onDayChanged">日付が違っていたときに呼ぶ処理。タイマーのスレッドから呼ぶので、UI スレッドへの切り替えは渡す側で行う</param>
    /// <exception cref="InvalidOperationException">すでに開始している。</exception>
    /// <remarks>
    /// 始めた直後にも 1 回比べる。
    /// 破棄したあとに呼んでも、何もしない。画面の最初の読み込みを待つ間に画面が閉じて、読み込みの後で呼ばれることがあるため。
    /// </remarks>
    public void Start(Action onDayChanged)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _onDayChanged = onDayChanged;
            _scheduler.Start(OnMinute);
        }
    }

    /// <summary>画面に出した日付を覚える</summary>
    /// <param name="now">画面を作るのに使った現在の日時</param>
    public void MarkShown(DateTime now)
    {
        lock (_gate)
        {
            _shownDate = DateOnly.FromDateTime(now);
        }
    }

    /// <summary>止める</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _onDayChanged = null;
        }
        _scheduler.Dispose();
    }

    /// <summary>毎分、今の日付と画面に出している日付を比べる</summary>
    /// <param name="now">現在の日時</param>
    /// <returns>比較の完了を表すタスク</returns>
    private Task OnMinute(DateTime now)
    {
        Action? onDayChanged;
        lock (_gate)
        {
            onDayChanged = _shownDate == DateOnly.FromDateTime(now) ? null : _onDayChanged;
        }
        onDayChanged?.Invoke();
        return Task.CompletedTask;
    }
}
