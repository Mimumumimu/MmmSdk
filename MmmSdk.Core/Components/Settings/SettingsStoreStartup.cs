using MmmSdk.Core.Components.Features;

namespace MmmSdk.Core.Components.Settings;

/// <summary>共通の設定ファイルの先読み (起動時の準備。各機能より先に行う)</summary>
/// <param name="settings">共通の設定ストア</param>
/// <remarks>
/// 設定ストアは、最初の <c>Get</c> で同期で読み込む。それを UI スレッドで行わずに済むよう、ここで非同期で読んでおく。
/// 読めなかったときも例外にならず、<see cref="ISettingsStore.LoadError"/> に残る (設定ページなどで知らせる)。
/// ホストは、<c>AddStartupTask&lt;SettingsStoreStartup&gt;()</c> を、各機能の準備より先に登録する (機能のオン・オフ・サイドバーの並びは、設定を読んで決まるため)。
/// </remarks>
public sealed class SettingsStoreStartup(ISettingsStore settings) : IStartupTask
{
    /// <inheritdoc />
    public Task StartAsync() => settings.EnsureLoadedAsync();
}
