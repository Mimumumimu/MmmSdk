namespace MmmSdk.Core.Utilities;

/// <summary>待たずに走らせるタスクの扱い</summary>
public static class FireAndForgetExtensions
{
    /// <summary>タスクを待たずに走らせる</summary>
    /// <param name="task">走らせるタスク</param>
    /// <remarks>
    /// 失敗はその場ですぐに未処理例外として扱われる。
    /// 取り消し (<see cref="OperationCanceledException"/>)はエラーではないので、受けて何もしない。
    /// これは安全網の <c>FatalErrorHandler.AttachTo</c> が待たれなかったタスクの取り消しを報告しないのと同じ基準。
    /// <c>_ = SomeAsync();</c> のように捨てると、失敗が誰にも見えず、ガベージコレクションのときに初めて分かる。
    /// いつ落ちるか読めない。
    /// これは失敗した時点で、呼び出し元の同期コンテキスト (UI スレッドなど)か、なければスレッドプールで例外を投げる。
    /// アプリの受け皿 (<c>FatalErrorHandler.AttachTo</c>)がログ・ダイアログ・終了で受ける。
    /// 起きると分かっている失敗 (<c>DataFileException</c> など)はタスクの中で受けて画面に出すこと。
    /// ここへ来るのは予想外の失敗だけにする。
    /// </remarks>
    public static async void Forget(this Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // 取り消しはエラーではない (アプリの終了中・画面を閉じたときのタスクなど)
        }
    }
}
