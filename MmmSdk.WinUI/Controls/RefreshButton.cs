using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Controls;

/// <summary>ページの最新の内容を、今すぐ取り込む更新ボタン (アイコンだけ)</summary>
/// <remarks>
/// ページの見出しの行の右端に置く。押したときの処理は <c>Command</c> で受ける。
/// 通常のボタンの見た目のまま、アイコンとツールチップだけを決める (どのページでも同じ見た目・同じ言葉にするため)。
/// </remarks>
public sealed partial class RefreshButton : Button
{
    /// <summary>ツールチップとスクリーンリーダーに使う名前</summary>
    private const string Label = "更新";

    /// <summary>ボタンを作る</summary>
    public RefreshButton()
    {
        // 派生クラスにはスタイルが無いので、Button の既定のスタイルを使う
        DefaultStyleKey = typeof(Button);
        Content = new FontIcon { FontSize = 14, Glyph = "" };
        // 単独で置かれても、横に並ぶ文字のボタンと同じ高さにする
        MinHeight = 32;
        ToolTipService.SetToolTip(this, Label);
        AutomationProperties.SetName(this, Label);
    }
}
