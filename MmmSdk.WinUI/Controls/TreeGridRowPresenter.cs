using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace MmmSdk.WinUI.Controls;

/// <summary><see cref="TreeGridView"/> の 1 行の表示部品 (行の一覧の要素ごとに、使い回される)</summary>
/// <remarks>
/// 固定の列を並べた部分と、スクロールする列を並べた部分の 2 つを持つ。固定の部分は不透明で、スクロールの位置に合わせて逆向きに動かし、左に残す。
/// セルは、列ごとに <see cref="ContentControl"/> を 1 つ作り、列の <see cref="TreeGridColumn.CellTemplate"/> に行を渡して描く。使い回すときは、行だけを差し替える。
/// 表の中で使う部品で、単独では使わない (表の項目のテンプレートが作る)。
/// </remarks>
public sealed partial class TreeGridRowPresenter : Grid
{
    /// <summary>固定の列の部分</summary>
    private readonly Border _frozenPanel = new();

    /// <summary>スクロールする列の部分</summary>
    private readonly Border _scrollPanel = new();

    /// <summary>固定の列を並べる Grid</summary>
    private readonly Grid _frozenGrid = new();

    /// <summary>スクロールする列を並べる Grid</summary>
    private readonly Grid _scrollGrid = new();

    /// <summary>行の色 (固定の部分)</summary>
    private readonly Border _frozenTint = new() { IsHitTestVisible = false };

    /// <summary>スクロールする列ごとの、集計の行の背景と行の色 (列ごとに分けて、一部の列だけ表の面の色のままにできるようにする)</summary>
    private readonly List<(TreeGridColumn Column, Border Backing, Border Tint)> _scrollBackings = [];

    /// <summary>選んだ行の枠 (固定の列の部分。行の背景の色と混ざらない)</summary>
    /// <remarks>セルのフォーカスの枠より後ろに描くため、行全体ではなく、固定の列・スクロールする列それぞれの Grid の中に置く。</remarks>
    private readonly Border _frozenSelection = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, -1) };

    /// <summary>選んだ行の枠 (スクロールする列の部分)</summary>
    private readonly Border _scrollSelection = new() { IsHitTestVisible = false, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, -1) };

    /// <summary>セル (表示する列の順)</summary>
    private readonly List<(TreeGridColumn Column, ContentControl Cell)> _cells = [];

    /// <summary>列ごとの、作ったセル (列を変えて作り直すとき、表示を続ける列のセルを使い回すため)</summary>
    private readonly Dictionary<TreeGridColumn, ContentControl> _cellCache = [];

    /// <summary>セルごとの、フォーカスの枠 (セルの上に重ねる)</summary>
    private readonly Dictionary<ContentControl, Border> _frames = [];

    /// <summary>木構造の列のセルを並べた入れ物 (字下げ・開閉ボタン・セル)</summary>
    private Grid? _treeGrid;

    /// <summary>木構造の列の字下げ</summary>
    private readonly Border _indent = new();

    /// <summary>木構造の列の開閉ボタン</summary>
    private readonly Button _toggle = new();

    /// <summary>開閉ボタンの矢印</summary>
    private readonly FontIcon _toggleIcon = new() { FontSize = 10 };

    /// <summary>この部品が属する表</summary>
    private TreeGridView? _owner;

    /// <summary>今の行</summary>
    private ITreeGridRow? _row;

    /// <summary>要素がセルの中にあるとき、そのセルの列のキーを返す (入力中でなくてもよい)</summary>
    /// <param name="element">フォーカスのある要素など</param>
    /// <returns>列のキー。どのセルの中でもなければ null</returns>
    internal string? GetFocusedColumnKey(DependencyObject? element)
    {
        foreach (var (column, cell) in _cells)
        {
            if (IsInside(cell, element))
            {
                return column.Key;
            }
        }
        return null;
    }

    /// <summary>入力用の見た目になっているセル (無ければ null。1 行に 1 つだけ)</summary>
    private (TreeGridColumn Column, ContentControl Cell)? _editing;

    /// <summary>セルを作ったときの、表の列の版</summary>
    private int _columnsVersion = -1;

    /// <summary>固定の列の部分を、スクロールに合わせて動かす式を設定済みか</summary>
    private bool _anchored;

    /// <summary>今の行 (使い回しに戻している間は null)</summary>
    public ITreeGridRow? Row => _row;

    /// <summary>行の表示部品を作る</summary>
    public TreeGridRowPresenter()
    {
        _frozenGrid.Children.Add(_frozenTint);
        _frozenPanel.Child = _frozenGrid;
        _scrollPanel.Child = _scrollGrid;
        _frozenPanel.HorizontalAlignment = HorizontalAlignment.Left;
        _scrollPanel.HorizontalAlignment = HorizontalAlignment.Left;
        // 順番 (Tab の順)は、固定の列が先。重なりは、固定の列が上
        Children.Add(_frozenPanel);
        Children.Add(_scrollPanel);
        Canvas.SetZIndex(_frozenPanel, 1);

        _toggleIcon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons");
        _toggle.Content = _toggleIcon;
        _toggle.IsTabStop = false;
        _toggle.Click += (_, _) =>
        {
            if (_owner is not null && _row is not null)
            {
                _owner.RaiseRowToggleRequested(_row);
            }
        };

        // 行の全体を、クリックを受ける面にする (背景が無い所は、クリックが通り抜けて、表の外側に当たってしまう)
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        // セルへのクリックは、セルの部品ではなく、行の上の位置で受ける (セルの空いた所も、同じセルのクリックにするため)
        AddHandler(TappedEvent, new TappedEventHandler(OnRowTapped), handledEventsToo: true);

        // 行の選択は、フォーカスではなく、行の中の押下 (右クリックを含む)で決める (入力欄などが処理済みにしても、受ける)
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            Activate();
            if (_owner is not null)
            {
                var point = e.GetCurrentPoint(this);
                var contextCell = point.Properties.IsRightButtonPressed ? FindEditableCellAt(point.Position) : null;
                _owner.ContextColumnKey = contextCell?.Column.Key;
                _owner.ContextCell = contextCell?.Cell;

                // 入力に入らないセル (集計の行・備考のボタンなど)は、押した時点で、セルにフォーカスを移す。
                // 備考のボタンが開く入れ物を閉じたとき、フォーカスは、開く前の場所に戻るので、先にそのセルへ移しておく。
                // 右クリックは、どのセルでも同じように移す (入力は始めない。入力中のセルの中は、入力欄自身のメニューに任せる)
                if (FindCellAt(point.Position) is { } pressed && pressed.Column.IsFocusable)
                {
                    var isRight = point.Properties.IsRightButtonPressed;
                    var canEdit = pressed.Column.IsEditable && _row is not null && _row.CanEdit(pressed.Column.Key);
                    var isEditingCell = _editing is { } editing && ReferenceEquals(editing.Cell, pressed.Cell);
                    if ((point.Properties.IsLeftButtonPressed && !canEdit) || (isRight && !isEditingCell))
                    {
                        pressed.Cell.Focus(FocusState.Programmatic);
                        _owner.SetCursor(pressed.Cell);
                    }
                }
            }
        }), handledEventsToo: true);
        // キーボードの操作では、右クリックで当たった列を使わない (メニューキーで開くメニューに、前の右クリックの列を残さない)
        AddHandler(KeyDownEvent, new KeyEventHandler((_, _) =>
        {
            if (_owner is not null)
            {
                _owner.ContextColumnKey = null;
                _owner.ContextCell = null;
            }
        }), handledEventsToo: true);
        // キーボードのフォーカスだけ、行を選ぶ (表の空いた所を押したときに、XAML が最初の要素へ移すフォーカスでは、選ばない)
        GotFocus += (_, e) =>
        {
            if (e.OriginalSource is Control { FocusState: FocusState.Keyboard })
            {
                Activate();
            }
        };
        AddHandler(RightTappedEvent, new RightTappedEventHandler((_, _) => Activate()), handledEventsToo: true);
    }

    /// <summary>行に結び付ける</summary>
    /// <param name="owner">表</param>
    /// <param name="row">行</param>
    internal void Bind(TreeGridView owner, ITreeGridRow row)
    {
        if (!ReferenceEquals(_owner, owner) || _columnsVersion != owner.ColumnsVersion)
        {
            _owner = owner;
            Rebuild();
        }

        if (_row is not null)
        {
            _row.PropertyChanged -= OnRowPropertyChanged;
        }
        ResetEdit(raiseEnding: false);

        // 使い回しの待機中に、列の幅が変わっていたことがあるので、結び付けるたびに、今の幅に合わせる
        UpdateWidths();
        _row = row;
        _row.PropertyChanged += OnRowPropertyChanged;

        Height = owner.RowHeight;
        ContextFlyout = owner.RowContextFlyout;
        foreach (var (column, cell) in _cells)
        {
            cell.Content = row;
            cell.IsTabStop = column.IsFocusable;
            if (_frames.TryGetValue(cell, out var frame))
            {
                frame.Visibility = Visibility.Collapsed;
            }
        }
        UpdateSummary();
        UpdateTint();
        UpdateTree();
        UpdateSelected();
    }

    /// <summary>行の色 (<see cref="ITreeGridRow.RowTint"/>)を、両方の部分に重ねる</summary>
    private void UpdateTint()
    {
        Microsoft.UI.Xaml.Media.Brush? brush = _row?.RowTint is { } color ? new SolidColorBrush(color) : null;
        _frozenTint.Background = brush;
        foreach (var (column, _, tint) in _scrollBackings)
        {
            tint.Background = column.KeepsBaseBackground ? null : brush;
        }
    }

    /// <summary>行の背景を直す (集計の行は、固定の部分・スクロールする部分とも、集計の行の背景。入力できる行は、固定の部分だけ、固定の列の背景)</summary>
    /// <remarks>
    /// 入力できる行 (入力欄が並ぶ)と、入力できない集計の行を、背景の色で見分けられるようにする。
    /// 使う側が色を渡していれば (<see cref="TreeGridView.SummaryBackground"/>・<see cref="TreeGridView.FrozenBackground"/>)、その色にする。
    /// </remarks>
    internal void UpdateSummary()
    {
        if (_owner is null)
        {
            return;
        }

        var summary = _row is { IsSummary: true };
        _frozenPanel.ClearValue(Border.BackgroundProperty);
        _scrollPanel.ClearValue(Border.BackgroundProperty);
        _frozenPanel.Style = (Style)_owner.Resources[summary ? "SummaryPanelStyle" : "FrozenPanelStyle"];
        _scrollPanel.Style = (Style)_owner.Resources[summary ? "SummaryPanelStyle" : "ScrollPanelStyle"];
        foreach (var (_, backing, _) in _scrollBackings)
        {
            backing.ClearValue(FrameworkElement.StyleProperty);
            backing.Background = null;
        }

        if (summary && _owner.SummaryBackground is { } custom)
        {
            _frozenPanel.Background = custom;
            // スクロールする部分は、列ごとの背景にする (表の面の色のままにする列を、除くため)
            _scrollPanel.Background = null;
            foreach (var (column, backing, _) in _scrollBackings)
            {
                if (column.KeepsBaseBackground)
                {
                    // 入力できる行のスクロールする列と同じ色 (テーマごとの、表の面の色)にする
                    backing.ClearValue(Border.BackgroundProperty);
                    backing.Style = (Style)_owner.Resources["ScrollBackingStyle"];
                }
                else
                {
                    backing.Background = custom;
                }
            }
        }
        else if (!summary && _owner.FrozenBackground is { } frozen)
        {
            _frozenPanel.Background = frozen;
        }
    }

    /// <summary>行との結び付きを外す (使い回しに戻すとき)</summary>
    internal void Unbind()
    {
        ResetEdit(raiseEnding: false);
        if (_row is not null)
        {
            _row.PropertyChanged -= OnRowPropertyChanged;
        }
        _row = null;
    }

    /// <summary>列に合わせて、セルを作り直す</summary>
    internal void Rebuild()
    {
        if (_owner is null)
        {
            return;
        }

        _columnsVersion = _owner.ColumnsVersion;
        _editing = null;
        // 字下げと開閉ボタンは、作り直しても同じものを使うので、前の入れ物から外しておく (別の入れ物に、二重には入れられない)
        _treeGrid?.Children.Clear();
        _frozenGrid.Children.Clear();
        _frozenGrid.ColumnDefinitions.Clear();
        _scrollGrid.Children.Clear();
        _scrollGrid.ColumnDefinitions.Clear();
        _scrollBackings.Clear();
        _cells.Clear();
        _frames.Clear();

        _frozenPanel.Style = (Style)_owner.Resources["FrozenPanelStyle"];
        _scrollPanel.Style = (Style)_owner.Resources["ScrollPanelStyle"];
        _frozenSelection.Style = (Style)_owner.Resources["SelectionFrameStyle"];
        _scrollSelection.Style = (Style)_owner.Resources["SelectionFrameStyle"];
        _frozenSelection.Margin = new Thickness(0, 0, 0, -1);
        _scrollSelection.Margin = new Thickness(0, 0, 0, -1);
        _toggle.Style = (Style)_owner.Resources["ToggleButtonStyle"];
        _frozenGrid.Children.Add(_frozenTint);

        var frozenWidth = 0.0;
        var scrollWidth = 0.0;
        var pendingFrames = new List<(Grid Grid, Border Frame)>();
        foreach (var column in _owner.VisibleColumns)
        {
            var grid = column.IsFrozen ? _frozenGrid : _scrollGrid;
            var index = grid.ColumnDefinitions.Count;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(column.Width) });
            if (!column.IsFrozen)
            {
                var tint = new Border { IsHitTestVisible = false };
                var backing = new Border { IsHitTestVisible = false, Child = tint };
                Grid.SetColumn(backing, index);
                grid.Children.Add(backing);
                _scrollBackings.Add((column, backing, tint));
            }

            // 見た目の作成 (テンプレートの展開)が重いので、前から表示していた列のセルは作り直さず、使い回す
            // (入力用の見た目に替わっていたセルは、テンプレートが違うので、作り直す)
            if (!_cellCache.TryGetValue(column, out var cell) || !ReferenceEquals(cell.ContentTemplate, column.CellTemplate))
            {
                cell = new ContentControl
                {
                    ContentTemplate = column.CellTemplate,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    Padding = new Thickness(0),
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                };
                AttachCell(column, cell);
                _cellCache[column] = cell;
            }
            cell.IsTabStop = column.IsFocusable;
            cell.Content = _row;
            _cells.Add((column, cell));

            FrameworkElement element = column.IsTree ? BuildTreeCell(cell) : cell;
            Grid.SetColumn(element, index);
            grid.Children.Add(element);
            var frame = new Border
            {
                BorderThickness = new Thickness(1),
                // 上端は行の上端に合わせ、下端は行の下線の位置まで 1px はみ出す (行の高さいっぱいにする)
                Margin = new Thickness(0, 0, 0, -1),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };
            Grid.SetColumn(frame, index);
            pendingFrames.Add((grid, frame));
            _frames[cell] = frame;

            if (column.IsFrozen)
            {
                frozenWidth += column.Width;
            }
            else
            {
                scrollWidth += column.Width;
            }
        }

        // 重なりの順: セル・背景 → 選んだ行の紫の線 → セルのフォーカスの枠 (枠が、紫の線より前に出る)
        Grid.SetColumnSpan(_frozenSelection, Math.Max(1, _frozenGrid.ColumnDefinitions.Count));
        Grid.SetColumnSpan(_scrollSelection, Math.Max(1, _scrollGrid.ColumnDefinitions.Count));
        _frozenGrid.Children.Add(_frozenSelection);
        _scrollGrid.Children.Add(_scrollSelection);
        foreach (var (grid, frame) in pendingFrames)
        {
            grid.Children.Add(frame);
        }

        // 表示しなくなった列のセルは、持ち続けない (行への結び付きを残さない。また表示するときは、作り直す)
        foreach (var column in _cellCache.Keys.Where(c => !_owner.VisibleColumns.Contains(c)).ToList())
        {
            _cellCache.Remove(column);
        }

        Grid.SetColumnSpan(_frozenTint, Math.Max(1, _frozenGrid.ColumnDefinitions.Count));
        _frozenPanel.Width = frozenWidth;
        _scrollPanel.Width = scrollWidth;
        _scrollPanel.Margin = new Thickness(frozenWidth, 0, 0, 0);

        TryAnchor();
        UpdateSummary();
        UpdateTint();
        UpdateTree();
        UpdateSelected();
    }

    /// <summary>列の定義の幅を、違うときだけ変える</summary>
    /// <param name="definition">列の定義</param>
    /// <param name="width">幅</param>
    private static void SetWidth(ColumnDefinition definition, double width)
    {
        if (definition.Width.Value != width || !definition.Width.IsAbsolute)
        {
            definition.Width = new GridLength(width);
        }
    }

    /// <summary>列の幅だけを、表の今の値に合わせる (セルは作り直さない)</summary>
    internal void UpdateWidths()
    {
        if (_owner is null || _columnsVersion != _owner.ColumnsVersion)
        {
            return;
        }

        var frozenWidth = 0.0;
        var scrollWidth = 0.0;
        var frozenIndex = 0;
        var scrollIndex = 0;
        foreach (var column in _owner.VisibleColumns)
        {
            if (column.IsFrozen)
            {
                SetWidth(_frozenGrid.ColumnDefinitions[frozenIndex++], column.Width);
                frozenWidth += column.Width;
            }
            else
            {
                SetWidth(_scrollGrid.ColumnDefinitions[scrollIndex++], column.Width);
                scrollWidth += column.Width;
            }
        }

        _frozenPanel.Width = frozenWidth;
        _scrollPanel.Width = scrollWidth;
        _scrollPanel.Margin = new Thickness(frozenWidth, 0, 0, 0);
    }

    /// <summary>固定の部分を、スクロールの位置に合わせて動かす式を設定する (表を画面に出したあとから)</summary>
    /// <remarks>横のスクロールの分だけ逆向きに動かして、左に残す。表がまだ画面に出ていなければ、何もしない (表が出たときに、もう一度呼ばれる)。</remarks>
    internal void TryAnchor()
    {
        if (_anchored || _owner is not { IsLoaded: true })
        {
            return;
        }

        TreeGridView.Anchor(_frozenPanel, _owner.GetScrollProperties(), "-scroll.Translation.X", "Translation.X");
        _anchored = true;
    }

    /// <summary>選んだ行かどうかに合わせて、選んだ行の枠を出す・消す</summary>
    internal void UpdateSelected()
    {
        var selected = _row is not null && _owner is not null && ReferenceEquals(_owner.SelectedRow, _row);
        _frozenSelection.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        _scrollSelection.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>行のセルにフォーカスを移す</summary>
    /// <param name="columnKey">列のキー</param>
    /// <param name="edit">入力も始めるなら true。フォーカスだけ移す (枠が出るだけ)なら false</param>
    internal void FocusCell(string columnKey, bool edit)
    {
        foreach (var (column, cell) in _cells)
        {
            if (column.Key != columnKey)
            {
                continue;
            }

            if (edit)
            {
                StartEdit(column, cell);
            }
            else
            {
                Activate();
                cell.Focus(FocusState.Keyboard);
            }
            return;
        }
    }

    /// <summary>列のキーのセルを返す</summary>
    /// <param name="columnKey">列のキー</param>
    /// <returns>セル。その列が無いか、表示していなければ null</returns>
    internal FrameworkElement? FindCell(string columnKey)
    {
        foreach (var (column, cell) in _cells)
        {
            if (column.Key == columnKey)
            {
                return cell;
            }
        }
        return null;
    }

    /// <summary>セルに、フォーカスの枠・キー操作の仕組みを付ける</summary>
    /// <param name="column">列</param>
    /// <param name="cell">セル</param>
    /// <remarks>
    /// フォーカスの来たセルが「今のセル」になり、枠が出る (入力中も、入力できないセルも同じ)。
    /// キーボードでフォーカスが来ただけでは、入力を始めない (メニュー・カレンダーが、閉じたあとに戻るフォーカスで開き直し続けるため)。
    /// フォーカスのあるセルで Enter か Space を押すと、入力を始める (入力できないセルは、中のボタンを押す)。
    /// </remarks>
    private void AttachCell(TreeGridColumn column, ContentControl cell)
    {
        cell.GotFocus += (_, _) =>
        {
            _owner?.SetCursor(cell);
            if (cell.FocusState == FocusState.Keyboard)
            {
                // 画面の外 (固定の列の下を含む)にあるセルなら、見える位置までスクロールする
                _owner?.RevealCell(cell, column.IsFrozen);
            }
        };
        cell.KeyDown += (_, e) =>
        {
            // Space は、入力欄に文字として届き、値を置き換えてしまうので、入力欄が開く列では使わない
            var usesSpace = column.IsInvokable || !column.IsEditable;
            if (!ReferenceEquals(e.OriginalSource, cell) || !(e.Key == Windows.System.VirtualKey.Enter || (usesSpace && e.Key == Windows.System.VirtualKey.Space)))
            {
                return;
            }

            e.Handled = true;
            if (column.IsEditable)
            {
                StartEdit(column, cell);
            }
            else if (MmmSdk.WinUI.Utilities.VisualTreeSearch.FindDescendant<Button>(cell) is { } button)
            {
                (new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke) as IInvokeProvider)?.Invoke();
            }
        };
        cell.LostFocus += (_, _) =>
        {
            // 入力欄の LostFocus (使う側の確定)が済んでから、判断する
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_editing is { } editing && ReferenceEquals(editing.Cell, cell))
                {
                    EndEditIfLeft(cell);
                }
                else
                {
                    UpdateFrame(cell);
                }
            });
        };
    }

    /// <summary>今のセルかどうかに合わせて、すべてのセルの枠を直す</summary>
    internal void RefreshFrames()
    {
        foreach (var cell in _frames.Keys)
        {
            UpdateFrame(cell);
        }
    }

    /// <summary>セルの中にフォーカスがあるか</summary>
    /// <param name="element">フォーカスのある要素など</param>
    /// <returns>セルそのもの (入力欄・ボタンの中ではない)にフォーカスがあるとき true</returns>
    internal bool IsCellItself(DependencyObject? element) => _cells.Any(c => ReferenceEquals(c.Cell, element));

    /// <summary>入力できるセルのうち、行の上の位置にあるものを返す</summary>
    /// <param name="position">行の左上を原点にした位置</param>
    /// <returns>セル。無ければ null</returns>
    private (TreeGridColumn Column, ContentControl Cell)? FindEditableCellAt(Windows.Foundation.Point position)
        => FindCellAt(position) is { } found && found.Column.IsEditable && _row is not null && _row.CanEdit(found.Column.Key) ? found : null;

    /// <summary>行の上の位置にあるセルを返す (入力できないセルも)</summary>
    /// <param name="position">行の左上を原点にした位置</param>
    /// <returns>セル。無ければ null</returns>
    private (TreeGridColumn Column, ContentControl Cell)? FindCellAt(Windows.Foundation.Point position)
    {
        foreach (var (column, cell) in _cells)
        {
            if (cell.ActualWidth <= 0)
            {
                continue;
            }

            var bounds = cell.TransformToVisual(this).TransformBounds(new Windows.Foundation.Rect(0, 0, cell.ActualWidth, cell.ActualHeight));
            if (bounds.Contains(position))
            {
                return (column, cell);
            }
        }
        return null;
    }

    /// <summary>行の上を左クリックしたとき、その位置のセルを入力用にする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">タップの情報</param>
    private void OnRowTapped(object sender, TappedRoutedEventArgs e)
    {
        if (FindEditableCellAt(e.GetPosition(this)) is { } target)
        {
            StartEdit(target.Column, target.Cell);
        }
    }

    /// <summary>入力中のセルの外が押されたら、入力を終えて、表示用に戻す</summary>
    /// <param name="source">押された要素</param>
    /// <remarks>戻す前に、<see cref="TreeGridView.EditEnding"/> で、使う側が入力した値を確定する。</remarks>
    internal void EndEditIfOutside(DependencyObject? source)
    {
        if (_editing is { } editing && !IsInside(editing.Cell, source))
        {
            ResetEdit();
        }
    }

    /// <summary>セルの入力を始める (入力欄に切り替える。クリックで選ぶ列は、使う側に知らせる)</summary>
    /// <param name="column">列</param>
    /// <param name="cell">セル</param>
    private void StartEdit(TreeGridColumn column, ContentControl cell)
    {
        if (_row is null || _owner is null || !column.IsEditable || !_row.CanEdit(column.Key))
        {
            return;
        }

        Activate();
        if (column.IsInvokable)
        {
            // 開く前にセルへフォーカスを移す (メニューが閉じたとき、フォーカスが戻る先。開いている間も、枠が出る)
            cell.Focus(FocusState.Programmatic);
            _owner.SetCursor(cell);
            _owner.RaiseCellInvoked(_row, column.Key, cell);
        }
        else
        {
            BeginEdit(column, cell);
        }
    }

    /// <summary>セルを入力用の見た目にして、入力欄にフォーカスを移す</summary>
    /// <param name="column">列</param>
    /// <param name="cell">セル</param>
    private void BeginEdit(TreeGridColumn column, ContentControl cell)
    {
        if (_row is null || column.EditTemplate is null || !_row.CanEdit(column.Key))
        {
            return;
        }
        if (_editing is { } current)
        {
            if (ReferenceEquals(current.Cell, cell))
            {
                return;
            }
            ResetEdit();
        }

        Activate();
        _editing = (column, cell);
        cell.IsTabStop = false;
        cell.ContentTemplate = column.EditTemplate;

        // 入力欄を作らせてから、フォーカスを移す (作られる前は、フォーカスできる要素が無い)
        cell.UpdateLayout();
        if (FocusManager.FindFirstFocusableElement(cell) is Control target)
        {
            target.Focus(FocusState.Programmatic);
        }
        _owner?.SetCursor(cell);
        _owner?.RevealCell(cell, column.IsFrozen);
    }

    /// <summary>フォーカスのあるセルに、枠を付ける (入力中・入力を終えてセルにフォーカスが残っている間)</summary>
    /// <param name="cell">セル</param>
    /// <remarks>今のセルで、フォーカスがセルの中 (入力欄を含む)にあるか、メニューなどのポップアップが開いている間だけ付ける。</remarks>
    private void UpdateFrame(ContentControl cell)
    {
        if (!_frames.TryGetValue(cell, out var frame))
        {
            return;
        }

        var on = ReferenceEquals(_owner?.CursorCell, cell)
            && (IsInside(cell, FocusManager.GetFocusedElement(XamlRoot) as DependencyObject) || VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot).Count > 0);
        frame.BorderBrush = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        frame.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>入力中のセルの中のキー入力 (Esc)で、入力を取りやめる</summary>
    /// <param name="source">キーを受けた要素</param>
    /// <returns>入力中のセルの中で、取りやめたら true</returns>
    /// <remarks>使う側に知らせてから (入力欄を元の値に戻してもらう)、表示用に戻す。戻すときのフォーカスの外れでは、元の値なので、保存されない。</remarks>
    internal bool TryCancelEdit(DependencyObject? source)
    {
        if (_editing is not { } editing || _row is null || _owner is null || !IsInside(editing.Cell, source))
        {
            return false;
        }

        _owner.RaiseEditCanceled(_row, editing.Column.Key, editing.Cell);
        EndEditLater(raiseEnding: false);
        return true;
    }

    /// <summary>入力用の見た目を、表示用に戻す (使う側の確定が済んでから)</summary>
    /// <param name="raiseEnding">戻す直前に、使う側へ知らせるか (Esc の取りやめでは知らせない)</param>
    internal void EndEditLater(bool raiseEnding = true) => DispatcherQueue.TryEnqueue(() => ResetEdit(raiseEnding));

    /// <summary>フォーカスがセルの外に出ていて、開いたままのプルダウン・日付の選択も無ければ、表示用に戻す</summary>
    /// <param name="cell">フォーカスが外れたセル</param>
    private void EndEditIfLeft(ContentControl cell)
    {
        if (_editing is not { } editing || !ReferenceEquals(editing.Cell, cell))
        {
            return;
        }

        // フォーカスが、まだセルの中にある、またはポップアップを開いている (ポップアップの中身は、セルの子ではない)間は、続ける
        if (IsInside(cell, FocusManager.GetFocusedElement(XamlRoot) as DependencyObject) || HasOpenPopup(cell))
        {
            return;
        }
        ResetEdit();
    }

    /// <summary>入力用の見た目を、すぐ表示用に戻す</summary>
    /// <param name="raiseEnding">戻す直前に、使う側へ知らせるか (<see cref="TreeGridView.EditEnding"/>。行の使い回し・Esc の取りやめでは知らせない)</param>
    /// <remarks>
    /// フォーカスのある入力欄を、フォーカスごと消すと、フォーカスが、画面の最初の要素 (表の 1 行目のセル)に飛び、行の選択まで 1 行目に移る。
    /// そこで、入力欄にフォーカスがあるときは、先にセル自身へ移してから、入力用の見た目を外す。
    /// 入力欄を外すと、フォーカスが外れた通知 (<c>LostFocus</c>)は、入力欄から行をたどれない状態で届くか、届かないことがあるので、
    /// 外す前に <see cref="TreeGridView.EditEnding"/> で、使う側に確定させる。
    /// </remarks>
    private void ResetEdit(bool raiseEnding = true)
    {
        if (_editing is not { } editing)
        {
            return;
        }

        _editing = null;
        if (raiseEnding && _row is not null)
        {
            _owner?.RaiseEditEnding(_row, editing.Column.Key, editing.Cell);
        }
        var hadFocus = IsInside(editing.Cell, FocusManager.GetFocusedElement(XamlRoot) as DependencyObject);
        editing.Cell.IsTabStop = true;
        if (hadFocus)
        {
            editing.Cell.Focus(FocusState.Programmatic);
        }
        editing.Cell.ContentTemplate = editing.Column.CellTemplate;
        editing.Cell.IsTabStop = true;
        UpdateFrame(editing.Cell);
    }

    /// <summary>要素が、親の中にあるか</summary>
    /// <param name="parent">親</param>
    /// <param name="element">調べる要素 (null でもよい)</param>
    /// <returns>親の中 (親そのものを含む)なら true</returns>
    private static bool IsInside(DependencyObject parent, DependencyObject? element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (ReferenceEquals(current, parent))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>セルの中に、開いたままのプルダウン・日付の選択があるか</summary>
    /// <param name="element">調べる要素</param>
    /// <returns>あれば true</returns>
    private static bool HasOpenPopup(DependencyObject element)
    {
        if (element is ComboBox { IsDropDownOpen: true } or CalendarDatePicker { IsCalendarOpen: true })
        {
            return true;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            if (HasOpenPopup(VisualTreeHelper.GetChild(element, i)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>木構造の列のセルに、字下げと開閉ボタンを付ける</summary>
    /// <param name="cell">列のセル</param>
    /// <returns>字下げ・開閉ボタン・セルを並べたもの</returns>
    private Grid BuildTreeCell(ContentControl cell)
    {
        var grid = new Grid();
        _treeGrid = grid;
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(_toggle, 1);
        Grid.SetColumn(cell, 2);
        grid.Children.Add(_indent);
        grid.Children.Add(_toggle);
        grid.Children.Add(cell);
        return grid;
    }

    /// <summary>字下げと開閉ボタンを、行の値に合わせる</summary>
    private void UpdateTree()
    {
        if (_row is null || _owner is null)
        {
            return;
        }

        // 子の無い行は、開閉ボタンの幅だけ、字下げを足して、名前の位置をそろえる
        var toggleWidth = 22.0;
        _indent.Width = 4 + (_row.Level * _owner.IndentWidth) + (_row.HasChildren ? 0 : toggleWidth);
        _toggle.Visibility = _row.HasChildren ? Visibility.Visible : Visibility.Collapsed;
        _toggleIcon.Glyph = _row.IsExpanded ? "" : "";
    }

    /// <summary>行の値が変わったとき、字下げと開閉ボタンを直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ITreeGridRow.RowTint) or null or "")
        {
            UpdateTint();
        }
        if (e.PropertyName is nameof(ITreeGridRow.Level) or nameof(ITreeGridRow.HasChildren) or nameof(ITreeGridRow.IsExpanded) or null or "")
        {
            UpdateTree();
        }
    }

    /// <summary>この行を、表の選んだ行にする</summary>
    private void Activate()
    {
        if (_owner is not null && _row is not null)
        {
            _owner.NotifyRowActivated(_row);
        }
    }
}
