namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>
/// アプリ全体で使うダイアログを開く（ViewModel から UI 型に触れずに使うための口）。
/// </summary>
/// <remarks>アプリ固有の画面を開く口は、アプリ側に置く。</remarks>
public interface IDialogService
{
    /// <summary>確認ダイアログを開く</summary>
    /// <param name="title">ダイアログのタイトル</param>
    /// <param name="message">確認する内容のメッセージ</param>
    /// <param name="primaryText">実行するボタンの文言（「削除」等）。</param>
    /// <param name="closeText">取りやめるボタンの文言（「キャンセル」等。アプリの言語で渡す）</param>
    /// <returns>実行するボタンが押されたら true</returns>
    Task<bool> ConfirmAsync(string title, string message, string primaryText, string closeText);
}
