using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Utilities;

namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>ダイアログの親を決めて、ダイアログ・モーダルウィンドウを開く</summary>
/// <remarks>
/// いちばん手前のモーダルウィンドウの上に表示する。無ければ、最後に操作した普通のウィンドウ (<see cref="TrackWindow"/> で登録したもの)の上に表示する。
/// 親にするのは、見えているウィンドウだけ (× でトレイへ退避した、隠れたウィンドウの上に出すと、ダイアログが見えない)。見えているものが無いときだけ、隠れたウィンドウを返す (<see cref="Owner"/>)。
/// 確認ダイアログは、同時に 1 つしか開けない (<c>ContentDialog</c> は、同じ画面に 2 つ同時に開くと例外になる)ので、順番に開く。
/// モーダルウィンドウは開いている間だけ覚えておき、その上で開くダイアログの親にする (一覧の上に入力画面・確認を重ねるため)。
/// アプリ固有の画面は、アプリ側のサービスが <see cref="IDialogHost"/> の <see cref="Owner"/> と <see cref="ShowModalAsync"/> (ウィンドウ)、<see cref="Attach"/> (<c>ContentDialog</c>。親の画面とテーマを渡す)を使って開く (具象型ではなく、<see cref="IDialogHost"/> / <see cref="IDialogService"/> に依存する)。UI スレッドから呼ぶ。
/// </remarks>
public sealed class DialogService : IDialogService, IDialogHost
{
    /// <summary>開いているモーダルウィンドウ (開いた順)</summary>
    private readonly List<Window> _modals = [];

    /// <summary>親の候補にした普通のウィンドウ (登録順。閉じられたら外す)</summary>
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
    public void Attach(ContentDialog dialog)
    {
        // Owner は計算するプロパティなので、1 回だけ読む (2 回読むと別のウィンドウを返しうる)
        var content = Owner.Content;
        dialog.XamlRoot = content.XamlRoot;
        if (content is FrameworkElement element)
        {
            dialog.RequestedTheme = element.ActualTheme;
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
                // コードで作るときは既定のスタイルが当たらないため、明示する (付けないと旧来の見た目になる)
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Title = title,
                Content = message,
                PrimaryButtonText = primaryText,
                CloseButtonText = closeText,
                // 取り消しにくい操作なので、既定のボタンを置かない (Enter で誤って実行しない)。
                // キャンセルを既定にすると、キャンセルが強調色になり、主な操作に見えてしまうため、どちらも強調しない
                DefaultButton = ContentDialogButton.None,
            };
            // 待っている間に親が変わることがあるので、順番が来てから決める
            Attach(dialog);
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        finally
        {
            _confirmLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<FileConflictChoice> AskFileConflictAsync(
        string title,
        string message,
        string replaceText,
        string skipText,
        string closeText,
        string? decideEachText = null)
    {
        await _confirmLock.WaitAsync();
        try
        {
            var choice = FileConflictChoice.Cancel;
            var dialog = new ContentDialog
            {
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Title = title,
                CloseButtonText = closeText,
                DefaultButton = ContentDialogButton.None,
            };
            Attach(dialog);

            // 選択肢のボタンを押したら、選択を覚えてダイアログを閉じる
            Button CreateOption(string glyph, string text, FileConflictChoice value)
            {
                var button = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(12, 10, 12, 10),
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        Children =
                        {
                            new FontIcon { Glyph = glyph, FontSize = 16 },
                            new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center },
                        },
                    },
                };
                button.Click += (_, _) =>
                {
                    choice = value;
                    dialog.Hide();
                };
                return button;
            }

            // 既定のボタンを置かなくても、最初の入力欄 (置き換えるボタン)にフォーカスが当たり、Enter・Space 1 回で置き換えてしまう。
            // 開いたら、取りやめるボタンへフォーカスを移す (取りやめるボタンを強調色にしないままにするため、DefaultButton には使わない)
            dialog.Opened += (_, _) => VisualTreeSearch.FindDescendant<Button>(dialog, "CloseButton")?.Focus(FocusState.Programmatic);

            var options = new StackPanel { Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
            options.Children.Add(CreateOption("", replaceText, FileConflictChoice.Replace));
            options.Children.Add(CreateOption("", skipText, FileConflictChoice.Skip));
            if (decideEachText is not null)
            {
                options.Children.Add(CreateOption("", decideEachText, FileConflictChoice.DecideEach));
            }

            var content = new StackPanel();
            content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords });
            content.Children.Add(options);
            dialog.Content = content;

            await dialog.ShowAsync();
            return choice;
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
