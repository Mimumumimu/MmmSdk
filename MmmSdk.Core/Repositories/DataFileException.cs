namespace MmmSdk.Core.Repositories;

/// <summary>
/// 保存データの読み書きに失敗した。メッセージはそのままユーザーへ表示できる形にする。
/// </summary>
/// <param name="message">ユーザーへ表示できるメッセージ</param>
/// <param name="innerException">原因の例外</param>
public sealed class DataFileException(string message, Exception innerException) : Exception(message, innerException);
