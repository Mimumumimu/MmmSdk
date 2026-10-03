using Microsoft.Extensions.DependencyInjection;

namespace MmmSdk.WinUI.Tray;

/// <summary>トレイアイコンの DI 登録</summary>
public static class TrayServiceCollectionExtensions
{
    /// <summary>トレイアイコンを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="options">アプリごとの設定</param>
    /// <returns>連続して呼べるよう、渡したサービスコレクション</returns>
    /// <remarks>
    /// <see cref="TrayIcon"/> は Singleton。メニューの項目は、<see cref="ITrayMenuSource"/> を Singleton で登録した順に並ぶ。
    /// <see cref="TrayIcon"/> は UI スレッドで解決すること（UI スレッドのディスパッチャーを覚えるため）。
    /// </remarks>
    public static IServiceCollection AddMmmSdkTray(this IServiceCollection services, TrayIconOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<TrayIcon>();
        return services;
    }
}
