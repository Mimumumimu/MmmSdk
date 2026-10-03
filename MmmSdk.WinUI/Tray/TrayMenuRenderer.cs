using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;
using static MmmSdk.WinUI.Interop.NativeMethods;

namespace MmmSdk.WinUI.Tray;

/// <summary>
/// トレイメニューの項目を自分で描く（オーナードロー）。
/// </summary>
/// <remarks>
/// Win32 の標準メニューは文字のフォント・大きさ・色をアプリから変えられないため、項目の大きさの計測と描画を引き受ける。
/// 枠（外周・影）は Windows が描く（ダーク／ライトは uxtheme で合わせてある）。
/// メニューを開くたびに作り、閉じたら破棄する（その時点の DPI・テーマで描くため）。
/// </remarks>
internal sealed unsafe class TrayMenuRenderer : IDisposable
{
    /// <summary>文字のフォント</summary>
    /// <remarks>先頭から順に、入っているものを使う。</remarks>
    private static readonly string[] FontFaces = ["BIZ UDGothic", "Yu Gothic UI", "Segoe UI"];

    /// <summary>文字の大きさ（pt）</summary>
    private const int FontSizePoint = 12;

    /// <summary>サブメニューの矢印に使うアイコンフォント</summary>
    /// <remarks>先頭から順に、入っているものを使う。</remarks>
    private static readonly string[] IconFontFaces = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

    /// <summary>右向きの山形（ChevronRight）。</summary>
    private const string ChevronGlyph = "";

    #region 寸法（96 DPI のときの px）

    /// <summary>文字の左の余白</summary>
    private const int PaddingLeft = 36;
    /// <summary>文字の右の余白（サブメニューの矢印が無いとき）</summary>
    private const int PaddingRight = 24;
    /// <summary>サブメニューの矢印の領域の幅</summary>
    private const int ArrowArea = 32;
    /// <summary>行の上下の余白</summary>
    private const int PaddingVertical = 4;
    /// <summary>メニューの最小の幅</summary>
    private const int MinWidth = 140;
    /// <summary>区切り線の行の高さ</summary>
    private const int SeparatorHeight = 7;
    /// <summary>区切り線の左右の余白</summary>
    private const int SeparatorInset = 8;
    /// <summary>選択中の背景の左右の余白</summary>
    private const int HoverInsetX = 4;
    /// <summary>選択中の背景の上下の余白</summary>
    private const int HoverInsetY = 2;
    /// <summary>選択中の背景の角の丸み</summary>
    private const int HoverRadius = 8;
    /// <summary>サブメニューの矢印の大きさ</summary>
    private const int ArrowSize = 10;

    #endregion

    /// <summary>項目ごとの描画内容</summary>
    /// <remarks>itemData には「添字 + 1」を入れる（0 は未設定と区別するため）。</remarks>
    private readonly List<Entry> _entries = [];

    /// <summary>DPI の倍率（96 DPI を 1.0 とする）</summary>
    private readonly double _scale;
    /// <summary>配色</summary>
    private readonly Palette _palette;
    /// <summary>文字のフォント</summary>
    private readonly HFONT _font;

    /// <summary>矢印用のフォント</summary>
    /// <remarks>入っていなければ null のハンドル（矢印は Windows に描かせる）。</remarks>
    private readonly HFONT _iconFont;
    /// <summary>背景のブラシ</summary>
    private readonly HBRUSH _backgroundBrush;
    /// <summary>選択中の項目の背景のブラシ</summary>
    private readonly HBRUSH _hoverBrush;
    /// <summary>区切り線のブラシ</summary>
    private readonly HBRUSH _separatorBrush;
    /// <summary>1 行の文字の高さ</summary>
    private readonly int _lineHeight;

    /// <summary>指定位置のモニターの DPI と現在のテーマで描く準備をする</summary>
    /// <param name="x">メニューを出す位置の X（この位置のモニターの DPI で描く）。</param>
    /// <param name="y">メニューを出す位置の Y。</param>
    public TrayMenuRenderer(int x, int y)
    {
        var monitor = PInvoke.MonitorFromPoint(new System.Drawing.Point(x, y), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        var dpi = PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _).Succeeded ? dpiX : PInvoke.GetDpiForSystem();
        _scale = dpi / 96.0;

        _palette = IsDarkMode() ? Palette.Dark : Palette.Light;
        _backgroundBrush = PInvoke.CreateSolidBrush(new COLORREF(_palette.Background));
        _hoverBrush = PInvoke.CreateSolidBrush(new COLORREF(_palette.Hover));
        _separatorBrush = PInvoke.CreateSolidBrush(new COLORREF(_palette.Separator));

        _font = CreateFont(FindInstalledFont(FontFaces) ?? FontFaces[^1], -(int)Math.Round(FontSizePoint * dpi / 72.0));
        _iconFont = FindInstalledFont(IconFontFaces) is { } iconFace ? CreateFont(iconFace, -Px(ArrowSize)) : HFONT.Null;
        _lineHeight = MeasureText("Ag").Height;
    }

    #region 項目の追加

    /// <summary>コマンドの項目を追加する。</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    /// <param name="id">コマンド ID（押せない項目は 0）</param>
    /// <param name="text">表示する文字</param>
    /// <param name="isEnabled">押せるか</param>
    public void AppendCommand(HMENU menu, int id, string text, bool isEnabled)
        => Append(menu, MENU_ITEM_FLAGS.MF_STRING | (isEnabled ? 0 : MENU_ITEM_FLAGS.MF_GRAYED), (nuint)id, new Entry(text, IsSeparator: false, HasSubmenu: false));

    /// <summary>サブメニューを開く項目を追加する。</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    /// <param name="submenu">開くサブメニューのハンドル</param>
    /// <param name="text">表示する文字</param>
    /// <param name="isEnabled">押せるか</param>
    public void AppendSubmenu(HMENU menu, HMENU submenu, string text, bool isEnabled)
        => Append(menu, MENU_ITEM_FLAGS.MF_POPUP | (isEnabled ? 0 : MENU_ITEM_FLAGS.MF_GRAYED), (nuint)(nint)submenu, new Entry(text, IsSeparator: false, HasSubmenu: true));

    /// <summary>区切り線を追加する</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    public void AppendSeparator(HMENU menu)
        => Append(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, new Entry("", IsSeparator: true, HasSubmenu: false));

    /// <summary>項目を追加して、描画内容を覚えておく</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    /// <param name="flags">項目の種類を示すフラグ（<c>MF_*</c>）</param>
    /// <param name="idOrSubmenu">コマンド ID、またはサブメニューのハンドル</param>
    /// <param name="entry">項目の描画内容</param>
    private void Append(HMENU menu, MENU_ITEM_FLAGS flags, nuint idOrSubmenu, Entry entry)
    {
        _entries.Add(entry);
        AppendOwnerDrawMenu(menu, flags, idOrSubmenu, (nuint)_entries.Count);
    }

    /// <summary>メニューの背景を合わせ、チェックマーク用の左の余白を無くす</summary>
    /// <param name="menu">対象のメニューのハンドル</param>
    /// <remarks>サブメニューを含むメニュー全体が対象。 サブメニューにも反映させるため、項目をすべて追加したあとに呼ぶ。</remarks>
    public void ApplyTo(HMENU menu)
    {
        var info = new MENUINFO
        {
            cbSize = (uint)sizeof(MENUINFO),
            fMask = MENUINFO_MASK.MIM_BACKGROUND | MENUINFO_MASK.MIM_STYLE | MENUINFO_MASK.MIM_APPLYTOSUBMENUS,
            dwStyle = MENUINFO_STYLE.MNS_NOCHECK,
            hbrBack = _backgroundBrush,
        };
        PInvoke.SetMenuInfo(menu, in info);
    }

    #endregion

    #region 計測・描画（WM_MEASUREITEM / WM_DRAWITEM）

    /// <summary>項目の大きさを計測する</summary>
    /// <param name="item">計測する項目の情報。大きさを書き込む</param>
    public void Measure(MEASUREITEMSTRUCT* item)
    {
        if (GetEntry(item->itemData) is not { } entry) return;

        if (entry.IsSeparator)
        {
            item->itemWidth = 0;
            item->itemHeight = (uint)Px(SeparatorHeight);
            return;
        }

        var width = Px(PaddingLeft) + MeasureText(entry.Text).Width + Px(entry.HasSubmenu ? ArrowArea : PaddingRight);
        item->itemWidth = (uint)Math.Max(width, Px(MinWidth));
        item->itemHeight = (uint)(_lineHeight + Px(PaddingVertical) * 2);
    }

    /// <summary>項目を描く</summary>
    /// <param name="item">描く項目の情報（デバイスコンテキスト・範囲・状態）</param>
    public void Draw(DRAWITEMSTRUCT* item)
    {
        if (GetEntry(item->itemData) is not { } entry) return;

        var hdc = item->hDC;
        var bounds = item->rcItem;
        PInvoke.FillRect(hdc, in bounds, _backgroundBrush);

        if (entry.IsSeparator)
        {
            // 線は文字の書き出し位置から右端の手前まで
            var top = (bounds.top + bounds.bottom) / 2;
            var line = new RECT { left = bounds.left + Px(PaddingLeft), top = top, right = bounds.right - Px(SeparatorInset), bottom = top + Math.Max(1, Px(1)) };
            PInvoke.FillRect(hdc, in line, _separatorBrush);
            return;
        }

        var isDisabled = (item->itemState & (ODS_FLAGS.ODS_GRAYED | ODS_FLAGS.ODS_DISABLED)) != 0;
        if (!isDisabled && (item->itemState & ODS_FLAGS.ODS_SELECTED) != 0)
        {
            var oldBrush = PInvoke.SelectObject(hdc, _hoverBrush);
            var oldPen = PInvoke.SelectObject(hdc, PInvoke.GetStockObject(GET_STOCK_OBJECT_FLAGS.NULL_PEN));
            PInvoke.RoundRect(hdc, bounds.left + Px(HoverInsetX), bounds.top + Px(HoverInsetY), bounds.right - Px(HoverInsetX), bounds.bottom - Px(HoverInsetY), Px(HoverRadius), Px(HoverRadius));
            PInvoke.SelectObject(hdc, oldPen);
            PInvoke.SelectObject(hdc, oldBrush);
        }

        PInvoke.SetBkMode(hdc, BACKGROUND_MODE.TRANSPARENT);
        PInvoke.SetTextColor(hdc, new COLORREF(isDisabled ? _palette.DisabledText : _palette.Text));
        var oldFont = PInvoke.SelectObject(hdc, _font);

        var textBounds = bounds;
        textBounds.left += Px(PaddingLeft);
        textBounds.right -= Px(entry.HasSubmenu ? ArrowArea : PaddingRight);
        // 「&」をアクセスキーの印として扱わない（名前をそのまま出す）
        DrawText(hdc, entry.Text, ref textBounds, DRAW_TEXT_FORMAT.DT_SINGLELINE | DRAW_TEXT_FORMAT.DT_VCENTER | DRAW_TEXT_FORMAT.DT_NOPREFIX | DRAW_TEXT_FORMAT.DT_END_ELLIPSIS);

        if (entry.HasSubmenu && !_iconFont.IsNull)
        {
            PInvoke.SelectObject(hdc, _iconFont);
            var arrowBounds = bounds;
            arrowBounds.left = bounds.right - Px(ArrowArea);
            arrowBounds.right = bounds.right - Px(HoverInsetX);
            DrawText(hdc, ChevronGlyph, ref arrowBounds, DRAW_TEXT_FORMAT.DT_SINGLELINE | DRAW_TEXT_FORMAT.DT_VCENTER | DRAW_TEXT_FORMAT.DT_CENTER | DRAW_TEXT_FORMAT.DT_NOPREFIX);

            // Windows が後から描く標準の矢印を止める（テーマによっては背景と同じ色になって見えない）
            PInvoke.ExcludeClipRect(hdc, bounds.left, bounds.top, bounds.right, bounds.bottom);
        }

        PInvoke.SelectObject(hdc, oldFont);
    }

    /// <summary>itemData から描画内容を引く</summary>
    /// <param name="itemData">項目に持たせた値（添字 + 1）</param>
    /// <returns>描画内容。無ければ null</returns>
    private Entry? GetEntry(nuint itemData)
    {
        var index = (int)itemData - 1;
        return index >= 0 && index < _entries.Count ? _entries[index] : null;
    }

    #endregion

    #region フォント・寸法

    /// <summary>96 DPI のときの px を、実際の px にする</summary>
    /// <param name="value">96 DPI のときの px</param>
    /// <returns>実際の px</returns>
    private int Px(double value) => (int)Math.Round(value * _scale);

    /// <summary>文字の大きさを測る</summary>
    /// <param name="text">測る文字列</param>
    /// <returns>文字列の幅と高さ（px）</returns>
    private (int Width, int Height) MeasureText(string text)
    {
        var hdc = PInvoke.GetDC(HWND.Null);
        try
        {
            var oldFont = PInvoke.SelectObject(hdc, _font);
            var bounds = new RECT();
            DrawText(hdc, text, ref bounds, DRAW_TEXT_FORMAT.DT_SINGLELINE | DRAW_TEXT_FORMAT.DT_NOPREFIX | DRAW_TEXT_FORMAT.DT_CALCRECT);
            PInvoke.SelectObject(hdc, oldFont);
            return (bounds.right - bounds.left, bounds.bottom - bounds.top);
        }
        finally
        {
            PInvoke.ReleaseDC(HWND.Null, hdc);
        }
    }

    /// <summary>文字列を 1 つ描く（または大きさを測る）</summary>
    /// <param name="hdc">描画先のデバイスコンテキスト</param>
    /// <param name="text">描く文字列</param>
    /// <param name="bounds">描く範囲。<c>DT_CALCRECT</c> のときは、必要な大きさを書き込む</param>
    /// <param name="format">描き方のフラグ</param>
    private static void DrawText(HDC hdc, string text, ref RECT bounds, DRAW_TEXT_FORMAT format)
    {
        fixed (char* chars = text)
        fixed (RECT* rect = &bounds)
        {
            PInvoke.DrawText(hdc, chars, -1, rect, format);
        }
    }

    /// <summary>フォントを作る</summary>
    /// <param name="face">フォント名</param>
    /// <param name="height">文字の高さ（論理単位。負なら文字の高さ）</param>
    /// <returns>フォントのハンドル</returns>
    private static HFONT CreateFont(string face, int height)
    {
        var logFont = new LOGFONTW
        {
            lfHeight = height,
            lfWeight = 400,
            lfCharSet = FONT_CHARSET.DEFAULT_CHARSET,
            lfQuality = FONT_QUALITY.CLEARTYPE_QUALITY,
        };
        CopyToBuffer(face, logFont.lfFaceName.AsSpan());
        return PInvoke.CreateFontIndirect(in logFont);
    }

    /// <summary>候補のうち、この PC に入っている最初のフォント名。</summary>
    /// <param name="faces">フォント名の候補（優先順）</param>
    /// <returns>入っている最初のフォント名。1 つも無ければ null</returns>
    /// <remarks>無い名前を指定しても GDI は別のフォントで代用して作れてしまうため、先に有無を調べる。</remarks>
    private static string? FindInstalledFont(string[] faces)
    {
        var hdc = PInvoke.GetDC(HWND.Null);
        try
        {
            foreach (var face in faces)
            {
                var logFont = new LOGFONTW { lfCharSet = FONT_CHARSET.DEFAULT_CHARSET };
                CopyToBuffer(face, logFont.lfFaceName.AsSpan());
                var found = 0;
                PInvoke.EnumFontFamiliesEx(hdc, &logFont, &OnFontFound, (nint)(&found), 0);
                if (found != 0)
                {
                    return face;
                }
            }
            return null;
        }
        finally
        {
            PInvoke.ReleaseDC(HWND.Null, hdc);
        }
    }

    /// <summary>フォントが見つかったことを記録して、列挙を止める</summary>
    /// <param name="logFont">見つかったフォントの情報</param>
    /// <param name="textMetric">見つかったフォントの寸法</param>
    /// <param name="fontType">フォントの種類</param>
    /// <param name="found">見つかったことを書き込む先（int へのポインタ）</param>
    /// <returns>列挙を続けるなら 0 以外、止めるなら 0</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int OnFontFound(LOGFONTW* logFont, TEXTMETRICW* textMetric, uint fontType, LPARAM found)
    {
        *(int*)found.Value = 1;
        // 1 件見つかれば十分なので列挙を止める
        return 0;
    }

    #endregion

    #region 配色

    /// <summary>アプリのダークモード設定（Windows の「アプリ モード」）。</summary>
    /// <returns>ダークモードなら true。読めなければ false</returns>
    /// <remarks>メニューの枠は uxtheme がこの設定に従うので、中身も同じ設定に合わせる。</remarks>
    private static bool IsDarkMode()
    {
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    /// <summary>色（COLORREF。0x00BBGGRR）。</summary>
    /// <param name="Background">背景</param>
    /// <param name="Hover">選択中の項目の背景</param>
    /// <param name="Separator">区切り線</param>
    /// <param name="Text">文字</param>
    /// <param name="DisabledText">押せない項目の文字</param>
    private sealed record Palette(uint Background, uint Hover, uint Separator, uint Text, uint DisabledText)
    {
        /// <summary>ダークモードの配色</summary>
        public static readonly Palette Dark = new(Rgb(0x2C, 0x2C, 0x2C), Rgb(0x3D, 0x3D, 0x3D), Rgb(0x48, 0x48, 0x48), Rgb(0xFF, 0xFF, 0xFF), Rgb(0x7A, 0x7A, 0x7A));

        /// <summary>ライトモードの配色</summary>
        public static readonly Palette Light = new(Rgb(0xF9, 0xF9, 0xF9), Rgb(0xE6, 0xE6, 0xE6), Rgb(0xDC, 0xDC, 0xDC), Rgb(0x1A, 0x1A, 0x1A), Rgb(0xA0, 0xA0, 0xA0));

        /// <summary>RGB を COLORREF にする</summary>
        /// <param name="r">赤（0〜255）</param>
        /// <param name="g">緑（0〜255）</param>
        /// <param name="b">青（0〜255）</param>
        /// <returns>COLORREF の値</returns>
        private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
    }

    #endregion

    /// <summary>項目ごとの描画内容</summary>
    /// <param name="Text">表示する文字</param>
    /// <param name="IsSeparator">区切り線か</param>
    /// <param name="HasSubmenu">サブメニューを持つか</param>
    private sealed record Entry(string Text, bool IsSeparator, bool HasSubmenu);

    /// <summary>描画に使ったリソースを解放する</summary>
    /// <remarks>メニューが背景のブラシを使っているので、メニューを破棄してから呼ぶ。</remarks>
    public void Dispose()
    {
        PInvoke.DeleteObject(_font);
        if (!_iconFont.IsNull) PInvoke.DeleteObject(_iconFont);
        PInvoke.DeleteObject(_backgroundBrush);
        PInvoke.DeleteObject(_hoverBrush);
        PInvoke.DeleteObject(_separatorBrush);
    }
}
