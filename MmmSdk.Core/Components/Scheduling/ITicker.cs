namespace MmmSdk.Core.Components.Scheduling;

/// <summary>定期処理の合図</summary>
/// <remarks>
/// アプリ全体で 1 つ。
/// 機能は自分でタイマーを持たず、ここに処理を登録する。
/// 合図は時刻が来た登録にだけ渡し、戻りを待たない。
/// 受け取った処理はそれぞれ自分のペースで動く。
/// ある処理が前の合図の分をまだ終えていないときはその処理にだけ今回の合図を渡さずに飛ばす。
/// ほかの処理の合図は止めない。
/// スリープ明けにたまった分をまとめて動かさない。
/// 毎分の合図は同じ分に同じ処理を 2 回動かさない。
/// 処理が起こした予想外の例外は握りつぶさず、アプリの未処理例外の受け皿へ渡す。
/// これは <c>Forget</c> と同じ扱い。
/// 取り消しはエラーとして扱わない。
/// タイマーは 1 つで、登録のうち、いちばん早く来る時刻に掛け直す。
/// </remarks>
public interface ITicker
{
    /// <summary>合図を受け取る処理を登録する</summary>
    /// <param name="handler">
    /// 合図のたびに動かす処理。
    /// 現在の日時を受け取る。
    /// <see cref="TickOptions.OnCallerContext"/> が false なら、スレッドプールのスレッドで動く。
    /// </param>
    /// <param name="options">
    /// 合図のタイミングと条件。
    /// 省略すると、毎分 00 秒に渡す。
    /// </param>
    /// <returns>
    /// 登録の控え
    /// <see cref="IDisposable.Dispose"/> で解除する
    /// 持ち主 (画面・ViewModel)を閉じるときに必ず解除する
    /// </returns>
    /// <exception cref="ObjectDisposedException">アプリの終了で、すでに破棄されている。</exception>
    /// <exception cref="InvalidOperationException"><see cref="TickOptions.OnCallerContext"/> が true なのに、呼んだスレッドに <see cref="SynchronizationContext"/> が無い。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="TickOptions.Timing"/> が <see cref="TickTiming.Interval"/> なのに、<see cref="TickOptions.Interval"/> が 1 秒より短い。</exception>
    ITickSubscription Subscribe(Func<DateTime, Task> handler, TickOptions? options = null);

    /// <summary>合図を受け取る処理を登録する</summary>
    /// <param name="handler">
    /// 合図のたびに動かす処理。
    /// 現在の日時を受け取り、結果を返す。
    /// </param>
    /// <param name="options">
    /// 合図のタイミングと条件。
    /// 省略すると、毎分 00 秒に渡す。
    /// </param>
    /// <returns>
    /// 登録の控え
    /// <see cref="IDisposable.Dispose"/> で解除する
    /// 持ち主 (画面・ViewModel)を閉じるときに必ず解除する
    /// </returns>
    /// <remarks>
    /// 処理が成功か失敗かを伝え、失敗が続くと間隔を延ばす。
    /// <see cref="TickResult.Failed"/> が続くたびに次の合図までの時間を 2 倍に延ばす。
    /// 延ばす前の時間は <see cref="TickTiming.Interval"/> なら今の間隔、毎分の合図なら 1 分。
    /// 上限は <see cref="Ticker.MaxBackoff"/>。
    /// 全員の端末が同じ時刻に戻らないよう、延ばした時間にはばらつきを付ける。
    /// <see cref="TickResult.Done"/> で元に戻る。
    /// 飛ばした回 (<see cref="TickOptions.IsActive"/> が false・前の処理が続いている・<see cref="TickOptions.MinimumGap"/> が足りない)は延ばす状態を変えない。
    /// <see cref="ITickSubscription.WakeNow"/> は延ばしている間も、すぐ動かす。
    /// 利用者の操作のため。
    /// </remarks>
    /// <exception cref="ObjectDisposedException">アプリの終了で、すでに破棄されている。</exception>
    /// <exception cref="InvalidOperationException"><see cref="TickOptions.OnCallerContext"/> が true なのに、呼んだスレッドに <see cref="SynchronizationContext"/> が無い。</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="TickOptions.Timing"/> が <see cref="TickTiming.Interval"/> なのに、<see cref="TickOptions.Interval"/> が 1 秒より短い。</exception>
    ITickSubscription Subscribe(Func<DateTime, Task<TickResult>> handler, TickOptions? options = null);
}

/// <summary>定期処理 1 回の結果</summary>
public enum TickResult
{
    /// <summary>終えた</summary>
    /// <remarks>
    /// 読む相手に届かなかった失敗ではない
    /// ログインの前や読むものが無かったときも含む
    /// </remarks>
    Done,

    /// <summary>読む相手に届かなかった</summary>
    /// <remarks>
    /// 読む相手は DB など
    /// 続くと次の合図までの時間を延ばす
    /// </remarks>
    Failed,
}

/// <summary>定期処理の登録の控え</summary>
public interface ITickSubscription : IDisposable
{
    /// <summary>動かした印を付ける</summary>
    /// <remarks>
    /// 合図とは別に、この処理を今動かした、または今動かした扱いにする、と覚えさせる。
    /// 利用者が手動で取り込んだ直後に自動の取り込みが重ならないようにするために使う。
    /// <see cref="TickOptions.MinimumGap"/> を指定した登録で、直前の実行から間隔が空いていなければ、今回の合図を飛ばす。
    /// </remarks>
    void MarkRan();

    /// <summary>次の合図の時刻を待たずすぐ動かす</summary>
    /// <remarks>
    /// 動かすかどうかの条件 (<see cref="TickOptions.IsActive"/>・前の処理が続いているか・<see cref="TickOptions.MinimumGap"/>)は通常の合図と同じ
    /// 前の処理が続いているときは飛ばす
    /// 次の合図で追いつく
    /// </remarks>
    void WakeNow();

    /// <summary>合図の間隔を変える</summary>
    /// <param name="interval">
    /// 新しい間隔
    /// 1 秒以上
    /// </param>
    /// <remarks>
    /// 直前に合図を渡した時刻から数え直す。
    /// すでに新しい間隔を過ぎていれば、すぐ渡す。
    /// 同じ間隔なら何もしない。
    /// 画面を見ている間だけ短くするような、場面で間隔が変わる処理向け。
    /// </remarks>
    /// <exception cref="InvalidOperationException">この登録の <see cref="TickOptions.Timing"/> が <see cref="TickTiming.Interval"/> ではない</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> が 1 秒より短い</exception>
    void SetInterval(TimeSpan interval);
}

/// <summary>合図の渡し方</summary>
public enum TickTiming
{
    /// <summary>毎分 00 秒に渡す</summary>
    /// <remarks>
    /// 全員へ同時に渡す。
    /// 時刻そのものが意味を持つ処理向けで、リマインダーの判定や日付の変わりの監視が当たる。
    /// </remarks>
    OnTheMinute,

    /// <summary>登録ごとの秒に渡す</summary>
    /// <remarks>
    /// 毎分 1 回渡す。
    /// 秒は登録ごとに決め、1〜59 秒のどこかになる。
    /// DB を読み直す処理向け。
    /// 00 秒にそろえると全員の端末が同じ 1 秒にサーバーへ読みに来るため。
    /// </remarks>
    Staggered,

    /// <summary>決めた間隔がたつたびに渡す</summary>
    /// <remarks>
    /// 前に渡した時刻から、<see cref="TickOptions.Interval"/> がたつたびに渡す。
    /// 1 分より短い周期や 1 分以外の周期の処理向け。
    /// 時刻にはそろえない。
    /// </remarks>
    Interval,
}

/// <summary>定期処理の登録の条件</summary>
public sealed record TickOptions
{
    /// <summary>合図を渡すタイミング</summary>
    public TickTiming Timing { get; init; } = TickTiming.OnTheMinute;

    /// <summary>間隔</summary>
    /// <remarks>
    /// <see cref="TickTiming.Interval"/> のときの間隔
    /// 1 秒以上
    /// あとから <see cref="ITickSubscription.SetInterval"/> で変えられる
    /// ほかのタイミングでは使わない
    /// </remarks>
    public TimeSpan Interval { get; init; }

    /// <summary>登録した直後にも動かすか</summary>
    /// <remarks>
    /// 1 回動かす
    /// 次の合図を待つ間も画面や通知を最新にしたい処理向け
    /// </remarks>
    public bool RunImmediately { get; init; }

    /// <summary>この処理を動かしてよいか</summary>
    /// <remarks>
    /// 合図のたびに確かめ、false なら今回の合図を飛ばす。
    /// 機能のオン・オフや、管理者による無効は起動後にも変わるため。
    /// null なら常に動かす。
    /// </remarks>
    public Func<bool>? IsActive { get; init; }

    /// <summary>最低限空ける時間</summary>
    /// <remarks>
    /// 直前に動かし始めた時刻から数える。
    /// <see cref="ITickSubscription.MarkRan"/> と組み合わせる。
    /// 既定は 0 で、空けない。
    /// </remarks>
    public TimeSpan MinimumGap { get; init; }

    /// <summary>登録を呼んだスレッドで処理を動かすか</summary>
    /// <remarks>
    /// <see cref="SynchronizationContext"/> が指す UI スレッドで動かす
    /// UI スレッドで状態を持つ処理向け
    /// false ならスレッドプールで動かす
    /// </remarks>
    public bool OnCallerContext { get; init; }
}
