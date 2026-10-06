using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>ホストが作ったページを調べる口</summary>
/// <remarks>機能 (プラグイン)が、自分のページの状態 (ターミナルが動いているかなど)を調べるために使う。ページの作成と破棄はホストが持つ。</remarks>
public interface IPageCache
{
    /// <summary>作ったことのあるページを取得する (作っていなければ作らない)</summary>
    /// <typeparam name="TPage">ページの型</typeparam>
    /// <returns>作ってあるページ。まだ作っていなければ null</returns>
    TPage? GetCreatedPage<TPage>()
        where TPage : Page;
}
