using System.Text.RegularExpressions;

namespace MmmSdk.Core.Components.TextLinks;

/// <summary>
/// 文章の中の <c>http://</c>・<c>https://</c> で始まる URL を、リンクにできるものとして見つける。
/// </summary>
/// <remarks>
/// 画面ごとの表示 (HTML・WinUI の <c>Hyperlink</c>・Razor)は、見つけた位置 (<see cref="TextLink"/>)から、各画面が組み立てる。
/// 見つけ方と、開いてよい URL の判定を 1 か所に持ち、画面ごとに違う結果が出ないようにする。
/// </remarks>
public static partial class TextLinkFinder
{
    /// <summary>リンク先の最大の長さ</summary>
    private const int MaxHrefLength = 2000;

    /// <summary>文章の中の URL (<c>http://</c>・<c>https://</c>で始まり、URL に使える半角の文字が続くところまで)</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"(?<![A-Za-z0-9])https?://[A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;=%]+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    /// <summary>文章の中の、リンクにできる URL を、出てくる順に見つける</summary>
    /// <param name="text">文章</param>
    /// <returns>見つけた URL。重ならず、位置の順。無ければ空</returns>
    /// <remarks>
    /// 終わりの <c>.</c> <c>,</c> <c>)</c> などは、文の区切りとして URL に含めない。日本語など全角の文字の手前で終わる。
    /// 空白・引用符・<c>&lt;</c> <c>&gt;</c> を含むもの、長すぎるもの、ホストの無いものは、リンクにしない。
    /// </remarks>
    public static IReadOnlyList<TextLink> Find(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var links = new List<TextLink>();
        foreach (Match match in UrlRegex().Matches(text))
        {
            var url = TrimUrl(match.Value);
            if (TryNormalize(url, out var href))
            {
                links.Add(new TextLink(match.Index, url.Length, href));
            }
        }
        return links;
    }

    /// <summary>URL として開いてよいかを調べ、書き直した形にする</summary>
    /// <param name="url">URL の候補</param>
    /// <param name="href">開いてよいときの、正規化した URL</param>
    /// <returns><c>http</c> か <c>https</c> で、ホストがあり、長すぎず、危ない文字を含まないとき true</returns>
    private static bool TryNormalize(string url, out string href)
    {
        href = "";
        if (url.Length is 0 or > MaxHrefLength)
        {
            return false;
        }
        foreach (var c in url)
        {
            if (c <= ' ' || c is '"' or '<' or '>' or '\\' or '\'' or '`')
            {
                return false;
            }
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.Host.Length == 0)
        {
            return false;
        }

        href = uri.AbsoluteUri;
        return true;
    }

    /// <summary>URL の終わりの、文の区切りの文字を取り除く</summary>
    /// <param name="value">URL の候補</param>
    /// <returns>終わりの <c>.</c> <c>,</c> <c>)</c> などを除いた URL</returns>
    /// <remarks>
    /// 閉じ括弧は、対応する開き括弧が URL の中に無いときだけ除く。
    /// 「…/wiki/日本_(国)」のように、URL の一部になっている閉じ括弧は残す。
    /// </remarks>
    private static string TrimUrl(string value)
    {
        var end = value.Length;
        while (end > 0)
        {
            var last = value[end - 1];
            var removable = last switch
            {
                '.' or ',' or ';' or ':' or '!' or '?' or '\'' => true,
                ')' => Count(value, end, '(') < Count(value, end, ')'),
                ']' => Count(value, end, '[') < Count(value, end, ']'),
                _ => false,
            };
            if (!removable)
            {
                break;
            }
            end--;
        }
        return value[..end];
    }

    /// <summary>文字列の先頭から指定の長さまでに、ある文字が何個あるか数える</summary>
    /// <param name="value">文字列</param>
    /// <param name="length">数える長さ</param>
    /// <param name="target">数える文字</param>
    /// <returns>個数</returns>
    private static int Count(string value, int length, char target)
    {
        var count = 0;
        for (var i = 0; i < length; i++)
        {
            if (value[i] == target)
            {
                count++;
            }
        }
        return count;
    }
}
