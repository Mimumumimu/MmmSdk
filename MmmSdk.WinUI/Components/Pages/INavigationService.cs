using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>
/// 機能が、サイドバーのページを開いてもらうための口。
/// </summary>
/// <remarks>
/// 通知のクリックなど、ページの外から「このページを見せたい」ときに使う。UI スレッドから呼ぶ。
/// ホストのウィンドウを前面に出すのは、ホストアプリの役目で、<see cref="ShowRequested"/> を受けて行う。
/// </remarks>
public interface INavigationService
{
    /// <summary>ホストのウィンドウを、前面に出してほしい</summary>
    /// <remarks>ウィンドウをトレイへ隠している間でも、ページを見せられるよう、ページを開くたびに知らせる。</remarks>
    event EventHandler? ShowRequested;

    /// <summary>サイドバーで、指定のページの項目を選んで、ページを開く</summary>
    /// <typeparam name="TPage">開くページの型 (<c>AddNavigationPage</c> で登録したもの)</typeparam>
    /// <returns>ページを開いたら true。サイドバーに項目が無い (機能がオフ・未登録)なら false</returns>
    bool Open<TPage>()
        where TPage : Page;
}
