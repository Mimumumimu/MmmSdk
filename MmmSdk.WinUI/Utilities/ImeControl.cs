using MmmSdk.WinUI.Interop;

namespace MmmSdk.WinUI.Utilities;

/// <summary>IME（日本語入力）のオン・オフを切り替える</summary>
/// <remarks>フォーカスのあるウィンドウが対象。入力欄にフォーカスが来たときに呼ぶ（日本語を打つ欄はオン、英数字を打つ欄はオフ）。</remarks>
public static class ImeControl
{
    /// <summary>フォーカスのあるウィンドウの IME をオンにする</summary>
    /// <remarks>日本語入力・漢字変換の状態にする。</remarks>
    public static void TurnOn() => NativeMethods.TurnOnImeForFocusedWindow();

    /// <summary>フォーカスのあるウィンドウの IME をオフにする</summary>
    /// <remarks>英数字の直接入力の状態にする。</remarks>
    public static void TurnOff() => NativeMethods.TurnOffImeForFocusedWindow();
}
