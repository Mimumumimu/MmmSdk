using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>機能 (プラグイン)が、サイドバーのページと設定の部品を登録するための拡張メソッド</summary>
public static class PageServiceCollectionExtensions
{
    /// <summary>サイドバーにページを登録する</summary>
    /// <typeparam name="TPage">表示するページの型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="title">サイドバーの表示名</param>
    /// <param name="glyph">サイドバーのアイコン (Segoe Fluent Icons のグリフ)</param>
    /// <param name="area">サイドバーの中で出す場所</param>
    /// <param name="featureKey">属する機能のキー (<c>AddFeature</c> で登録したもの)。オフにできない機能は null</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>項目は場所ごとに登録した順に並ぶ。項目のキーはページの型名。機能がオフの間は出さない。</remarks>
    public static IServiceCollection AddNavigationPage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(this IServiceCollection services, string title, string glyph, NavigationArea area, string? featureKey = null)
        where TPage : Page
    {
        services.AddTransient<TPage>();
        services.AddSingleton(new NavigationPage(new NavigationItem(typeof(TPage).Name, title, glyph), typeof(TPage), area, featureKey));
        return services;
    }

    /// <summary>設定ページに、機能ごとの設定の部品を登録する</summary>
    /// <typeparam name="TControl">設定の部品 (<c>UserControl</c>)の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">属する機能のキー (<c>AddFeature</c> で登録したもの)。オフにできない機能は null</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>部品は設定ページを開くときに DI から作る (Transient)。登録した順に並ぶ。値の読み書きと画面の状態は、その機能の ViewModel・サービスが持つ。機能がオフの間は並べない。</remarks>
    public static IServiceCollection AddSettingsSection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TControl>(this IServiceCollection services, string? featureKey = null)
        where TControl : UserControl
    {
        services.AddTransient<TControl>();
        services.AddSingleton(new SettingsSection(typeof(TControl), featureKey));
        return services;
    }
}
