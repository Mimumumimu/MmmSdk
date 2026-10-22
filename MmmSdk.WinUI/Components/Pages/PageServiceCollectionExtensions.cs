using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Features;

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
    /// <param name="order">並び順の値。null なら、登録中の機能の値 (<c>AddFeaturePlugin</c> で渡したもの。機能の外では 0)</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>項目は場所ごとに並び順の値の順に並ぶ (同じ値は登録順)。<paramref name="featureKey"/> が登録された機能 (<c>AddFeature</c> / <c>AddAlwaysOnFeature</c>)のときは、利用者が決めた機能の並びが優先される (<c>FeatureService.OrderOf</c>)。項目のキーはページの型名。機能がオフの間は出さない。</remarks>
    public static IServiceCollection AddNavigationPage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage>(this IServiceCollection services, string title, string glyph, NavigationArea area, string? featureKey = null, int? order = null)
        where TPage : Page
    {
        services.AddTransient<TPage>();
        services.AddSingleton(new NavigationPage(new NavigationItem(typeof(TPage).Name, title, glyph), typeof(TPage), area, featureKey, order ?? FeatureRegistrationScope.CurrentOrder));
        return services;
    }

    /// <summary>サイドバーに、別のページの子の項目として、ページを登録する</summary>
    /// <typeparam name="TPage">表示するページの型</typeparam>
    /// <typeparam name="TParentPage">親の項目のページの型 (<see cref="AddNavigationPage{TPage}"/> で登録したもの)</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="title">サイドバーの表示名</param>
    /// <param name="glyph">サイドバーのアイコン (Segoe Fluent Icons のグリフ)</param>
    /// <param name="featureKey">属する機能のキー (<c>AddFeature</c> で登録したもの)。オフにできない機能は null</param>
    /// <param name="order">並び順の値 (親の下での順)。null なら、登録中の機能の値 (<c>AddFeaturePlugin</c> で渡したもの。機能の外では 0)</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// 子の項目は、サイドバーの上部の、親の項目の直後に、字下げして並べる (下部には出さない。開閉は無く、常に出す)。親がオフの間は出さない。子の下に、さらに子は持てない。
    /// 項目のキーはページの型名。親の項目は、この呼び出しより前でも後でも、登録されていればよい。
    /// </remarks>
    public static IServiceCollection AddNavigationChildPage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPage, TParentPage>(this IServiceCollection services, string title, string glyph, string? featureKey = null, int? order = null)
        where TPage : Page
        where TParentPage : Page
    {
        services.AddTransient<TPage>();
        services.AddSingleton(new NavigationPage(new NavigationItem(typeof(TPage).Name, title, glyph) { IsChild = true }, typeof(TPage), NavigationArea.Top, featureKey, order ?? FeatureRegistrationScope.CurrentOrder, typeof(TParentPage).Name));
        return services;
    }

    /// <summary>サイドバーの本体 (<see cref="NavigationHost"/>)と、その部品を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>
    /// ホストアプリだけが呼ぶ。機能は呼ばない。<c>FeatureService</c>(<c>AddFeatureService</c>)を先に登録すること。
    /// <see cref="NavigationHost"/> は UI スレッドで解決すること。<see cref="IPageCache"/> は <see cref="PageProvider"/> と同じインスタンスを返す。
    /// 機能がページを開いてもらう口 (<see cref="INavigationService"/>)も登録する。ホストのウィンドウを前面に出すのは、ホストアプリが <see cref="INavigationService.ShowRequested"/> で行う。
    /// </remarks>
    public static IServiceCollection AddNavigationHost(this IServiceCollection services)
    {
        services.AddSingleton<PageProvider>();
        services.AddSingleton<IPageCache>(provider => provider.GetRequiredService<PageProvider>());
        services.AddSingleton<IBusyCursor, BusyCursor>();
        services.AddSingleton<NavigationHostViewModel>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<NavigationHost>();
        return services;
    }

    /// <summary>設定ページに、機能ごとの設定の部品を登録する</summary>
    /// <typeparam name="TControl">設定の部品 (<c>UserControl</c>)の型</typeparam>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="featureKey">属する機能のキー (<c>AddFeature</c> で登録したもの)。オフにできない機能は null</param>
    /// <param name="order">並び順の値。null なら、登録中の機能の値 (<c>AddFeaturePlugin</c> で渡したもの。機能の外では 0)</param>
    /// <returns>登録先のサービスコレクション (続けて登録するため)</returns>
    /// <remarks>部品は設定ページを開くときに DI から作る (Transient)。並び順の値の順に並ぶ (同じ値は登録順。登録された機能のものは、利用者が決めた機能の並びが優先される)。値の読み書きと画面の状態は、その機能の ViewModel・サービスが持つ。機能がオフの間は並べない。</remarks>
    public static IServiceCollection AddSettingsSection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TControl>(this IServiceCollection services, string? featureKey = null, int? order = null)
        where TControl : UserControl
    {
        services.AddTransient<TControl>();
        services.AddSingleton(new SettingsSection(typeof(TControl), featureKey, order ?? FeatureRegistrationScope.CurrentOrder));
        return services;
    }
}
