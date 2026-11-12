namespace MmmSdk.Core.Components.Features;

/// <summary>機能をオフにする前の確認</summary>
/// <remarks>
/// 作業中の内容が失われるときに機能が登録する。
/// ホストが機能をオフにする前に呼ぶ。
/// 確認が要らない機能は登録しない。
/// 機能をオフにしたあとの後始末はホストの切り替えの通知を受けるか、ページを捨てるとき (ページを作ったスコープの破棄)に行う。
/// </remarks>
public interface IFeatureDisableConfirmation
{
    /// <summary>対象の機能のキー</summary>
    string FeatureKey { get; }

    /// <summary>オフにしてよいかを確かめる</summary>
    /// <returns>
    /// オフにしてよければ true で、確認が要らないとき、確認で了承されたとき。
    /// 取りやめるときは false。
    /// </returns>
    Task<bool> ConfirmAsync();
}
