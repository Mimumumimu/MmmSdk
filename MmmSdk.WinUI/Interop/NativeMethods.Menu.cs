using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MmmSdk.WinUI.Interop;

internal static partial class NativeMethods
{
    /// <summary>uxtheme の SetPreferredAppMode の序数</summary>
    /// <remarks>
    /// Win32 のメニューは既定では常にライトの見た目になる。uxtheme の非公開 API（序数指定）でシステムのダーク設定に従わせる。
    /// Windows 10 1903 以降で使える（エクスプローラー等も使っている）。使えない環境（1903 より前・見つからない）では何もしない（ライトのまま）。
    /// </remarks>
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

    /// <summary>SetPreferredAppMode が使える最小のビルド番号（Windows 10 1903）</summary>
    /// <remarks>序数 135 は、1809（17763）では別の関数（AllowDarkModeForApp）なので、1903 以降でだけ呼ぶ。</remarks>
    private const int MinBuildForPreferredAppMode = 18362;

    /// <summary>SetPreferredAppMode のアドレス。使えない環境では null</summary>
    private static readonly unsafe delegate* unmanaged<int, int> SetPreferredAppMode =
        (delegate* unmanaged<int, int>)GetUxThemeFunction(UxThemeOrdinalSetPreferredAppMode);

    /// <summary>FlushMenuThemes のアドレス。使えない環境では null</summary>
    private static readonly unsafe delegate* unmanaged<void> FlushMenuThemesFunction =
        (delegate* unmanaged<void>)GetUxThemeFunction(UxThemeOrdinalFlushMenuThemes);

    /// <summary>メニューをシステムのダーク／ライト設定に従わせる。</summary>
    public static unsafe void AllowDarkMenus()
    {
        if (SetPreferredAppMode is not null)
        {
            SetPreferredAppMode(PreferredAppModeAllowDark);
        }
        FlushMenuThemes();
    }

    /// <summary>メニューの見た目をいまのテーマで作り直させる</summary>
    /// <remarks>ダーク／ライトの切り替え時に使う。</remarks>
    public static unsafe void FlushMenuThemes()
    {
        if (FlushMenuThemesFunction is not null)
        {
            FlushMenuThemesFunction();
        }
    }

    /// <summary>uxtheme の関数のアドレスを、序数で取得する</summary>
    /// <param name="ordinal">関数の序数</param>
    /// <returns>関数のアドレス。見つからない、または序数の意味が違う可能性のある古い Windows なら null</returns>
    /// <remarks>
    /// uxtheme.dll は、ウィンドウを持つプロセスがすでに読み込んでいるので、新しく読み込まずに、読み込み済みのものを取る
    /// （パスなしの LoadLibrary は、検索順で別の DLL を拾いうる）。呼び出しは型の初期化で 1 度だけ。
    /// </remarks>
    private static unsafe void* GetUxThemeFunction(int ordinal)
    {
        if (Environment.OSVersion.Version.Build < MinBuildForPreferredAppMode) return null;

        var uxtheme = PInvoke.GetModuleHandle("uxtheme.dll");
        if (uxtheme.IsNull) return null;

        return (void*)PInvoke.GetProcAddress(uxtheme, new PCSTR((byte*)ordinal)).Value;
    }
}
