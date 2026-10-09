namespace MmmSdk.WinUI.Components.Terminal;

/// <summary>
/// 送信の依頼。
/// </summary>
/// <param name="Text">貼り付けるテキスト</param>
/// <param name="ReadyMarker">貼り付けの取り込みが終わった合図になる、画面の出力に含まれる文字列 (無ければ null)</param>
public sealed record SubmitRequest(string Text, string? ReadyMarker);
