using Microsoft.Windows.Storage.Pickers;

namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>ファイル選択を開く</summary>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
public sealed class FilePickerService(IDialogHost dialogs) : IFilePickerService
{
    /// <inheritdoc />
    public async Task<string?> PickFileAsync()
    {
        var picker = new FileOpenPicker(dialogs.Owner.AppWindow.Id);
        picker.FileTypeFilter.Add("*");
        var result = await picker.PickSingleFileAsync();
        return result?.Path;
    }
}
