using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MmmSdk.WinUI.Dialogs;

/// <summary>Windows 標準のメッセージボックス</summary>
/// <remarks>WinUI のウィンドウやアプリの初期化（XAML の読み込み）より前でも出せる。多重起動の案内など、画面を作る前に知らせたいときに使う。</remarks>
public static class NativeMessageBox
{
    /// <summary>情報のメッセージボックスを出して、閉じられるまで待つ</summary>
    /// <param name="text">本文</param>
    /// <param name="caption">タイトル</param>
    public static unsafe void ShowInformation(string text, string caption)
    {
        fixed (char* textPtr = text)
        fixed (char* captionPtr = caption)
        {
            PInvoke.MessageBox(HWND.Null, textPtr, captionPtr, MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONINFORMATION);
        }
    }
}
