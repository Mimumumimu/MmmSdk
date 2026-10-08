using MmmSdk.Core.Components.Features;

namespace MmmSdk.WinUI.Components.Tray;

/// <summary>登録されたトレイメニューの項目の元に、並び順の値を付け、機能がオフの間は項目を出さないようにする包み</summary>
/// <param name="order">並び順の値 (小さいほど先)</param>
/// <param name="featureKey">機能のキー。オフにできない機能は null</param>
/// <param name="inner">機能のトレイメニューの項目の元</param>
/// <param name="features">機能のオン・オフ。機能のキーが null のときは使わないので null</param>
/// <remarks>
/// <see cref="TrayServiceCollectionExtensions.AddTrayMenuSource{TSource}"/> が、登録のたびに使う。メニューは開くたびに作るので、切り替えはすぐ反映される。
/// オフにできない機能 (キーが null)は、<see cref="IFeatureStatus"/> を受け取らない (トレイアイコンを作る時点で、機能のオン・オフの管理を作らないため)。
/// </remarks>
internal sealed class RegisteredTrayMenuSource(int order, string? featureKey, ITrayMenuSource inner, IFeatureStatus? features) : ITrayMenuSource
{
    /// <summary>並び順の値 (小さいほど先)</summary>
    public int Order { get; } = order;

    /// <inheritdoc />
    public IReadOnlyList<TrayMenuItem> GetItems() => featureKey is null || features!.IsEnabled(featureKey) ? inner.GetItems() : [];
}
