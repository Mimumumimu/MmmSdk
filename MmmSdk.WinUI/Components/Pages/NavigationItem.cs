using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーに表示するナビゲーション項目</summary>
/// <param name="key">画面を識別するキー</param>
/// <param name="title">表示名</param>
/// <param name="glyph">Segoe Fluent Icons のグリフ</param>
/// <remarks>
/// 項目は登録ごとに 1 つだけ作って使い回すので、同じ項目かは参照で比べる。
/// 外れている状態を画面へ知らせるため、<see cref="IsDetached"/> を持つ。
/// </remarks>
public sealed partial class NavigationItem(string key, string title, string glyph) : ObservableObject
{
    /// <summary>子の項目の字下げ</summary>
    /// <remarks>サイドバーの項目の左の余白</remarks>
    private const double ChildIndent = 12;

    /// <summary>画面を識別するキー</summary>
    public string Key { get; } = key;

    /// <summary>表示名</summary>
    public string Title { get; } = title;

    /// <summary>Segoe Fluent Icons のグリフ</summary>
    public string Glyph { get; } = glyph;

    /// <summary>別の項目の子か</summary>
    /// <remarks>
    /// 子の項目は親の項目の下に字下げして並べる。
    /// 開閉は無く、常に出す。
    /// </remarks>
    public bool IsChild { get; init; }

    /// <summary>項目の中身の左の余白</summary>
    /// <remarks>
    /// 子の項目だけ字下げする
    /// アイコンだけのときは字下げしない
    /// </remarks>
    public Thickness Indent => new(IsChild && !IsCompact ? ChildIndent : 0, 0, 0, 0);

    /// <summary>サイドバーがアイコンだけの幅か</summary>
    /// <remarks>
    /// ホストがサイドバーの開閉に合わせて決める。
    /// アイコンだけのときは文字と外すボタンが隠れるので、項目名をツールチップで出し、知らせの印はアイコンの角に重ねる。
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Indent))]
    [NotifyPropertyChangedFor(nameof(CompactToolTip))]
    public partial bool IsCompact { get; set; }

    /// <summary>アイコンだけのときに出すツールチップ</summary>
    /// <remarks>
    /// 中身は項目名
    /// 文字が出ているときは null
    /// </remarks>
    public string? CompactToolTip => IsCompact ? Title : null;

    /// <summary>ページを別のウィンドウに外せるか</summary>
    /// <remarks>
    /// 登録のときに機能が決める。
    /// 外せない項目には外すボタンを出さず、ドラッグでも外せない。
    /// </remarks>
    public bool CanDetach { get; init; }

    /// <summary>ページが別のウィンドウに外れているか</summary>
    /// <remarks>
    /// 外れている間はメインの表示領域に出さない
    /// サイドバーの項目には外れていることを示す印を出す
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsBadge))]
    public partial bool IsDetached { get; set; }

    /// <summary>機能が知らせの印を求めているか</summary>
    /// <remarks>
    /// 知らせの印はバッジ
    /// 機能が <see cref="INavigationService.SetBadge{TPage}"/> で決める
    /// ページを開いていない間に見てほしいことが起きたことを知らせる
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsBadge))]
    public partial bool HasBadge { get; set; }

    /// <summary>サイドバーで選ばれている項目か</summary>
    /// <remarks>ホストが選択に合わせて決める</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsBadge))]
    public partial bool IsSelected { get; set; }

    /// <summary>サイドバーの項目に知らせの印を出すか</summary>
    /// <remarks>
    /// 選ばれている項目と別のウィンドウに外している項目には出さない
    /// ページを開いている間はページの中で知らせを見られるため
    /// </remarks>
    public bool ShowsBadge => HasBadge && !IsSelected && !IsDetached;
}
