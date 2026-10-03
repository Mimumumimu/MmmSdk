using Microsoft.UI;
using Microsoft.UI.Xaml;
using MmmSdk.WinUI.Interop;

namespace MmmSdk.WinUI.Windowing;

/// <summary>Window の拡張メソッド（Win32 を直接使う処理）</summary>
public static class WindowExtensions
{
    /// <summary>ウィンドウを前面に出す</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <remarks>表示（<c>Activate</c>）のあとに呼ぶ。別のアプリが前面にあると、Windows の制限で前面にならないことがある。</remarks>
    public static void SetForeground(this Window window)
        => NativeMethods.SetForegroundWindow(Win32Interop.GetWindowFromWindowId(window.AppWindow.Id));

    /// <summary>ウィンドウの DPI 倍率（100% で 1.0）</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <returns>ウィンドウがあるモニターの DPI 倍率</returns>
    /// <remarks>表示する前に大きさを決めるために使う（<c>XamlRoot</c> は表示するまで無いため）。</remarks>
    public static double GetDpiScale(this Window window)
        => NativeMethods.GetDpiForWindow(Win32Interop.GetWindowFromWindowId(window.AppWindow.Id)) / 96.0;
}
