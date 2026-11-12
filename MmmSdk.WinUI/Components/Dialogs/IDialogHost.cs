using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>ダイアログ・モーダルウィンドウの親を決める口</summary>
/// <remarks>
/// アプリ固有の画面を開くサービスが使う
/// 確認ダイアログを開く口は <see cref="IDialogService"/>
/// どちらも同じ <see cref="DialogService"/> が実装する
/// </remarks>
public interface IDialogHost
{
    /// <summary>ダイアログの親</summary>
    /// <remarks>
    /// 最後に操作した (アクティブになった)ウィンドウで、登録した普通のウィンドウ・開いているモーダルウィンドウ。
    /// まだ無ければ、いちばん手前のモーダルウィンドウ、それも無ければ最初に登録したウィンドウ。
    /// 普通のウィンドウは見えているもの (<c>AppWindow.IsVisible</c>)だけを選ぶ。
    /// × でトレイへ退避した、隠れたウィンドウの上に出すと、ダイアログが見えないため。
    /// 見えているものが 1 つも無いときだけ、隠れたウィンドウ (最後に操作したもの、それも無ければ最初に登録したもの)を返す。
    /// このときは。
    /// 親の画面が見えないので、<c>ContentDialog</c> は見えないまま待ち続ける。
    /// 呼ぶ側が先に見えるウィンドウを出してから呼ぶこと。
    /// </remarks>
    /// <exception cref="InvalidOperationException">親にできるウィンドウが 1 つも登録されていない</exception>
    Window Owner { get; }

    /// <summary>ダイアログを今の親の上に載せる</summary>
    /// <param name="dialog">開く前のダイアログ</param>
    /// <remarks>
    /// 開く前に呼ぶ。
    /// 親の画面 (<c>XamlRoot</c>)と親のテーマ (ライト・ダーク)を渡す。
    /// <c>ContentDialog</c> は親のテーマを引き継がないため、渡さないと、
    /// 背景・タイトルと、中身の部品とでテーマが食い違い、ダークモードで読めなくなる。
    /// ダイアログは必ず、これを通して開く。
    /// <c>Owner.Content.XamlRoot</c> を直接代入しない。
    /// </remarks>
    /// <exception cref="InvalidOperationException">親にできるウィンドウが 1 つも登録されていない</exception>
    void Attach(ContentDialog dialog);

    /// <summary>普通のウィンドウをダイアログの親の候補にする</summary>
    /// <param name="window">親の候補にするウィンドウ</param>
    /// <remarks>
    /// そのウィンドウを操作した (アクティブになった)ら、以後のダイアログをその上に出す。
    /// 閉じられたら候補から外す。
    /// </remarks>
    void TrackWindow(Window window);

    /// <summary>モーダルウィンドウを今の親の上に開き、閉じるまで覚えておく</summary>
    /// <typeparam name="T">ウィンドウが返す結果の型</typeparam>
    /// <param name="window">開くウィンドウ</param>
    /// <param name="show">親を受け取って表示し、閉じるまで待つ処理。</param>
    /// <returns>ウィンドウが返した結果</returns>
    Task<T> ShowModalAsync<T>(Window window, Func<Window, Task<T>> show);

    /// <summary>開いているウィンドウを閉じる</summary>
    /// <param name="keep">
    /// 閉じずに残すウィンドウで、アプリのメインウィンドウ。
    /// 無ければ null。
    /// </param>
    /// <remarks>
    /// アプリの終了の前に呼ぶ。
    /// モーダルウィンドウを新しいものから、そのあと <see cref="TrackWindow"/> で登録した普通のウィンドウを新しいものから、閉じる。
    /// 各ウィンドウの閉じる処理 (後始末・結果の返却)を、DI のホストを破棄する前に、親子の順に済ませるため。
    /// 最後のウィンドウを閉じるとアプリが終了するので、<paramref name="keep"/> は残し、アプリの終了 (<c>Application.Exit</c>)に任せる。
    /// UI スレッドから呼ぶ。
    /// </remarks>
    void CloseAll(Window? keep);

    /// <summary>閉じている最中か</summary>
    /// <remarks>
    /// <see cref="CloseAll"/> で閉じている最中かを表す。
    /// 各ウィンドウの閉じる処理が同期の中で調べる。
    /// アプリの終了では DI のホストをすぐ破棄するので、閉じる前に非同期の処理を挟むウィンドウ (外したページのウィンドウなど)は挟まずに閉じる。
    /// </remarks>
    bool IsClosingAll { get; }
}
