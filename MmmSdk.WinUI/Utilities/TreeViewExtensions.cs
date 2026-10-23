using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MmmSdk.WinUI.Utilities;

/// <summary>データを <c>ItemsSource</c> で渡した <see cref="TreeView"/> から、行のデータを取り出す</summary>
/// <remarks>
/// <c>TreeView.SelectedItem</c> は <c>object</c> 型で <c>x:Bind</c> の双方向にできないため、選択は画面側で ViewModel と相互に合わせる。
/// そのときに要る、選択の変更と行からのデータの取り出しをまとめる。
/// </remarks>
public static class TreeViewExtensions
{
    /// <summary>選択の変更で、新しく選ばれたデータを取り出す</summary>
    /// <typeparam name="T">データの型</typeparam>
    /// <param name="args">選択の変更の情報</param>
    /// <returns>新しく選ばれたデータ。選びが外れた・型が合わないときは null</returns>
    /// <remarks>
    /// <c>SelectionChanged</c> の中では <c>sender.SelectedItem</c> がまだ前の選択を返すので、args の追加分から取る。
    /// 追加分はクリックでの選択ならデータだが、コードから選択したときは <see cref="TreeViewNode"/> で来ることがあるので、両方から取り出す。
    /// </remarks>
    public static T? GetAddedItem<T>(this TreeViewSelectionChangedEventArgs args) where T : class
        => args.AddedItems.FirstOrDefault() switch
        {
            T item => item,
            TreeViewNode { Content: T item } => item,
            _ => null,
        };

    /// <summary>画面上の要素から、その行のデータを探す</summary>
    /// <typeparam name="T">データの型</typeparam>
    /// <param name="tree">行を持つ <see cref="TreeView"/></param>
    /// <param name="source">画面上の要素 (クリックされた要素など)</param>
    /// <returns>その行のデータ。見つからなければ null</returns>
    /// <remarks>行 (<see cref="TreeViewItem"/>)の <c>DataContext</c> はデータではなく <see cref="TreeViewNode"/> のことがあるので、<see cref="TreeView"/> に行からデータを引かせる。</remarks>
    public static T? FindItem<T>(this TreeView tree, object source) where T : class
    {
        for (var current = source as DependencyObject; current is not null and not TreeView; current = VisualTreeHelper.GetParent(current))
        {
            if (current is TreeViewItem container)
            {
                return tree.ItemFromContainer(container) as T;
            }
        }
        return null;
    }
}
