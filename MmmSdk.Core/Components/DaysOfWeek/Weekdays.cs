namespace MmmSdk.Core.Components.DaysOfWeek;

/// <summary>曜日フラグ (月〜日のビット OR)</summary>
/// <remarks>値は、保存する数 (JSON・DB の列)と同じ。月 = 1・火 = 2・水 = 4・木 = 8・金 = 16・土 = 32・日 = 64 を足した数で、0 から 127。</remarks>
[Flags]
public enum Weekdays
{
    /// <summary>1 つも選んでいない (「毎日」と同じ意味で扱うかは、使う側が決める)</summary>
    None = 0,
    /// <summary>月曜日</summary>
    Monday = 1,
    /// <summary>火曜日</summary>
    Tuesday = 2,
    /// <summary>水曜日</summary>
    Wednesday = 4,
    /// <summary>木曜日</summary>
    Thursday = 8,
    /// <summary>金曜日</summary>
    Friday = 16,
    /// <summary>土曜日</summary>
    Saturday = 32,
    /// <summary>日曜日</summary>
    Sunday = 64,
    /// <summary>7 つ全部</summary>
    All = Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday,
}
