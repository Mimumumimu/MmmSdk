namespace MmmSdk.WinUI.Controls;

/// <summary>行に関するイベントの情報</summary>
/// <param name="row">対象の行</param>
/// <remarks><see cref="TreeGridView"/> のイベントで渡す</remarks>
public sealed class TreeGridRowEventArgs(ITreeGridRow row) : EventArgs
{
    /// <summary>対象の行</summary>
    public ITreeGridRow Row { get; } = row;
}
