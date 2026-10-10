using System.Collections;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;

namespace MmmSdk.WinUI.Controls;

/// <summary>
/// 木構造の列と、普通の入力列を持つ表。見えている行を 1 つの一覧にならして持ち、各行が字下げの段と開閉の状態を持つ。
/// </summary>
/// <remarks>
/// <para>
/// 行 (<see cref="ITreeGridRow"/>)は、使う側が作って <see cref="ItemsSource"/> に渡す (開閉に合わせた行の増減も、使う側が行う)。
/// セルの見た目は、列の <see cref="TreeGridColumn.CellTemplate"/> で決まる (行がデータとして渡る)。行の高さは固定で、見えている行だけを作る (仮想化)。
/// </para>
/// <para>
/// 固定の列 (<see cref="TreeGridColumn.IsFrozen"/>)は、横にスクロールしても左に残り、見出しの行は、縦にスクロールしても上に残る。
/// どちらも、スクロールの位置に合わせて、コンポジションの式で、要素を逆向きに動かして実現する (スクロールのたびに、コードは動かない)。
/// </para>
/// </remarks>
public sealed partial class TreeGridView : UserControl
{
    /// <summary>画面に出ている行の表示部品 (列の変更・選択の変更を伝えるため)</summary>
    private readonly HashSet<TreeGridRowPresenter> _presenters = [];

    /// <summary>見出しの、固定の列の部分</summary>
    private readonly Border _headerFrozenPanel = new();

    /// <summary>見出しの、スクロールする列の部分</summary>
    private readonly Border _headerScrollPanel = new();

    /// <summary>表示する列 (固定の列が先)</summary>
    private List<TreeGridColumn> _visibleColumns = [];

    /// <summary>スクロールの位置を表す、コンポジションの値の集まり</summary>
    private CompositionPropertySet? _scrollProperties;

    /// <summary>選んだ行</summary>
    private ITreeGridRow? _selectedRow;

    /// <summary>列を作り直した回数 (行の表示部品が、自分のセルが古いかを知るため)</summary>
    internal int ColumnsVersion { get; private set; }

    /// <summary>表示する列 (固定の列が先)</summary>
    internal IReadOnlyList<TreeGridColumn> VisibleColumns => _visibleColumns;

    /// <summary>列の定義 (並べた順に出る。固定の列を先に並べる)</summary>
    public IList<TreeGridColumn> Columns { get; } = [];

    /// <summary>行の高さ (固定)</summary>
    public double RowHeight { get; set; } = 36;

    /// <summary>見出しの高さ</summary>
    public double HeaderHeight { get; set; } = 32;

    /// <summary>字下げの 1 段の幅</summary>
    public double IndentWidth { get; set; } = 18;

    /// <summary>行の右クリックのメニュー (行・行の中の入力欄で開く)</summary>
    public FlyoutBase? RowContextFlyout { get; set; }

    /// <summary>行のない所の右クリックのメニュー</summary>
    public FlyoutBase? BlankContextFlyout
    {
        get => Scroller.ContextFlyout;
        set => Scroller.ContextFlyout = value;
    }

    /// <summary>行の一覧 (<see cref="ITreeGridRow"/> の一覧。増減を通知する一覧にすると、差分だけが反映される)</summary>
    public object? ItemsSource
    {
        get => Repeater.ItemsSource;
        set => Repeater.ItemsSource = value;
    }

    /// <summary>選んだ行 (行の中の入力欄にフォーカスが入った・右クリックしたときに変わる)</summary>
    public ITreeGridRow? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (ReferenceEquals(_selectedRow, value))
            {
                return;
            }

            _selectedRow = value;
            foreach (var presenter in _presenters)
            {
                presenter.UpdateSelected();
            }
            SelectedRowChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>クリックで選ぶ列 (<see cref="TreeGridColumn.IsInvokable"/>)のセルが、クリックされた</summary>
    public event EventHandler<TreeGridCellEventArgs>? CellInvoked;

    /// <summary>選んだ行が変わった</summary>
    public event EventHandler? SelectedRowChanged;

    /// <summary>開閉ボタンが押された (使う側が、開閉の状態と行の一覧を直す)</summary>
    public event EventHandler<TreeGridRowEventArgs>? RowToggleRequested;

    /// <summary>表を作る</summary>
    public TreeGridView()
    {
        InitializeComponent();

        _headerFrozenPanel.Style = (Style)Resources["HeaderPanelStyle"];
        _headerFrozenPanel.HorizontalAlignment = HorizontalAlignment.Left;
        _headerScrollPanel.HorizontalAlignment = HorizontalAlignment.Left;
        HeaderHost.Children.Add(_headerScrollPanel);
        HeaderHost.Children.Add(_headerFrozenPanel);

        // 入力中のセルの外が押されたら、入力を終える (行の中でも外でも。入力欄はクリックでフォーカスが移らない所があるので、自分で閉じる)
        Scroller.AddHandler(PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler((_, e) =>
        {
            foreach (var presenter in _presenters)
            {
                presenter.EndEditIfOutside(e.OriginalSource as DependencyObject);
            }
        }), handledEventsToo: true);
        Loaded += (_, _) =>
        {
            AttachHeaderAnimations();
            foreach (var presenter in _presenters)
            {
                presenter.TryAnchor();
            }
        };
    }

    /// <summary>列の変更 (追加・表示・非表示・幅)を、表に反映する</summary>
    /// <remarks>列を並べ終えたあと・表示の切り替えのあとに呼ぶ。</remarks>
    public void RefreshColumns()
    {
        _visibleColumns = [.. Columns.Where(c => c.IsVisible).OrderBy(c => c.IsFrozen ? 0 : 1)];
        ColumnsVersion++;
        ContentGrid.Width = _visibleColumns.Sum(c => c.Width);
        RebuildHeader();
        foreach (var presenter in _presenters)
        {
            presenter.Rebuild();
        }
    }

    /// <summary>入力用の見た目を、表示用に戻す</summary>
    /// <param name="element">入力欄 (入力用の見た目の中の要素)</param>
    /// <remarks>
    /// 入力を確定したとき・プルダウンや日付の選択を閉じたときに呼ぶ。フォーカスが入力欄にあれば、セルに戻す。
    /// フォーカスが外れたときは、自動で戻るので、呼ばなくてよい。
    /// </remarks>
    public static void EndEdit(DependencyObject element)
    {
        for (var current = element; current is not null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is TreeGridRowPresenter presenter)
            {
                presenter.EndEditLater();
                return;
            }
        }
    }

    /// <summary>列の幅の変更だけを、表に反映する</summary>
    /// <remarks>
    /// <see cref="RefreshColumns"/> と違い、セルを作り直さない (入力中のフォーカスを保つ)。
    /// 列の表示・非表示・見出し・並びを変えたときは、<see cref="RefreshColumns"/> を呼ぶ。
    /// </remarks>
    public void RefreshColumnWidths()
    {
        ContentGrid.Width = _visibleColumns.Sum(c => c.Width);
        RebuildHeader();
        foreach (var presenter in _presenters)
        {
            presenter.UpdateWidths();
        }
    }

    /// <summary>行のセルにフォーカスを移す (行が画面の外なら、見える所までスクロールする)</summary>
    /// <param name="row">行</param>
    /// <param name="columnKey">列のキー (<see cref="TreeGridColumn.Key"/>)</param>
    /// <remarks>行を作って配置し終えてから移すので、少し遅れて動く。</remarks>
    public void FocusCell(ITreeGridRow row, string columnKey)
    {
        var index = (Repeater.ItemsSource as IList)?.IndexOf(row) ?? -1;
        if (index < 0)
        {
            return;
        }

        (Repeater.TryGetElement(index) ?? Repeater.GetOrCreateElement(index)).StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (Repeater.TryGetElement(index) is TreeGridRowPresenter presenter)
            {
                presenter.FocusCell(columnKey);
            }
        });
    }

    /// <summary>要素が属する行を返す (行の中の入力欄・メニューの対象から、行を知るため)</summary>
    /// <param name="element">行の中の要素 (行の表示部品そのものでもよい)</param>
    /// <returns>属する行。行の外なら null</returns>
    public static ITreeGridRow? GetRow(DependencyObject? element)
    {
        for (var current = element; current is not null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is TreeGridRowPresenter presenter)
            {
                return presenter.Row;
            }
        }
        return null;
    }

    /// <summary>スクロールの位置を表す、コンポジションの値の集まりを返す</summary>
    /// <returns>値の集まり (Translation が、スクロールと逆向きに動く)</returns>
    internal CompositionPropertySet GetScrollProperties()
        => _scrollProperties ??= ElementCompositionPreview.GetScrollViewerManipulationPropertySet(Scroller);

    /// <summary>セルがクリックされたことを知らせる</summary>
    /// <param name="row">行</param>
    /// <param name="columnKey">列のキー</param>
    /// <param name="cell">セル</param>
    internal void RaiseCellInvoked(ITreeGridRow row, string columnKey, FrameworkElement cell)
        => CellInvoked?.Invoke(this, new TreeGridCellEventArgs(row, columnKey, cell));

    /// <summary>行の開閉ボタンが押されたことを知らせる</summary>
    /// <param name="row">押された行</param>
    internal void RaiseRowToggleRequested(ITreeGridRow row) => RowToggleRequested?.Invoke(this, new TreeGridRowEventArgs(row));

    /// <summary>行の中にフォーカスが入った・右クリックされたとき、その行を選ぶ</summary>
    /// <param name="row">その行</param>
    internal void NotifyRowActivated(ITreeGridRow row) => SelectedRow = row;

    /// <summary>行の表示部品を、作ったときに登録する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">表示部品の情報</param>
    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is TreeGridRowPresenter presenter && Repeater.ItemsSourceView.GetAt(args.Index) is ITreeGridRow row)
        {
            _presenters.Add(presenter);
            presenter.Bind(this, row);
        }
    }

    /// <summary>行の表示部品を、使い回しに戻すときに外す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">表示部品の情報</param>
    private void OnElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is TreeGridRowPresenter presenter)
        {
            presenter.Unbind();
            _presenters.Remove(presenter);
        }
    }

    /// <summary>見出しの行を作り直す</summary>
    private void RebuildHeader()
    {
        HeaderHost.Height = HeaderHeight;
        var frozen = _visibleColumns.Where(c => c.IsFrozen).ToList();
        var scrolling = _visibleColumns.Where(c => !c.IsFrozen).ToList();
        var frozenWidth = frozen.Sum(c => c.Width);

        _headerFrozenPanel.Width = frozenWidth;
        _headerFrozenPanel.Child = BuildHeaderGrid(frozen);
        _headerScrollPanel.Margin = new Thickness(frozenWidth, 0, 0, 0);
        _headerScrollPanel.Width = scrolling.Sum(c => c.Width);
        _headerScrollPanel.Style = (Style)Resources["ScrollPanelStyle"];
        _headerScrollPanel.Child = BuildHeaderGrid(scrolling);
    }

    /// <summary>見出しのセルを並べた Grid を作る</summary>
    /// <param name="columns">並べる列</param>
    /// <returns>見出しのセルを並べた Grid</returns>
    private Grid BuildHeaderGrid(List<TreeGridColumn> columns)
    {
        var grid = new Grid();
        for (var i = 0; i < columns.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(columns[i].Width) });
            var cell = new Border
            {
                Background = columns[i].HeaderBackground,
                BorderBrush = columns[i].HeaderUnderline,
                BorderThickness = new Thickness(0, 0, 0, columns[i].HeaderUnderline is null ? 0 : 2),
            };
            cell.Child = new TextBlock { Text = columns[i].Header, Style = (Style)Resources["HeaderTextStyle"] };
            if (columns[i].HeaderToolTip is { } toolTip)
            {
                ToolTipService.SetToolTip(cell, toolTip);
            }
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }
        return grid;
    }

    /// <summary>見出しを、スクロールの位置に合わせて動かす式を設定する</summary>
    /// <remarks>見出しの行全体は、縦のスクロールの分だけ、固定の列の部分は、横のスクロールの分だけ、逆向きに動かして、画面に残す。</remarks>
    private void AttachHeaderAnimations()
    {
        var scroll = GetScrollProperties();
        Anchor(HeaderHost, scroll, "-scroll.Translation.Y", "Translation.Y");
        Anchor(_headerFrozenPanel, scroll, "-scroll.Translation.X", "Translation.X");
    }

    /// <summary>要素を、スクロールの位置に合わせて動かす式を設定する</summary>
    /// <param name="element">動かす要素</param>
    /// <param name="scroll">スクロールの位置を表す値の集まり</param>
    /// <param name="expression">動かす量の式</param>
    /// <param name="property">動かす対象のプロパティ (Translation.X など)</param>
    internal static void Anchor(UIElement element, CompositionPropertySet scroll, string expression, string property)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
        var animation = visual.Compositor.CreateExpressionAnimation(expression);
        animation.SetReferenceParameter("scroll", scroll);
        visual.StartAnimation(property, animation);
    }
}
