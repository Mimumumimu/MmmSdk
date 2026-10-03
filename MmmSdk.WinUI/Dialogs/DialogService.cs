using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Dialogs;

/// <summary>ダイアログの親を決めて、ダイアログ・モーダルウィンドウを開く</summary>
/// <remarks>
/// いちばん手前のモーダルウィンドウの上に表示する。無ければ、最後に操作した普通のウィンドウ（<see cref="TrackWindow"/> で登録したもの）の上に表示する。
/// 親にするのは、見えているウィンドウだけ（× でトレイへ退避した、隠れたウィンドウの上に出すと、ダイアログが見えない）。見えているものが無いときだけ、隠れたウィンドウを返す（<see cref="Owner"/>）。
/// 確認ダイアログは、同時に 1 つしか開けない（<c>ContentDialog</c> は、同じ画面に 2 つ同時に開くと例外になる）ので、順番に開く。
/// モーダルウィンドウは開いている間だけ覚えておき、その上で開くダイアログの親にする（一覧の上に入力画面・確認を重ねるため）。
/// アプリ固有の画面は、アプリ側のサービスが <see cref="IDialogHost"/> の <see cref="Owner"/> と <see cref="ShowModalAsync"/> を使って開く（具象型ではなく、<see cref="IDialogHost"/> / <see cref="IDialogService"/> に依存する）。UI スレッドから呼ぶ。
/// </remarks>
public sealed class DialogService : IDialogService, IDialogHost
{
    /// <summary>開いているモーダルウィンドウ（開いた順）</summary>
    private readonly List<Window> _modals = [];

    /// <summary>親の候補にした普通のウィンドウ（登録順。閉じられたら外す）</summary>
    private readonly List<Window> _tracked = [];

    /// <summary>最後に操作した普通のウィンドウ。無い・閉じられたら null</summary>
    private Window? _lastActive;

    /// <summary>確認ダイアログを順番に開くためのロック</summary>
    private readonly SemaphoreSlim _confirmLock = new(1, 1);

    /// <inheritdoc />
    public Window Owner
    {
        get
        {
            if (_modals.Count > 0)
            {
                return _modals[^1];
            }
            if (_lastActive is { } last && last.AppWindow.IsVisible)
            {
                return last;
            }

            return _tracked.FirstOrDefault(window => window.AppWindow.IsVisible)
                ?? _lastActive
                ?? _tracked.FirstOrDefault()
                ?? throw new InvalidOperationException("ダイアログの親にできるウィンドウがありません。");
        }
    }

    /// <inheritdoc />
    public void TrackWindow(Window window)
    {
        _tracked.Add(window);
        window.Activated += (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                _lastActive = window;
            }
        };
        window.Closed += (_, _) =>
        {
            _tracked.Remove(window);
            if (_lastActive == window)
            {
                _lastActive = null;
            }
        };
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText)
    {
        await _confirmLock.WaitAsync();
        try
        {
            var dialog = new ContentDialog
            {
                // 待っている間に親が変わることがあるので、順番が来てから決める
                XamlRoot = Owner.Content.XamlRoot,
                // コードで作るときは既定のスタイルが当たらないため、明示する（付けないと旧来の見た目になる）
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Title = title,
                Content = message,
                PrimaryButtonText = primaryText,
                CloseButtonText = closeText,
                // 取り消しにくい操作なので、Enter で誤って実行しないようキャンセルを既定にする
                DefaultButton = ContentDialogButton.Close,
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        finally
        {
            _confirmLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<T> ShowModalAsync<T>(Window window, Func<Window, Task<T>> show)
    {
        var owner = Owner;
        _modals.Add(window);
        try
        {
            return await show(owner);
        }
        finally
        {
            _modals.Remove(window);
        }
    }
}
