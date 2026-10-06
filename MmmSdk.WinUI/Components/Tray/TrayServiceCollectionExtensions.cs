using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;

namespace MmmSdk.WinUI.Components.Tray;

/// <summary>機能 (プラグイン)が、トレイメニューの項目を登録するための拡張メソッド</summary>
public static class TrayServiceCollectionExtensions
{
    /// <summary>トレイメニューに項目を出す機能を登録する</summary>
    /// <typeparam name="TSource">トレイメニューの項目の元の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">属する機能のキー (<see cref="FeatureServiceCollectionExtensions.AddFeature"/> で登録したもの)。オフにできない機能は null</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 登録した順に、区切り線で分けて並ぶ。機能がオフの間は項目を出さない。
    /// 機能のオン・オフは、ホストが登録する <see cref="IFeatureStatus"/> で調べる。
    /// </remarks>
    public static IServiceCollection AddTrayMenuSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSource>(this IServiceCollection services, string? featureKey = null)
        where TSource : class, ITrayMenuSource
    {
        if (featureKey is null)
        {
            return services.AddSingleton<ITrayMenuSource, TSource>();
        }

        services.AddSingleton<TSource>();
        return services.AddSingleton<ITrayMenuSource>(provider => new FeatureTrayMenuSource(
            featureKey,
            provider.GetRequiredService<TSource>(),
            provider.GetRequiredService<IFeatureStatus>()));
    }
}
