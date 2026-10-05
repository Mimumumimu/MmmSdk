using MmmSdk.Core.Components.Notifications;

namespace MmmSdk.WinUI.Components.Notifications;

/// <summary>
/// <see cref="INotificationDialogService"/> の実装。通知ウィンドウを 1 枚だけ持つ。
/// </summary>
/// <param name="createWindow">通知ウィンドウを作る処理 (DI への登録で渡す。ウィンドウは閉じたら作り直すので、作る処理を受け取る)</param>
/// <remarks>ウィンドウは初回表示時に作る。ユーザーが閉じたら破棄し、次の通知で作り直す。</remarks>
public sealed class NotificationDialogService(Func<NotificationWindow> createWindow) : INotificationDialogService
{
    /// <summary>通知ウィンドウ。まだ作っていない (または閉じられた)なら null</summary>
    private NotificationWindow? _window;

    /// <inheritdoc />
    public void Show(string title, IReadOnlyList<NotificationItem> items, Action? onClicked = null, string positionKey = INotificationDialogService.DefaultPositionKey)
    {
        if (_window is null)
        {
            var window = createWindow();
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_window, window)) _window = null;
            };
            _window = window;
        }

        _window.Present(title, items, onClicked, positionKey);
    }

    /// <inheritdoc />
    public void Show(string title, string message, Action? onClicked = null, string positionKey = INotificationDialogService.DefaultPositionKey) =>
        Show(title, [new NotificationItem(message)], onClicked, positionKey);
}
