namespace MmmSdk.WinUI.Components.Terminal;

/// <summary>ターミナルの画面を、別の <see cref="TerminalControl"/> へ引き継ぐための情報</summary>
/// <param name="Snapshot">画面の内容 (見えている範囲とスクロールの履歴)を、端末へ書き込める文字列にしたもの。取り出せなかったときは null</param>
/// <param name="Columns">スナップショットを取ったときの端末の列数</param>
/// <param name="Rows">スナップショットを取ったときの端末の行数</param>
/// <param name="PendingOutput">スナップショットのあとに届いて、まだ画面へ送っていない出力</param>
public sealed record TerminalHandover(string? Snapshot, int Columns, int Rows, string PendingOutput);
