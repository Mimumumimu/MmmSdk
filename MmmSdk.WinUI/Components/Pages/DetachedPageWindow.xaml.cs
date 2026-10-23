using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Utilities;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーから外した、1 つのページを出すウィンドウ</summary>
/// <remarks>
/// 閉じるときは、ページをウィンドウから外してから閉じる。ページはホストが持ち続けるので、ウィンドウと一緒に壊さない。
/// ウィンドウの管理 (開く・前に出す・閉じたら元へ戻す)は <see cref="PageDetacher"/> が行う。
/// </remarks>
public sealed partial class DetachedPageWindow : Window
{
    /// <summary>ウィンドウを作る</summary>
    /// <param name="page">出すページ</param>
    /// <param name="title">項目の表示名 (タイトルバーに出す)</param>
    /// <param name="appName">アプリの名前 (タスクバーなどのウィンドウの名前に添える)</param>
    /// <param name="iconPath">アプリのアイコン (.ico)のパス</param>
    public DetachedPageWindow(Page page, string title, string appName, string iconPath)
    {
        InitializeComponent();
        Title = $"{title} - {appName}";
        TitleText.Text = title;
        this.UseCustomTitleBar(AppTitleBar, iconPath);
        ContentHost.Child = page;
        AppWindow.Closing += (_, _) => ReleasePage();
    }

    /// <summary>ページをウィンドウから外す (何度呼んでもよい)</summary>
    public void ReleasePage() => ContentHost.Child = null;
}
