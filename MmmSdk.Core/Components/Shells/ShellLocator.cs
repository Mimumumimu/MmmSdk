using System.Runtime.Versioning;

namespace MmmSdk.Core.Components.Shells;

/// <summary>既定で起動するシェルを決める。PowerShell 7 (pwsh)があればそれを、無ければ Windows PowerShell を使う</summary>
/// <remarks>BCL だけで書けるので Core に置く。Windows PowerShell の場所 (システムフォルダー)を決め打ちするため、Windows 専用。</remarks>
[SupportedOSPlatform("windows")]
public static class ShellLocator
{
    /// <summary>PowerShell 7 の実行ファイル名</summary>
    private const string PwshFileName = "pwsh.exe";

    /// <summary>Windows PowerShell の実行ファイル名</summary>
    private const string WindowsPowerShellFileName = "powershell.exe";

    /// <summary>既定のシェル (最初に使うときに 1 度だけ探す)</summary>
    private static readonly Lazy<ShellInfo> DefaultShell = new(FindDefault);

    /// <summary>既定のシェル</summary>
    /// <remarks>
    /// 最初に読んだときに PATH を探す (ディスクへ触れるので、遅い環境では時間がかかる)。UI スレッドで初めて読まないよう、起動時の準備でバックグラウンドから読んでおく。
    /// 結果は保持するので、2 回目以降は探さない (アプリの起動中に PowerShell 7 を入れても、次の起動まで反映しない)。
    /// </remarks>
    public static ShellInfo Default => DefaultShell.Value;

    /// <summary>WSL のシェル (システムフォルダーの wsl.exe)</summary>
    /// <remarks>
    /// 引数なしの wsl.exe は、既定のディストリビューションで、そのユーザーの既定のシェル (bash など)を起動する。
    /// 開始位置は、起動するプロセスの作業ディレクトリ (Windows のパス)を wsl.exe が Linux のパスに変換して使う。
    /// WSL が入っていない PC でも wsl.exe はあり、起動すると入れ方の案内を出して終了する。
    /// </remarks>
    public static ShellInfo Wsl { get; } = new(Path.Combine(Environment.SystemDirectory, "wsl.exe"), ShellKind.Wsl);

    /// <summary>既定のシェルを探す</summary>
    /// <returns>PATH 上の pwsh.exe があればそれ、無ければ Windows PowerShell (システムフォルダー内)</returns>
    private static ShellInfo FindDefault()
    {
        var path = FindOnPath(PwshFileName)
            ?? Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", WindowsPowerShellFileName);
        return new ShellInfo(path, ShellKind.PowerShell);
    }

    /// <summary>PATH 上の実行ファイルを探す</summary>
    /// <param name="fileName">実行ファイル名</param>
    /// <returns>最初に見つかったもののフルパス。無ければ null</returns>
    /// <remarks>
    /// <c>.</c> などの相対パスの項目は飛ばす (カレントフォルダーの同名のファイルが選ばれるのを防ぐ)。前後の引用符は外す (PATH には <c>"C:\Program Files\x"</c> のように書かれることがある)。
    /// </remarks>
    private static string? FindOnPath(string fileName)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        return paths
            .Select(dir => dir.Trim().Trim('"').Trim())
            .Where(dir => dir.Length > 0 && Path.IsPathFullyQualified(dir))
            .Select(dir => Path.Combine(dir, fileName))
            .FirstOrDefault(File.Exists);
    }
}
