using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>ホストが作ったページを調べる口</summary>
/// <remarks>
/// 機能 (プラグイン)が自分のページの状態 (ターミナルが動いているかなど)を調べるために使う
/// ページの作成と破棄はホストが持つ
/// </remarks>
public interface IPageCache
{
    /// <summary>作ったことのあるページを取得する</summary>
    /// <typeparam name="TPage">ページの型</typeparam>
    /// <returns>
    /// 作ってあるページ
    /// まだ作っていなければ null
    /// </returns>
    /// <remarks>作っていなければ作らない</remarks>
    TPage? GetCreatedPage<TPage>()
        where TPage : Page;
}
