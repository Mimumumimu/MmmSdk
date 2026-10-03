using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MmmSdk.WinUI.Interop;

internal static partial class NativeMethods
{
    // Win32 のメニューは既定では常にライトの見た目になる。uxtheme の非公開 API（序数指定）でシステムのダーク設定に従わせる。
    // Windows 10 1903 以降で使える（エクスプローラー等も使っている）。見つからない環境では何もしない（ライトのまま）

    /// <summary>uxtheme の SetPreferredAppMode の序数</summary>
    private const int UxThemeOrdinalSetPreferredAppMode = 135;
    /// <summary>uxtheme の FlushMenuThemes の序数</summary>
    private const int UxThemeOrdinalFlushMenuThemes = 136;
    /// <summary>システムのダーク設定に従う（AllowDark）</summary>
    private const int PreferredAppModeAllowDark = 1;

    /// <summary>オーナードローの項目を追加する</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    /// <param name="flags">項目の種類を示すフラグ（<c>MF_OWNERDRAW</c> は付けなくてよい）</param>
    /// <param name="idOrSubmenu">コマンド ID、またはサブメニューのハンドル</param>
    /// <param name="itemData">描画時に受け取る値</param>
    /// <returns>成功すれば true</returns>
    /// <remarks>AppendMenu の lpNewItem は、オーナードローの項目では文字列ではなく、描画時に受け取る値になる。</remarks>
    public static unsafe bool AppendOwnerDrawMenu(HMENU menu, MENU_ITEM_FLAGS flags, nuint idOrSubmenu, nuint itemData) =>
        PInvoke.AppendMenu(menu, flags | MENU_ITEM_FLAGS.MF_OWNERDRAW, idOrSubmenu, new PCWSTR((char*)itemData));

    /// <summary>メニューをシステムのダーク／ライト設定に従わせる。</summary>
    public static unsafe void AllowDarkMenus()
    {
        var uxtheme = PInvoke.LoadLibrary("uxtheme.dll");
        if (uxtheme.IsNull) return;

        var setPreferredAppMode = (delegate* unmanaged<int, int>)GetUxThemeFunction(uxtheme, UxThemeOrdinalSetPreferredAppMode);
        if (setPreferredAppMode is not null)
        {
            setPreferredAppMode(PreferredAppModeAllowDark);
        }
        FlushMenuThemes();
    }

    /// <summary>メニューの見た目をいまのテーマで作り直させる</summary>
    /// <remarks>ダーク／ライトの切り替え時に使う。</remarks>
    public static unsafe void FlushMenuThemes()
    {
        var uxtheme = PInvoke.LoadLibrary("uxtheme.dll");
        if (uxtheme.IsNull) return;

        var flushMenuThemes = (delegate* unmanaged<void>)GetUxThemeFunction(uxtheme, UxThemeOrdinalFlushMenuThemes);
        if (flushMenuThemes is not null)
        {
            flushMenuThemes();
        }
    }

    /// <summary>uxtheme の関数のアドレスを、序数で取得する</summary>
    /// <param name="uxtheme">uxtheme.dll のモジュールのハンドル</param>
    /// <param name="ordinal">関数の序数</param>
    /// <returns>関数のアドレス。見つからなければ null</returns>
    private static unsafe void* GetUxThemeFunction(HMODULE uxtheme, int ordinal) =>
        (void*)PInvoke.GetProcAddress(uxtheme, new PCSTR((byte*)ordinal)).Value;
}
