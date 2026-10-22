namespace MmmSdk.Core.Components.Notifications;

/// <summary>
/// 通知ダイアログの節 1 つ。通知の送り元 (機能)ごとに 1 つで、見出しと項目の並びを持つ。
/// </summary>
/// <param name="SourceKey">送り元のキー。同じキーの節は、新しく通知したものに入れ替わり、ほかの送り元の節は残る。</param>
/// <param name="Order">並びの値。小さいほど上に出る。</param>
/// <param name="Title">節の見出し。</param>
/// <param name="Items">節の項目。</param>
/// <param name="OnClicked">
/// ユーザーが節をクリックして閉じたときだけ呼ぶ処理。×・Alt+F4・同じ送り元の次の通知への入れ替えでは呼ばない。
/// 無ければ null。
/// </param>
public sealed record NotificationSection(string SourceKey, int Order, string Title, IReadOnlyList<NotificationItem> Items, Action? OnClicked = null);
