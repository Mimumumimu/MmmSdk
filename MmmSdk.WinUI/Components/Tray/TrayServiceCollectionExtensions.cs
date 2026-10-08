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
    /// 登録中の機能の並び順の値 (<see cref="FeatureServiceCollectionExtensions.AddFeaturePlugin{TPlugin}"/> で渡したもの)の順に、区切り線で分けて並ぶ (同じ値は登録順)。
    /// 値が大きい項目ほど下 (メニューが開く位置のカーソルに近い側)に出る。機能がオフの間は項目を出さない。
    /// 機能のオン・オフは、ホストが登録する <see cref="IFeatureStatus"/> で調べる。
    /// </remarks>
    public static IServiceCollection AddTrayMenuSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSource>(this IServiceCollection services, string? featureKey = null)
        where TSource : class, ITrayMenuSource
    {
        var order = FeatureRegistrationScope.CurrentOrder;
        services.AddSingleton<TSource>();
        return services.AddSingleton<ITrayMenuSource>(provider => new RegisteredTrayMenuSource(
            order,
            featureKey,
            provider.GetRequiredService<TSource>(),
            featureKey is null ? null : provider.GetRequiredService<IFeatureStatus>()));
    }
}
