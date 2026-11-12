namespace MmmSdk.Core.Components.Shells;

/// <summary>シェルの種類</summary>
public enum ShellKind
{
    /// <summary>PowerShell</summary>
    PowerShell,
    /// <summary>コマンドプロンプト</summary>
    Cmd,
    /// <summary>WSL</summary>
    /// <remarks>
    /// wsl.exe で起動する、既定のディストリビューションの既定のシェルで、bash など。
    /// コマンドは POSIX のシェルの書き方で作り、パスは Linux の形 (<c>/mnt/d/...</c>)に変換して渡す。
    /// 変換は <see cref="ShellCommands"/> が行う。
    /// </remarks>
    Wsl,
}
