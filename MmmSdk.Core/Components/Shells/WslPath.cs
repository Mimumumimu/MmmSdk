namespace MmmSdk.Core.Components.Shells;

/// <summary>Windows のパスを、WSL の中から見たパス (Linux の形)に変換する</summary>
/// <remarks>
/// 文字列の変換だけで、ファイルには触れない (wslpath を呼ばない。パスごとに WSL のプロセスを起動すると遅いため)。
/// <list type="bullet">
/// <item>ドライブのパス (<c>D:\work\a.txt</c>)→ <c>/mnt/d/work/a.txt</c></item>
/// <item>WSL のファイルの共有 (<c>\\wsl.localhost\Ubuntu\tmp\a.jpg</c>・<c>\\wsl$\Ubuntu\tmp\a.jpg</c>)→ <c>/tmp/a.jpg</c></item>
/// <item>そのほかのネットワークのパス (<c>\\server\share\...</c>)は、WSL から同じ形では開けないので、変換できない</item>
/// </list>
/// ドライブの割り当て先は WSL の既定の <c>/mnt/</c> とする。制約: <c>/etc/wsl.conf</c> の <c>[automount] root</c> を変えた環境では、違うパスになる (この値は WSL の中からしか読めない)。
/// WSL の共有のパスは、ディストリビューション名を見ずに変換する (シェルが動いているのと別のディストリビューションのパスを渡すと、違う場所を指す)。
/// </remarks>
public static class WslPath
{
    /// <summary>Windows のドライブを割り当てる場所 (WSL の既定)</summary>
    private const string MountRoot = "/mnt/";

    /// <summary>WSL のファイルの共有のホスト名 (<c>\\wsl.localhost\</c>・旧来の <c>\\wsl$\</c>)</summary>
    private static readonly string[] ShareHosts = ["wsl.localhost", "wsl$"];

    /// <summary>Windows のパスを Linux の形に変換する</summary>
    /// <param name="windowsPath">Windows の絶対パス</param>
    /// <param name="linuxPath">変換したパス。変換できないときは空</param>
    /// <returns>変換できたら true。相対パス・WSL の共有ではないネットワークのパスなら false</returns>
    public static bool TryToLinux(string windowsPath, out string linuxPath)
    {
        var path = windowsPath.Trim().Replace('\\', '/');

        // ドライブのパス (D:/...)
        if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '/')
        {
            linuxPath = TrimEndSlash($"{MountRoot}{char.ToLowerInvariant(path[0])}/{path[3..]}");
            return true;
        }

        // WSL の共有 (//wsl.localhost/<ディストリビューション名>/...)
        if (path.StartsWith("//", StringComparison.Ordinal))
        {
            var parts = path[2..].Split('/', 3);
            if (parts.Length >= 2 && ShareHosts.Contains(parts[0], StringComparer.OrdinalIgnoreCase) && parts[1].Length > 0)
            {
                linuxPath = TrimEndSlash("/" + (parts.Length == 3 ? parts[2] : ""));
                return true;
            }
        }

        linuxPath = "";
        return false;
    }

    /// <summary>末尾の / を外す (ルートの / だけは残す)</summary>
    /// <param name="path">Linux の形のパス</param>
    /// <returns>末尾の / を外したパス</returns>
    private static string TrimEndSlash(string path)
    {
        var trimmed = path.TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
    }
}
