using Microsoft.UI.Xaml;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーに表示するナビゲーション項目。</summary>
/// <param name="Key">画面を識別するキー。</param>
/// <param name="Title">表示名。</param>
/// <param name="Glyph">Segoe Fluent Icons のグリフ。</param>
public sealed record NavigationItem(string Key, string Title, string Glyph)
{
    /// <summary>子の項目の字下げ (サイドバーの項目の左の余白)</summary>
    private const double ChildIndent = 24;

    /// <summary>別の項目の子か (親の項目の下に、字下げして並べる。開閉は無く、常に出す)</summary>
    public bool IsChild { get; init; }

    /// <summary>項目の中身の左の余白 (子の項目だけ字下げする)</summary>
    public Thickness Indent => new(IsChild ? ChildIndent : 0, 0, 0, 0);
}
