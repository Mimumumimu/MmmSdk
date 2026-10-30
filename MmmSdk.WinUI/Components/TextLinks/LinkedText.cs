using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.TextLinks;

namespace MmmSdk.WinUI.Components.TextLinks;

/// <summary>
/// <see cref="TextBlock"/> に文章を出すとき、<c>http://</c>・<c>https://</c> の URL を、押すと既定のブラウザで開くリンクにする添付プロパティ。
/// </summary>
/// <remarks>
/// <para>
/// <c>TextBlock.Text</c> の代わりに <see cref="TextProperty"/> に文章を渡す。URL の見つけ方は <see cref="TextLinkFinder"/> に従い、リンクにするのは <c>http</c> と <c>https</c> だけ。
/// リンクは <see cref="Hyperlink"/> にして、押すと <see cref="LinkCommandProperty"/> のコマンドへ URL を渡す。開く処理と、開けなかったときの表示は、受け取る側が持つ。
/// </para>
/// <para>
/// 強調色の地の上など、既定のリンクの色では読めない場所では、<see cref="LinkForegroundProperty"/> で色を渡す。
/// </para>
/// </remarks>
public static class LinkedText
{
    /// <summary>出す文章の添付プロパティ</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(LinkedText), new PropertyMetadata("", OnChanged));

    /// <summary>リンクの文字の色の添付プロパティ。null のときは、リンクの既定の色</summary>
    public static readonly DependencyProperty LinkForegroundProperty = DependencyProperty.RegisterAttached(
        "LinkForeground", typeof(Brush), typeof(LinkedText), new PropertyMetadata(null, OnChanged));

    /// <summary>リンクを押したときに実行するコマンドの添付プロパティ。引数は、開く URL の文字列</summary>
    public static readonly DependencyProperty LinkCommandProperty = DependencyProperty.RegisterAttached(
        "LinkCommand", typeof(ICommand), typeof(LinkedText), new PropertyMetadata(null, OnChanged));

    /// <summary>出す文章を取る</summary>
    /// <param name="element">対象の <see cref="TextBlock"/></param>
    /// <returns>文章</returns>
    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    /// <summary>出す文章を決める</summary>
    /// <param name="element">対象の <see cref="TextBlock"/></param>
    /// <param name="value">文章</param>
    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

    /// <summary>リンクの文字の色を取る</summary>
    /// <param name="element">対象の <see cref="TextBlock"/></param>
    /// <returns>色。既定の色のときは null</returns>
    public static Brush? GetLinkForeground(DependencyObject element) => (Brush?)element.GetValue(LinkForegroundProperty);

    /// <summary>リンクの文字の色を決める</summary>
    /// <param name="element">対象の <see cref="TextBlock"/></param>
    /// <param name="value">色。既定の色にするときは null</param>
    public static void SetLinkForeground(DependencyObject element, Brush? value) => element.SetValue(LinkForegroundProperty, value);

    /// <summary>リンクを押したときのコマンドを取る</summary>
    /// <param name="element">対象の <see cref="TextBlock"/></param>
    /// <returns>コマンド。無いときは null</returns>
    public static ICommand? GetLinkCommand(DependencyObject element) => (ICommand?)element.GetValue(LinkCommandProperty);

    /// <summary>リンクを押したときのコマンドを決める</summary>
    /// <param name="element">対象の <see cref="TextBlock"/></param>
    /// <param name="value">コマンド。引数は、開く URL の文字列</param>
    public static void SetLinkCommand(DependencyObject element, ICommand? value) => element.SetValue(LinkCommandProperty, value);

    /// <summary>文章か色が変わったとき、<see cref="TextBlock"/> の中身を組み立て直す</summary>
    /// <param name="d">対象</param>
    /// <param name="e">変更の内容</param>
    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBlock block)
        {
            Rebuild(block);
        }
    }

    /// <summary>文章を、文字とリンクに分けて、<see cref="TextBlock"/> の <c>Inlines</c> に並べる</summary>
    /// <param name="block">対象の <see cref="TextBlock"/></param>
    private static void Rebuild(TextBlock block)
    {
        var text = GetText(block) ?? "";
        var brush = GetLinkForeground(block);
        var command = GetLinkCommand(block);

        block.Inlines.Clear();
        var position = 0;
        foreach (var link in TextLinkFinder.Find(text))
        {
            AddRun(block, text[position..link.Start]);

            var hyperlink = new Hyperlink();
            var href = link.Href;
            hyperlink.Click += (_, _) =>
            {
                if (command?.CanExecute(href) == true)
                {
                    command.Execute(href);
                }
            };
            if (brush is not null)
            {
                hyperlink.Foreground = brush;
            }
            hyperlink.Inlines.Add(new Run { Text = text.Substring(link.Start, link.Length) });
            block.Inlines.Add(hyperlink);
            position = link.Start + link.Length;
        }
        AddRun(block, text[position..]);
    }

    /// <summary>文字の部分を、<see cref="Run"/> として足す</summary>
    /// <param name="block">足す先の <see cref="TextBlock"/></param>
    /// <param name="text">文字。空のときは足さない</param>
    private static void AddRun(TextBlock block, string text)
    {
        if (text.Length > 0)
        {
            block.Inlines.Add(new Run { Text = text });
        }
    }
}
