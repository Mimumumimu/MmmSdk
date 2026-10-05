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

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PickFilesAsync()
    {
        var picker = new FileOpenPicker(dialogs.Owner.AppWindow.Id);
        picker.FileTypeFilter.Add("*");
        var results = await picker.PickMultipleFilesAsync();
        return results.Select(result => result.Path).ToList();
    }

    /// <inheritdoc />
    public async Task<string?> PickSaveFileAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker(dialogs.Owner.AppWindow.Id)
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
        };

        // 保存の種類は、元のファイルの拡張子 1 つだけにする (拡張子が無いファイルは "." で、拡張子なしを表す)
        var extension = Path.GetExtension(suggestedFileName);
        picker.FileTypeChoices.Add(
            extension.Length > 0 ? $"{extension.TrimStart('.').ToUpperInvariant()} ファイル" : "ファイル",
            [extension.Length > 0 ? extension : "."]);

        var result = await picker.PickSaveFileAsync();
        return result?.Path;
    }
}
