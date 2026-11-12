namespace MmmSdk.Core.Components.Hosting;

/// <summary>アプリの名前とデータ・キャッシュ・アイコンの置き場所</summary>
/// <param name="Name">アプリの名前 (添付の一時フォルダーなど、アプリごとに分ける場所の名前に使う)。</param>
/// <param name="DataDirectory">JSON などの保存先フォルダー</param>
/// <param name="CacheDirectory">消しても作り直されるデータ (キャッシュ)の保存先フォルダー</param>
/// <param name="IconPath">アプリのアイコン (.ico)のパス (ウィンドウのタイトルバーに使う)</param>
/// <remarks>
/// ホストが 1 つ登録し、機能 (プラグイン)が使う。
/// 機能はホストの型 (アプリ本体のクラス)を参照できないため、この値を DI から受け取る。
/// </remarks>
public sealed record AppEnvironment(string Name, string DataDirectory, string CacheDirectory, string IconPath)
{
    /// <summary>WebView2 のデータの保存先フォルダー</summary>
    /// <remarks>
    /// キャッシュなどを入れる。
    /// 同じプロセスの WebView2 は同じフォルダーなら同じ設定で作る必要があるので、アプリの中の WebView2 はすべてここを使い、フォルダー名を使う側が別々に書かない。
    /// 場所は <see cref="CacheDirectory"/> の下の <c>WebView2</c>。
    /// </remarks>
    public string WebView2Directory => Path.Combine(CacheDirectory, "WebView2");
}
