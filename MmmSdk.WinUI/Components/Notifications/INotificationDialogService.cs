using MmmSdk.Core.Components.Notifications;

namespace MmmSdk.WinUI.Components.Notifications;

/// <summary>
/// デスクトップ通知のウィンドウを表示する。
/// </summary>
/// <remarks>
/// ウィンドウはアプリ内で常に 1 枚だけ。中は、通知の送り元 (機能)ごとの節 (<see cref="NotificationSection"/>)を、並びの値の順に並べる。
/// 送り元が通知すると、その送り元の節だけが入れ替わり、ほかの送り元の節は残る。節がなくなったら、ウィンドウは閉じる。
/// 各機能は、窓を直接操作せず、自分の節を出すだけにする。複数の機能の通知が、互いを消さないため。UI スレッドから呼ぶ。
/// </remarks>
public interface INotificationDialogService
{
    /// <summary>汎用の通知 (<see cref="Show(string, IReadOnlyList{NotificationItem}, Action?)"/>)の送り元のキー</summary>
    const string GeneralSourceKey = "General";

    /// <summary>汎用の通知の並びの値。機能の節より下に出す</summary>
    const int GeneralOrder = 1000;

    /// <summary>送り元の節を出す。同じ送り元の節があれば入れ替える</summary>
    /// <param name="section">出す節</param>
    void Show(NotificationSection section);

    /// <summary>タイトルと項目のリストを、汎用の節として出す</summary>
    /// <param name="title">節の見出し</param>
    /// <param name="items">本文の項目で、1 項目 1 行。リンク付きにもできる</param>
    /// <param name="onClicked">ユーザーが節をクリックして閉じたときだけ呼ぶ処理。次の通知で入れ替わるときは呼ばない</param>
    /// <remarks>機能の通知ではなく、エラーの知らせなどに使う。機能の通知は、送り元を決めて <see cref="Show(NotificationSection)"/> で出す。</remarks>
    void Show(string title, IReadOnlyList<NotificationItem> items, Action? onClicked = null);

    /// <summary>タイトルとメッセージだけの簡易通知を、汎用の節として出す</summary>
    /// <param name="title">節の見出し</param>
    /// <param name="message">本文のメッセージ</param>
    /// <param name="onClicked">ユーザーが節をクリックして閉じたときだけ呼ぶ処理</param>
    /// <remarks>内部でリンクなしの項目 1 件にして、項目リスト版へ渡す。</remarks>
    void Show(string title, string message, Action? onClicked = null);
}
