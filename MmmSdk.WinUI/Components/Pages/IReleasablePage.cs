namespace MmmSdk.WinUI.Components.Pages;

/// <summary>機能をオフにしてページを捨てるときに画面の後始末が要るページ</summary>
/// <remarks>
/// ホストがオフにした機能のページをキャッシュから外すとき、ページを作ったスコープを破棄する前に呼ぶ。
/// <c>IDisposable</c> のサービス (ターミナルのセッションなど)の解放はスコープの破棄が行うので、ここには画面の部品に触れる後始末だけを書く。
/// <c>IDisposable</c> にしないのは DI がアプリの終了時にも <c>Dispose</c> を呼ぶためで、そのとき画面の部品に触れない。
/// ページを別のウィンドウへ作り直すときに、スコープを捨てずに画面だけを閉じるページは <see cref="IHandoverPage"/> を使う。
/// </remarks>
public interface IReleasablePage
{
    /// <summary>画面の部品の後始末をする</summary>
    /// <remarks>ターミナルのコントロールからセッションを外すなど</remarks>
    void Release();
}
