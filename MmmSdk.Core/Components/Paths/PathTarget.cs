using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;

namespace MmmSdk.Core.Components.Paths;

/// <summary>
/// リンクのパスの解釈 (環境変数の展開・種類の判定)。
/// </summary>
/// <remarks>Windows 前提 (実行ファイルの拡張子・環境変数の展開が Windows の流儀。Windows 以外から使うと、ビルドで警告になる)。</remarks>
[SupportedOSPlatform("windows")]
public static partial class PathTarget
{
    /// <summary>実行ファイルとみなす拡張子</summary>
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".com", ".msc",
    };

    /// <summary>開くときの実際のパス</summary>
    /// <param name="path">入力されたパス</param>
    /// <returns>前後の空白・引用符を除き、環境変数を展開したパス</returns>
    /// <remarks>前後の空白・引用符を除き、環境変数を展開する。</remarks>
    public static string Expand(string path)
        => Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

    /// <summary>URL のパーセントエンコードのうち、日本語などの非 ASCII の文字を戻した、表示用の文字列</summary>
    /// <param name="path">入力されたパス</param>
    /// <returns>URL のときは表示用の URL (環境変数を展開済み)。URL でないときは null</returns>
    /// <remarks>
    /// 表示だけに使う。開くときは元のパスをそのまま使う。
    /// `%20` や `%2F` のような ASCII の文字は、戻すと意味が変わる (区切りになる) ので、エンコードのまま残す。
    /// UTF-8 として読めないエンコード (不正な並びなど)も、そのまま残す。
    /// </remarks>
    public static string? DecodeForDisplay(string path)
    {
        var expanded = Expand(path);
        if (!Uri.TryCreate(expanded, UriKind.Absolute, out var uri) || uri.IsFile)
        {
            return null;
        }

        return EncodedRun().Replace(expanded, match => DecodeNonAscii(match.Value));
    }

    /// <summary>連続するパーセントエンコード (`%E6%97%A5` など)</summary>
    [GeneratedRegex("(?:%[0-9A-Fa-f]{2})+")]
    private static partial Regex EncodedRun();

    /// <summary>連続するパーセントエンコードを UTF-8 として読み、非 ASCII の文字だけを戻す。</summary>
    /// <param name="run">連続するパーセントエンコード</param>
    /// <returns>非 ASCII の文字を戻した文字列。UTF-8 として読めないときは元の文字列</returns>
    private static string DecodeNonAscii(string run)
    {
        var bytes = new byte[run.Length / 3];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(run.Substring((i * 3) + 1, 2), 16);
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (ArgumentException)
        {
            return run;
        }

        var builder = new StringBuilder();
        foreach (var c in text)
        {
            if (c < 0x80)
            {
                builder.Append('%').Append(((int)c).ToString("X2"));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>パスの種類を判定する。</summary>
    /// <param name="path">入力されたパス</param>
    /// <returns>パスが指す先の種類</returns>
    /// <remarks>ファイルの有無を調べるため、ネットワーク上のパスでは時間がかかることがある。UI スレッドからは呼ばない。</remarks>
    public static PathTargetKind Classify(string path)
    {
        var expanded = Expand(path);
        if (expanded.Length == 0)
        {
            return PathTargetKind.Empty;
        }

        // https: や mailto: 等のスキーム付き。C:\ のようなドライブ文字や \\server\ は file として扱われるので除く
        if (Uri.TryCreate(expanded, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return PathTargetKind.Url;
        }

        if (Directory.Exists(expanded))
        {
            return PathTargetKind.Folder;
        }

        if (File.Exists(expanded))
        {
            return ExecutableExtensions.Contains(Path.GetExtension(expanded)) ? PathTargetKind.Executable : PathTargetKind.File;
        }

        return PathTargetKind.NotFound;
    }
}
