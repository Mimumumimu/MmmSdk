using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MmmSdk.WinUI.Utilities;

/// <summary>ビジュアルツリーから要素を探す</summary>
/// <remarks>コントロールのテンプレート内の要素（ScrollViewer・内部のボタンなど）に触るために使う。</remarks>
public static class VisualTreeSearch
{
    /// <summary>子孫から、指定の型の要素を探す</summary>
    /// <typeparam name="T">探す要素の型</typeparam>
    /// <param name="parent">探し始める要素</param>
    /// <returns>見つかった要素。無ければ null</returns>
    public static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if ((child as T ?? FindDescendant<T>(child)) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>名前が一致する子孫要素を探す</summary>
    /// <typeparam name="T">探す要素の型</typeparam>
    /// <param name="parent">探し始める要素</param>
    /// <param name="name">探す要素の名前</param>
    /// <returns>見つかった要素。無ければ null</returns>
    public static T? FindDescendant<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }
            if (FindDescendant<T>(child, name) is { } found)
            {
                return found;
            }
        }
        return null;
    }
}
