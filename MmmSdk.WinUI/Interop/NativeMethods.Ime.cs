using Windows.Win32;

namespace MmmSdk.WinUI.Interop;

internal static partial class NativeMethods
{
    /// <summary>フォーカスのあるウィンドウの IME をオンにする</summary>
    /// <remarks>日本語入力・漢字変換の状態にする。</remarks>
    public static void TurnOnImeForFocusedWindow() => SetImeForFocusedWindow(true);

    /// <summary>フォーカスのあるウィンドウの IME をオフにする</summary>
    /// <remarks>英数字の直接入力の状態にする。</remarks>
    public static void TurnOffImeForFocusedWindow() => SetImeForFocusedWindow(false);

    /// <summary>フォーカスのあるウィンドウの IME のオン・オフを切り替える</summary>
    /// <param name="open">オンにするなら true、オフなら false</param>
    private static void SetImeForFocusedWindow(bool open)
    {
        var hwnd = PInvoke.GetFocus();
        if (hwnd.IsNull) return;
        var imc = PInvoke.ImmGetContext(hwnd);
        if (imc.IsNull) return;
        PInvoke.ImmSetOpenStatus(imc, open);
        PInvoke.ImmReleaseContext(hwnd, imc);
    }
}
