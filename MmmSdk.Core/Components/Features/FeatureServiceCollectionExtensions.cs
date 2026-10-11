using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace MmmSdk.Core.Components.Features;

/// <summary>機能 (プラグイン)が、ホストへ自分を登録するための拡張メソッド (UI に依存しないもの)</summary>
public static class FeatureServiceCollectionExtensions
{
    /// <summary>機能のオン・オフの管理 (<see cref="FeatureService"/>)と、機能が状態を調べる口 (<see cref="IFeatureStatus"/>)を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>ホストが 1 回だけ呼ぶ。<see cref="Settings.ISettingsStore"/> を先に登録しておくこと。</remarks>
    public static IServiceCollection AddFeatureService(this IServiceCollection services)
    {
        services.AddSingleton<FeatureService>();
        return services.AddSingleton<IFeatureStatus>(provider => provider.GetRequiredService<FeatureService>());
    }

    /// <summary>設定でオン・オフを切り替えられる機能として登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">機能のキー (設定ファイルにも使う。決めたら変えない)</param>
    /// <param name="displayName">設定ページに出す機能の名前</param>
    /// <param name="defaultEnabled">保存が無いとき (初回起動)のオン・オフ</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 機能の <c>Add&lt;機能&gt;()</c> の中で、ページ・トレイメニュー・起動時の準備などの登録に、同じキーを渡す。
    /// 設定ページの「機能」の一覧に並ぶ (利用者が決めた並びが無いときは、機能の並び順の値 (<see cref="AddFeaturePlugin{TPlugin}"/>)の順)。
    /// </remarks>
    public static IServiceCollection AddFeature(this IServiceCollection services, string featureKey, string displayName, bool defaultEnabled = true)
        => services.AddSingleton(new FeatureInfo(featureKey, displayName, defaultEnabled, FeatureRegistrationScope.CurrentOrder));

    /// <summary>オフにできない機能として登録する (設定ページの「機能」の一覧に、スイッチなしで出し、並び順だけ使う)</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">機能のキー (設定ファイルにも使う。決めたら変えない)</param>
    /// <param name="displayName">設定ページに出す機能の名前</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 常にオンなので、起動時の準備・トレイメニューの登録には、キーを渡さなくてよい。
    /// サイドバーのページ・設定の部品の登録には、並び順を使うために同じキーを渡す。
    /// </remarks>
    public static IServiceCollection AddAlwaysOnFeature(this IServiceCollection services, string featureKey, string displayName)
        => services.AddSingleton(new FeatureInfo(featureKey, displayName, true, FeatureRegistrationScope.CurrentOrder, false));

    /// <summary>起動時の準備を登録する</summary>
    /// <typeparam name="TTask">起動時の準備の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">属する機能のキー (<see cref="AddFeature"/> で登録したもの)。オフにできない機能・共通の準備は null</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>登録した順に実行される。機能がオフの間は実行しない (オンにしたときに実行する)。</remarks>
    public static IServiceCollection AddStartupTask<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TTask>(this IServiceCollection services, string? featureKey = null)
        where TTask : class, IStartupTask
    {
        services.AddSingleton<TTask>();
        return services.AddSingleton(new StartupTaskRegistration(typeof(TTask), featureKey));
    }

    /// <summary>機能 (プラグイン)を、入口から登録する</summary>
    /// <typeparam name="TPlugin">機能の入口の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="order">機能の並び順の値 (小さいほど先。サイドバー・トレイメニュー・設定の機能の一覧・設定の部品の並びに使う)</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 画面の並びは <paramref name="order"/> で決まり、登録の順には依らない (同じ値は登録順)。
    /// 起動時の準備だけは、順序に依存するものがあるので、登録した順に実行する。
    /// </remarks>
    public static IServiceCollection AddFeaturePlugin<TPlugin>(this IServiceCollection services, int order)
        where TPlugin : IFeaturePlugin, new()
    {
        using (FeatureRegistrationScope.Begin(order))
        {
            new TPlugin().Register(services);
        }

        return services;
    }
}
