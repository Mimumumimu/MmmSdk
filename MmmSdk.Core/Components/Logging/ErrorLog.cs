using System.Reflection;
using System.Text;

namespace MmmSdk.Core.Components.Logging;

/// <summary>
/// エラーを日付ごとのファイルに追記するログ
/// </summary>
/// <param name="directoryPath">ログを置くフォルダー</param>
/// <remarks>
/// 落ちる直前（未処理例外・復旧できない失敗）に呼ばれることを想定して、同期で書き、書き終えてファイルを閉じてから戻る。
/// ログを書けないこと自体は、元のエラーの報告を妨げないよう、例外にせず null を返す。
/// </remarks>
public sealed class ErrorLog(string directoryPath)
{
    /// <summary>書き込みの排他用ロック</summary>
    private readonly Lock _gate = new();

    /// <summary>ログを置くフォルダー</summary>
    public string DirectoryPath { get; } = directoryPath;

    /// <summary>例外をログに追記する</summary>
    /// <param name="context">どこで起きたか（呼び出し元が分かる短い説明）</param>
    /// <param name="exception">記録する例外（内部例外とスタックトレースを含めて書く）</param>
    /// <returns>書き込んだファイルのパス。書き込めなかったときは null</returns>
    public string? Write(string context, Exception exception)
    {
        var now = DateTimeOffset.Now;
        var path = Path.Combine(DirectoryPath, $"{now:yyyy-MM-dd}.log");
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        var text = new StringBuilder()
            .Append('[').Append(now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")).Append("] ").AppendLine(context)
            .Append("バージョン: ").Append(version).Append(" / OS: ").Append(Environment.OSVersion).AppendLine()
            .AppendLine(exception.ToString())
            .AppendLine()
            .ToString();

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(path, text, Encoding.UTF8);
            }
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ファイルを開けない・権限が無い・ディスクがいっぱい。元のエラーの報告を優先するため、ここでは例外にしない
            return null;
        }
    }
}
