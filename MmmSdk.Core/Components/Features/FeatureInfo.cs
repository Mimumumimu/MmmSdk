namespace MmmSdk.Core.Components.Features;

/// <summary>設定でオン・オフを切り替えられる機能 (登録情報)</summary>
/// <param name="Key">機能を識別するキー (設定ファイルにも使う。変えると、保存済みのオン・オフが外れる)</param>
/// <param name="DisplayName">設定ページに出す機能の名前</param>
/// <param name="DefaultEnabled">保存が無いとき (初回起動)のオン・オフ</param>
/// <param name="Order">機能の並び順の値 (小さいほど先)</param>
/// <remarks>
/// 各機能が <see cref="FeatureServiceCollectionExtensions.AddFeature"/> で登録する。
/// オフにできない機能は登録しない (サイドバー・トレイ・起動時の準備などの登録で、キーを指定しない)。
/// </remarks>
public sealed record FeatureInfo(string Key, string DisplayName, bool DefaultEnabled = true, int Order = 0);
