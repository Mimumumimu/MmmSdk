namespace MmmSdk.Core.Components.Features;

/// <summary>機能のオン・オフの状態を調べる口</summary>
/// <remarks>機能 (プラグイン)が、自分のキーの状態を調べるために使う。状態の保存と切り替えは <see cref="FeatureService"/> が行う。</remarks>
public interface IFeatureStatus
{
    /// <summary>機能がオンか調べる</summary>
    /// <param name="featureKey">機能のキー (<see cref="FeatureInfo.Key"/>)</param>
    /// <returns>オンなら true。オフにできない機能のキー (未登録)も true</returns>
    bool IsEnabled(string featureKey);
}
