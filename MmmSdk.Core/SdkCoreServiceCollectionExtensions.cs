using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Repositories;
using MmmSdk.Core.Repositories.Json;
using MmmSdk.Core.Services;

namespace MmmSdk.Core;

/// <summary>MmmSdk.Core の DI 登録</summary>
public static class SdkCoreServiceCollectionExtensions
{
    /// <summary>SDK の UI 非依存のサービスを登録する</summary>
    /// <remarks>
    /// JSON ファイルの読み書き（<see cref="JsonFileStore"/>）・汎用設定ストア（<see cref="ISettingsStore"/>）・
    /// ウィンドウ位置の保存・パスを開く処理を、すべてアプリ全体で 1 つとして登録する。
    /// アプリ固有の保存でも <see cref="JsonFileStore"/> を共有して使える。
    /// </remarks>
    /// <param name="dataDirectory">JSON を保存するフォルダ。</param>
    public static IServiceCollection AddMmmSdkCore(this IServiceCollection services, string dataDirectory)
    {
        services.AddSingleton(new JsonFileStore(dataDirectory));
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<WindowPositionService>();
        services.AddSingleton<PathOpener>();
        return services;
    }
}
