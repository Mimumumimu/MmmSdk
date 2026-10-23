using System.Drawing;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.Notifications;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Components.WindowPositions;
using MmmSdk.WinUI.Interop;
using MmmSdk.WinUI.Utilities;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MmmSdk.WinUI.Components.Notifications;

/// <summary>デスクトップ通知のウィンドウ (見た目・表示内容と、ドラッグ・クリックで閉じる・位置の保存などの挙動)</summary>
/// <remarks>
/// 常に最前面で、フォーカスを奪わない (WS_EX_NOACTIVATE + 非アクティブ表示)。ウィンドウ全体のどこを掴んでもドラッグでき、
/// 動かさずに離したら (移動量がシステムのドラッグしきい値未満)クリックとみなして閉じる。
/// </remarks>
public sealed partial class NotificationWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 400;
    /// <summary>項目 (件名と補足)を、節の見出しより右へ字下げする幅 (DIP)</summary>
    private const double ItemIndent = 12;
    /// <summary>ウィンドウの最低の高さ (DIP)</summary>
    private const double MinWindowHeight = 160;
    /// <summary>ウィンドウの最大の高さ (DIP)。これを超える分は本文をスクロールする</summary>
    private const double MaxWindowHeight = 480;
    /// <summary>作業領域の端からの余白 (既定位置。物理ピクセル)</summary>
    private const int ScreenMargin = 16;
    /// <summary>保存位置を「十分に見える」とみなす、ウィンドウ面積に対する見えている割合</summary>
    private const double VisibleRatio = 0.5;

    /// <summary>位置の保存・復元</summary>
    private readonly IWindowPositionService _positions;

    /// <summary>ウィンドウのハンドル</summary>
    private readonly nint _hwnd;

    /// <summary>位置を保存・復元するキー (アプリ内で 1 枚のウィンドウなので、1 つ)</summary>
    private const string PositionKey = "Notification";

    /// <summary>保存位置を復元した (または既定の位置に置いた)か。表示するのは初回だけ復元する</summary>
    private bool _positionRestored;

    /// <summary>押下した位置にある節の送り元のキー。節の外なら null</summary>
    private string? _pressedSection;

    /// <summary>節が 1 つだけのときの、その節の送り元のキー。2 つ以上なら null</summary>
    /// <remarks>1 つだけなら、余白を含むウィンドウのどこをクリックしても、その節のクリックとして扱う。</remarks>
    private string? _soleSectionKey;

    /// <summary>左ボタンを押している最中か</summary>
    private bool _pressed;

    /// <summary>押下してからしきい値を超えて動かした (ドラッグ中)か</summary>
    private bool _dragging;

    /// <summary>この押下の間にリンクがクリックされたか</summary>
    private bool _linkClicked;

    /// <summary>押下したときのカーソル位置 (画面座標)</summary>
    private Point _startCursor;

    /// <summary>押下したときのウィンドウ位置</summary>
    private PointInt32 _startWindow;

    /// <summary>節をクリックして閉じようとした (引数は、その節の送り元のキー)</summary>
    /// <remarks>ウィンドウは、自分では閉じない。節を捨てるか、ウィンドウごと閉じるかは、受けた側が決める。</remarks>
    public event EventHandler<string>? SectionClicked;

    /// <summary>ウィンドウの ViewModel</summary>
    public NotificationWindowViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="positions">位置の保存・復元</param>
    public NotificationWindow(NotificationWindowViewModel viewModel, IWindowPositionService positions)
    {
        ViewModel = viewModel;
        _positions = positions;
        InitializeComponent();

        _hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);

        // コンテンツをタイトルバー領域まで広げる。標準のドラッグ領域 (SetTitleBar)は使わず、ポインタイベントで自前実装する
        ExtendsContentIntoTitleBar = true;
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }
        NativeMethods.SetNoActivate(_hwnd);

        AppWindow.Closing += (_, _) => SavePosition();

        // リンクなど子が処理済みにした押下・移動・離しも受ける
        RootGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnPointerPressed), true);
        RootGrid.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        RootGrid.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnPointerReleased), true);
        RootGrid.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnPointerCaptureLost), true);
    }

    /// <summary>節を並べ直して表示し、到達を明滅で知らせる</summary>
    /// <param name="sections">出す節 (並べ終えたもの)</param>
    /// <param name="blink">到達を明滅で知らせるか。節をクリックして残りを作り直すときは false</param>
    /// <remarks>大きさ・位置を決めてから、フォーカスを奪わずに最前面へ表示する。</remarks>
    public void Present(IReadOnlyList<NotificationSection> sections, bool blink)
    {
        BuildBody(sections);

        FitHeight();
        var position = AppWindow.Position;
        if (!_positionRestored)
        {
            _positionRestored = true;
            position = RestoreOrDefaultPosition(PositionKey);
        }
        // 差し替えで高さが変わると、下や右がはみ出すことがあるため、毎回収める
        AppWindow.Move(KeepInWorkArea(position));

        // 倍率が違うモニターへ移ると、Windows が大きさを倍率の比で変えるため、移したあとの倍率で高さを合わせ直して収め直す
        FitHeight();
        AppWindow.Move(KeepInWorkArea(AppWindow.Position));

        NativeMethods.ShowTopmostNoActivate(_hwnd);

        BlinkStoryboard.Stop();
        if (blink)
        {
            BlinkStoryboard.Begin();
        }
    }

    /// <summary>位置を保存して、ウィンドウを閉じる</summary>
    /// <remarks>節をクリックして、最後の節がなくなったときに、サービスが呼ぶ。Window.Close() では AppWindow.Closing が発火しないため、先に位置を保存する。</remarks>
    public void CloseAfterClick()
    {
        SavePosition();
        Close();
    }

    /// <summary>本文を組み立てる</summary>
    /// <param name="sections">出す節 (並べ終えたもの)</param>
    /// <remarks>節ごとに、見出しと項目を縦に並べ、節の間には区切り線を引く。押した位置の節を知るため、節の枠に送り元のキーを持たせる。</remarks>
    private void BuildBody(IReadOnlyList<NotificationSection> sections)
    {
        BodyPanel.Children.Clear();
        _soleSectionKey = sections.Count == 1 ? sections[0].SourceKey : null;

        for (var i = 0; i < sections.Count; i++)
        {
            if (i > 0)
            {
                BodyPanel.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                });
            }
            BodyPanel.Children.Add(BuildSection(sections[i]));
        }
    }

    /// <summary>節 1 つを組み立てる</summary>
    /// <param name="section">節</param>
    /// <returns>見出しと項目を縦に並べた枠。送り元のキーを持つ</returns>
    private StackPanel BuildSection(NotificationSection section)
    {
        var panel = new StackPanel { Spacing = 10, Tag = section.SourceKey };
        panel.Children.Add(new TextBlock
        {
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            Text = section.Title,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        foreach (var item in section.Items)
        {
            panel.Children.Add(BuildItem(item));
        }
        return panel;
    }

    /// <summary>項目 1 つを組み立てる</summary>
    /// <param name="item">本文の項目</param>
    /// <returns>項目の表示</returns>
    /// <remarks>
    /// 項目は 1 つの <see cref="TextBlock"/> (Inlines)にする。リンクはクリックで開く。
    /// リンクだけの行は当たり判定が行末の余白まで横に伸びてしまうため、リンクの直後に全角スペースの Run を 1 つ置いて文字幅側に留める。
    /// 補足 (<see cref="NotificationItem.Detail"/>)があれば、項目の下に、小さく薄い字の <see cref="TextBlock"/> で、改行のまま出す。
    /// </remarks>
    private UIElement BuildItem(NotificationItem item)
    {
        // 太字は節の見出しだけにして、項目は見出しより右に字下げする (補足が無くても、見出しと項目の上下が分かるように)
        var text = new TextBlock { Margin = new Thickness(ItemIndent, 0, 0, 0), TextWrapping = TextWrapping.Wrap };
        if (string.IsNullOrWhiteSpace(item.LinkPath))
        {
            text.Inlines.Add(new Run { Text = item.Text });
        }
        else
        {
            var path = item.LinkPath;
            var link = new Hyperlink();
            link.Inlines.Add(new Run { Text = item.Text });
            link.Click += (_, _) =>
            {
                // クリックで閉じる判定 (OnPointerReleased)から、リンクのクリックを区別する
                _linkClicked = true;
                ViewModel.OpenLinkCommand.Execute(path);
            };
            text.Inlines.Add(link);
            text.Inlines.Add(new Run { Text = "　" });
        }

        if (string.IsNullOrWhiteSpace(item.Detail))
        {
            return text;
        }

        var detail = new TextBlock
        {
            Text = item.Detail.Trim(),
            FontSize = 13,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        // 補足は、字下げして左に細い線を付け、件名の下の引用として見せる
        var detailBox = new Border
        {
            Margin = new Thickness(ItemIndent + 2, 0, 0, 0),
            Padding = new Thickness(10, 0, 0, 0),
            BorderBrush = (Brush)Application.Current.Resources["ControlStrongStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(2, 0, 0, 0),
            Child = detail,
        };
        var group = new StackPanel { Spacing = 4 };
        group.Children.Add(text);
        group.Children.Add(detailBox);
        return group;
    }

    /// <summary>本文に合わせて高さを決める (表示前に確定する)</summary>
    /// <remarks>幅は固定。上の余白と節の必要高さを実測し、最低・最大の高さの範囲で合わせる。</remarks>
    private void FitHeight()
    {
        var scale = this.GetDpiScale();

        RootGrid.Measure(new Windows.Foundation.Size(WindowWidth, double.PositiveInfinity));
        var height = Math.Clamp(RootGrid.DesiredSize.Height, MinWindowHeight, MaxWindowHeight);

        AppWindow.ResizeClient(new SizeInt32((int)Math.Round(WindowWidth * scale), (int)Math.Round(height * scale)));
    }

    /// <summary>保存位置が十分に見えていれば復元し、見えなければ既定位置 (プライマリ作業領域の右下)を返す</summary>
    /// <param name="key">位置を保存しているキー</param>
    /// <returns>ウィンドウの左上の位置 (物理ピクセル)</returns>
    /// <remarks>現在のモニタ構成で判定する。</remarks>
    private PointInt32 RestoreOrDefaultPosition(string key)
    {
        var size = AppWindow.Size;

        if (_positions.Load(key) is { } saved)
        {
            if (WindowPlacement.IsVisibleEnough(new RectInt32(saved.X, saved.Y, size.Width, size.Height), VisibleRatio))
            {
                return new PointInt32(saved.X, saved.Y);
            }
        }

        var area = DisplayArea.Primary.WorkArea;
        return new PointInt32(
            area.X + area.Width - size.Width - ScreenMargin,
            area.Y + area.Height - size.Height - ScreenMargin);
    }

    /// <summary>ウィンドウ全体が作業領域に収まるよう、位置をずらす</summary>
    /// <param name="position">ウィンドウの左上の位置 (物理ピクセル)</param>
    /// <returns>作業領域に収まるようにずらした左上の位置</returns>
    /// <remarks>
    /// その位置にいちばん近いモニタの作業領域を使い、上下左右のはみ出しを内側へ寄せる。
    /// 作業領域より大きいときは左上に合わせる。保存する位置は変えない (ユーザーが置いた位置を残す)。
    /// </remarks>
    private PointInt32 KeepInWorkArea(PointInt32 position)
    {
        var size = AppWindow.Size;
        var work = DisplayArea.GetFromRect(new RectInt32(position.X, position.Y, size.Width, size.Height), DisplayAreaFallback.Nearest).WorkArea;

        return WindowPlacement.ClampToWorkArea(position, size, work);
    }

    /// <summary>現在の位置を保存する</summary>
    /// <remarks>
    /// 閉じ方 (クリック・×・Alt+F4)に関わらず閉じるときと、ドラッグが終わったときに呼ぶ。
    /// 保存に失敗 (ロック・権限・ディスク)しても、位置が戻らないだけで、通知は使える。<c>async void</c> の例外は受け皿が無くアプリごと落ちるため、この失敗だけを受ける (次の保存でやり直す)。
    /// </remarks>
    private async void SavePosition()
    {
        if (!_positionRestored) return;
        var position = AppWindow.Position;
        try
        {
            await _positions.SaveAsync(PositionKey, new WindowPosition(position.X, position.Y));
        }
        catch (DataFileException)
        {
            // 位置が保存できないだけなので、続ける
        }
    }

    #region ドラッグ移動・クリックで閉じる

    /// <summary>押下を記録する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ポインタイベントの情報</param>
    /// <remarks>右上のキャプションボタン領域・スクロールバーでの押下は対象外 (閉じるのは OS のボタンに任せる)。</remarks>
    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(RootGrid);
        if (!point.Properties.IsLeftButtonPressed) return;
        if (IsOnCaptionButtons(point.Position) || IsOnScrollBar(e.OriginalSource as DependencyObject)) return;

        PInvoke.GetCursorPos(out _startCursor);
        _startWindow = AppWindow.Position;
        _pressed = true;
        _dragging = false;
        _linkClicked = false;
        _pressedSection = FindSectionKey(e.OriginalSource as DependencyObject) ?? _soleSectionKey;
    }

    /// <summary>押した要素がある節の、送り元のキーを探す</summary>
    /// <param name="element">押した要素。無ければ null</param>
    /// <returns>節の送り元のキー。節の外 (余白・区切り線)なら null</returns>
    private string? FindSectionKey(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is FrameworkElement { Tag: string key } frame && ReferenceEquals(frame.Parent, BodyPanel))
            {
                return key;
            }
        }
        return null;
    }

    /// <summary>しきい値を超えて動いたらドラッグとして、ウィンドウを動かす</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ポインタイベントの情報</param>
    /// <remarks>
    /// ウィンドウが動くとポインタの相対座標が変わるため、画面座標のカーソル位置で移動量を測る。
    /// ドラッグになった時点でポインタをキャプチャする (リンクの押下を横取りしないよう、動かすまではしない)。
    /// </remarks>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed) return;

        PInvoke.GetCursorPos(out var cursor);
        var dx = cursor.X - _startCursor.X;
        var dy = cursor.Y - _startCursor.Y;

        if (!_dragging)
        {
            if (Math.Abs(dx) < PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXDRAG)
                && Math.Abs(dy) < PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYDRAG))
            {
                return;
            }
            _dragging = true;
            RootGrid.CapturePointer(e.Pointer);
        }

        AppWindow.Move(new PointInt32(_startWindow.X + dx, _startWindow.Y + dy));
    }

    /// <summary>離したとき、ドラッグなら位置を保存し、動かしていなければクリックとして閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ポインタイベントの情報</param>
    /// <remarks>
    /// リンクのクリックとの発火順に依存しないよう、閉じる判定は 1 サイクル遅らせ、その時点でリンクがクリックされていなければ閉じる。
    /// </remarks>
    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed) return;
        _pressed = false;

        if (_dragging)
        {
            RootGrid.ReleasePointerCapture(e.Pointer);
            EndDrag();
            return;
        }

        var section = _pressedSection;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_linkClicked || section is null) return;
            SectionClicked?.Invoke(this, section);
        });
    }

    /// <summary>キャプチャを失ったら、押下の追跡を終える</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ポインタイベントの情報</param>
    /// <remarks>離した通知 (Released)より先にキャプチャ喪失が来ることがあるため、ドラッグ中だったらここでも終了処理 (位置の保存)を行う。</remarks>
    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _pressed = false;
        EndDrag();
    }

    /// <summary>ドラッグを終えて、位置を保存する</summary>
    /// <remarks>ドラッグ中でなければ何もしない (離した通知とキャプチャ喪失の両方から呼ばれても 1 回だけ保存する)。</remarks>
    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        SavePosition();
    }

    /// <summary>右上のキャプションボタン (×など)の領域か</summary>
    /// <param name="position">ルート要素内の座標 (DIP)。</param>
    /// <returns>キャプションボタンの領域なら true</returns>
    private bool IsOnCaptionButtons(Windows.Foundation.Point position)
    {
        var scale = this.GetDpiScale();
        var titleBar = AppWindow.TitleBar;
        return position.Y * scale < titleBar.Height
            && position.X * scale >= WindowWidth * scale - titleBar.RightInset;
    }

    /// <summary>押下した要素がスクロールバーの中か</summary>
    /// <param name="element">押下した要素。無ければ null</param>
    /// <returns>スクロールバーの中なら true</returns>
    private static bool IsOnScrollBar(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ScrollBar) return true;
        }
        return false;
    }

    #endregion
}
