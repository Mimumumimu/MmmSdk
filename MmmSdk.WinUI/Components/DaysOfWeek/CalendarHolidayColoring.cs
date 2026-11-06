using MmmSdk.Core.Components.Holidays;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmSdk.WinUI.Components.DaysOfWeek;

/// <summary>日付の選択欄 (<see cref="CalendarDatePicker"/>)のカレンダーで、土曜に青、日曜と祝日に赤の線を、日の下に付ける添付プロパティ</summary>
/// <remarks>
/// アプリの既定のスタイルで <see cref="IsEnabledProperty"/> を <see langword="true"/> にして、すべての選択欄に効かせる。
/// 色は <see cref="WeekdayColors"/> で、曜日のボタンやスケジュールの日付と同じ。
/// 日の数字は、カレンダーの内部の描画で書かれ、日ごとに文字の色を変えられない。そのため、日ごとに指定できる「密度の線」 (<see cref="CalendarViewDayItem.SetDensityColors"/>)を使う。
/// </remarks>
public static class CalendarHolidayColoring
{
    /// <summary>土日祝の色付けをするか (添付プロパティ)</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(CalendarHolidayColoring), new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>土日祝の色付けをするかを取得する</summary>
    /// <param name="element">日付の選択欄</param>
    /// <returns>色付けをするなら <see langword="true"/></returns>
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    /// <summary>土日祝の色付けをするかを設定する</summary>
    /// <param name="element">日付の選択欄</param>
    /// <param name="value">色付けをするなら <see langword="true"/></param>
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>添付プロパティが変わったら、カレンダーの日ごとのイベントを購読・解除する</summary>
    /// <param name="d">対象</param>
    /// <param name="e">変更の情報</param>
    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CalendarDatePicker picker)
        {
            return;
        }

        // スタイルの再適用などで二重に購読しないよう、先に外す
        picker.CalendarViewDayItemChanging -= OnDayItemChanging;
        if (e.NewValue is true)
        {
            picker.CalendarViewDayItemChanging += OnDayItemChanging;
        }
    }

    /// <summary>カレンダーの日が作られる・使い回されるたびに、日の下の線の色を決める</summary>
    /// <param name="sender">カレンダー</param>
    /// <param name="args">日の情報</param>
    private static void OnDayItemChanging(CalendarView sender, CalendarViewDayItemChangingEventArgs args)
    {
        // 準備が終わった段階でも、もう一度設定する。1 回目だけだと、線が丸まらず、横棒になる (準備が終わった段階を通ると、丸い線になる)
        if (args.Phase == 0)
        {
            args.RegisterUpdateCallback(OnDayItemChanging);
        }

        var item = args.Item;
        var date = DateOnly.FromDateTime(item.Date.Date);
        var theme = sender.ActualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;

        // 日の部品は使い回されるので、前の日の線が残らないよう、土日祝でない日は空にして戻す
        if (date.DayOfWeek == DayOfWeek.Sunday || JapaneseHolidays.IsHoliday(date))
        {
            item.SetDensityColors([WeekdayColors.Sunday(theme)]);
        }
        else if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            item.SetDensityColors([WeekdayColors.Saturday(theme)]);
        }
        else
        {
            item.SetDensityColors([]);
        }
    }
}
