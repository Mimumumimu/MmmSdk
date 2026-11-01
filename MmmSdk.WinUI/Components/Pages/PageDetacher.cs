using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Components.WindowPositions;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using Windows.Graphics;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーの項目のページを、別のウィンドウに外して持つ</summary>
/// <remarks>
/// 外すときは、外すウィンドウ用にページを新しく作る。引き継げるページ (<see cref="IHandoverPage"/>)は、同じスコープから作って、シェルなどの状態と画面の内容を引き継ぐ。それ以外は、メインに残る同じ機能のページを捨てて、作り直す。
/// 閉じるとき、引き継げるページは、メインのページへ作り直して渡す。それ以外は、外したページを破棄する。
/// 閉じたあと、サイドバーで項目を押すと、メインの表示領域に、新しく作ったページが出る。
/// 1 つの機能のページは同時に 1 か所にしか出せないので、外している間は、メインの表示領域に出さない。
/// 機能をオフにしたときは、ページを捨てる前に、外したウィンドウも閉じる。
/// 外したウィンドウの大きさは、機能ごとに <see cref="WindowBoundsKeeper"/> で覚える。キーは <c>DetachedPage.&lt;項目のキー&gt;</c>。
/// </remarks>
public sealed class PageDetacher
{
    /// <summary>ウィンドウの大きさを覚えるキーの前に付ける名前</summary>
    private const string BoundsKeyPrefix = "DetachedPage.";

    /// <summary>タイトルバーの高さ (DIP)</summary>
    /// <remarks>外したウィンドウの高さは、ページの高さにこれを足した値にする。</remarks>
    private const double TitleBarDip = 32;

    /// <summary>ボタンで外したとき、メインウィンドウの左上からずらす距離 (DIP)</summary>
    private const double CascadeOffsetDip = 40;

    /// <summary>メインのウィンドウ (外すときに渡されたもの)</summary>
    /// <remarks>ウィンドウを閉じて、メインのページへ引き継ぐとき、ページが載るウィンドウとして渡す。</remarks>
    private AppWindow? _mainWindow;

    /// <summary>外したページと、そのウィンドウ (項目のキー → 項目・ウィンドウ)</summary>
    private readonly Dictionary<string, (NavigationItem Item, DetachedPageWindow Window)> _windows = [];

    /// <summary>ページを持つもの</summary>
    private readonly PageProvider _pages;

    /// <summary>ダイアログの親の候補にするため</summary>
    private readonly IDialogHost _dialogs;

    /// <summary>ウィンドウの大きさの保存・復元</summary>
    private readonly IWindowPositionService _positions;

    /// <summary>アプリの名前とアイコン</summary>
    private readonly AppEnvironment _environment;

    /// <summary>外す部品を作る</summary>
    /// <param name="pages">ページを持つもの (ページを捨てるときの通知を受け取る)</param>
    /// <param name="dialogs">ダイアログの親の候補にする口</param>
    /// <param name="positions">ウィンドウの大きさの保存・復元</param>
    /// <param name="environment">アプリの名前とアイコン</param>
    public PageDetacher(PageProvider pages, IDialogHost dialogs, IWindowPositionService positions, AppEnvironment environment)
    {
        _pages = pages;
        _dialogs = dialogs;
        _positions = positions;
        _environment = environment;
        pages.PageEvicting += OnPageEvicting;
    }

    /// <summary>外したウィンドウを前に出す</summary>
    /// <param name="item">外している項目</param>
    public void Activate(NavigationItem item)
    {
        if (_windows.TryGetValue(item.Key, out var entry))
        {
            entry.Window.BringToFront();
        }
    }

    /// <summary>ページを別のウィンドウに外す</summary>
    /// <param name="item">外す項目</param>
    /// <param name="unparent">メインの表示領域を、別の項目へ移す処理。ページを作る前に呼ぶ</param>
    /// <param name="pageSizeDip">メインの表示領域の大きさ。大きさを覚えていないときの、ウィンドウの大きさに使う</param>
    /// <param name="main">メインのウィンドウ</param>
    /// <param name="at">ウィンドウを置く位置。ドラッグで離した位置を、物理ピクセルで渡す。無ければ、メインウィンドウから少しずらして置く</param>
    /// <returns>外す処理の完了を表すタスク</returns>
    /// <remarks>
    /// すでに外している項目は、前に出すだけ。
    /// ページは、外すウィンドウ用に新しく作る。WebView2 を持つページは、別のウィンドウへ付け替えると、日本語入力が効かなくなるため。
    /// 引き継げるページ (<see cref="IHandoverPage"/>)は、スコープを捨てず、同じスコープから新しいページを作り、画面の内容を引き継ぐ。
    /// ウィンドウを閉じるときも、同じ形でメインのページへ戻す。
    /// 引き継げないページは、メインに残る同じ機能のページを先に捨てて、新しく作る。同じ機能のページが 2 つ生きたままにならず、メモリも持ち続けない。ウィンドウを閉じたら、外したページを破棄する。
    /// </remarks>
    public async Task OpenAsync(NavigationItem item, Action unparent, Windows.Foundation.Size pageSizeDip, AppWindow main, PointInt32? at)
    {
        if (_windows.ContainsKey(item.Key))
        {
            Activate(item);
            return;
        }

        item.IsDetached = true;
        _mainWindow = main;

        // 引き継げるページは、画面を出したまま、内容を取り出す (画面が隠れたあとだと、隠れた WebView2 の応答に頼ることになるため)
        var source = _pages.FindMainHandoverPage(item.Key);
        if (source is not null)
        {
            await source.PrepareHandoverAsync();
        }

        unparent();

        Page? page;
        if (source is not null && _pages.FindMainHandoverPage(item.Key) == source)
        {
            // 古い画面を閉じてから新しいページへ渡すまで、続けて行う (あいだに届いたシェルの出力を、捨てないため)
            var handover = source.TakeHandover();
            page = _pages.RebuildAsDetached(item.Key);
            (page as IHandoverPage)?.ApplyHandover(handover, null);
        }
        else
        {
            _pages.Evict(item.Key);
            page = _pages.CreateDetachedPage(item);
        }

        if (page is null)
        {
            item.IsDetached = false;
            return;
        }

        var window = new DetachedPageWindow(page, item.Title, _environment.Name, _environment.IconPath);
        if (page is IHandoverPage detachedSource)
        {
            window.BeforeClosingAsync = () => ReturnToMainAsync(item.Key, window, detachedSource);
            window.SkipBeforeClosing = () => _dialogs.IsClosingAll;
        }

        _windows[item.Key] = (item, window);
        window.Closed += (_, _) =>
        {
            _windows.Remove(item.Key);
            item.IsDetached = false;
            _pages.ReleaseDetachedPage(item.Key);
        };
        _dialogs.TrackWindow(window);

        Place(window, BoundsKeyPrefix + item.Key, pageSizeDip, main, at);

        // 外す直前に、メインの表示領域が別の項目へ移り、そのあとで、フォーカスをメインウィンドウの表示領域へ置く処理が、順番待ちで走る。
        // 先に前へ出すと、その処理で、メインウィンドウが前に出直すので、同じ優先度の順番待ちの最後に回す
        window.DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, window.BringToFront);
    }

    /// <summary>外したウィンドウを閉じるとき、引き継げるページを、メインのページへ作り直して渡す</summary>
    /// <param name="key">項目のキー</param>
    /// <param name="window">閉じるウィンドウ</param>
    /// <param name="source">外していたページ</param>
    /// <returns>引き継ぎの完了を表すタスク</returns>
    /// <remarks>メインのページは、サイドバーで項目を押したときに出る。それまでの出力は、新しいページが画面の準備ができるまでためる。</remarks>
    private async Task ReturnToMainAsync(string key, DetachedPageWindow window, IHandoverPage source)
    {
        await source.PrepareHandoverAsync();

        // 取り出しを待つあいだに機能がオフになったときは、引き継がない。ページは、このあと閉じたウィンドウの後始末で破棄する
        if (window.BeforeClosingAsync is null)
        {
            return;
        }

        var handover = source.TakeHandover();
        window.ReleasePage();
        (_pages.RebuildAsMain(key) as IHandoverPage)?.ApplyHandover(handover, _mainWindow);
    }

    /// <summary>ウィンドウの大きさと位置を決める</summary>
    /// <param name="window">外したウィンドウ</param>
    /// <param name="key">大きさを覚えるキー</param>
    /// <param name="pageSizeDip">覚えていないときの、ページの大きさ</param>
    /// <param name="main">メインのウィンドウ</param>
    /// <param name="at">置く位置。ドラッグで離した位置。無ければ、メインウィンドウから少しずらす</param>
    /// <remarks>
    /// 大きさは、覚えていればそれ、無ければメインの表示領域と同じ大きさ。
    /// 位置は、ドラッグで離したときは、離した位置がタイトルバーの中央に来る位置。ボタンで外したときは、メインウィンドウの左上から少しずらした位置。
    /// 位置は、覚えていても使わない。外した場所がそのまま出る方が、直観に合うため。
    /// </remarks>
    private void Place(DetachedPageWindow window, string key, Windows.Foundation.Size pageSizeDip, AppWindow main, PointInt32? at)
    {
        var saved = _positions.LoadBounds(key);
        WindowBoundsKeeper.Attach(window, _positions, key, pageSizeDip.Width, pageSizeDip.Height + TitleBarDip);

        var appWindow = window.AppWindow;
        var size = saved is { Width: > 0, Height: > 0 } ? new SizeInt32(saved.Width, saved.Height) : appWindow.Size;
        var scale = window.GetDpiScale();
        var position = at is { } point
            ? new PointInt32(point.X - size.Width / 2, point.Y - (int)Math.Round(TitleBarDip / 2 * scale))
            : new PointInt32(main.Position.X + (int)Math.Round(CascadeOffsetDip * scale), main.Position.Y + (int)Math.Round(CascadeOffsetDip * scale));

        var workArea = DisplayArea.GetFromRect(new RectInt32(position.X, position.Y, size.Width, size.Height), DisplayAreaFallback.Nearest).WorkArea;
        // 位置を先に移してから大きさを指定する (モニターの倍率が違うと、移した時点で大きさが変わるため)
        appWindow.Move(WindowPlacement.ClampToWorkArea(position, size, workArea));
        appWindow.Resize(size);
    }

    /// <summary>機能をオフにするとき、外したウィンドウを閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="key">オフにする機能のページの項目のキー</param>
    /// <remarks>外したページの破棄は、ウィンドウの <c>Closed</c> で行う。</remarks>
    private void OnPageEvicting(object? sender, string key)
    {
        if (_windows.TryGetValue(key, out var entry))
        {
            entry.Item.IsDetached = false;
            // 機能をオフにするので、メインのページへ引き継がずに閉じる (ページはこのあと捨てる)
            entry.Window.BeforeClosingAsync = null;
            entry.Window.Close();
        }
    }
}
