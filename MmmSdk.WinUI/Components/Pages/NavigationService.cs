using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>
/// サイドバーのページを、機能から開く口の実装。
/// </summary>
/// <param name="viewModel">サイドバーの項目と選択</param>
/// <param name="detacher">別のウィンドウに外したページ</param>
public sealed class NavigationService(NavigationHostViewModel viewModel, PageDetacher detacher) : INavigationService
{
    /// <inheritdoc />
    public event EventHandler? ShowRequested;

    /// <inheritdoc />
    /// <remarks>ページを別のウィンドウに外しているときは、そのウィンドウを前に出す。メインウィンドウは出さない。</remarks>
    public bool Open<TPage>()
        where TPage : Page
    {
        var key = typeof(TPage).Name;
        var item = viewModel.MenuItems.Concat(viewModel.FooterItems).FirstOrDefault(i => i.Key == key);
        if (item is null)
        {
            return false;
        }

        if (item.IsDetached)
        {
            detacher.Activate(item);
            return true;
        }

        viewModel.SelectedItem = item;
        ShowRequested?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <inheritdoc />
    public void SetBadge<TPage>(bool visible)
        where TPage : Page
    {
        if (viewModel.FindRegisteredItem(typeof(TPage).Name) is { } item)
        {
            item.HasBadge = visible;
        }
    }
}
