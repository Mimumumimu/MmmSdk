using MmmSdk.Core.Components.Notifications;

namespace MmmSdk.WinUI.Components.Notifications;

/// <summary>
/// <see cref="INotificationDialogService"/> の実装。通知ウィンドウを 1 枚だけ持ち、出している節を管理する。
/// </summary>
/// <param name="createWindow">通知ウィンドウを作る処理。DI への登録で渡す。ウィンドウは閉じたら作り直すので、作る処理を受け取る</param>
/// <remarks>
/// ウィンドウは初回表示時に作る。ユーザーが閉じたら破棄し、出していた節も捨てて、次の通知で作り直す。
/// 節は、送り元のキーごとに 1 つ持つ。節をクリックしたら、その節だけを捨てて、クリックの処理を呼ぶ。節が残っていれば、残りでウィンドウを作り直し、無ければ閉じる。
/// </remarks>
public sealed class NotificationDialogService(Func<NotificationWindow> createWindow) : INotificationDialogService
{
    /// <summary>出している節 (送り元のキー → 節)</summary>
    private readonly Dictionary<string, NotificationSection> _sections = [];

    /// <summary>通知ウィンドウ。まだ作っていない (または閉じられた)なら null</summary>
    private NotificationWindow? _window;

    /// <inheritdoc />
    public void Show(NotificationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        _sections[section.SourceKey] = section;
        EnsureWindow().Present(Ordered(), blink: true);
    }

    /// <inheritdoc />
    public void Show(string title, IReadOnlyList<NotificationItem> items, Action? onClicked = null) =>
        Show(new NotificationSection(INotificationDialogService.GeneralSourceKey, INotificationDialogService.GeneralOrder, title, items, onClicked));

    /// <inheritdoc />
    public void Show(string title, string message, Action? onClicked = null) =>
        Show(title, [new NotificationItem(message)], onClicked);

    /// <summary>通知ウィンドウを取得する。無ければ作る</summary>
    /// <returns>通知ウィンドウ</returns>
    private NotificationWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        var window = createWindow();
        window.SectionClicked += OnSectionClicked;
        window.Closed += (_, _) =>
        {
            // ×・Alt+F4・最後の節のクリックで閉じたとき。出していた節も捨てる
            if (ReferenceEquals(_window, window))
            {
                _window = null;
                _sections.Clear();
            }
        };
        _window = window;
        return window;
    }

    /// <summary>節をクリックされたら、その節だけを捨てて、クリックの処理を呼ぶ</summary>
    /// <param name="sender">ウィンドウ</param>
    /// <param name="sourceKey">クリックされた節の送り元のキー</param>
    /// <remarks>
    /// 節が残っていれば、明滅せずに、残りでウィンドウを作り直す。無ければ閉じる。
    /// クリックの処理は、ウィンドウを閉じたあと、または作り直したあとに呼ぶ。
    /// </remarks>
    private void OnSectionClicked(object? sender, string sourceKey)
    {
        if (!_sections.Remove(sourceKey, out var section))
        {
            return;
        }

        if (_sections.Count == 0)
        {
            _window?.CloseAfterClick();
        }
        else
        {
            _window?.Present(Ordered(), blink: false);
        }
        section.OnClicked?.Invoke();
    }

    /// <summary>出している節を、並びの値の順 (同じ値は送り元のキーの順)に並べる</summary>
    /// <returns>並べた節</returns>
    private List<NotificationSection> Ordered() => [.. _sections.Values.OrderBy(s => s.Order).ThenBy(s => s.SourceKey, StringComparer.Ordinal)];
}
