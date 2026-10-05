namespace MmmSdk.Core.Components.Shells;

/// <summary>シェルの種類</summary>
public enum ShellKind
{
    /// <summary>PowerShell</summary>
    PowerShell,
    /// <summary>コマンドプロンプト (cmd)</summary>
    Cmd,
    /// <summary>WSL (wsl.exe で起動する、既定のディストリビューションの既定のシェル。bash など)</summary>
    /// <remarks>コマンドは POSIX のシェルの書き方で作り、パスは Linux の形 (<c>/mnt/d/...</c>)に変換して渡す (<see cref="ShellCommands"/>)。</remarks>
    Wsl,
}
