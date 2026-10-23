using System.ComponentModel;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.Core.Components.Features;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Win32;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーと、選んだ項目のページを出す部品 (アプリのウィンドウに載せて使う)</summary>
/// <remarks>
/// 登録されたページ (<see cref="NavigationPage"/>)と機能のオン・オフ・並び順に合わせて項目を作り直し、オフになった機能のページを捨てる。
/// 項目は、メインウィンドウの外へドラッグするか、マウスを乗せたときに出るボタンで、ページを別のウィンドウに外せる。外したウィンドウは <see cref="PageDetacher"/> が持つ。
/// ウィンドウ (タイトルバー・位置の保存・トレイへの退避)は、アプリが持つ。
/// </remarks>
public sealed partial class NavigationHost : UserControl
{
    /// <summary>項目に対応するページ</summary>
    private readonly PageProvider _pages;

    /// <summary>別のウィンドウに外したページ</summary>
    private readonly PageDetacher _detacher;

    /// <summary>メインの表示領域に出している項目 (外している項目を選んだとき、選択を戻す先)</summary>
    private NavigationItem? _shownItem;

    /// <summary>サイドバーの項目と選択</summary>
    public NavigationHostViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">サイドバーの項目と選択</param>
    /// <param name="pages">項目に対応するページ</param>
    /// <param name="detacher">別のウィンドウに外したページ</param>
    /// <param name="features">機能のオン・オフ</param>
    /// <param name="busyCursor">マウスカーソルを処理待ちの形にする口</param>
    public NavigationHost(NavigationHostViewModel viewModel, PageProvider pages, PageDetacher detacher, FeatureService features, IBusyCursor busyCursor)
    {
        ViewModel = viewModel;
        _pages = pages;
        _detacher = detacher;

        InitializeComponent();

        // サイドバーとページの全体に、処理待ちのカーソルを当てる (ProtectedCursor は、派生クラスの中からしか変えられない)
        busyCursor.Changed += (_, _) => ProtectedCursor = busyCursor.IsBusy ? InputSystemCursor.Create(InputSystemCursorShape.Wait) : null;

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.HistoryTraversed += OnHistoryTraversed;
        features.Changed += OnFeatureChanged;
        features.OrderChanged += (_, _) => ViewModel.Refresh();
        NavigateTo(ViewModel.SelectedItem);
    }

    /// <summary>機能のオン・オフが切り替わったら、サイドバーの項目を作り直し、オフにした機能のページを捨てる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="featureKey">切り替わった機能のキー</param>
    /// <remarks>表示中のページが消えるときは、項目の更新の中で別のページへ移る。ページを捨てるのは、そのあと。表示中のページを捨てないため。</remarks>
    private void OnFeatureChanged(object? sender, string featureKey)
    {
        ViewModel.Refresh();
        _pages.EvictDisabledPages();
    }

    /// <summary>選択が変わったら、そのページへ移動する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    /// <remarks>
    /// 別のウィンドウに外している項目が選ばれたときは、そのウィンドウを前に出し、選択を元の項目へ戻す。
    /// 戻すのも、前に出すのも、サイドバーの選択の処理が済んだあと。
    /// </remarks>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NavigationHostViewModel.SelectedItem))
        {
            return;
        }

        if (ViewModel.SelectedItem is { IsDetached: true } detached)
        {
            var back = _shownItem;
            DispatcherQueue.TryEnqueue(() =>
            {
                if (ViewModel.SelectedItem == detached && back is { IsDetached: false } && (ViewModel.MenuItems.Contains(back) || ViewModel.FooterItems.Contains(back)))
                {
                    ViewModel.SelectedItem = back;
                }

                // 選択を戻す処理のあとに、サイドバーの選択の見た目やフォーカスの処理が順番待ちで走る。
                // 先に前へ出すと、その処理でメインウィンドウが前に出直すので、同じ優先度の順番待ちの最後に回す
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => _detacher.Activate(detached));
            });
            return;
        }

        NavigateTo(ViewModel.SelectedItem);
    }

    /// <summary>戻る・進むでページが替わったら、フォーカスをページの表示領域に置く</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 移る前のページのフォーカスがあった要素が画面から外れると、WinUI は、画面で最初にフォーカスできる要素 (サイドバーの先頭の項目)へフォーカスを移し、白い枠が出る。
    /// それを避けるため、枠の出ない表示領域に置く。ページが自分でフォーカスを置くとき (CLI補助の入力欄など)は、そのあとの読み込みで置き直される。
    /// 外れたあとの移動が済んでから置くよう、順番待ちに回す。
    /// </remarks>
    private void OnHistoryTraversed(object? sender, EventArgs e)
        => DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => ContentFrame.Focus(Microsoft.UI.Xaml.FocusState.Programmatic));

    /// <summary>項目に対応するページを表示する</summary>
    /// <param name="item">表示するナビゲーション項目。無いときは、表示領域を空にする</param>
    private void NavigateTo(NavigationItem? item)
    {
        if (item is null)
        {
            ContentFrame.Content = null;
            _shownItem = null;
            return;
        }

        if (_pages.GetPage(item) is not { } page)
        {
            return;
        }

        ContentFrame.Content = page;
        _shownItem = item;
    }

    #region ページを別のウィンドウに外す

    /// <summary>項目のドラッグに入れるデータの形式名 (このアプリ専用)</summary>
    private const string DragFormat = "MmmSdk.NavigationItemKey";

    /// <summary>項目を、メインウィンドウの外へドラッグし始めたとき</summary>
    /// <param name="sender">ドラッグする項目の部品</param>
    /// <param name="args">ドラッグの情報</param>
    /// <remarks>外している項目と、外せない項目は、ドラッグさせない。</remarks>
    private void OnItemDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (ItemOf(sender) is not { CanDetach: true, IsDetached: false } item)
        {
            args.Cancel = true;
            return;
        }

        // 文字列にすると、ほかのアプリの入力欄の上で離したときに、項目のキーが入力されてしまう。このアプリ専用の形式にする
        args.Data.SetData(DragFormat, item.Key);
        args.Data.RequestedOperation = DataPackageOperation.Move;
    }

    /// <summary>ドラッグを終えたとき、メインウィンドウの外で離していたら、ページを外す</summary>
    /// <param name="sender">ドラッグした項目の部品</param>
    /// <param name="args">ドラッグの結果</param>
    /// <remarks>離した位置を、新しいウィンドウを置く位置にする。ほかのアプリが受け取ったとき、つまり結果が「なし」でないときは、外さない。</remarks>
    private void OnItemDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        if (args.DropResult != DataPackageOperation.None
            || ItemOf(sender) is not { IsDetached: false } item || !PInvoke.GetCursorPos(out var cursor) || XamlRoot is null)
        {
            return;
        }

        var main = MainAppWindow();
        var inside = cursor.X >= main.Position.X && cursor.X < main.Position.X + main.Size.Width
            && cursor.Y >= main.Position.Y && cursor.Y < main.Position.Y + main.Size.Height;
        if (!inside)
        {
            Detach(item, new PointInt32(cursor.X, cursor.Y));
        }
    }

    /// <summary>外せる項目を押したら、その項目を選ぶ</summary>
    /// <param name="sender">項目の部品</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// ドラッグできる項目 (<c>CanDrag</c>)は、押した操作をドラッグの仕組みが先に取り、<c>NavigationView</c> の選択が起きなくなる。
    /// そのため、押した (タップした)ことを、ここで受けて選ぶ。外すボタンを押したときは、選ばない。
    /// </remarks>
    private void OnItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (ItemOf(sender) is not { CanDetach: true } item || IsInsideButton(e.OriginalSource as DependencyObject, (DependencyObject)sender))
        {
            return;
        }

        ViewModel.SelectedItem = item;
    }

    /// <summary>押した部品が、指定した部品の中のボタンかを調べる</summary>
    /// <param name="source">押した部品</param>
    /// <param name="root">探す範囲の最上位の部品</param>
    /// <returns><paramref name="root"/> の中の、ボタンの上なら true</returns>
    private static bool IsInsideButton(DependencyObject? source, DependencyObject root)
    {
        for (var current = source; current is not null && current != root; current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is Button)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>項目にマウスを乗せたら、外すボタンを出す</summary>
    /// <param name="sender">項目の部品</param>
    /// <param name="e">イベントの情報</param>
    private void OnItemPointerEntered(object sender, PointerRoutedEventArgs e) => SetDetachButtonShown((Grid)sender, true);

    /// <summary>項目からマウスが出たら、外すボタンを隠す</summary>
    /// <param name="sender">項目の部品</param>
    /// <param name="e">イベントの情報</param>
    private void OnItemPointerExited(object sender, PointerRoutedEventArgs e) => SetDetachButtonShown((Grid)sender, false);

    /// <summary>外すボタンを押したら、ページを外す</summary>
    /// <param name="sender">外すボタン</param>
    /// <param name="e">イベントの情報</param>
    private void OnItemDetachClick(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is { } item)
        {
            Detach(item, null);
        }
    }

    /// <summary>項目の外すボタンを、出す・隠す</summary>
    /// <param name="itemPanel">項目の部品</param>
    /// <param name="shown">出すか</param>
    /// <remarks>隠している間は、押せないようにもする。右端を押して、意図せず外れないため。</remarks>
    private static void SetDetachButtonShown(Grid itemPanel, bool shown)
    {
        foreach (var button in itemPanel.Children.OfType<Button>())
        {
            button.Opacity = shown ? 1 : 0;
            button.IsHitTestVisible = shown;
        }
    }

    /// <summary>ページを別のウィンドウに外す</summary>
    /// <param name="item">外す項目</param>
    /// <param name="at">ウィンドウを置く位置を、物理ピクセルで渡す。ボタンで外すときは null</param>
    /// <remarks>表示領域に出ているページなら、先に別の項目を選んで、表示領域から外す。</remarks>
    private void Detach(NavigationItem item, PointInt32? at)
    {
        if (item.IsDetached)
        {
            _detacher.Activate(item);
            return;
        }

        if (!item.CanDetach || XamlRoot is null)
        {
            return;
        }

        var pageSize = new Windows.Foundation.Size(ContentFrame.ActualWidth, ContentFrame.ActualHeight);
        _detacher.Open(item, () => ViewModel.SelectAnotherThan(item), pageSize, MainAppWindow(), at);
    }

    /// <summary>このサイドバーを載せているウィンドウを取得する</summary>
    /// <returns>メインウィンドウ (表示してから呼ぶこと)</returns>
    private AppWindow MainAppWindow() => AppWindow.GetFromWindowId(XamlRoot.ContentIslandEnvironment.AppWindowId);

    /// <summary>項目の部品から、項目を取り出す</summary>
    /// <param name="element">項目の部品か、その中のボタン</param>
    /// <returns>項目。取り出せなければ null</returns>
    private static NavigationItem? ItemOf(object element) => (element as FrameworkElement)?.Tag as NavigationItem;

    /// <summary>外している項目にだけ、外している印を出すための表示の切り替え</summary>
    /// <param name="isDetached">外しているか</param>
    /// <returns>外しているなら表示</returns>
    public static Visibility VisibleWhenDetached(bool isDetached) => isDetached ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>外せる項目で、まだ外していないものにだけ、外すボタンを置くための表示の切り替え</summary>
    /// <param name="canDetach">外せる項目か</param>
    /// <param name="isDetached">外しているか</param>
    /// <returns>外せて、外していないなら表示</returns>
    public static Visibility VisibleWhenCanDetach(bool canDetach, bool isDetached) => canDetach && !isDetached ? Visibility.Visible : Visibility.Collapsed;

    #endregion
}
