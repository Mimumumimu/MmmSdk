using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace MmmSdk.WinUI.Interop;

/// <summary>仮想デスクトップ (Win+Tab で切り替えるデスクトップ)の補助</summary>
internal static partial class NativeMethods
{
    /// <summary>ウィンドウを、いま見ている仮想デスクトップへ移す</summary>
    /// <param name="hWnd">自分のアプリのウィンドウのハンドル</param>
    /// <remarks>
    /// すでに同じデスクトップにあるとき、いま見ているデスクトップが分からないときは、何もしない。
    /// 他のアプリのウィンドウは移せない (Windows の制限。移せるのは呼び出したアプリ自身のウィンドウだけ)。UI スレッド (STA)から呼ぶ。
    /// </remarks>
    public static unsafe void MoveToCurrentVirtualDesktop(nint hWnd)
    {
        IVirtualDesktopManager* manager;
        var clsid = typeof(VirtualDesktopManager).GUID;
        if (PInvoke.CoCreateInstance(in clsid, null, CLSCTX.CLSCTX_ALL, out manager).Failed)
        {
            return;
        }

        try
        {
            var window = (HWND)hWnd;
            BOOL onCurrent;
            manager->IsWindowOnCurrentVirtualDesktop(window, &onCurrent);
            if (onCurrent)
            {
                return;
            }

            var desktopId = GetCurrentVirtualDesktopId(manager);
            if (desktopId != Guid.Empty)
            {
                manager->MoveWindowToDesktop(window, in desktopId);
            }
        }
        catch (COMException)
        {
            // 調べられない・移せないときは、移さずに、いまの動きのまま前面にする
        }
        finally
        {
            manager->Release();
        }
    }

    /// <summary>いま見ているデスクトップの ID を調べる</summary>
    /// <param name="manager">仮想デスクトップの管理 (COM)</param>
    /// <returns>デスクトップの ID。調べられなければ <see cref="Guid.Empty"/></returns>
    /// <remarks>
    /// Windows が保存している「いまのデスクトップ」(エクスプローラーが、デスクトップを切り替えるたびに書く値)を読む。<c>IVirtualDesktopManager</c> には、いまのデスクトップを返す口が無い。
    /// 読めないときは、前面のウィンドウが属するデスクトップを使う。ただし、トレイのメニューの直後は、前面が非表示の内部ウィンドウで、調べられない。
    /// </remarks>
    private static unsafe Guid GetCurrentVirtualDesktopId(IVirtualDesktopManager* manager)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops"))
        {
            if (key?.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } bytes)
            {
                var id = new Guid(bytes);
                if (id != Guid.Empty)
                {
                    return id;
                }
            }
        }

        var foreground = PInvoke.GetForegroundWindow();
        if (!foreground.IsNull)
        {
            try
            {
                Guid id;
                manager->GetWindowDesktopId(foreground, &id);
                return id;
            }
            catch (COMException)
            {
                // 前面のウィンドウがデスクトップに属さない (シェルの特殊なウィンドウなど)ときは、調べられなかったことにする
            }
        }

        return Guid.Empty;
    }
}
