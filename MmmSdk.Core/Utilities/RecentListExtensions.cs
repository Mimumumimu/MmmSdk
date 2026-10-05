namespace MmmSdk.Core.Utilities;

/// <summary>「最近使った順」のリスト (先頭が最新)の操作</summary>
public static class RecentListExtensions
{
    /// <summary>項目を先頭に入れる (同じ項目があれば先頭へ移す)。最大件数を超えた古いものは捨てる</summary>
    /// <typeparam name="T">項目の型</typeparam>
    /// <param name="list">最近使った順のリスト (先頭が最新)</param>
    /// <param name="item">先頭に入れる項目</param>
    /// <param name="isSame">既にある項目が <paramref name="item"/> と同じものかの判定 (比較の仕方は使う側が決める。例: 大文字小文字を区別しない)</param>
    /// <param name="maxCount">残す最大件数</param>
    public static void AddRecent<T>(this List<T> list, T item, Predicate<T> isSame, int maxCount)
    {
        list.RemoveAll(isSame);
        list.Insert(0, item);
        if (list.Count > maxCount)
        {
            list.RemoveRange(maxCount, list.Count - maxCount);
        }
    }
}
