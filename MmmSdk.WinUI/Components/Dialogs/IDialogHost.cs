using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>
/// ダイアログ・モーダルウィンドウの親を決める口 (アプリ固有の画面を開くサービスが使う)。
/// </summary>
/// <remarks>確認ダイアログを開く口は <see cref="IDialogService"/>。どちらも同じ <see cref="DialogService"/> が実装する。</remarks>
public interface IDialogHost
{
    /// <summary>ダイアログの親</summary>
    /// <remarks>
    /// いちばん手前のモーダルウィンドウ、無ければ最後に操作した普通のウィンドウ、それも無ければ最初に登録したウィンドウ。
    /// 普通のウィンドウは、見えているもの (<c>AppWindow.IsVisible</c>)だけを選ぶ。× でトレイへ退避した、隠れたウィンドウの上に出すと、ダイアログが見えないため。
    /// 見えているものが 1 つも無いときだけ、隠れたウィンドウ (最後に操作したもの、それも無ければ最初に登録したもの)を返す。このときは、
    /// 親の画面が見えないので、<c>ContentDialog</c> は見えないまま待ち続ける。呼ぶ側が、先に見えるウィンドウを出してから呼ぶこと。
    /// </remarks>
    /// <exception cref="InvalidOperationException">親にできるウィンドウが 1 つも登録されていない。</exception>
    Window Owner { get; }

    /// <summary>ダイアログを、今の親の上に載せる (開く前に呼ぶ)</summary>
    /// <param name="dialog">開く前のダイアログ</param>
    /// <remarks>
    /// 親の画面 (<c>XamlRoot</c>)と、親のテーマ (ライト・ダーク)を渡す。<c>ContentDialog</c> は親のテーマを引き継がないため、渡さないと、
    /// 背景・タイトルと、中身の部品とでテーマが食い違い、ダークモードで読めなくなる。ダイアログは必ず、これを通して開く
    /// (<c>Owner.Content.XamlRoot</c> を直接代入しない)。
    /// </remarks>
    /// <exception cref="InvalidOperationException">親にできるウィンドウが 1 つも登録されていない。</exception>
    void Attach(ContentDialog dialog);

    /// <summary>普通のウィンドウを、ダイアログの親の候補にする</summary>
    /// <param name="window">親の候補にするウィンドウ</param>
    /// <remarks>そのウィンドウを操作した (アクティブになった)ら、以後のダイアログをその上に出す。閉じられたら候補から外す。</remarks>
    void TrackWindow(Window window);

    /// <summary>モーダルウィンドウを今の親の上に開き、閉じるまで覚えておく</summary>
    /// <typeparam name="T">ウィンドウが返す結果の型</typeparam>
    /// <param name="window">開くウィンドウ</param>
    /// <param name="show">親を受け取って表示し、閉じるまで待つ処理</param>
    /// <returns>ウィンドウが返した結果</returns>
    Task<T> ShowModalAsync<T>(Window window, Func<Window, Task<T>> show);
}
