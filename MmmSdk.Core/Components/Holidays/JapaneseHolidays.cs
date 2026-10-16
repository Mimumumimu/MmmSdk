using System.Globalization;
using System.Text;

namespace MmmSdk.Core.Components.Holidays;

/// <summary>日本の国民の祝日・休日の判定</summary>
/// <remarks>
/// 内閣府が公開している「国民の祝日」の一覧 (<c>syukujitsu.csv</c>。1955 年から、公開されている最後の年まで)を、
/// アプリの <c>Assets/Holidays/syukujitsu.csv</c> から読む (ダウンロードしたままの Shift_JIS のファイル。UTF-8 でも読める)。外へは通信しない。
/// ファイルを、内閣府の最新の一覧に置き換えると、次にアプリを起動したときから、新しい年の祝日が使える。
/// ファイルが無い・読めないときと、一覧に無い年 (最後の年より先)は、祝日として扱わない。
/// 出典: 内閣府「国民の祝日について」(https://www8.cao.go.jp/chosei/shukujitsu/gaiyou.html)。
/// 公共データ利用規約 (第 1.0 版)に従い、出典を、配布物のライセンスの一覧に書く。
/// </remarks>
public static class JapaneseHolidays
{
    /// <summary>一覧の日付の書式 (例: <c>2026/1/1</c>)</summary>
    private const string DateFormat = "yyyy/M/d";

    /// <summary>祝日の日付 (初めて使うときに、ファイルから読む)</summary>
    private static readonly Lazy<HashSet<DateOnly>> Dates = new(Load);

    /// <summary>日付が、国民の祝日・休日か</summary>
    /// <param name="date">日付</param>
    /// <returns>祝日・休日なら true (振替休日・国民の休日を含む。日曜と土曜は、祝日でなければ false)</returns>
    public static bool IsHoliday(DateOnly date) => Dates.Value.Contains(date);

    /// <summary>一覧のファイルを読む</summary>
    /// <returns>祝日の日付の集まり (ファイルが無い・読めないときは空)</returns>
    /// <remarks>先頭の見出しの行と、日付として読めない行は、飛ばす。</remarks>
    private static HashSet<DateOnly> Load()
    {
        var dates = new HashSet<DateOnly>();
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
                dates.Add(date);
            }
        }
        return dates;
    }

    /// <summary>ファイルの中身を、文字にする</summary>
    /// <param name="bytes">ファイルの中身</param>
    /// <returns>UTF-8 として読めればそのまま、読めなければ Shift_JIS (内閣府のファイルの形式)として読んだ文字</returns>
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
