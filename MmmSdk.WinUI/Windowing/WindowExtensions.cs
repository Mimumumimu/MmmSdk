using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace MmmSdk.WinUI.Windowing;

/// <summary>Window の拡張メソッド（Win32 を直接使う処理）</summary>
public static class WindowExtensions
{
    /// <summary>ウィンドウを前面に出す</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <remarks>表示（<c>Activate</c>）のあとに呼ぶ。別のアプリが前面にあると、Windows の制限で前面にならないことがある。</remarks>
    public static void SetForeground(this Window window)
        => PInvoke.SetForegroundWindow((HWND)Win32Interop.GetWindowFromWindowId(window.AppWindow.Id));

    /// <summary>トレイ・最小化・非表示から確実に戻して、前面に出す</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <remarks>復元（最小化しているとき）→ 表示 → 前面化の順に行う。別のアプリが前面にあると、Windows の制限で前面にならないことがある（<see cref="SetForeground"/>）。</remarks>
    public static void BringToFront(this Window window)
    {
        if (window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }
        window.AppWindow.Show(activateWindow: true);
        window.Activate();
        window.SetForeground();
    }

    /// <summary>アプリのアイコンを付け、タイトルバーを自分で描く（コンテンツをタイトルバーまで広げる）</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <param name="titleBar">ドラッグでウィンドウを動かせるタイトルバーの領域</param>
    /// <param name="iconPath">アイコンファイル（.ico）のパス</param>
    public static void UseCustomTitleBar(this Window window, UIElement titleBar, string iconPath)
    {
        window.AppWindow.SetIcon(iconPath);
        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(titleBar);
    }

    /// <summary>最大化・最小化できない重ね合わせ型のウィンドウにする</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <param name="isDialog">ダイアログ用の見た目（<see cref="OverlappedPresenter.CreateForDialog"/>）にするか</param>
    /// <param name="isResizable">大きさを変えられるか</param>
    /// <param name="minimumSize">最小の大きさ（物理ピクセル）。0 なら指定しない</param>
    public static void UseFixedPresenter(this Window window, bool isDialog, bool isResizable, SizeInt32 minimumSize = default)
    {
        var presenter = isDialog ? OverlappedPresenter.CreateForDialog() : OverlappedPresenter.Create();
        presenter.IsResizable = isResizable;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        if (minimumSize.Width > 0)
        {
            presenter.PreferredMinimumWidth = minimumSize.Width;
        }
        if (minimumSize.Height > 0)
        {
            presenter.PreferredMinimumHeight = minimumSize.Height;
        }
        window.AppWindow.SetPresenter(presenter);
    }

    /// <summary>クライアント領域の大きさを、論理サイズ（DIP）と倍率から決める</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <param name="width">幅（DIP）</param>
    /// <param name="height">高さ（DIP）</param>
    /// <param name="scale">DPI 倍率（<see cref="GetDpiScale"/> や <c>XamlRoot.RasterizationScale</c>）</param>
    /// <param name="roundUp">端数を切り上げるか（false なら切り捨て）。中身をちょうど収めたいときは true</param>
    public static void ResizeClientDip(this Window window, double width, double height, double scale, bool roundUp = false)
    {
        var w = roundUp ? Math.Ceiling(width * scale) : Math.Floor(width * scale);
        var h = roundUp ? Math.Ceiling(height * scale) : Math.Floor(height * scale);
        window.AppWindow.ResizeClient(new SizeInt32((int)w, (int)h));
    }

    /// <summary>指定した範囲の中央に置く（作業領域からはみ出す分は内側へ寄せる）</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <param name="bounds">中央に合わせる範囲（物理ピクセル。親ウィンドウや作業領域）</param>
    /// <remarks>収める作業領域は、その範囲にいちばん近いモニターのもの。</remarks>
    public static void MoveCentered(this Window window, RectInt32 bounds)
    {
        var size = window.AppWindow.Size;
        var position = new PointInt32(bounds.X + (bounds.Width - size.Width) / 2, bounds.Y + (bounds.Height - size.Height) / 2);
        var workArea = DisplayArea.GetFromRect(bounds, DisplayAreaFallback.Nearest).WorkArea;
        window.AppWindow.Move(WindowPlacement.ClampToWorkArea(position, size, workArea));
    }

    /// <summary>ウィンドウの DPI 倍率（100% で 1.0）</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <returns>ウィンドウがあるモニターの DPI 倍率</returns>
    /// <remarks>表示する前に大きさを決めるために使う（<c>XamlRoot</c> は表示するまで無いため）。</remarks>
    public static double GetDpiScale(this Window window)
        => PInvoke.GetDpiForWindow((HWND)Win32Interop.GetWindowFromWindowId(window.AppWindow.Id)) / 96.0;
}
