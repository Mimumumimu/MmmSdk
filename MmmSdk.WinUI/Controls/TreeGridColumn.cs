using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace MmmSdk.WinUI.Controls;

/// <summary><see cref="TreeGridView"/> の列の定義</summary>
/// <remarks>
/// 列を変えたら (表示・非表示など)、<see cref="TreeGridView.RefreshColumns"/> を呼ぶ。
/// 固定の列 (<see cref="IsFrozen"/>)は、表の左に置き、横にスクロールしても動かない。固定の列は、固定でない列より前に並べる。
/// </remarks>
public sealed class TreeGridColumn
{
    /// <summary>列を区別するキー (<see cref="TreeGridView.FocusCell"/> で使う)</summary>
    public string Key { get; set; } = "";

    /// <summary>見出し</summary>
    public string Header { get; set; } = "";

    /// <summary>列の幅</summary>
    public double Width { get; set; } = 100;

    /// <summary>見出しに触れたときに出す説明 (null なら出さない。見出しを短くするために使う)</summary>
    public string? HeaderToolTip { get; set; }

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

    /// <summary>クリックされたとき、入力欄に切り替えず、<see cref="TreeGridView.CellInvoked"/> で知らせる列か</summary>
    /// <remarks>メニューやカレンダーなど、使う側が、セルの下に選択の入れ物を出す列に使う (その列は <see cref="EditTemplate"/> を持たない)。</remarks>
    public bool IsInvokable { get; set; }

    /// <summary>クリックで入力できる列か (入力用の見た目か、<see cref="IsInvokable"/> のどちらかがある)</summary>
    public bool IsEditable => EditTemplate is not null || IsInvokable;

    /// <summary>入力するときのセルの見た目 (null なら、入力できない列)</summary>
    /// <remarks>
    /// 左クリック・キーボードでセルに移ったとき・<see cref="TreeGridView.FocusCell"/> で、<see cref="CellTemplate"/> からこちらに切り替わり、入力欄にフォーカスが入る。
    /// 右クリックでは切り替わらない (メニューだけを出すため)。入力が終わったら、使う側が <see cref="TreeGridView.EndEdit"/> を呼ぶか、フォーカスが外れると、表示用に戻る。
    /// </remarks>
    public DataTemplate? EditTemplate { get; set; }
}
