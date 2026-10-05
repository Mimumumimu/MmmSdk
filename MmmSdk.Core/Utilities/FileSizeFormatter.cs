namespace MmmSdk.Core.Utilities;

/// <summary>
/// ファイルの大きさ (バイト数)を、画面に出す文字列にする
/// </summary>
public static class FileSizeFormatter
{
    /// <summary>バイト数を、読みやすい単位にする</summary>
    /// <param name="bytes">バイト数</param>
    /// <returns>B・KB・MB・GB のいずれかで表した文字列</returns>
    public static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:0.#} MB",
        _ => $"{bytes / 1024.0 / 1024 / 1024:0.#} GB",
    };
}
