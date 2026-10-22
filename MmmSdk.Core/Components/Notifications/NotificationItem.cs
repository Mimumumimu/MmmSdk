namespace MmmSdk.Core.Components.Notifications;

/// <summary>
/// 通知ダイアログの本文の 1 項目。
/// </summary>
/// <param name="Text">表示テキスト。</param>
/// <param name="LinkPath">リンク先 (URL・ファイル・フォルダのパス)で、無ければ通常のテキストとして表示する。</param>
/// <param name="Detail">補足で、あれば、表示テキストの下に、小さく薄い字で、改行のまま出す。</param>
public sealed record NotificationItem(string Text, string? LinkPath = null, string? Detail = null);
