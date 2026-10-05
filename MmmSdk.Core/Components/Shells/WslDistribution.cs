using System.Runtime.Versioning;
using Microsoft.Win32;

namespace MmmSdk.Core.Components.Shells;

/// <summary>WSL のディストリビューションの情報</summary>
/// <remarks>WSL が登録する、ユーザーごとのレジストリ (<c>HKCU\Software\Microsoft\Windows\CurrentVersion\Lxss</c>)から読む (wsl.exe を起動しないので速い)。Windows 専用。</remarks>
[SupportedOSPlatform("windows")]
public static class WslDistribution
{
    /// <summary>WSL のディストリビューションの登録先</summary>
    private const string LxssKey = @"Software\Microsoft\Windows\CurrentVersion\Lxss";

    /// <summary>既定のディストリビューションの名前を調べる</summary>
    /// <param name="name">既定のディストリビューションの名前 (例: <c>Ubuntu</c>)。見つからないときは空</param>
    /// <returns>見つかったら true。WSL が入っていない・ディストリビューションが 1 つも無いときは false</returns>
    /// <remarks>引数なしの wsl.exe (<see cref="ShellLocator.Wsl"/>)が起動するのと同じディストリビューション。</remarks>
    public static bool TryGetDefaultName(out string name)
    {
        using var lxss = Registry.CurrentUser.OpenSubKey(LxssKey);
        if (lxss?.GetValue("DefaultDistribution") is string id && id.Length > 0)
        {
            using var distribution = lxss.OpenSubKey(id);
            if (distribution?.GetValue("DistributionName") is string found && found.Length > 0)
            {
                name = found;
                return true;
            }
        }

        name = "";
        return false;
    }
}
