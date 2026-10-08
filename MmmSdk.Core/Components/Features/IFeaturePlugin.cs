using Microsoft.Extensions.DependencyInjection;

namespace MmmSdk.Core.Components.Features;

/// <summary>機能 (プラグイン)がホストへ入る入口</summary>
/// <remarks>
/// 機能ごとのライブラリが 1 つずつ実装する。ホストは <see cref="FeatureServiceCollectionExtensions.AddFeaturePlugin{TPlugin}"/> で、入口を呼ぶ。
/// 機能は、<see cref="Register"/> の中で、自分の保存先・サービス・画面・トレイメニュー・起動時の準備を DI に登録する。
/// サイドバー・トレイメニュー・設定は、ホストが渡す並び順の値 (<c>AddFeaturePlugin</c> の <c>order</c>)の順に並ぶ。起動時の準備は、登録した順に実行する。
/// </remarks>
public interface IFeaturePlugin
{
    /// <summary>機能のサービス・画面などを DI に登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    void Register(IServiceCollection services);
}
