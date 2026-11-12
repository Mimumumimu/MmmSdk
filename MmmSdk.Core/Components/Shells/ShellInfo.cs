namespace MmmSdk.Core.Components.Shells;

/// <summary>起動するシェル 1 つ分</summary>
/// <param name="Path">実行ファイルのフルパス</param>
/// <param name="Kind">シェルの種類</param>
/// <remarks>
/// 実行ファイルと種類を持つ。
/// 種類は起動コマンドの文字列から推測せず、シェルを決める側が指定する。
/// <see cref="ShellCommands"/> が種類ごとのコマンドを作るため。
/// </remarks>
public sealed record ShellInfo(string Path, ShellKind Kind)
{
    /// <summary>起動コマンドライン</summary>
    /// <remarks>
    /// 実行ファイルのフルパスを引用符で囲んだもの。
    /// 名前だけで起動すると、Windows が実行ファイルを探す場所 (アプリのフォルダー・カレントフォルダーなど)に同名のファイルがあれば、それが起動してしまう。
    /// フルパスにして、起動するファイルを決めておく。
    /// </remarks>
    public string CommandLine => $"\"{Path}\"";

    /// <summary>実行ファイル名</summary>
    /// <remarks>
    /// フォルダーを除いたもの。
    /// シェルの中で、そのシェル自身を呼ぶコマンドに使う。
    /// シェルが自分で PATH から探すので、フルパスは要らない。
    /// </remarks>
    public string FileName => System.IO.Path.GetFileName(Path);
}
