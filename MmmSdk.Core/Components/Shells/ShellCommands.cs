using System.Text;

namespace MmmSdk.Core.Components.Shells;

/// <summary>シェルの種類に合わせたコマンド文字列を組み立てる</summary>
public static class ShellCommands
{
    /// <summary>PowerShell が単一引用符として扱う文字（ASCII の <c>'</c> と、U+2018 / U+2019 / U+201A / U+201B）</summary>
    private const string PowerShellSingleQuotes = "'\u2018\u2019\u201A\u201B";

    /// <summary>作業ディレクトリを移動するコマンドを作る</summary>
    /// <param name="shell">コマンドを送るシェル</param>
    /// <param name="directory">移動先のディレクトリ</param>
    /// <param name="command">シェルへ送るコマンド文字列（作れたとき）</param>
    /// <returns>作れたら true。cmd で、パスに <c>%</c> を含むときは false</returns>
    /// <remarks>
    /// cmd は、対話入力では引用符の中でも <c>%名前%</c> を環境変数に展開し、<c>%</c> を安全に打ち消す方法もない。
    /// 意図しないパスへ移動しないよう、cmd で <c>%</c> を含むパスは、コマンドを作らない。
    /// </remarks>
    public static bool TryChangeDirectory(ShellInfo shell, string directory, out string command)
    {
        if (shell.Kind == ShellKind.Cmd)
        {
            command = directory.Contains('%') ? "" : $"cd /d \"{directory}\"";
            return command.Length > 0;
        }

        // 単一引用符なら $ や ` が展開されない。パス中の単一引用符は、2 つ重ねてエスケープする
        command = $"Set-Location -LiteralPath '{EscapePowerShellSingleQuoted(directory)}'";
        return true;
    }

    /// <summary>PowerShell の単一引用符の文字列の中身をエスケープする</summary>
    /// <param name="text">エスケープする文字列</param>
    /// <returns>単一引用符として扱われる文字をすべて 2 つ重ねた文字列</returns>
    /// <remarks>
    /// PowerShell は、全角風の引用符（U+2018 / U+2019 / U+201A / U+201B）も単一引用符として扱う。
    /// これらはフォルダー名に使える文字なので、重ねないと、文字列が途中で閉じて、残りがコマンドとして実行されてしまう。
    /// </remarks>
    private static string EscapePowerShellSingleQuoted(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            builder.Append(c);
            if (PowerShellSingleQuotes.Contains(c))
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }
}
