using System.Runtime.Versioning;

namespace MmmSdk.Core.Components.Paths;

/// <summary>
/// リンクのパスの解釈（環境変数の展開・種類の判定）。
/// </summary>
/// <remarks>Windows 前提（実行ファイルの拡張子・環境変数の展開が Windows の流儀。Windows 以外から使うと、ビルドで警告になる）。</remarks>
[SupportedOSPlatform("windows")]
public static class PathTarget
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
