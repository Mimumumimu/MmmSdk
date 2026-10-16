namespace MmmSdk.WinUI.Controls;

/// <summary><see cref="TreeGridView"/> に渡す行の一覧を、あるべき並びに、差分だけで合わせる</summary>
/// <remarks>
/// 行を作り直さず、同じ行を使い回して並びだけを直すので、入力欄の位置・フォーカス・スクロールの位置が保たれる。
/// 行を動かす通知 (<c>Move</c>)は使わない (表が扱わないことがあるため。外して入れ直す)。
/// </remarks>
public static class TreeGridRowSync
{
    /// <summary>行の一覧を、あるべき並びに合わせる</summary>
    /// <typeparam name="TRow">行の型 (同じ行は、同じインスタンスで渡す)</typeparam>
    /// <param name="rows">表に渡している行の一覧 (増減を通知する一覧)</param>
    /// <param name="target">あるべき行の並び</param>
    /// <remarks>並びが変わった行は、一度外して入れ直す。余った行は、末尾から外す。</remarks>
    public static void Sync<TRow>(IList<TRow> rows, IReadOnlyList<TRow> target)
        where TRow : class
    {
        for (var i = 0; i < target.Count; i++)
        {
            if (i < rows.Count && ReferenceEquals(rows[i], target[i]))
            {
                continue;
            }

            var existing = rows.IndexOf(target[i]);
            if (existing >= 0)
            {
                rows.RemoveAt(existing);
            }
            rows.Insert(i, target[i]);
        }

        while (rows.Count > target.Count)
        {
            rows.RemoveAt(rows.Count - 1);
        }
    }
}
