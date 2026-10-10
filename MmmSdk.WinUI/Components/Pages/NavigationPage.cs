namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーに出すページの登録情報</summary>
/// <param name="Item">サイドバーの項目</param>
/// <param name="PageType">表示するページの型 (DI から作る)</param>
/// <param name="Area">サイドバーの中で出す場所</param>
/// <param name="FeatureKey">属する機能のキー。オフにできない機能は null</param>
/// <param name="Order">並び順の値 (小さいほど先。同じ値は登録順)</param>
/// <remarks>各機能が <see cref="PageServiceCollectionExtensions.AddNavigationPage{TPage}"/> で登録し、並び順の値の順に並ぶ (登録された機能のページは、利用者が決めた機能の並びが優先)。機能がオフの間は出さない。</remarks>
public sealed record NavigationPage(NavigationItem Item, Type PageType, NavigationArea Area, string? FeatureKey, int Order = 0);
