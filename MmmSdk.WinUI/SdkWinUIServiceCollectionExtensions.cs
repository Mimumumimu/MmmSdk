using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Secrets;
using MmmSdk.WinUI.Components.Attachments;
using MmmSdk.WinUI.Components.Clipboards;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Notifications;
using MmmSdk.WinUI.Components.Secrets;
using MmmSdk.WinUI.Components.Tray;

namespace MmmSdk.WinUI;

/// <summary>MmmSdk.WinUI の DI 登録</summary>
public static class SdkWinUIServiceCollectionExtensions
{
    /// <summary>SDK の WinUI 依存のサービス (通知ダイアログ・確認ダイアログ・ファイル/フォルダー選択・画像の変換・クリップボード・秘密の保存)を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>連続して呼べるよう、渡したサービスコレクション</returns>
    /// <remarks>
    /// 先に <c>AddMmmSdkCore</c> を呼んでおくこと (位置の保存・リンクを開く処理を使う)。
    /// <see cref="DialogService"/> は、<see cref="IDialogService"/>(確認ダイアログ)と <see cref="IDialogHost"/>(親の決定)のどちらからも同じインスタンスを受け取れる (親にするウィンドウの登録を共有するため)。アプリは具象型ではなく、この 2 つの口に依存する。
    /// 通知ウィンドウはユーザーが閉じたら作り直すので、ウィンドウと ViewModel は Transient で登録する。
    /// </remarks>
    public static IServiceCollection AddMmmSdkWinUI(this IServiceCollection services)
    {
        services.AddSingleton<INotificationDialogService, NotificationDialogService>();
        services.AddTransient<NotificationWindow>();
        services.AddSingleton<Func<NotificationWindow>>(provider => () => provider.GetRequiredService<NotificationWindow>());
        services.AddTransient<NotificationWindowViewModel>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<DialogService>());
        services.AddSingleton<IDialogHost>(provider => provider.GetRequiredService<DialogService>());
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IImageConverter, ImageConverter>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<ISecretStore, CredentialSecretStore>();
        return services;
    }

    /// <summary>トレイアイコンを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="options">アプリごとの設定</param>
    /// <returns>連続して呼べるよう、渡したサービスコレクション</returns>
    /// <remarks>
    /// トレイを使うアプリだけが呼ぶ (<see cref="AddMmmSdkWinUI"/> には含まれない)。
    /// <see cref="TrayIcon"/> は Singleton。メニューの項目は、<see cref="ITrayMenuSource"/> を Singleton で登録した順に並ぶ。
    /// <see cref="TrayIcon"/> は UI スレッドで解決すること (UI スレッドのディスパッチャーを覚えるため)。
    /// </remarks>
    public static IServiceCollection AddMmmSdkTray(this IServiceCollection services, TrayIconOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<TrayIcon>();
        return services;
    }
}
