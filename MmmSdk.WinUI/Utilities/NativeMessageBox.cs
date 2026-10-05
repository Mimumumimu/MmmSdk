using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MmmSdk.WinUI.Utilities;

/// <summary>Windows 標準のメッセージボックス</summary>
/// <remarks>WinUI のウィンドウやアプリの初期化 (XAML の読み込み)より前でも出せる。多重起動の案内など、画面を作る前に知らせたいときに使う。</remarks>
public static class NativeMessageBox
{
    /// <summary>情報のメッセージボックスを出して、閉じられるまで待つ</summary>
    /// <param name="text">本文</param>
    /// <param name="caption">タイトル</param>
    public static void ShowInformation(string text, string caption)
        => Show(text, caption, MESSAGEBOX_STYLE.MB_ICONINFORMATION);

    /// <summary>エラーのメッセージボックスを出して、閉じられるまで待つ</summary>
    /// <param name="text">本文</param>
    /// <param name="caption">タイトル</param>
    public static void ShowError(string text, string caption)
        => Show(text, caption, MESSAGEBOX_STYLE.MB_ICONERROR);

    /// <summary>OK ボタンだけのメッセージボックスを出して、閉じられるまで待つ</summary>
    /// <param name="text">本文</param>
    /// <param name="caption">タイトル</param>
    /// <param name="icon">アイコンの種類</param>
    private static unsafe void Show(string text, string caption, MESSAGEBOX_STYLE icon)
    {
        fixed (char* textPtr = text)
        fixed (char* captionPtr = caption)
        {
            PInvoke.MessageBox(HWND.Null, textPtr, captionPtr, MESSAGEBOX_STYLE.MB_OK | icon);
        }
    }
}
