using System.Globalization;
using System.Text;

namespace MmmSdk.Core.Components.Holidays;

/// <summary>日本の国民の祝日・休日の判定</summary>
/// <remarks>
/// 内閣府が公開している「国民の祝日」の一覧を、アプリの <c>Assets/Holidays/syukujitsu.csv</c> から読む。
/// 一覧は 1955 年から、公開されている最後の年まで。
/// ダウンロードしたままの Shift_JIS のファイルで、UTF-8 でも読める。
/// 外へは通信しない。
/// ファイルを、内閣府の最新の一覧に置き換えると、次にアプリを起動したときから、新しい年の祝日が使える。
/// ファイルが無い・読めないときと、一覧に無い年 (最後の年より先)は祝日として扱わない。
/// 出典: 内閣府「国民の祝日について」で、https://www8.cao.go.jp/chosei/shukujitsu/gaiyou.html にある。
/// 公共データ利用規約 (第 1.0 版)に従い、出典を、配布物のライセンスの一覧に書く。
/// </remarks>
public static class JapaneseHolidays
{
    /// <summary>一覧の日付の書式</summary>
    /// <remarks>例は <c>2026/1/1</c></remarks>
    private const string DateFormat = "yyyy/M/d";

    /// <summary>祝日の日付と名前</summary>
    /// <remarks>初めて使うときにファイルから読む</remarks>
    private static readonly Lazy<Dictionary<DateOnly, string>> Holidays = new(Load);

    /// <summary>日付が国民の祝日・休日か</summary>
    /// <param name="date">日付</param>
    /// <returns>
    /// 祝日・休日なら true。
    /// 振替休日・国民の休日を含み、日曜と土曜は祝日でなければ false。
    /// </returns>
    public static bool IsHoliday(DateOnly date) => Holidays.Value.ContainsKey(date);

    /// <summary>国民の祝日・休日の名前を返す</summary>
    /// <param name="date">日付</param>
    /// <returns>
    /// 祝日・休日の名前で、勤労感謝の日など。
    /// 祝日・休日でなければ null。
    /// </returns>
    public static string? GetName(DateOnly date) => Holidays.Value.GetValueOrDefault(date);

    /// <summary>ある年の国民の祝日・休日を日付の順に返す</summary>
    /// <param name="year">西暦の年</param>
    /// <returns>
    /// 日付と名前の組
    /// 一覧に無い年は空
    /// </returns>
    public static IReadOnlyList<KeyValuePair<DateOnly, string>> GetHolidays(int year)
        => [.. Holidays.Value.Where(holiday => holiday.Key.Year == year).OrderBy(holiday => holiday.Key)];

    /// <summary>一覧のファイルを読む</summary>
    /// <returns>祝日の日付と名前 (ファイルが無い・読めないときは空)</returns>
    /// <remarks>先頭の見出しの行と、日付として読めない行は飛ばす。</remarks>
    private static Dictionary<DateOnly, string> Load()
    {
        var dates = new Dictionary<DateOnly, string>();
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Holidays", "syukujitsu.csv");
        string text;
        try
        {
            text = Decode(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return dates;
        }

        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            var comma = line.IndexOf(',');
            if (comma > 0 && DateOnly.TryParseExact(line.AsSpan(0, comma), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                dates[date] = line[(comma + 1)..].Trim().Trim('"');
            }
        }
        return dates;
    }

    /// <summary>ファイルの中身を文字にする</summary>
    /// <param name="bytes">ファイルの中身</param>
    /// <returns>UTF-8 として読めればそのまま、読めなければ Shift_JIS (内閣府のファイルの形式)として読んだ文字。</returns>
    private static string Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(932).GetString(bytes);
        }
    }
}
