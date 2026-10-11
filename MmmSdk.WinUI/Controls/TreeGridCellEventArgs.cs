using Microsoft.UI.Xaml;

namespace MmmSdk.WinUI.Controls;

/// <summary><see cref="TreeGridView"/> の、セルに関するイベントの情報</summary>
/// <param name="row">対象の行</param>
/// <param name="columnKey">対象の列のキー</param>
/// <param name="cell">対象のセル (メニューなどを出す位置の基準にする)</param>
public sealed class TreeGridCellEventArgs(ITreeGridRow row, string columnKey, FrameworkElement cell) : EventArgs
{
    /// <summary>対象の行</summary>
    public ITreeGridRow Row { get; } = row;

    /// <summary>対象の列のキー</summary>
    public string ColumnKey { get; } = columnKey;

    /// <summary>対象のセル</summary>
    public FrameworkElement Cell { get; } = cell;
}
