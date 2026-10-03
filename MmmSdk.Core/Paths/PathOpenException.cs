namespace MmmSdk.Core.Paths;

/// <summary>リンクを開けなかったことを表す例外</summary>
/// <param name="message">ユーザーへ表示できるメッセージ</param>
/// <param name="innerException">原因の例外。無ければ null</param>
public sealed class PathOpenException(string message, Exception? innerException = null) : Exception(message, innerException);
