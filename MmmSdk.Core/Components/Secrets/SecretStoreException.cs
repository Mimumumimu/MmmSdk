namespace MmmSdk.Core.Components.Secrets;

/// <summary>
/// 秘密の保管庫を読み書きできなかったときの例外。メッセージはそのまま画面に出せる。
/// </summary>
/// <param name="message">画面に出せるメッセージ</param>
public sealed class SecretStoreException(string message) : Exception(message);
