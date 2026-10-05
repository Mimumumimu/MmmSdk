using MmmSdk.Core.Components.Notifications;

namespace MmmSdk.WinUI.Components.Notifications;

/// <summary>
/// デスクトップ通知のウィンドウを表示する。
/// </summary>
/// <remarks>
/// ウィンドウはアプリ内で常に 1 枚だけ。新しい通知が来たら内容を差し替えて再表示する。UI スレッドから呼ぶ。
/// </remarks>
public interface INotificationDialogService
{
    /// <summary>位置を保存するキーの既定値</summary>
    const string DefaultPositionKey = "Notification";

    /// <summary>タイトルと項目のリストを表示する</summary>
    /// <param name="title">通知のタイトル</param>
    /// <param name="items">本文の項目 (1 項目 1 行。リンク付きにもできる)</param>
    /// <param name="onClicked">
    /// ユーザーが本文 (リンク以外)をクリックして閉じたときだけ呼ぶ。×・Alt+F4・別の通知への差し替えでは呼ばない。
    /// 通知ごとに設定し直す (渡さなければ前回の分もクリアされる)。
    /// </param>
    /// <param name="positionKey">位置を保存・復元するキー。</param>
    void Show(string title, IReadOnlyList<NotificationItem> items, Action? onClicked = null, string positionKey = DefaultPositionKey);

    /// <summary>タイトルとメッセージだけの簡易通知を表示する</summary>
    /// <param name="title">通知のタイトル</param>
    /// <param name="message">本文のメッセージ</param>
    /// <param name="onClicked">ユーザーが本文をクリックして閉じたときだけ呼ぶ処理。渡さなければ前回の分もクリアされる</param>
    /// <param name="positionKey">位置を保存・復元するキー。</param>
    /// <remarks>内部でリンクなしの項目 1 件にして、項目リスト版へ渡す。</remarks>
    void Show(string title, string message, Action? onClicked = null, string positionKey = DefaultPositionKey);
}
