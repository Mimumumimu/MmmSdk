using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーの幅 (文字つき・アイコンだけ)を切り替えるボタン (タイトルバーの左端に置く)</summary>
/// <remarks>
/// 使うウィンドウが <see cref="Bind"/> で、<see cref="NavigationHost.ViewModel"/> を渡す。
/// 状態の保存と、項目の見た目の切り替えは、<see cref="NavigationHostViewModel.IsPaneOpen"/> が行う。
/// </remarks>
public sealed partial class NavigationPaneToggleButton : UserControl
{
    /// <summary>サイドバーの開閉を持つ ViewModel (<see cref="Bind"/> で渡されるまでは null)</summary>
    private NavigationHostViewModel? ViewModel { get; set; }

    /// <summary>部品を作る</summary>
    public NavigationPaneToggleButton()
    {
        InitializeComponent();
    }

    /// <summary>サイドバーの開閉を持つ ViewModel につなぐ</summary>
    /// <param name="viewModel">サイドバーの項目と選択 (<see cref="NavigationHost.ViewModel"/>)</param>
    public void Bind(NavigationHostViewModel viewModel) => ViewModel = viewModel;

    /// <summary>ボタンを押したら、サイドバーの幅を切り替える</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.IsPaneOpen = !ViewModel.IsPaneOpen;
        }
    }
}
