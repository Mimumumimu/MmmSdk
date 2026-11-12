namespace MmmSdk.WinUI.Components.Pages;

/// <summary>ウィンドウ全体のマウスカーソルを処理待ちの形にする口</summary>
/// <remarks>
/// 処理待ちの形は砂時計。
/// サイドバーを押した直後はマウスがページの外 (サイドバーの上)にあるので、ページだけでカーソルを変えても見えない。
/// そのため、サイドバーとページを載せる <see cref="NavigationHost"/> がこの口の状態に合わせてカーソルを変える。
/// UI スレッドで使う。
/// </remarks>
public interface IBusyCursor
{
    /// <summary>処理待ちの形にしているか</summary>
    /// <remarks><see cref="Begin"/> の呼び出しが 1 つでも続いているかを表す</remarks>
    bool IsBusy { get; }

    /// <summary>処理待ちの状態が変わった</summary>
    event EventHandler? Changed;

    /// <summary>処理待ちの形にし始める</summary>
    /// <returns>
    /// 破棄すると、この分の処理待ちを終える入れ物。
    /// 複数が重なったときはすべて終わるまで続き、二重に破棄しても、1 回だけ数える。
    /// </returns>
    IDisposable Begin();

    /// <summary>処理待ちの形にして、砂時計が描かれるのを待ってから、処理を行い、処理の結果が画面に描かれたら元に戻す</summary>
    /// <param name="work">行う処理 (UI スレッドで動く)</param>
    /// <returns>処理の完了を表すタスク</returns>
    /// <remarks>
    /// UI スレッドで長く動く処理は動いている間、カーソルが変わらない。
    /// 先に砂時計を描かせるため、処理の前に、描画を 1 回待つ。
    /// UI スレッドから呼ぶ。
    /// </remarks>
    Task RunAsync(Action work);

    /// <summary>処理待ちの形にして、砂時計が描かれるのを待ってから、非同期の処理を行い、処理の結果が画面に描かれたら元に戻す</summary>
    /// <param name="work">行う処理 (UI スレッドから始まる)</param>
    /// <returns>処理の完了を表すタスク</returns>
    Task RunAsync(Func<Task> work);
}
