using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Paths;
using MmmSdk.Core.Settings;
using MmmSdk.Core.Settings.Json;
using MmmSdk.Core.Storage;
using MmmSdk.Core.WindowPositions;

namespace MmmSdk.Core;

/// <summary>MmmSdk.Core の DI 登録</summary>
public static class SdkCoreServiceCollectionExtensions
{
    /// <summary>SDK の UI 非依存のサービスを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="dataDirectory">JSON を保存するフォルダ。</param>
    /// <returns>連続して呼べるよう、渡したサービスコレクション</returns>
    /// <remarks>
    /// JSON ファイルの読み書き（<see cref="IJsonFileStore"/>）・汎用設定ストア（<see cref="ISettingsStore"/>）・
    /// ウィンドウ位置の保存・パスを開く処理を、すべてアプリ全体で 1 つとして登録する。
    /// アプリ固有の保存でも <see cref="IJsonFileStore"/> を共有して使える。いずれも、実装の型ではなく、インターフェース（<c>IJsonFileStore</c>・<c>ISettingsStore</c>・<c>IWindowPositionService</c>・<c>IPathOpener</c>）で受け取る。
    /// </remarks>
    public static IServiceCollection AddMmmSdkCore(this IServiceCollection services, string dataDirectory)
    {
        services.AddSingleton<IJsonFileStore>(new JsonFileStore(dataDirectory));
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IWindowPositionService, WindowPositionService>();
        // パスを開く処理は Windows 前提（PathTarget・PathOpener）。Windows 以外では登録しない
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IPathOpener, PathOpener>();
        }
        return services;
    }
}
