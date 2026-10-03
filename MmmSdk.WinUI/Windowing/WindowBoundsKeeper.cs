using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MmmSdk.Core.Storage;
using MmmSdk.Core.WindowPositions;
using Windows.Graphics;

namespace MmmSdk.WinUI.Windowing;

/// <summary>ウィンドウの位置と大きさを覚えて、次の起動で復元する</summary>
/// <remarks>
/// 作ったときに、保存した位置と大きさを復元する。保存が無い、または今のモニター構成で十分に見えない（画面外になる）ときは、
/// 既定の大きさ（論理サイズ。DPI に合わせる）にして、位置は Windows に任せる。
/// 位置・大きさが変わったら、動かし終わるのを待ってから（<see cref="SaveDelay"/>）保存する。
/// 最小化・最大化・非表示の間は保存しない（最小化の座標や、最大化の大きさを、復元の対象にしないため）。
/// UI スレッドで <see cref="Attach"/> する。ウィンドウに結び付いて動き、ウィンドウが閉じたら自分で後始末する（呼び出し側が持ち続けたり、破棄したりしなくてよい）。
/// </remarks>
public sealed class WindowBoundsKeeper
{
    /// <summary>復元する位置を「十分に見える」とみなす、ウィンドウ面積に対する見えている割合</summary>
    private const double VisibleRatio = 0.5;

    /// <summary>位置・大きさが変わってから保存するまでの待ち時間（動かしている間は保存しない）</summary>
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(1);

    /// <summary>対象のウィンドウ</summary>
    private readonly Window _window;

    /// <summary>位置と大きさの保存・復元</summary>
    private readonly IWindowPositionService _positions;

    /// <summary>ウィンドウを区別するキー</summary>
    private readonly string _key;

    /// <summary>保存を遅らせるタイマー</summary>
    private readonly DispatcherQueueTimer _timer;

    /// <summary>後始末を済ませたか</summary>
    private bool _disposed;

    /// <summary>位置と大きさを復元して、変更の監視を始める</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <param name="positions">位置と大きさの保存・復元</param>
    /// <param name="key">ウィンドウを区別するキー</param>
    /// <param name="defaultWidthDip">保存が無いときの幅（論理サイズ。DIP）</param>
    /// <param name="defaultHeightDip">保存が無いときの高さ（論理サイズ。DIP）</param>
    /// <remarks>戻り値は無い。ウィンドウのイベントの購読で生き続け、ウィンドウが閉じたら止まる。</remarks>
    public static void Attach(Window window, IWindowPositionService positions, string key, double defaultWidthDip, double defaultHeightDip)
        => _ = new WindowBoundsKeeper(window, positions, key, defaultWidthDip, defaultHeightDip);

    /// <summary>位置と大きさを復元して、変更の監視を始める</summary>
    /// <param name="window">対象のウィンドウ</param>
    /// <param name="positions">位置と大きさの保存・復元</param>
    /// <param name="key">ウィンドウを区別するキー</param>
    /// <param name="defaultWidthDip">保存が無いときの幅（論理サイズ。DIP）</param>
    /// <param name="defaultHeightDip">保存が無いときの高さ（論理サイズ。DIP）</param>
    private WindowBoundsKeeper(Window window, IWindowPositionService positions, string key, double defaultWidthDip, double defaultHeightDip)
    {
        _window = window;
        _positions = positions;
        _key = key;

        Restore(defaultWidthDip, defaultHeightDip);

        _timer = window.DispatcherQueue.CreateTimer();
        _timer.Interval = SaveDelay;
        _timer.IsRepeating = false;
        _timer.Tick += OnTimerTick;
        window.AppWindow.Changed += OnChanged;
        window.Closed += OnClosed;
    }

    /// <summary>後始末をする（変更の監視とタイマーを止める）</summary>
    private void Detach()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _window.AppWindow.Changed -= OnChanged;
        _window.Closed -= OnClosed;
    }

    /// <summary>保存した位置と大きさを復元する。使えなければ既定の大きさにする</summary>
    /// <param name="defaultWidthDip">既定の幅（DIP）</param>
    /// <param name="defaultHeightDip">既定の高さ（DIP）</param>
    private void Restore(double defaultWidthDip, double defaultHeightDip)
    {
        if (_positions.LoadBounds(_key) is { Width: > 0, Height: > 0 } saved
            && WindowPlacement.IsVisibleEnough(new RectInt32(saved.X, saved.Y, saved.Width, saved.Height), VisibleRatio))
        {
            _window.AppWindow.MoveAndResize(new RectInt32(saved.X, saved.Y, saved.Width, saved.Height));
            return;
        }

        var scale = _window.GetDpiScale();
        _window.AppWindow.Resize(new SizeInt32((int)Math.Round(defaultWidthDip * scale), (int)Math.Round(defaultHeightDip * scale)));
    }

    /// <summary>位置・大きさが変わったら、保存を遅らせて待つ（動かしている間は、待ち直す）</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">変更の情報</param>
    private void OnChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange)
        {
            _timer.Stop();
            _timer.Start();
        }
    }

    /// <summary>動かし終わったら、位置と大きさを保存する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">イベントの情報</param>
    /// <remarks>
    /// 保存に失敗（ロック・権限・ディスク）しても、位置と大きさが戻らないだけで、ウィンドウは使える。<c>async void</c> の例外は受け皿が無くアプリごと落ちるため、この失敗だけを受ける（次の変更でやり直す）。
    /// 設定を読めず保存できない状態では、何もしない（例外にならない）。
    /// </remarks>
    private async void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        _timer.Stop();

        var appWindow = _window.AppWindow;
        if (!appWindow.IsVisible || appWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
        {
            return;
        }

        var position = appWindow.Position;
        var size = appWindow.Size;
        try
        {
            await _positions.SaveBoundsAsync(_key, new WindowBounds(position.X, position.Y, size.Width, size.Height));
        }
        catch (DataFileException)
        {
            // 位置と大きさが保存できないだけなので、続ける
        }
    }

    /// <summary>ウィンドウが閉じたら、後始末をする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args) => Detach();
}
