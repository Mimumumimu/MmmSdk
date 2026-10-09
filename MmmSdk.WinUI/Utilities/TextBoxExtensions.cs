using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Utilities;

/// <summary><see cref="TextBox"/> の拡張メソッド</summary>
public static class TextBoxExtensions
{
    /// <summary>キャレットが表示の外に出たとき、見える位置までスクロールするようにする</summary>
    /// <param name="textBox">対象の複数行の入力欄</param>
    /// <remarks>
    /// 高さを固定した複数行の <see cref="TextBox"/> は、末尾で Enter を押したときなどに、キャレットは動くのに表示がスクロールされないことがある。
    /// キャレットが表示の中にあるときは何もしないので、<see cref="TextBox"/> 自身のスクロールとは重ならない。
    /// 選択位置が変わるたびに、レイアウトが済んだあとで確かめる。縦方向だけを扱う (折り返す入力欄を想定)。
    /// 詳しい決まり (位置の取り方など)は、SDK の docs/controls.md の「TextBoxExtensions」。
    /// </remarks>
    public static void KeepCaretVisible(this TextBox textBox)
    {
        ScrollViewer? scrollViewer = null;
        textBox.SelectionChanged += (_, _) =>
            textBox.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                // テンプレートの適用後でないと見つからないので、最初に必要になったときに探す
                scrollViewer ??= VisualTreeSearch.FindDescendant<ScrollViewer>(textBox);
                if (scrollViewer is not null)
                {
                    ScrollCaretIntoView(textBox, scrollViewer);
                }
            });
    }

    /// <summary>キャレットが表示の外にあれば、見える位置までスクロールする</summary>
    /// <param name="textBox">対象の入力欄</param>
    /// <param name="scrollViewer">入力欄の中のスクロール表示</param>
    private static void ScrollCaretIntoView(TextBox textBox, ScrollViewer scrollViewer)
    {
        var viewportHeight = scrollViewer.ViewportHeight;
        if (!textBox.IsLoaded || !scrollViewer.IsLoaded || viewportHeight <= 0)
        {
            return;
        }

        // 予約してから実行するまでに、文字が変わっている (送信で空になるなど)ことがあるので、今の文字数に収める
        var index = Math.Min(textBox.SelectionStart + textBox.SelectionLength, textBox.Text.Length);

        // 位置の Y は、文章全体の中での位置 (今のスクロール量を含まない)。見えている範囲の中の位置は、スクロール量を引いて求める
        // (表示の中の位置として扱うと、見えているのに外と判断して、逆にスクロールしてしまう)
        Windows.Foundation.Rect caret;
        try
        {
            caret = textBox.GetRectFromCharacterIndex(index, false);
        }
        catch (ArgumentException)
        {
            // レイアウトがまだ整っていない・IME の変換中などで、位置を取れないとき。補助なので、このときは何もしない (確かめる手段が無い)
            return;
        }

        var caretHeight = caret.Height > 0 ? caret.Height : textBox.FontSize * 1.5;
        var top = caret.Y - scrollViewer.VerticalOffset;
        var bottom = top + caretHeight;

        double? offset = null;
        if (top < 0)
        {
            offset = caret.Y;
        }
        else if (bottom > viewportHeight)
        {
            offset = caret.Y + caretHeight - viewportHeight;
        }

        if (offset is { } wanted)
        {
            var value = Math.Clamp(wanted, 0, scrollViewer.ScrollableHeight);
            scrollViewer.ChangeView(null, value, null, disableAnimation: true);
        }
    }
}
