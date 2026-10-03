namespace MmmSdk.Core.Tasks;

/// <summary>
/// 待たずに走らせるタスクの扱い
/// </summary>
public static class FireAndForgetExtensions
{
    /// <summary>タスクを待たずに走らせる</summary>
    /// <param name="task">走らせるタスク</param>
    /// <remarks>
    /// 失敗は、その場ですぐに未処理例外として扱われる。
    /// <c>_ = SomeAsync();</c> のように捨てると、失敗が誰にも見えず、ガベージコレクションのときに初めて分かる（いつ落ちるか読めない）。
    /// これは、失敗した時点で、呼び出し元の同期コンテキスト（UI スレッドなど）か、なければスレッドプールで例外を投げる。
    /// アプリの受け皿（<c>FatalErrorHandler.AttachTo</c>）が、ログ・ダイアログ・終了で受ける。
    /// 起きると分かっている失敗（<c>DataFileException</c> など）は、タスクの中で受けて画面に出すこと。ここへ来るのは、予想外の失敗だけにする。
    /// </remarks>
    public static async void Forget(this Task task) => await task;
}
