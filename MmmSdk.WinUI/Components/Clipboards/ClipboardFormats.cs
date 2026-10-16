using Windows.Win32;
using Windows.Win32.System.Ole;

namespace MmmSdk.WinUI.Components.Clipboards;

/// <summary>クリップボードに、どの種類の中身が載っているかを、持ち主のアプリに問い合わせずに調べる</summary>
/// <remarks>
/// <c>Windows.ApplicationModel.DataTransfer.Clipboard</c> の <c>GetContent</c> や <c>Contains</c> は、コピー元のアプリへ別プロセスの通信をして返事を待つ。
/// 返事が遅いと、待っている間 UI スレッドで画面の操作 (キー・マウス)が処理され続け、貼り付けなどのイベントの途中で WinUI が内部で失敗して強制終了する。
/// ここの判定は <c>IsClipboardFormatAvailable</c> で、OS が持つ形式の一覧を見るだけなので、待たされない。
/// 判定は「載っているかもしれない」の目安で、取り出しは別に <c>Clipboard.GetContent()</c> で行う。
/// 迷うものは「ある」と答える (見落とすと、取り出す処理を飛ばして動きが変わるため)。
/// </remarks>
public static class ClipboardFormats
{
    /// <summary>ファイルが載っている形式 (エクスプローラーのコピー・メールの添付ファイルなど、パスの無いファイルを含む)</summary>
    private static readonly uint[] FileFormats =
    [
        (uint)CLIPBOARD_FORMAT.CF_HDROP,
        PInvoke.RegisterClipboardFormat("FileGroupDescriptorW"),
        PInvoke.RegisterClipboardFormat("FileGroupDescriptor"),
        PInvoke.RegisterClipboardFormat("Shell IDList Array"),
    ];

    /// <summary>画像が載っている形式</summary>
    private static readonly uint[] ImageFormats =
    [
        (uint)CLIPBOARD_FORMAT.CF_BITMAP,
        (uint)CLIPBOARD_FORMAT.CF_DIB,
        (uint)CLIPBOARD_FORMAT.CF_DIBV5,
        PInvoke.RegisterClipboardFormat("PNG"),
    ];

    /// <summary>テキストが載っている形式 (ほかの形式のテキストは、OS が <c>CF_UNICODETEXT</c> に変換して見せる)</summary>
    private static readonly uint[] TextFormats =
    [
        (uint)CLIPBOARD_FORMAT.CF_UNICODETEXT,
        (uint)CLIPBOARD_FORMAT.CF_TEXT,
        (uint)CLIPBOARD_FORMAT.CF_OEMTEXT,
    ];

    /// <summary>ファイルが載っているか</summary>
    /// <returns>載っていれば true</returns>
    public static bool HasFiles() => HasAny(FileFormats);

    /// <summary>画像が載っているか</summary>
    /// <returns>載っていれば true</returns>
    public static bool HasImage() => HasAny(ImageFormats);

    /// <summary>テキストが載っているか</summary>
    /// <returns>載っていれば true</returns>
    public static bool HasText() => HasAny(TextFormats);

    /// <summary>形式のどれかが載っているか</summary>
    /// <param name="formats">調べる形式</param>
    /// <returns>どれかが載っていれば true</returns>
    private static bool HasAny(uint[] formats)
    {
        foreach (var format in formats)
        {
            // 0 は、登録に失敗した形式 (調べても意味が無い)
            if (format != 0 && PInvoke.IsClipboardFormatAvailable(format))
            {
                return true;
            }
        }
        return false;
    }
}
