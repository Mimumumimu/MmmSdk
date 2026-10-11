namespace MmmSdk.WinUI.Components.Pages;

/// <summary>設定ページに並べる、機能ごとの設定の部品 (登録情報)</summary>
/// <param name="ControlType">設定の部品 (<c>UserControl</c>)の型</param>
/// <param name="FeatureKey">属する機能のキー。オフにできない機能は null</param>
/// <param name="Order">並び順の値 (小さいほど先。同じ値は登録順)</param>
/// <remarks>各機能が <see cref="PageServiceCollectionExtensions.AddSettingsSection{TControl}"/> で登録し、設定ページが並び順の値の順 (登録された機能の部品は、利用者が決めた機能の並び)に並べる (サイドバーの <see cref="NavigationPage"/> と同じ形)。機能がオフの間は並べない。</remarks>
public sealed record SettingsSection(Type ControlType, string? FeatureKey, int Order = 0);
