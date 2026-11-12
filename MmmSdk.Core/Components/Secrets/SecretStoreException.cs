namespace MmmSdk.Core.Components.Secrets;

/// <summary>秘密の保管庫を読み書きできなかったときの例外</summary>
/// <param name="message">画面に出せるメッセージ</param>
/// <remarks>メッセージはそのまま画面に出せる</remarks>
public sealed class SecretStoreException(string message) : Exception(message);
