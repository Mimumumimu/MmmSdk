using Microsoft.Windows.Storage.Pickers;

namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>フォルダ選択を開く</summary>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
public sealed class FolderPickerService(IDialogHost dialogs) : IFolderPickerService
{
    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string? startDirectory = null)
    {
        // Windows App SDK のピッカーは、アンパッケージでもウィンドウ ID を渡すだけで使える
        var picker = new FolderPicker(dialogs.Owner.AppWindow.Id);
        // ピッカーは、フルパスでない値や無いフォルダを渡すと例外になるので、先に確かめる
        // (存在の確認は、ネットワークパスで UI スレッドが止まることがあるので、バックグラウンドで行う)
        if (await Task.Run(() => FindExistingDirectory(startDirectory)) is { } start)
        {
            picker.SuggestedFolder = start;
            picker.SuggestedStartFolder = start;
        }
        var result = await picker.PickSingleFolderAsync();
        // 選ばれたフォルダは、末尾を「\」にそろえる (「D:\」と「D:\work\」で、表記が分かれないようにする)
        return result?.Path is { } path ? Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar : null;
    }

    /// <summary>入力されたパスから、実際にあるフォルダを探す</summary>
    /// <param name="path">入力されたパス</param>
    /// <returns>あるフォルダのパス。無いときは、存在する一番近い親。見つからないときは null</returns>
    /// <remarks>「D:」のようにドライブだけの入力は、ドライブの直下 (「D:\」)として扱う。</remarks>
    private static string? FindExistingDirectory(string? path)
    {
        var trimmed = path?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        if (trimmed.Length == 2 && char.IsAsciiLetter(trimmed[0]) && trimmed[1] == ':')
        {
            trimmed += '\\';
        }
        if (!Path.IsPathFullyQualified(trimmed))
        {
            return null;
        }

        for (var current = trimmed; current is not null; current = Path.GetDirectoryName(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }
        }
        return null;
    }
}
