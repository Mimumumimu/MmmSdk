using Windows.ApplicationModel.DataTransfer;

namespace MmmSdk.WinUI.Components.Clipboards;

/// <summary>クリップボードのテキストを読み書きする</summary>
public sealed class ClipboardService : IClipboardService
{
    /// <inheritdoc />
    public void SetText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    /// <inheritdoc />
    public async Task<string?> GetTextAsync()
    {
        var content = Clipboard.GetContent();
        return content.Contains(StandardDataFormats.Text) ? await content.GetTextAsync() : null;
    }
}
