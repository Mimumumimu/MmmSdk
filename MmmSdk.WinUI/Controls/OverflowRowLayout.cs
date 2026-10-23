using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace MmmSdk.WinUI.Controls;

/// <summary>要素を 1 行に並べ、幅に収まらない分を隠すレイアウト (<c>ItemsRepeater</c> 用)</summary>
/// <remarks>
/// 隠した要素は、使う側が「⋯」のようなボタンの一覧に出す。
/// 収まらないときは、隠した要素の数に関わらず、右に <see cref="ReservedWidth"/> の幅を空けて、そこにボタンを重ねる。
/// 全部収まるときは、空けない。
/// </remarks>
public sealed partial class OverflowRowLayout : NonVirtualizingLayout
{
    /// <summary>要素どうしの間隔の依存関係プロパティ</summary>
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(OverflowRowLayout), new PropertyMetadata(0d, OnLayoutPropertyChanged));

    /// <summary>収まらないとき右に空ける幅の依存関係プロパティ</summary>
    public static readonly DependencyProperty ReservedWidthProperty = DependencyProperty.Register(
        nameof(ReservedWidth), typeof(double), typeof(OverflowRowLayout), new PropertyMetadata(0d, OnLayoutPropertyChanged));

    /// <summary>最後に通知した、隠し始める位置</summary>
    private int _notifiedHiddenStartIndex = -1;

    /// <summary>最後に通知した、隠した要素があるか</summary>
    private bool _notifiedHasHidden;

    /// <summary>要素どうしの間隔</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>収まらないとき右に空ける幅 (隠した要素の一覧を開くボタンの幅と間隔)</summary>
    public double ReservedWidth
    {
        get => (double)GetValue(ReservedWidthProperty);
        set => SetValue(ReservedWidthProperty, value);
    }

    /// <summary>隠し始める要素の位置</summary>
    /// <remarks>先頭から数えて、この位置以降の要素が隠れる。隠れる要素が無ければ、要素の数と同じ。</remarks>
    public int HiddenStartIndex { get; private set; }

    /// <summary>収まらず、隠した要素があるか</summary>
    public bool HasHidden { get; private set; }

    /// <summary>隠す要素が変わった</summary>
    /// <remarks>配置の途中で呼ばれる。画面を変えるときは、配置が終わってからにする。</remarks>
    public event EventHandler? OverflowChanged;

    /// <summary>間隔・空ける幅が変わったら、並べ直す</summary>
    /// <param name="d">変更されたレイアウト</param>
    /// <param name="e">変更の情報</param>
    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((OverflowRowLayout)d).InvalidateMeasure();

    /// <inheritdoc />
    protected override Size MeasureOverride(NonVirtualizingLayoutContext context, Size availableSize)
    {
        var children = context.Children;
        var widths = new double[children.Count];
        var height = 0d;
        for (var i = 0; i < children.Count; i++)
        {
            children[i].Measure(new Size(double.PositiveInfinity, availableSize.Height));
            widths[i] = children[i].DesiredSize.Width;
            height = Math.Max(height, children[i].DesiredSize.Height);
        }

        HiddenStartIndex = CountVisible(widths, availableSize.Width);
        HasHidden = HiddenStartIndex < widths.Length;
        return new Size(GetUsedWidth(widths, HiddenStartIndex), height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(NonVirtualizingLayoutContext context, Size finalSize)
    {
        var children = context.Children;
        var x = 0d;
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (i < HiddenStartIndex)
            {
                var size = child.DesiredSize;
                child.Arrange(new Rect(x, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
                x += size.Width + Spacing;
            }
            else
            {
                // 隠す要素は、大きさ 0 に置く
                child.Arrange(new Rect(0, 0, 0, 0));
            }
        }

        if (HiddenStartIndex != _notifiedHiddenStartIndex || HasHidden != _notifiedHasHidden)
        {
            _notifiedHiddenStartIndex = HiddenStartIndex;
            _notifiedHasHidden = HasHidden;
            OverflowChanged?.Invoke(this, EventArgs.Empty);
        }
        return finalSize;
    }

    /// <summary>幅に収まる要素の数を数える</summary>
    /// <param name="widths">各要素の幅</param>
    /// <param name="availableWidth">使える幅</param>
    /// <returns>先頭から収まる要素の数</returns>
    private int CountVisible(double[] widths, double availableWidth)
    {
        if (double.IsInfinity(availableWidth) || GetUsedWidth(widths, widths.Length) <= availableWidth)
        {
            return widths.Length;
        }

        var limit = availableWidth - ReservedWidth;
        var count = 0;
        while (count < widths.Length && GetUsedWidth(widths, count + 1) <= limit)
        {
            count++;
        }
        return count;
    }

    /// <summary>先頭から指定の数の要素を並べたときの幅を求める</summary>
    /// <param name="widths">各要素の幅</param>
    /// <param name="count">並べる要素の数</param>
    /// <returns>要素の幅と間隔の合計</returns>
    private double GetUsedWidth(double[] widths, int count)
    {
        var total = 0d;
        for (var i = 0; i < count; i++)
        {
            total += widths[i] + (i > 0 ? Spacing : 0);
        }
        return total;
    }
}
