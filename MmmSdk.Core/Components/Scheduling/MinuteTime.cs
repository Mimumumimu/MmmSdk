namespace MmmSdk.Core.Components.Scheduling;

/// <summary>分単位の時刻の計算</summary>
public static class MinuteTime
{
    /// <summary>秒以下を切り捨てる</summary>
    /// <param name="value">日時</param>
    /// <returns>秒以下を 0 にした日時</returns>
    public static DateTime TruncateToMinute(DateTime value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);

    /// <summary>秒以下を切り捨てる</summary>
    /// <param name="value">日時</param>
    /// <returns>
    /// 秒以下を 0 にした日時
    /// 時差は保つ
    /// </returns>
    public static DateTimeOffset TruncateToMinute(DateTimeOffset value)
        => new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Offset);
}
