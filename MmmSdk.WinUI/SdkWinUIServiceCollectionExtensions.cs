using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Dialogs;
using MmmSdk.WinUI.Notifications;

namespace MmmSdk.WinUI;

/// <summary>MmmSdk.WinUI の DI 登録</summary>
public static class SdkWinUIServiceCollectionExtensions
{
    /// <summary>SDK の WinUI 依存のサービス（通知ダイアログ・確認ダイアログ・ファイル/フォルダー選択）を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>連続して呼べるよう、渡したサービスコレクション</returns>
    /// <remarks>
    /// 先に <c>AddMmmSdkCore</c> を呼んでおくこと（位置の保存・リンクを開く処理を使う）。
    /// <see cref="DialogService"/> は、具象型と <see cref="IDialogService"/> のどちらからも同じインスタンスを受け取れる（親にするウィンドウの登録を共有するため）。
    /// 通知ウィンドウはユーザーが閉じたら作り直すので、ウィンドウと ViewModel は Transient で登録する。
    /// </remarks>
    public static IServiceCollection AddMmmSdkWinUI(this IServiceCollection services)
    {
        services.AddSingleton<INotificationDialogService, NotificationDialogService>();
        services.AddTransient<NotificationWindow>();
        services.AddTransient<NotificationDialogViewModel>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<IDialogService>(provider => provider.GetRequiredService<DialogService>());
        services.AddSingleton<IFilePickerService, FilePickerService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        return services;
    }
}
