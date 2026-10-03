namespace MmmSdk.Core.Components.Shells;

/// <summary>起動するシェル 1 つ分（実行ファイルと種類）</summary>
/// <param name="Path">実行ファイルのフルパス</param>
/// <param name="Kind">シェルの種類</param>
/// <remarks>種類は、起動コマンドの文字列から推測せず、シェルを決める側が指定する（<see cref="ShellCommands"/> が種類ごとのコマンドを作るため）。</remarks>
public sealed record ShellInfo(string Path, ShellKind Kind)
{
    /// <summary>起動コマンドライン（実行ファイルのフルパスを引用符で囲んだもの）</summary>
    /// <remarks>
    /// 名前だけで起動すると、Windows が実行ファイルを探す場所（アプリのフォルダー・カレントフォルダーなど）に同名のファイルがあれば、それが起動してしまう。
    /// フルパスにして、起動するファイルを決めておく。
    /// </remarks>
    public string CommandLine => $"\"{Path}\"";

    /// <summary>実行ファイル名（フォルダーを除いたもの）</summary>
    /// <remarks>シェルの中で、そのシェル自身を呼ぶコマンドに使う（シェルが自分で PATH から探すので、フルパスは要らない）。</remarks>
    public string FileName => System.IO.Path.GetFileName(Path);
}
