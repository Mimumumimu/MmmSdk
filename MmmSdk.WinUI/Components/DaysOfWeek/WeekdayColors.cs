using Microsoft.UI.Xaml;
using Windows.UI;

namespace MmmSdk.WinUI.Components.DaysOfWeek;

/// <summary>土曜と日曜の文字の色 (ライト・ダークの両方の値を持つ、名前のついた色)</summary>
/// <remarks>
/// 土曜は青、日曜と祝日は赤。曜日のボタンとスケジュールの日付が、同じ色を使う。
/// どちらのテーマも、背景に対して 4.5 倍以上のコントラストにする。
/// 標準のアクセント色には連動させない。設定色によっては、区別できなくなるため。
/// </remarks>
public static class WeekdayColors
{
    /// <summary>土曜の文字の色</summary>
    /// <param name="theme">要素の実際のテーマ (<see cref="FrameworkElement.ActualTheme"/>)</param>
    /// <returns>ダークならダークの色、それ以外はライトの色</returns>
    public static Color Saturday(ElementTheme theme)
        => theme == ElementTheme.Dark ? Color.FromArgb(0xFF, 0x78, 0xAA, 0xFF) : Color.FromArgb(0xFF, 0x1E, 0x5A, 0xC8);

    /// <summary>日曜・祝日の文字の色</summary>
    /// <param name="theme">要素の実際のテーマ (<see cref="FrameworkElement.ActualTheme"/>)</param>
    /// <returns>ダークならダークの色、それ以外はライトの色</returns>
    public static Color Sunday(ElementTheme theme)
        => theme == ElementTheme.Dark ? Color.FromArgb(0xFF, 0xFF, 0x99, 0xA4) : Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C);
}
