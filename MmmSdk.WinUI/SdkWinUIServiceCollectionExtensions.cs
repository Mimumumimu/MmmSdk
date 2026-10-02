using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Services;
using MmmSdk.WinUI.ViewModels;
using MmmSdk.WinUI.Views;

namespace MmmSdk.WinUI;

/// <summary>MmmSdk.WinUI の DI 登録</summary>
public static class SdkWinUIServiceCollectionExtensions
{
    /// <summary>SDK の WinUI 依存のサービス（通知ダイアログ）を登録する</summary>
    /// <remarks>
    /// 先に <c>AddMmmSdkCore</c> を呼んでおくこと（位置の保存・リンクを開く処理を使う）。
    /// 通知ウィンドウはユーザーが閉じたら作り直すので、ウィンドウと ViewModel は Transient で登録する。
    /// </remarks>
    public static IServiceCollection AddMmmSdkWinUI(this IServiceCollection services)
    {
        services.AddSingleton<INotificationDialogService, NotificationDialogService>();
        services.AddTransient<NotificationWindow>();
        services.AddTransient<NotificationDialogViewModel>();
        return services;
    }
}
