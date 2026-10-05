namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>ファイル選択</summary>
/// <remarks>ViewModel から UI 型に触れずに使うための口。</remarks>
public interface IFilePickerService
{
    /// <summary>ファイル選択を開く</summary>
    /// <returns>選ばれたファイルのパス。キャンセルなら null</returns>
    Task<string?> PickFileAsync();

    /// <summary>複数のファイルを選べるファイル選択を開く</summary>
    /// <returns>選ばれたファイルのパス。キャンセルなら空</returns>
    Task<IReadOnlyList<string>> PickFilesAsync();

    /// <summary>保存先を選ぶ (名前を付けて保存) ダイアログを開く</summary>
    /// <param name="suggestedFileName">最初に入れておくファイル名</param>
    /// <returns>選ばれた保存先のパス。キャンセルなら null</returns>
    /// <remarks>同じ名前のファイルがあるときの上書きの確認は、ダイアログが出す。</remarks>
    Task<string?> PickSaveFileAsync(string suggestedFileName);
}
