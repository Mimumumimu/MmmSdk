using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MmmSdk.WinUI.Interop;
using MmmSdk.WinUI.Utilities;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace MmmSdk.WinUI.Components.Windowing;

/// <summary>ウィンドウを親の上に擬似モーダルで出す</summary>
/// <remarks>
/// 表示中は親を操作できないようにし (Win32 のモーダルダイアログと同じく <c>EnableWindow</c> で親を無効にする。<c>OverlappedPresenter.IsModal</c> では親を操作できてしまった)、
/// 閉じるときに親を戻して前面に出す。モーダルの上にさらにモーダルを重ねてもよい (親が自分を無効にし、閉じたら戻す)。
/// </remarks>
public sealed class PseudoModal
{
    /// <summary>対象のウィンドウ</summary>
    private readonly Window _window;

    /// <summary>親ウィンドウのハンドル。表示するまでは 0</summary>
    private nint _owner;

    /// <summary>親ウィンドウ。表示するまでは null</summary>
    private AppWindow? _ownerWindow;

    /// <summary>対象のウィンドウに付ける</summary>
    /// <param name="window">擬似モーダルにするウィンドウ</param>
    public PseudoModal(Window window)
    {
        _window = window;
        // × / Alt+F4 で閉じるとき
        _window.AppWindow.Closing += (_, _) => EnableOwner();
        _window.Closed += OnClosed;
    }

    /// <summary>親ウィンドウの表示倍率</summary>
    public double OwnerScale { get; private set; } = 1.0;

    /// <summary>親を設定する (表示の前に呼ぶ)</summary>
    /// <param name="owner">親のウィンドウ</param>
    /// <remarks>常に親の手前に出るようにする。表示倍率も親に合わせて取っておく (初期サイズを決めるため)。</remarks>
    public void SetOwner(Window owner)
    {
        _owner = Win32Interop.GetWindowFromWindowId(owner.AppWindow.Id);
        _ownerWindow = owner.AppWindow;
        OwnerScale = owner.Content?.XamlRoot?.RasterizationScale ?? 1.0;
        NativeMethods.SetOwner(Win32Interop.GetWindowFromWindowId(_window.AppWindow.Id), _owner);
    }

    /// <summary>表示して、親を操作できないようにする</summary>
    public void Show()
    {
        _window.Activate();
        if (_owner != 0)
        {
            PInvoke.EnableWindow((HWND)_owner, false);
        }
    }

    /// <summary>親のいるモニターの作業領域に収まる、ウィンドウの高さの上限</summary>
    /// <param name="marginDip">上下に空ける余白の合計 (DIP)</param>
    /// <returns>DIP の上限の高さ。親を設定する前は <see cref="double.PositiveInfinity"/></returns>
    /// <remarks>
    /// 位置を内側へ寄せても、ウィンドウが作業領域より高いと下が切れる。
    /// 大きさを決める前にこの値で高さを抑えて、画面の小さいモニターや高い表示倍率でも全体が見えるようにする。
    /// </remarks>
    public double GetAvailableHeightDip(double marginDip = 16)
    {
        if (_ownerWindow is null)
        {
            return double.PositiveInfinity;
        }

        var bounds = new RectInt32(_ownerWindow.Position.X, _ownerWindow.Position.Y, _ownerWindow.Size.Width, _ownerWindow.Size.Height);
        var workArea = DisplayArea.GetFromRect(bounds, DisplayAreaFallback.Nearest).WorkArea;
        return workArea.Height / OwnerScale - marginDip;
    }

    /// <summary>親の中央に置く (作業領域からはみ出す分は内側へ寄せる)</summary>
    public void CenterOnOwner()
    {
        if (_ownerWindow is null)
        {
            return;
        }

        var position = _ownerWindow.Position;
        var size = _ownerWindow.Size;
        _window.MoveCentered(new RectInt32(position.X, position.Y, size.Width, size.Height));
    }

    /// <summary>コードから閉じる (OK・キャンセル等)</summary>
    /// <remarks><c>Window.Close()</c> では <c>AppWindow.Closing</c> が来ないので、閉じる前にここで親を戻す。</remarks>
    public void Close()
    {
        EnableOwner();
        _window.Close();
    }

    /// <summary>閉じたら親を戻して前面に出す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        EnableOwner();
        if (_owner != 0)
        {
            PInvoke.SetForegroundWindow((HWND)_owner);
        }
    }

    /// <summary>親を操作できる状態に戻す</summary>
    /// <remarks>
    /// 自分が消える前に戻す (無効のままだと、閉じたあとに親ではなく別のアプリが前面に来るため)。
    /// 閉じ方 (コードから・×・Alt+F4)によって通る場所が違うので、何度呼んでもよい。
    /// </remarks>
    private void EnableOwner()
    {
        if (_owner != 0)
        {
            PInvoke.EnableWindow((HWND)_owner, true);
        }
    }
}
