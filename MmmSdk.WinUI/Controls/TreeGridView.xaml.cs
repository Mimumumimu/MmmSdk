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

    /// <summary>直近の右クリック (押下)で当たった、入力できるセルの列のキー (無ければ null)</summary>
    /// <remarks>行のメニューを開くときに、押した所の列に合わせて項目を変えるために使う。キーボードでメニューを開いたときは null。</remarks>
    public string? ContextColumnKey { get; internal set; }

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

    /// <summary>入力できる行の、固定の列の背景 (null なら、既定の色)</summary>
    /// <remarks>
    /// 固定の列を、スクロールする列と、背景の色で区別したいときに使う (例: 初回だけ入力する列を、薄い色にする)。
    /// 横にスクロールしたセルを隠すので、不透明な色にすること。
    /// </remarks>
    public Microsoft.UI.Xaml.Media.Brush? FrozenBackground
    {
        get => (Microsoft.UI.Xaml.Media.Brush?)GetValue(FrozenBackgroundProperty);
        set => SetValue(FrozenBackgroundProperty, value);
    }

    /// <summary><see cref="FrozenBackground"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty FrozenBackgroundProperty = DependencyProperty.Register(
        nameof(FrozenBackground),
        typeof(Microsoft.UI.Xaml.Media.Brush),
        typeof(TreeGridView),
        new PropertyMetadata(null, (d, _) =>
        {
            foreach (var presenter in ((TreeGridView)d)._presenters)
            {
                presenter.UpdateSummary();
            }
        }));

    /// <summary>集計の行の背景 (null なら、既定の色)</summary>
    /// <remarks>使う側のテーマの色を渡すときに使う (<c>{ThemeResource ...}</c>で渡すと、テーマの切り替えにも追従する)。</remarks>
    public Microsoft.UI.Xaml.Media.Brush? SummaryBackground
    {
        get => (Microsoft.UI.Xaml.Media.Brush?)GetValue(SummaryBackgroundProperty);
        set => SetValue(SummaryBackgroundProperty, value);
    }

    /// <summary><see cref="SummaryBackground"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty SummaryBackgroundProperty = DependencyProperty.Register(
        nameof(SummaryBackground),
        typeof(Microsoft.UI.Xaml.Media.Brush),
        typeof(TreeGridView),
        new PropertyMetadata(null, (d, _) =>
        {
            foreach (var presenter in ((TreeGridView)d)._presenters)
            {
                presenter.UpdateSummary();
            }
        }));

    /// <summary>入力中のセルで Esc が押され、入力が取りやめられた (入力欄を、元の値に戻すために使う)</summary>
    public event EventHandler<TreeGridCellEventArgs>? EditCanceled;

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

        // Tab・Shift+Tab は、入力中のセルから、次・前の入力できるセルへ移す
        Scroller.AddHandler(KeyDownEvent, new Microsoft.UI.Xaml.Input.KeyEventHandler(OnScrollerKeyDown), handledEventsToo: false);
        Scroller.GotFocus += (_, e) =>
        {
            // セルの外 (開閉ボタンなど)にフォーカスが来たら、「今のセル」を外す (枠が、前のセルと二重に残らないように)
            var inCell = false;
            for (var current = e.OriginalSource as DependencyObject; current is not null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
            {
                if (current is TreeGridRowPresenter presenter)
                {
                    inCell = presenter.GetFocusedColumnKey(e.OriginalSource as DependencyObject) is not null;
                    break;
                }
            }
            if (!inCell && CursorCell is not null)
            {
                CursorCell = null;
                RefreshFrames();
            }
        };
        Loaded += (_, _) =>
        {
            AttachHeaderAnimations();
            foreach (var presenter in _presenters)
            {
                presenter.TryAnchor();
            }
        };
    }

    /// <summary>今のセル (フォーカスの枠を出すセル)</summary>
    internal FrameworkElement? CursorCell { get; private set; }

    /// <summary>今のセルを切り替えて、枠を直す</summary>
    /// <param name="cell">フォーカスの来たセル</param>
    /// <remarks>ポップアップ (メニューなど)は、開くのが少し遅れるので、遅れても枠を直す。</remarks>
    internal void SetCursor(FrameworkElement cell)
    {
        CursorCell = cell;
        RefreshFrames();
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, RefreshFrames);
    }

    /// <summary>すべての行のセルの枠を直す</summary>
    private void RefreshFrames()
    {
        foreach (var presenter in _presenters)
        {
            presenter.RefreshFrames();
        }
    }

    /// <summary>Tab・Shift+Tab・矢印キーで、今のセルを移す (Esc は、入力の取りやめ)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キー入力の情報</param>
    /// <remarks>
    /// XAML の標準の移動に任せると、表全体が 1 つの止まる所になって Tab でセルの間を移れず、矢印キーは、見えないセルに移る。
    /// そこで、すべてのセル (入力できないセルも)を、行と列の並びで、自分で移す。移ったセルは、フォーカスと枠だけで、入力は始めない。
    /// Tab は、入力中でも同じ (入力は、フォーカスが外れて確定する)。矢印キーは、セルそのものにフォーカスがあるときだけ (入力欄の中では、文字のカーソルを動かす)。
    /// Tab で、表の最後・最初のセルから先へは移らず、そのまま表の外へ出る。
    /// </remarks>
    private void OnScrollerKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            // Esc は、どのセルでも同じ: 入力を取りやめて (元の値に戻して)、セルに戻る
            foreach (var presenter in _presenters)
            {
                if (presenter.TryCancelEdit(e.OriginalSource as DependencyObject))
                {
                    e.Handled = true;
                    return;
                }
            }
            return;
        }

        if (e.Key is not (Windows.System.VirtualKey.Tab or Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Up or Windows.System.VirtualKey.Down)
            || Repeater.ItemsSource is not IList rows)
        {
            return;
        }

        for (var current = e.OriginalSource as DependencyObject; current is not null; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is not TreeGridRowPresenter { Row: { } row } presenter)
            {
                continue;
            }

            var key = presenter.GetFocusedColumnKey(e.OriginalSource as DependencyObject);
            var column = _visibleColumns.FindIndex(c => c.Key == key);
            var rowIndex = rows.IndexOf(row);
            if (key is null || column < 0 || rowIndex < 0)
            {
                return;
            }

            var count = _visibleColumns.Count;
            int target;
            if (e.Key == Windows.System.VirtualKey.Tab)
            {
                var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                target = (rowIndex * count) + column + (shift ? -1 : 1);
                if (target < 0 || target >= rows.Count * count)
                {
                    return;
                }
            }
            else if (presenter.IsCellItself(e.OriginalSource as DependencyObject))
            {
                var (rowStep, columnStep) = e.Key switch
                {
                    Windows.System.VirtualKey.Left => (0, -1),
                    Windows.System.VirtualKey.Right => (0, 1),
                    Windows.System.VirtualKey.Up => (-1, 0),
                    _ => (1, 0),
                };
                var nextRow = rowIndex + rowStep;
                var nextColumn = column + columnStep;
                e.Handled = true;
                if (nextRow < 0 || nextRow >= rows.Count || nextColumn < 0 || nextColumn >= count)
                {
                    return;
                }
                target = (nextRow * count) + nextColumn;
            }
            else
            {
                return;
            }

            e.Handled = true;
            if (rows[target / count] is ITreeGridRow targetRow)
            {
                FocusCell(targetRow, _visibleColumns[target % count].Key, false);
            }
            return;
        }
    }

    /// <summary>セルが見える位置になるよう、スクロールする</summary>
    /// <param name="cell">セル</param>
    /// <param name="isFrozen">固定の列のセルか (横には動かさない)</param>
    /// <remarks>
    /// 固定の列は、見かけの位置だけをずらしているので、並び上は、スクロールする列の左端に重なる。
    /// そのため、<c>StartBringIntoView</c> では、固定の列の下に隠れたセルを、見えているものとして扱ってしまう。
    /// そこで、固定の列の幅と、見出しの高さを除いた範囲に、セルが入るように、位置を決める。
    /// </remarks>
    internal void RevealCell(FrameworkElement cell, bool isFrozen)
    {
        if (cell.ActualWidth <= 0)
        {
            return;
        }

        var bounds = cell.TransformToVisual(ContentGrid).TransformBounds(new Windows.Foundation.Rect(0, 0, cell.ActualWidth, cell.ActualHeight));
        var frozenWidth = _visibleColumns.Where(c => c.IsFrozen).Sum(c => c.Width);

        double? x = null;
        if (!isFrozen)
        {
            if (bounds.Left < Scroller.HorizontalOffset + frozenWidth)
            {
                x = bounds.Left - frozenWidth;
            }
            else if (bounds.Right > Scroller.HorizontalOffset + Scroller.ViewportWidth)
            {
                x = bounds.Right - Scroller.ViewportWidth;
            }
        }

        double? y = null;
        if (bounds.Top < Scroller.VerticalOffset + HeaderHeight)
        {
            y = bounds.Top - HeaderHeight;
        }
        else if (bounds.Bottom > Scroller.VerticalOffset + Scroller.ViewportHeight)
        {
            y = bounds.Bottom - Scroller.ViewportHeight;
        }

        if (x is not null || y is not null)
        {
            Scroller.ChangeView(x, y, null, true);
        }
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
    public void FocusCell(ITreeGridRow row, string columnKey) => FocusCell(row, columnKey, true);

    /// <summary>セルにフォーカスを移す</summary>
    /// <param name="row">行</param>
    /// <param name="columnKey">列のキー</param>
    /// <param name="edit">入力も始めるなら true。フォーカスだけ移す (枠が出るだけ)なら false</param>
    private void FocusCell(ITreeGridRow row, string columnKey, bool edit)
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
                presenter.FocusCell(columnKey, edit);
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

    /// <summary>入力が取りやめられたことを知らせる</summary>
    /// <param name="row">行</param>
    /// <param name="columnKey">列のキー</param>
    /// <param name="cell">セル</param>
    internal void RaiseEditCanceled(ITreeGridRow row, string columnKey, FrameworkElement cell)
        => EditCanceled?.Invoke(this, new TreeGridCellEventArgs(row, columnKey, cell));

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
