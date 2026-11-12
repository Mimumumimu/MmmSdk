using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Utilities;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーから外した 1 つのページを出すウィンドウ</summary>
/// <remarks>
/// 閉じるときはページをウィンドウから外してから閉じる。
/// ページはホストが持ち続けるので、ウィンドウと一緒に壊さない。
/// ウィンドウの管理 (開く・前に出す・閉じたら元へ戻す)は <see cref="PageDetacher"/> が行う。
/// </remarks>
public sealed partial class DetachedPageWindow : Window
{
    /// <summary>ウィンドウを作る</summary>
    /// <param name="page">出すページ</param>
    /// <param name="title">項目の表示名 (タイトルバーに出す)</param>
    /// <param name="appName">アプリの名前 (タスクバーなどのウィンドウの名前に添える)</param>
    /// <param name="iconPath">アプリのアイコン (.ico)のパス</param>
    public DetachedPageWindow(Page page, string title, string appName, string iconPath)
    {
        InitializeComponent();
        Title = $"{title} - {appName}";
        TitleText.Text = title;
        this.UseCustomTitleBar(AppTitleBar, iconPath);
        ContentHost.Child = page;
        AppWindow.Closing += OnClosing;
    }

    /// <summary>閉じる前に行う非同期の処理</summary>
    /// <remarks>
    /// ページの画面の内容をメインのページへ引き継ぐ処理などを入れる。
    /// 設定してあると、閉じる操作をいったん取り消し、この処理が終わってから閉じ直す。
    /// 閉じ直すあいだの閉じる操作は無視する。
    /// 機能をオフにして閉じるときなど、引き継がずに閉じたいときは null にしてから閉じる。
    /// </remarks>
    public Func<Task>? BeforeClosingAsync { get; set; }

    /// <summary>閉じる前の処理を行わずに閉じるべきときに true を返すもの</summary>
    /// <remarks>
    /// アプリの終了のように閉じたあとにスコープを使わないとき
    /// 非同期の処理を挟むと破棄したあとのスコープを触るため
    /// </remarks>
    public Func<bool>? SkipBeforeClosing { get; set; }

    /// <summary>閉じる前の処理の進み具合</summary>
    /// <remarks>0 が始めていない、1 が処理中、2 が済んだ。</remarks>
    private int _closingStep;

    /// <summary>ページをウィンドウから外す</summary>
    /// <remarks>何度呼んでもよい</remarks>
    public void ReleasePage() => ContentHost.Child = null;

    /// <summary>閉じる操作を受けたときの処理</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じる操作の情報</param>
    /// <remarks>
    /// 閉じる前の処理があれば、それを待ってから閉じ直す。
    /// 無ければ、ページを外して閉じる。
    /// </remarks>
    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closingStep == 1)
        {
            args.Cancel = true;
            return;
        }

        if (_closingStep == 0 && BeforeClosingAsync is { } before && SkipBeforeClosing?.Invoke() != true)
        {
            args.Cancel = true;
            _closingStep = 1;
            try
            {
                await before();
            }
            finally
            {
                _closingStep = 2;
                Close();
            }
            return;
        }

        ReleasePage();
    }
}
