using Microsoft.UI.Windowing;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>ページを別のウィンドウへ移すとき、画面だけを作り直して、画面の外にある状態を引き継げるページ</summary>
/// <remarks>
/// WebView2 は、作られたウィンドウでしか日本語入力 (IME)が働かず、別のウィンドウへ付け替えて使えない。
/// そこで、ページを持つスコープ (ViewModel やシェルなど、画面の外の状態)は捨てずに、同じスコープから新しいページを作り、画面の中身だけを引き継ぐ。
/// このインターフェースを持たないページは、移すときにスコープごと作り直す。
/// 移す手順は、<see cref="PrepareHandoverAsync"/> (画面は出したまま、内容を取り出す)→ <see cref="TakeHandover"/> (古い画面を閉じて、引き継ぎの情報を受け取る)→ 新しいページの <see cref="ApplyHandover"/>。
/// <see cref="TakeHandover"/> と <see cref="ApplyHandover"/> は、シェルの出力を取りこぼさないよう、UI スレッドの同じ処理の中で続けて呼ぶ。
/// </remarks>
public interface IHandoverPage : IReleasablePage
{
    /// <summary>画面の内容を取り出す準備をする</summary>
    /// <returns>準備の完了を表すタスク</returns>
    /// <remarks>
    /// 画面は表示したままでよい。準備のあいだに届いた出力は、画面へ送らずにためる。
    /// 呼んだあとは、<see cref="TakeHandover"/> を呼ぶこと。
    /// </remarks>
    Task PrepareHandoverAsync();

    /// <summary>古い画面を閉じて、引き継ぎの情報を受け取る</summary>
    /// <returns>引き継ぎの情報。新しいページの <see cref="ApplyHandover"/> へ渡す</returns>
    /// <remarks>
    /// 画面の部品と ViewModel とのつなぎをすべて外す。<see cref="IReleasablePage.Release"/> の内容も済ませる。
    /// ViewModel やシェルなど、スコープが持つものは終了しない。
    /// </remarks>
    object TakeHandover();

    /// <summary>引き継ぎの情報を、新しいページに反映する</summary>
    /// <param name="handover">古いページの <see cref="TakeHandover"/> の戻り値</param>
    /// <param name="hostWindow">ページが載るウィンドウ。まだ作っていないときは null</param>
    /// <remarks>
    /// ページを作った直後、画面に出す前に呼ぶ。
    /// <paramref name="hostWindow"/> は、ページが画面に出るまでに、ウィンドウへ知らせたいこと (タスクバーの点滅など)のために渡す。
    /// メインのページへ戻すときは、ページが画面に出るのは、サイドバーで項目を押したときになる。
    /// </remarks>
    void ApplyHandover(object handover, AppWindow? hostWindow);
}
