namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>
/// アプリ全体で使うダイアログを開く (ViewModel から UI 型に触れずに使うための口)。
/// </summary>
/// <remarks>アプリ固有の画面を開く口は、アプリ側に置く。</remarks>
public interface IDialogService
{
    /// <summary>確認ダイアログを開く</summary>
    /// <param name="title">ダイアログのタイトル</param>
    /// <param name="message">確認する内容のメッセージ</param>
    /// <param name="primaryText">実行するボタンの文言 (「削除」等)。</param>
    /// <param name="closeText">取りやめるボタンの文言 (「キャンセル」等。アプリの言語で渡す)</param>
    /// <returns>実行するボタンが押されたら true</returns>
    Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText);

    /// <summary>保存先に同じ名前のファイルがあるときの扱いを聞く</summary>
    /// <param name="title">ダイアログのタイトル</param>
    /// <param name="message">状況を知らせるメッセージ (「同じ名前のファイルが 3 個あります」等)</param>
    /// <param name="replaceText">置き換える選択肢の文言</param>
    /// <param name="skipText">置き換えずに飛ばす選択肢の文言</param>
    /// <param name="closeText">取りやめるボタンの文言 (アプリの言語で渡す)</param>
    /// <param name="decideEachText">ファイルごとに決める選択肢の文言。null なら、この選択肢を出さない (1 つのファイルについて聞くとき)</param>
    /// <returns>選ばれた扱い。取りやめるボタン・Esc は <see cref="FileConflictChoice.Cancel"/></returns>
    /// <remarks>
    /// 選択肢は縦に並べたボタンで出す (Windows のファイルのコピーの確認と同じ形)。置き換えは取り消しにくいので、既定のボタンを置かない。
    /// <paramref name="decideEachText"/> を渡したときだけ <see cref="FileConflictChoice.DecideEach"/> が返りうる。
    /// </remarks>
    Task<FileConflictChoice> AskFileConflictAsync(string title, string message, string replaceText, string skipText, string closeText, string? decideEachText = null);
}
