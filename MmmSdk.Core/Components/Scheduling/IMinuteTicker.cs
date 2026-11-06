namespace MmmSdk.Core.Components.Scheduling;

/// <summary>アプリ全体で 1 つの、毎分の合図。機能は自分でタイマーを持たず、ここに処理を登録する</summary>
/// <remarks>
/// 合図は、登録した全員に同時に渡し、戻りを待たない。受け取った処理は、それぞれ自分のペースで動く。
/// ある処理が前の合図の分をまだ終えていないときは、その処理にだけ今回の合図を渡さずに飛ばす。ほかの処理の合図は止めない。
/// スリープ明けに、たまった分をまとめて動かさない。同じ分に同じ処理を 2 回動かさない。
/// 処理が起こした予想外の例外は握りつぶさず、アプリの未処理例外の受け皿へ渡す。これは <c>Forget</c> と同じ扱い。取り消しは、エラーとして扱わない。
/// </remarks>
public interface IMinuteTicker
{
    /// <summary>毎分の合図を受け取る処理を登録する</summary>
    /// <param name="handler">合図のたびに動かす処理。現在の日時を受け取る。<see cref="MinuteTickOptions.OnCallerContext"/> が false なら、スレッドプールのスレッドで動く</param>
    /// <param name="options">合図のタイミングと条件。省略すると、毎分 00 秒に渡す</param>
    /// <returns>登録の控え。<see cref="IDisposable.Dispose"/> で解除する。持ち主 (画面・ViewModel)を閉じるときに必ず解除する</returns>
    /// <exception cref="ObjectDisposedException">アプリの終了で、すでに破棄されている。</exception>
    /// <exception cref="InvalidOperationException"><see cref="MinuteTickOptions.OnCallerContext"/> が true なのに、呼んだスレッドに <see cref="SynchronizationContext"/> が無い。</exception>
    IMinuteTickSubscription Subscribe(Func<DateTime, Task> handler, MinuteTickOptions? options = null);
}

/// <summary>毎分の合図の登録の控え</summary>
public interface IMinuteTickSubscription : IDisposable
{
    /// <summary>合図とは別に、この処理を今動かした (または、今動かした扱いにする)と覚えさせる</summary>
    /// <remarks>
    /// 利用者が手動で取り込んだ直後に、自動の取り込みが重ならないようにするために使う。
    /// <see cref="MinuteTickOptions.MinimumGap"/> を指定した登録で、直前の実行から間隔が空いていなければ、今回の合図を飛ばす。
    /// </remarks>
    void MarkRan();
}

/// <summary>毎分の合図の渡し方</summary>
public enum MinuteTickTiming
{
    /// <summary>毎分 00 秒に、全員へ同時に渡す。時刻そのものが意味を持つ処理 (リマインダーの判定・日付の変わりの監視)向け</summary>
    OnTheMinute,

    /// <summary>毎分 1 回、登録ごとに決めた秒 (1〜59 秒)に渡す。DB を読み直す処理向け。00 秒にそろえると、全員の端末が同じ 1 秒にサーバーへ読みに来るため</summary>
    Staggered,
}

/// <summary>毎分の合図の登録の条件</summary>
public sealed record MinuteTickOptions
{
    /// <summary>合図を渡すタイミング</summary>
    public MinuteTickTiming Timing { get; init; } = MinuteTickTiming.OnTheMinute;

    /// <summary>登録した直後にも、1 回動かすか。次の合図を待つ間も画面や通知を最新にしたい処理向け</summary>
    public bool RunImmediately { get; init; }

    /// <summary>今、この処理を動かしてよいか。合図のたびに確かめ、false なら今回の合図を飛ばす。機能のオン・オフや、管理者による無効は、起動後にも変わるため。null なら常に動かす</summary>
    public Func<bool>? IsActive { get; init; }

    /// <summary>直前の実行から、最低限空ける時間。<see cref="IMinuteTickSubscription.MarkRan"/> と組み合わせる。既定は 0 (空けない)</summary>
    public TimeSpan MinimumGap { get; init; }

    /// <summary>登録を呼んだスレッドの <see cref="SynchronizationContext"/> (UI スレッド)で、処理を動かすか。UI スレッドで状態を持つ処理向け。false ならスレッドプールで動かす</summary>
    public bool OnCallerContext { get; init; }
}
