using System.Runtime.InteropServices;

namespace MmmSdk.WinUI.Interop;

/// <summary>Win32 API の宣言（P/Invoke）。通知ウィンドウ（フォーカスを奪わない表示・ドラッグ）用</summary>
internal static partial class NativeMethods
{
    /// <summary>拡張ウィンドウスタイルのインデックス（GetWindowLongPtr / SetWindowLongPtr 用）</summary>
    public const int GWL_EXSTYLE = -20;

    /// <summary>クリックや表示でアクティブにならない拡張スタイル</summary>
    public const nint WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>常に最前面を指定する SetWindowPos のウィンドウハンドル</summary>
    public static readonly nint HWND_TOPMOST = -1;

    /// <summary>SetWindowPos: 大きさを変えない</summary>
    public const uint SWP_NOSIZE = 0x0001;

    /// <summary>SetWindowPos: 位置を変えない</summary>
    public const uint SWP_NOMOVE = 0x0002;

    /// <summary>SetWindowPos: アクティブにしない</summary>
    public const uint SWP_NOACTIVATE = 0x0010;

    /// <summary>SetWindowPos: ウィンドウを表示する</summary>
    public const uint SWP_SHOWWINDOW = 0x0040;

    /// <summary>ドラッグ開始とみなす水平方向の移動量（GetSystemMetrics 用）</summary>
    public const int SM_CXDRAG = 68;

    /// <summary>ドラッグ開始とみなす垂直方向の移動量（GetSystemMetrics 用）</summary>
    public const int SM_CYDRAG = 69;

    /// <summary>座標</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        /// <summary>X 座標</summary>
        public int x;
        /// <summary>Y 座標</summary>
        public int y;
    }

    /// <summary>ウィンドウの拡張スタイルなどを取得する</summary>
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    /// <summary>ウィンドウの拡張スタイルなどを設定する</summary>
    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    /// <summary>ウィンドウの前後関係・位置・表示状態を変える</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>マウスカーソルの位置（画面座標）を取得する</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    /// <summary>ウィンドウの DPI を取得する</summary>
    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hWnd);

    /// <summary>システムの寸法・設定値を取得する</summary>
    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int nIndex);

    /// <summary>ウィンドウを、フォーカスを奪わない（クリックでアクティブにならない）ウィンドウにする</summary>
    public static void SetNoActivate(nint hWnd) =>
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, GetWindowLongPtr(hWnd, GWL_EXSTYLE) | WS_EX_NOACTIVATE);

    /// <summary>フォーカスを奪わずに、最前面へ表示する</summary>
    public static void ShowTopmostNoActivate(nint hWnd) =>
        SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
}
