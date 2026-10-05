using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Paths;

namespace MmmSdk.WinUI.Components.Notifications;

/// <summary>通知ウィンドウの ViewModel</summary>
/// <param name="opener">リンク先を開く処理</param>
/// <remarks>本文 (項目の一覧)は、リンクのクリックを含む Inlines として View 側で組み立てるので、ここには持たない (バインドされず、使われないため)。</remarks>
public sealed partial class NotificationWindowViewModel(IPathOpener opener) : ObservableObject
{
    /// <summary>タイトル</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "";

    /// <summary>リンク先を開く</summary>
    /// <param name="path">開くリンク先のパス</param>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>開けなかったときは何も表示しない (通知ダイアログに失敗の表示は持たせない方針のため)。</remarks>
    [RelayCommand]
    private async Task OpenLinkAsync(string path)
    {
        try
        {
            await opener.OpenAsync(path);
        }
        catch (PathOpenException)
        {
        }
    }
}
