namespace MmmSdk.Core.Utilities;

/// <summary>扱い済みの更新の記録</summary>
/// <typeparam name="TKey">行を見分けるキー</typeparam>
/// <param name="keep">記録を残す古さ</param>
/// <remarks>
/// 基準を戻して読むと、同じ行を何度か読む。
/// 行のキーごとに、扱った更新日時を覚え、同じかそれより古い更新を外す。
/// キーは行の番号のほか、「誰に知らせたか」のように、同じ行でも扱いを分けたい組を使える。
/// <paramref name="keep"/> は戻して読む時間より十分に長くする。
/// 古い記録は <see cref="Prune"/> で捨て、メモリを増やし続けない。
/// スレッドの安全は持たないので、呼ぶ側が 1 つずつ使う。
/// </remarks>
public sealed class SeenChanges<TKey>(TimeSpan keep)
    where TKey : notnull
{
    /// <summary>行のキーごとの扱った更新日時</summary>
    private readonly Dictionary<TKey, DateTimeOffset> _seen = [];

    /// <summary>この更新をすでに扱ったか</summary>
    /// <param name="key">行のキー</param>
    /// <param name="updatedAt">更新日時</param>
    /// <returns>同じ行の、同じかそれより新しい更新日時を扱い済みなら true。</returns>
    public bool IsSeen(TKey key, DateTimeOffset updatedAt) => _seen.TryGetValue(key, out var seen) && seen >= updatedAt;

    /// <summary>更新を扱い済みとして覚える</summary>
    /// <param name="key">行のキー</param>
    /// <param name="updatedAt">更新日時</param>
    public void Remember(TKey key, DateTimeOffset updatedAt) => _seen[key] = updatedAt;

    /// <summary>基準より十分に古い記録を捨てる</summary>
    /// <param name="cursor">今の基準</param>
    public void Prune(DateTimeOffset cursor)
    {
        foreach (var id in _seen.Where(pair => pair.Value < cursor - keep).Select(pair => pair.Key).ToList())
        {
            _seen.Remove(id);
        }
    }
}
