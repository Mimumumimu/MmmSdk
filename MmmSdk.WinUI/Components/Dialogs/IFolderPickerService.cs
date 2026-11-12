namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>フォルダ選択</summary>
/// <remarks>ViewModel から UI 型に触れずに使うための口</remarks>
public interface IFolderPickerService
{
    /// <summary>フォルダ選択を開く</summary>
    /// <param name="startDirectory">
    /// 最初に開くフォルダのパス
    /// null・空・存在しないときは既定の場所
    /// </param>
    /// <returns>
    /// 選ばれたフォルダのパスで、末尾は <c>\</c>。
    /// キャンセルなら null。
    /// </returns>
    Task<string?> PickFolderAsync(string? startDirectory = null);
}
