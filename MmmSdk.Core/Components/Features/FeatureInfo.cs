namespace MmmSdk.Core.Components.Features;

/// <summary>設定でオン・オフを切り替えられる機能 (登録情報)</summary>
/// <param name="Key">機能を識別するキー (設定ファイルにも使う。変えると、保存済みのオン・オフが外れる)</param>
/// <param name="DisplayName">設定ページに出す機能の名前</param>
/// <param name="DefaultEnabled">保存が無いとき (初回起動)のオン・オフ</param>
/// <param name="Order">機能の並び順の値 (小さいほど先)</param>
/// <param name="CanDisable">オフにできるか。false の機能は常にオンで、並び順だけを持つ</param>
/// <remarks>
/// 各機能が <see cref="FeatureServiceCollectionExtensions.AddFeature"/> (オフにできる機能)または
/// <see cref="FeatureServiceCollectionExtensions.AddAlwaysOnFeature"/> (オフにできない機能。並び順だけ使う)で登録する。
/// </remarks>
public sealed record FeatureInfo(string Key, string DisplayName, bool DefaultEnabled = true, int Order = 0, bool CanDisable = true);
