using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace MmmSdk.WinUI.Components.WebViews;

/// <summary>
/// WebView2 の初期化 (保存先の指定つき)。ターミナルなど、WebView2 を使う部品が共有する。
/// </summary>
public static class WebView2Setup
{
    /// <summary>WebView2 を、指定の保存先で初期化する</summary>
    /// <param name="view">初期化する WebView2</param>
    /// <param name="userDataFolder">WebView2 のデータ (キャッシュなど)の保存先フォルダー。null なら WebView2 の既定 (EXE の隣の <c>&lt;EXE 名&gt;.WebView2</c>)</param>
    /// <returns>初期化の完了を表すタスク</returns>
    /// <remarks>
    /// 同じプロセスの WebView2 は、同じフォルダーなら同じ設定で作る必要があるので、設定はここ 1 か所に置く。
    /// WebView2 ランタイムが無い・起動できないときは <see cref="System.Runtime.InteropServices.COMException"/> が出る。
    /// </remarks>
    public static async Task EnsureAsync(WebView2 view, string? userDataFolder)
    {
        ArgumentNullException.ThrowIfNull(view);

        var environment = userDataFolder is null
            ? null
            : await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, new CoreWebView2EnvironmentOptions());
        await view.EnsureCoreWebView2Async(environment);
    }
}
