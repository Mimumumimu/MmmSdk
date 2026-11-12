using System.ComponentModel;

namespace MmmSdk.WinUI.Controls;

/// <summary>表の 1 行</summary>
/// <remarks>
/// <see cref="TreeGridView"/> の 1 行。
/// 見えている行を 1 つにならした一覧の要素。
/// 開閉の状態と、開閉に合わせた行の増減は使う側が持つ。
/// 表は字下げと開閉ボタンを描き、押されたことを <see cref="TreeGridView.RowToggleRequested"/> で知らせるだけ。
/// セルは普段は表示用の見た目だけを出し、入力するときだけ、その列の入力用の見た目 (<see cref="TreeGridColumn.EditTemplate"/>)に切り替わる。
/// 値が変わったときは <see cref="INotifyPropertyChanged.PropertyChanged"/> を発火する。
/// 表が字下げと開閉ボタンを描き直す。
/// </remarks>
public interface ITreeGridRow : INotifyPropertyChanged
{
    /// <summary>字下げの段</summary>
    /// <remarks>0 が最上位</remarks>
    int Level { get; }

    /// <summary>子を持つか</summary>
    /// <remarks>開閉ボタンを出すかを決める</remarks>
    bool HasChildren { get; }

    /// <summary>開いているか</summary>
    /// <remarks>子が一覧に出ているかを表す</remarks>
    bool IsExpanded { get; }

    /// <summary>行の背景に重ねる色</summary>
    /// <remarks>
    /// null なら、重ねない。
    /// 半透明の色にすると明るい表示でも暗い表示でも使える。
    /// 固定の列・スクロールする列の両方に重ねる。
    /// </remarks>
    Windows.UI.Color? RowTint => null;

    /// <summary>集計の行か</summary>
    /// <remarks>入力欄が並ぶ行と区別して、スクロールする部分の背景を、固定の部分と同じ色にする。</remarks>
    bool IsSummary => false;

    /// <summary>このセルを入力できるか</summary>
    /// <param name="columnKey">列のキー</param>
    /// <returns>
    /// 入力できるなら true で、既定。
    /// 計算値の表示など、入力できないセルは false。
    /// </returns>
    /// <remarks><see cref="TreeGridColumn.EditTemplate"/> を持つ列だけが対象</remarks>
    bool CanEdit(string columnKey) => true;
}
