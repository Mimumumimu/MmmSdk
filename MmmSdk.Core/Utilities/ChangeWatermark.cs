namespace MmmSdk.Core.Utilities;

/// <summary>差分の読み出しの基準の時刻</summary>
/// <param name="overlap">基準から戻して読む時間</param>
/// <remarks>
/// 「前回より後に更新された行だけを読む」処理で、次に読む日時を決める入れ物。
/// 更新が確定するのが遅れた書き込みを取りこぼさないよう、基準を <paramref name="overlap"/> だけ戻して読む。
/// 戻した分の行は何度か読むので、同じ行を二重に扱わないときは <see cref="SeenChanges{TKey}"/> を併せて使う。
/// 基準は前に進める向きにだけ動かす。
/// スレッドの安全は持たないので、呼ぶ側が 1 つずつ使う。
/// </remarks>
public sealed class ChangeWatermark(TimeSpan overlap)
{
    /// <summary>基準から戻して読む時間</summary>
    public TimeSpan Overlap { get; } = overlap;

    /// <summary>始めたときの基準</summary>
    /// <remarks>
    /// これ以前の更新は扱わない。
    /// <see cref="Start"/> を呼ぶまで null で、<see cref="Set"/> では変わらない。
    /// </remarks>
    public DateTimeOffset? Floor { get; private set; }

    /// <summary>次に読む基準</summary>
    /// <remarks>まだ決まっていなければ null</remarks>
    public DateTimeOffset? Cursor { get; private set; }

    /// <summary>基準が決まっているか</summary>
    public bool IsStarted => Cursor is not null;

    /// <summary>次に読む日時</summary>
    /// <remarks>
    /// この日時より後に更新された行を読む。
    /// 基準が決まっていないときは <see cref="DateTimeOffset.MinValue"/> で、全件を読む。
    /// </remarks>
    public DateTimeOffset Since => Cursor is { } cursor ? SinceOf(cursor, Overlap) : DateTimeOffset.MinValue;

    /// <summary>更新が下限より後か</summary>
    /// <param name="updatedAt">更新日時</param>
    /// <returns>下限が無いときは true</returns>
    /// <remarks>下限が無いのに、すべての更新を落とさないため。</remarks>
    public bool IsAfterFloor(DateTimeOffset updatedAt) => Floor is not { } floor || updatedAt > floor;

    /// <summary>基準から戻した読み出しの日時を求める</summary>
    /// <param name="cursor">基準</param>
    /// <param name="overlap">戻す時間</param>
    /// <returns>この日時より後に更新された行を読む</returns>
    /// <remarks>基準を端末が持ち、サーバーが状態を持たないときに使う。</remarks>
    public static DateTimeOffset SinceOf(DateTimeOffset cursor, TimeSpan overlap) => cursor - overlap;

    /// <summary>基準を決めて始める</summary>
    /// <param name="serverNow">データベースサーバーの時刻</param>
    /// <remarks>始める前の更新を扱わないよう、下限も同じ時刻にする。</remarks>
    public void Start(DateTimeOffset serverNow)
    {
        Floor = serverNow;
        Cursor = serverNow;
    }

    /// <summary>基準を直接決める</summary>
    /// <param name="cursor">新しい基準で、決められないときは null。</param>
    /// <remarks>
    /// 読み込み直しなどで、基準を置き換えるときに使う。
    /// 下限は変えない。
    /// </remarks>
    public void Set(DateTimeOffset? cursor) => Cursor = cursor;

    /// <summary>基準を前に進める</summary>
    /// <param name="readAt">読んだ結果の時刻</param>
    /// <returns>進めたら true</returns>
    /// <remarks>
    /// 今の基準より後のときだけ進める。
    /// 読み込みを途中でやめたときは呼ばず、次の回が同じ範囲から読み直す。
    /// </remarks>
    public bool Advance(DateTimeOffset readAt)
    {
        if (Cursor is { } cursor && readAt <= cursor)
        {
            return false;
        }

        Cursor = readAt;
        return true;
    }
}
