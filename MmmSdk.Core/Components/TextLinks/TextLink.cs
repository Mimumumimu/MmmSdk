namespace MmmSdk.Core.Components.TextLinks;

/// <summary>
/// 文章の中で見つけた、リンクにできる URL 1 つ。
/// </summary>
/// <param name="Start">URL の始まりの位置 (文章の先頭からの文字数)</param>
/// <param name="Length">URL の文字数。画面に出す文字は、文章のこの範囲</param>
/// <param name="Href">開くときの URL。<c>http</c> か <c>https</c> の絶対 URL に直したもの</param>
public readonly record struct TextLink(int Start, int Length, string Href);
