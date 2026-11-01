using System.Text;

namespace MmmSdk.Core.Components.DaysOfWeek;

/// <summary>曜日フラグ (<see cref="Weekdays"/>)の変換・判定・表記</summary>
/// <remarks>
/// 曜日を 1 つも選んでいないときと、7 つ全部を選んだときは、どちらも毎日の意味で扱う。
/// 同じ意味は、同じ見え方・同じ動きにするため。
/// </remarks>
public static class WeekdaySet
{
    /// <summary>毎日を表す表記</summary>
    private const string EverydayText = "毎日";

    /// <summary>曜日フラグと表示名を、月曜から順に並べたもの (日本語の表記順)</summary>
    public static IReadOnlyList<(Weekdays Flag, string Name)> Names { get; } =
    [
        (Weekdays.Monday, "月"),
        (Weekdays.Tuesday, "火"),
        (Weekdays.Wednesday, "水"),
        (Weekdays.Thursday, "木"),
        (Weekdays.Friday, "金"),
        (Weekdays.Saturday, "土"),
        (Weekdays.Sunday, "日"),
    ];

    /// <summary>曜日を曜日フラグにする</summary>
    /// <param name="dayOfWeek">曜日</param>
    /// <returns>対応する曜日フラグ</returns>
    public static Weekdays From(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => Weekdays.Monday,
        DayOfWeek.Tuesday => Weekdays.Tuesday,
        DayOfWeek.Wednesday => Weekdays.Wednesday,
        DayOfWeek.Thursday => Weekdays.Thursday,
        DayOfWeek.Friday => Weekdays.Friday,
        DayOfWeek.Saturday => Weekdays.Saturday,
        DayOfWeek.Sunday => Weekdays.Sunday,
        _ => Weekdays.None,
    };

    /// <summary>曜日フラグに指定の曜日が含まれるか</summary>
    /// <param name="weekdays">曜日フラグ</param>
    /// <param name="dayOfWeek">調べる曜日</param>
    /// <returns>含まれていれば true</returns>
    public static bool Contains(Weekdays weekdays, DayOfWeek dayOfWeek) => (weekdays & From(dayOfWeek)) != 0;

    /// <summary>毎日の意味か</summary>
    /// <param name="weekdays">曜日フラグ</param>
    /// <returns>1 つも選んでいない、または 7 つ全部なら true</returns>
    public static bool IsEveryday(Weekdays weekdays) => weekdays == Weekdays.None || (weekdays & Weekdays.All) == Weekdays.All;

    /// <summary>指定の曜日に当たるか</summary>
    /// <param name="weekdays">曜日フラグ</param>
    /// <param name="dayOfWeek">調べる曜日</param>
    /// <returns>毎日の意味か、その曜日を含んでいれば true</returns>
    public static bool OccursOn(Weekdays weekdays, DayOfWeek dayOfWeek) => IsEveryday(weekdays) || Contains(weekdays, dayOfWeek);

    /// <summary>曜日フラグを「月火水」のような日本語の文字列にする</summary>
    /// <param name="weekdays">曜日フラグ</param>
    /// <returns>月曜から順に並べた文字列。1 つも選んでいなければ空文字</returns>
    public static string ToJapanese(Weekdays weekdays)
    {
        var builder = new StringBuilder();
        foreach (var (flag, name) in Names)
        {
            if ((weekdays & flag) != 0)
            {
                builder.Append(name);
            }
        }
        return builder.ToString();
    }

    /// <summary>曜日の指定を、画面に出す文字列にする</summary>
    /// <param name="weekdays">曜日フラグ</param>
    /// <returns>「月火水」のような文字列。毎日の意味 (<see cref="IsEveryday"/>)なら「毎日」</returns>
    public static string Describe(Weekdays weekdays) => IsEveryday(weekdays) ? EverydayText : ToJapanese(weekdays);
}
