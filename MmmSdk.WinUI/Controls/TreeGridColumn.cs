using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace MmmSdk.WinUI.Controls;

/// <summary>表の列の定義</summary>
/// <remarks>
/// <see cref="TreeGridView"/> の列の定義。
/// 列を変えたら、<see cref="TreeGridView.RefreshColumns"/> を呼ぶ。
/// 変えるのは表示・非表示など。
/// 固定の列 (<see cref="IsFrozen"/>)は表の左に置き、横にスクロールしても動かない。
/// 固定の列は固定でない列より前に並べる。
/// </remarks>
public sealed class TreeGridColumn
{
    /// <summary>列を区別するキー</summary>
    /// <remarks><see cref="TreeGridView.FocusCell(ITreeGridRow, string)"/> で使う</remarks>
    public string Key { get; set; } = "";

    /// <summary>見出し</summary>
    public string Header { get; set; } = "";

    /// <summary>列の幅</summary>
    public double Width { get; set; } = 100;

    /// <summary>見出しに文字ではなく出す部品を作る関数</summary>
    /// <remarks>
    /// null なら、<see cref="Header"/> の文字を出す。
    /// 見出しを作り直すたび (<see cref="TreeGridView.RefreshColumns"/>・<see cref="TreeGridView.RefreshColumnWidths"/>)に新しい部品を作って渡す。
    /// 同じ部品を、前の見出しから外さずに、別の見出しへ入れられないため。
    /// 日付の軸のように列の幅の中に自分で描く見出しに使う。
    /// 見出しの高さは表全体で 1 つで、<see cref="TreeGridView.HeaderHeight"/> で決まる。
    /// </remarks>
    public Func<UIElement>? HeaderFactory { get; set; }

    /// <summary>見出しに触れたときに出す説明</summary>
    /// <remarks>
    /// null なら出さない
    /// 見出しを短くするために使う
    /// </remarks>
    public string? HeaderToolTip { get; set; }

    /// <summary>見出しの右クリックのメニュー</summary>
    /// <remarks>
    /// その列だけの操作に使う。
    /// null なら、右クリックしても何も出さない。
    /// </remarks>
    public FlyoutBase? HeaderContextFlyout { get; set; }

    /// <summary>見出しの下に引く線の色</summary>
    /// <remarks>
    /// 列のまとまりを示す
    /// null なら線なし
    /// </remarks>
    public Brush? HeaderUnderline { get; set; }

    /// <summary>横にスクロールしても動かない列か</summary>
    public bool IsFrozen { get; set; }

    /// <summary>木構造の列か</summary>
    /// <remarks>
    /// 字下げと開閉ボタンを付ける
    /// 表に 1 つだけ
    /// </remarks>
    public bool IsTree { get; set; }

    /// <summary>表示するか</summary>
    /// <remarks>false でも値は消えない</remarks>
    public bool IsVisible { get; set; } = true;

    /// <summary>見出しの背景</summary>
    /// <remarks>指定しなければ、表の見出しの既定。</remarks>
    public Brush? HeaderBackground { get; set; }

    /// <summary>セルの見た目</summary>
    /// <remarks>行の値がデータとして渡る</remarks>
    public DataTemplate? CellTemplate { get; set; }

    /// <summary>集計の行でも表の面の色のままにするか</summary>
    /// <remarks>
    /// 集計の行でもこのセルの背景に集計の行の色と行の色を付けない
    /// 行の色は <see cref="ITreeGridRow.RowTint"/>
    /// 集計の行でも入力できるセルを入力できる行のセルと同じ見た目にするために使う
    /// スクロールする列だけで使える
    /// 固定の列は固定の部分の背景が不透明なため
    /// 使う側が <see cref="TreeGridView.SummaryBackground"/> を渡しているときだけ働く
    /// </remarks>
    public bool KeepsBaseBackground { get; set; }

    /// <summary>セルがフォーカスを受けるか</summary>
    /// <remarks>
    /// false の列はキーボードのフォーカス・「今のセル」の枠・Tab や矢印キーの移動の対象にならず、押してもフォーカスを移さない。
    /// 日付の軸のような、表示だけで、幅が表の表示領域より広い列に使う。
    /// フォーカスが来るとその列の左端までスクロールが動いてしまうため。
    /// </remarks>
    public bool IsFocusable { get; set; } = true;

    /// <summary>クリックで選ぶ列か</summary>
    /// <remarks>
    /// クリックされたとき、入力欄に切り替えず、<see cref="TreeGridView.CellInvoked"/> で知らせる。
    /// メニューやカレンダーなど、使う側がセルの下に選択の入れ物を出す列に使う。
    /// その列は <see cref="EditTemplate"/> を持たない。
    /// </remarks>
    public bool IsInvokable { get; set; }

    /// <summary>クリックで入力できる列か</summary>
    /// <remarks>入力用の見た目か、<see cref="IsInvokable"/> のどちらかがある。</remarks>
    public bool IsEditable => EditTemplate is not null || IsInvokable;

    /// <summary>入力するときのセルの見た目</summary>
    /// <remarks>
    /// null なら、入力できない列。
    /// 左クリック・キーボードでセルに移ったとき・<see cref="TreeGridView.FocusCell(ITreeGridRow, string)"/> で、<see cref="CellTemplate"/> からこちらに切り替わり、入力欄にフォーカスが入る。
    /// 右クリックでは切り替わらない。
    /// メニューだけを出すため。
    /// 入力が終わったら、使う側が <see cref="TreeGridView.EndEdit"/> を呼ぶか、フォーカスが外れると、表示用に戻る。
    /// </remarks>
    public DataTemplate? EditTemplate { get; set; }
}
