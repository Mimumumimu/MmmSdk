namespace MmmSdk.Core.Components.Features;

/// <summary>機能のオン・オフの状態を調べる口</summary>
/// <remarks>機能 (プラグイン)が、自分のキーの状態を調べるために使う。状態の保存と切り替えは <see cref="FeatureService"/> が行う。</remarks>
public interface IFeatureStatus
{
    /// <summary>機能がオンか調べる</summary>
    /// <param name="featureKey">機能のキー (<see cref="FeatureInfo.Key"/>)</param>
    /// <returns>オンなら true。オフにできない機能のキー (未登録)も true</returns>
    bool IsEnabled(string featureKey);

    /// <summary>機能のオン・オフが切り替わったとき (引数は、切り替わった機能のキー)</summary>
    /// <remarks>ほかの機能の内容を出す画面が、見えている間に、出せる内容を合わせ直すために使う。見えなくなったら、購読を解除すること。</remarks>
    event EventHandler<string>? Changed;
}
