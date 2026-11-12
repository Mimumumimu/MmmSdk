using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MmmSdk.WinUI.Interop;

/// <summary>CsWin32 の API を組み合わせる小さな補助</summary>
/// <remarks>
/// オーナー設定・フォーカスを奪わない表示・ドラッグなど。
/// ほかの用途は <c>NativeMethods.*.cs</c> に置く。
/// IME とメニューは別のファイル。
/// 宣言は CsWin32 が生成し、定義は <c>NativeMethods.txt</c> に書く。
/// 手書きで残るのは CsWin32 のメタデータに無い uxtheme の非公開 API を序数指定で呼ぶもの (<c>NativeMethods.Menu.cs</c>)と、
/// 生成された API を組み合わせる小さな補助 (<c>NativeMethods.Ime.cs</c> など)だけ。
/// </remarks>
internal static partial class NativeMethods
{
    /// <summary>ウィンドウのオーナーを設定する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="owner">オーナーにするウィンドウのハンドル</param>
    /// <remarks>
    /// オーナーは親ウィンドウ。
    /// オーナーより常に手前に表示され、オーナーと一緒に最小化される。
    /// </remarks>
    public static void SetOwner(nint hWnd, nint owner) =>
        PInvoke.SetWindowLongPtr((HWND)hWnd, WINDOW_LONG_PTR_INDEX.GWLP_HWNDPARENT, owner);

    /// <summary>ウィンドウをフォーカスを奪わないウィンドウにする</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <remarks>クリックでアクティブにならない</remarks>
    public static void SetNoActivate(nint hWnd)
    {
        var hwnd = (HWND)hWnd;
        var style = PInvoke.GetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, style | (nint)WINDOW_EX_STYLE.WS_EX_NOACTIVATE);
    }

    /// <summary>フォーカスを奪わずに最前面へ表示する</summary>
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
}
