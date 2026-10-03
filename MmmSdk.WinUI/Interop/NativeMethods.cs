using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MmmSdk.WinUI.Interop;

/// <summary>Win32 API の宣言と、それを使う小さな補助。通知ウィンドウ（フォーカスを奪わない表示・ドラッグ）と、擬似モーダル（親の無効化）用</summary>
/// <remarks>宣言は CsWin32 が生成する（<c>NativeMethods.txt</c>）。まだ移していない宣言は、用途ごとの <c>NativeMethods.&lt;用途&gt;.cs</c> に手書きで残っている。</remarks>
internal static partial class NativeMethods
{
    /// <summary>座標</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        /// <summary>X 座標</summary>
        public int x;
        /// <summary>Y 座標</summary>
        public int y;
    }

    /// <summary>ウィンドウのオーナー（親）を設定する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="owner">オーナーにするウィンドウのハンドル</param>
    /// <remarks>オーナーより常に手前に表示され、オーナーと一緒に最小化される。</remarks>
    public static void SetOwner(nint hWnd, nint owner) =>
        PInvoke.SetWindowLongPtr((HWND)hWnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, owner);

    /// <summary>ウィンドウを、フォーカスを奪わない（クリックでアクティブにならない）ウィンドウにする</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    public static void SetNoActivate(nint hWnd)
    {
        var hwnd = (HWND)hWnd;
        var style = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style | (nint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
    }

    /// <summary>フォーカスを奪わずに、最前面へ表示する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    public static void ShowTopmostNoActivate(nint hWnd) =>
        PInvoke.SetWindowPos((HWND)hWnd, HWND.HWND_TOPMOST, 0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
            | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);

    /// <summary>固定長の文字列欄へ書き込む</summary>
    /// <param name="value">書き込む文字列</param>
    /// <param name="buffer">書き込み先の固定長バッファ</param>
    /// <remarks>収まらない分は切り捨て、必ず終端する。</remarks>
    public static void CopyToBuffer(string value, Span<char> buffer)
    {
        var count = Math.Min(value.Length, buffer.Length - 1);
        value.AsSpan(0, count).CopyTo(buffer);
        buffer[count] = '\0';
    }

    /// <summary>固定長の文字列欄へ書き込む</summary>
    /// <param name="value">書き込む文字列</param>
    /// <param name="buffer">書き込み先の固定長バッファ</param>
    /// <param name="length">バッファの文字数（終端を含む）</param>
    /// <remarks>収まらない分は切り捨て、必ず終端する。</remarks>
    public static unsafe void CopyToFixed(string value, char* buffer, int length) =>
        CopyToBuffer(value, new Span<char>(buffer, length));
}
