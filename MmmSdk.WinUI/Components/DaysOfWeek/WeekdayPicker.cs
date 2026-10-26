using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.DaysOfWeek;

namespace MmmSdk.WinUI.Components.DaysOfWeek;

/// <summary>曜日の選択欄 (月〜日の 7 つのトグルボタン)</summary>
/// <remarks>
/// 1 つも選ばない状態も、7 つ全部を選んだ状態も、そのまま <see cref="Value"/> に出る。
/// 毎日の意味かどうかは、使う側が <see cref="WeekdaySet.IsEveryday"/> で決める。
/// 土は青、日は赤の文字にする。色は <see cref="WeekdayColors"/> で、選んだ状態の文字は、トグルボタンの標準の色。
/// </remarks>
public sealed partial class WeekdayPicker : UserControl
{
    /// <summary><see cref="Value"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(Weekdays), typeof(WeekdayPicker), new PropertyMetadata(Weekdays.None, OnValueChanged));

    /// <summary>ボタンの幅</summary>
    private const double ButtonWidth = 44;
    /// <summary>ボタンの間隔</summary>
    private const double ButtonSpacing = 6;

    /// <summary>曜日のボタン (月から日の順)</summary>
    private readonly List<ToggleButton> _buttons = [];

    /// <summary>選択欄を作る</summary>
    public WeekdayPicker()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ButtonSpacing };
        foreach (var (flag, name) in WeekdaySet.Names)
        {
            var button = new ToggleButton { Width = ButtonWidth, Content = name, Tag = flag };
            button.Click += OnButtonClick;
            if (flag is Weekdays.Saturday or Weekdays.Sunday)
            {
                button.Resources = CreateForegroundResources(flag);
            }
            _buttons.Add(button);
            panel.Children.Add(button);
        }

        Content = panel;
        ShowValue();
    }

    /// <summary>選んでいる曜日</summary>
    public Weekdays Value
    {
        get => (Weekdays)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>値が変わったら、ボタンの選択を合わせる</summary>
    /// <param name="sender">変更されたコントロール</param>
    /// <param name="e">変更の情報</param>
    private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((WeekdayPicker)sender).ShowValue();

    /// <summary>ボタンの選択を、<see cref="Value"/> に合わせる</summary>
    private void ShowValue()
    {
        var value = Value;
        foreach (var button in _buttons)
        {
            button.IsChecked = (value & (Weekdays)button.Tag) != 0;
        }
    }

    /// <summary>ボタンが押されたら、選ばれている曜日を <see cref="Value"/> にする</summary>
    /// <param name="sender">押されたボタン</param>
    /// <param name="e">イベントの情報</param>
    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        var value = Weekdays.None;
        foreach (var button in _buttons)
        {
            if (button.IsChecked == true)
            {
                value |= (Weekdays)button.Tag;
            }
        }
        Value = value;
    }

    /// <summary>土曜か日曜のボタンの文字の色を、ライトとダークの両方の値で持つリソースを作る</summary>
    /// <param name="flag">土曜か日曜の曜日フラグ</param>
    /// <returns>ボタンのリソース。通常・ポインターを乗せたとき・押している間の文字の色を、テーマごとに持つ</returns>
    /// <remarks>
    /// ボタンを作ったときに渡す。テンプレートが読み込まれる前なので、テーマの切り替えにも追従する。
    /// 選んだ状態の文字の色は、上書きしない。
    /// </remarks>
    private static ResourceDictionary CreateForegroundResources(Weekdays flag)
    {
        var resources = new ResourceDictionary();
        foreach (var (key, theme) in (ReadOnlySpan<(string, ElementTheme)>)[("Light", ElementTheme.Light), ("Dark", ElementTheme.Dark)])
        {
            var color = flag == Weekdays.Saturday ? WeekdayColors.Saturday(theme) : WeekdayColors.Sunday(theme);
            var brush = new SolidColorBrush(color);
            resources.ThemeDictionaries[key] = new ResourceDictionary
            {
                ["ToggleButtonForeground"] = brush,
                ["ToggleButtonForegroundPointerOver"] = brush,
                ["ToggleButtonForegroundPressed"] = brush,
            };
        }
        return resources;
    }
}
