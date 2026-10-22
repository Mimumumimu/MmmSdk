using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーのページ移動の「戻る」「進む」ボタン (タイトルバーに置く)</summary>
/// <remarks>
/// Alt+← と Alt+→ のショートカットも持つ。ウィンドウのどこにフォーカスがあっても効く。
/// 使うウィンドウが <see cref="Bind"/> で、<see cref="NavigationHost.ViewModel"/> を渡す。
/// </remarks>
public sealed partial class NavigationHistoryButtons : UserControl
{
    /// <summary>移動の履歴を持つ ViewModel (<see cref="Bind"/> で渡されるまでは null)</summary>
    private NavigationHostViewModel? ViewModel { get; set; }

    /// <summary>部品を作る</summary>
    public NavigationHistoryButtons()
    {
        InitializeComponent();
    }

    /// <summary>移動の履歴を持つ ViewModel につなぐ</summary>
    /// <param name="viewModel">サイドバーの項目と選択 (<see cref="NavigationHost.ViewModel"/>)</param>
    public void Bind(NavigationHostViewModel viewModel)
    {
        ViewModel = viewModel;
        Bindings.Update();
    }

    /// <summary>「戻る」を押したら、ひとつ前のページへ戻る</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnBackClick(object sender, RoutedEventArgs e) => ViewModel?.GoBack();

    /// <summary>「進む」を押したら、戻る前のページへ進む</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnForwardClick(object sender, RoutedEventArgs e) => ViewModel?.GoForward();
}
