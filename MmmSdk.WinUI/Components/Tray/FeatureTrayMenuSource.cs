using MmmSdk.Core.Components.Features;

namespace MmmSdk.WinUI.Components.Tray;

/// <summary>機能がオフの間、トレイメニューの項目を出さないようにする包み</summary>
/// <param name="featureKey">機能のキー</param>
/// <param name="inner">機能のトレイメニューの項目の元</param>
/// <param name="features">機能のオン・オフ</param>
/// <remarks><see cref="TrayServiceCollectionExtensions.AddTrayMenuSource{TSource}"/> が、機能のキーを指定されたときだけ使う。メニューは開くたびに作るので、切り替えはすぐ反映される。</remarks>
internal sealed class FeatureTrayMenuSource(string featureKey, ITrayMenuSource inner, IFeatureStatus features) : ITrayMenuSource
{
    /// <inheritdoc />
    public IReadOnlyList<TrayMenuItem> GetItems() => features.IsEnabled(featureKey) ? inner.GetItems() : [];
}
