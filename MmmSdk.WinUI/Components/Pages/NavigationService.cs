using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>
/// サイドバーのページを、機能から開く口の実装。
/// </summary>
/// <param name="viewModel">サイドバーの項目と選択</param>
public sealed class NavigationService(NavigationHostViewModel viewModel) : INavigationService
{
    /// <inheritdoc />
    public event EventHandler? ShowRequested;

    /// <inheritdoc />
    public bool Open<TPage>()
        where TPage : Page
    {
        var key = typeof(TPage).Name;
        var item = viewModel.MenuItems.Concat(viewModel.FooterItems).FirstOrDefault(i => i.Key == key);
        if (item is null)
        {
            return false;
        }

        viewModel.SelectedItem = item;
        ShowRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
