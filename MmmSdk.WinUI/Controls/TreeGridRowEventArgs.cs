namespace MmmSdk.WinUI.Controls;

/// <summary><see cref="TreeGridView"/> の、行に関するイベントの情報</summary>
/// <param name="row">対象の行</param>
public sealed class TreeGridRowEventArgs(ITreeGridRow row) : EventArgs
{
    /// <summary>対象の行</summary>
    public ITreeGridRow Row { get; } = row;
}
