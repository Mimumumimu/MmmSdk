using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーに表示するナビゲーション項目。</summary>
/// <param name="key">画面を識別するキー。</param>
/// <param name="title">表示名。</param>
/// <param name="glyph">Segoe Fluent Icons のグリフ。</param>
/// <remarks>項目は登録ごとに 1 つだけ作って使い回すので、同じ項目かは参照で比べる。外れている状態を画面へ知らせるため、<see cref="IsDetached"/> を持つ。</remarks>
public sealed partial class NavigationItem(string key, string title, string glyph) : ObservableObject
{
    /// <summary>子の項目の字下げ (サイドバーの項目の左の余白)</summary>
    private const double ChildIndent = 24;

    /// <summary>画面を識別するキー</summary>
    public string Key { get; } = key;

    /// <summary>表示名</summary>
    public string Title { get; } = title;

    /// <summary>Segoe Fluent Icons のグリフ</summary>
    public string Glyph { get; } = glyph;

    /// <summary>別の項目の子か</summary>
    /// <remarks>子の項目は、親の項目の下に字下げして並べる。開閉は無く、常に出す。</remarks>
    public bool IsChild { get; init; }

    /// <summary>項目の中身の左の余白 (子の項目だけ字下げする)</summary>
    public Thickness Indent => new(IsChild ? ChildIndent : 0, 0, 0, 0);

    /// <summary>ページを別のウィンドウに外せるか</summary>
    /// <remarks>登録のときに、機能が決める。外せない項目には、外すボタンを出さず、ドラッグでも外せない。</remarks>
    public bool CanDetach { get; init; }

    /// <summary>ページが別のウィンドウに外れているか</summary>
    /// <remarks>外れている間は、メインの表示領域に出さない。サイドバーの項目には、外れていることを示す印を出す。</remarks>
    [ObservableProperty]
    public partial bool IsDetached { get; set; }
}
