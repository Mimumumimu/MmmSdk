using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace MmmSdk.WinUI.Controls;

/// <summary><see cref="TreeGridView"/> の列の定義</summary>
/// <remarks>
/// 列を変えたら (表示・非表示など)、<see cref="TreeGridView.RefreshColumns"/> を呼ぶ。
/// 固定の列 (<see cref="IsFrozen"/>)は、表の左に置き、横にスクロールしても動かない。固定の列は、固定でない列より前に並べる。
/// </remarks>
public sealed class TreeGridColumn
{
    /// <summary>列を区別するキー (<see cref="TreeGridView.FocusCell(ITreeGridRow, string)"/> で使う)</summary>
    public string Key { get; set; } = "";

    /// <summary>見出し</summary>
    public string Header { get; set; } = "";

    /// <summary>列の幅</summary>
    public double Width { get; set; } = 100;

    /// <summary>見出しに文字ではなく出す部品を作る関数 (null なら、<see cref="Header"/> の文字を出す)</summary>
    /// <remarks>
    /// 見出しを作り直すたび (<see cref="TreeGridView.RefreshColumns"/>・<see cref="TreeGridView.RefreshColumnWidths"/>)に、新しい部品を作って渡す
    /// (同じ部品を、前の見出しから外さずに、別の見出しへ入れられないため)。日付の軸のように、列の幅の中に自分で描く見出しに使う。
    /// 見出しの高さは、表全体で 1 つ (<see cref="TreeGridView.HeaderHeight"/>)。
    /// </remarks>
    public Func<UIElement>? HeaderFactory { get; set; }

    /// <summary>見出しに触れたときに出す説明 (null なら出さない。見出しを短くするために使う)</summary>
    public string? HeaderToolTip { get; set; }

    /// <summary>見出しの右クリックのメニュー</summary>
    /// <remarks>その列だけの操作に使う。null なら、右クリックしても何も出さない。</remarks>
    public FlyoutBase? HeaderContextFlyout { get; set; }

    /// <summary>見出しの下に引く線の色 (null なら線なし。列のまとまりを示す)</summary>
    public Brush? HeaderUnderline { get; set; }

    /// <summary>横にスクロールしても動かない列か</summary>
    public bool IsFrozen { get; set; }

    /// <summary>木構造の列か (字下げと開閉ボタンを付ける。表に 1 つだけ)</summary>
    public bool IsTree { get; set; }

    /// <summary>表示するか (false でも、値は消えない)</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>見出しの背景 (指定しなければ、表の見出しの既定)</summary>
    public Brush? HeaderBackground { get; set; }

    /// <summary>セルの見た目 (行の値が、データとして渡る)</summary>
    public DataTemplate? CellTemplate { get; set; }

    /// <summary>集計の行でも、このセルの背景に集計の行の色と行の色 (<see cref="ITreeGridRow.RowTint"/>)を付けず、表の面の色のままにするか</summary>
    /// <remarks>
    /// 集計の行でも入力できるセルを、入力できる行のセルと同じ見た目にするために使う。スクロールする列だけで使える (固定の列は、固定の部分の背景が不透明なため)。
    /// 使う側が <see cref="TreeGridView.SummaryBackground"/> を渡しているときだけ働く。
    /// </remarks>
    public bool KeepsBaseBackground { get; set; }

    /// <summary>セルがフォーカスを受けるか (既定は true)</summary>
    /// <remarks>
    /// false の列は、キーボードのフォーカス・「今のセル」の枠・Tab や矢印キーの移動の対象にならず、押してもフォーカスを移さない。
    /// 日付の軸のような、表示だけで、幅が表の表示領域より広い列に使う (フォーカスが来ると、その列の左端までスクロールが動いてしまうため)。
    /// </remarks>
    public bool IsFocusable { get; set; } = true;

    /// <summary>クリックされたとき、入力欄に切り替えず、<see cref="TreeGridView.CellInvoked"/> で知らせる列か</summary>
    /// <remarks>メニューやカレンダーなど、使う側が、セルの下に選択の入れ物を出す列に使う (その列は <see cref="EditTemplate"/> を持たない)。</remarks>
    public bool IsInvokable { get; set; }

    /// <summary>クリックで入力できる列か (入力用の見た目か、<see cref="IsInvokable"/> のどちらかがある)</summary>
    public bool IsEditable => EditTemplate is not null || IsInvokable;

    /// <summary>入力するときのセルの見た目 (null なら、入力できない列)</summary>
    /// <remarks>
    /// 左クリック・キーボードでセルに移ったとき・<see cref="TreeGridView.FocusCell(ITreeGridRow, string)"/> で、<see cref="CellTemplate"/> からこちらに切り替わり、入力欄にフォーカスが入る。
    /// 右クリックでは切り替わらない (メニューだけを出すため)。入力が終わったら、使う側が <see cref="TreeGridView.EndEdit"/> を呼ぶか、フォーカスが外れると、表示用に戻る。
    /// </remarks>
    public DataTemplate? EditTemplate { get; set; }
}
