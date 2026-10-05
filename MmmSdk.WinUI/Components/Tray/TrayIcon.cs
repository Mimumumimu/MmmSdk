using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using static MmmSdk.WinUI.Interop.NativeMethods;

namespace MmmSdk.WinUI.Components.Tray;

/// <summary>
/// タスクトレイのアイコンと右クリックメニュー。Win32 の Shell_NotifyIcon を直接使う。メニューの項目は <see cref="ITrayMenuSource"/> から作る。
/// </summary>
/// <remarks>
/// トレイからの通知を受けるため、専用の非表示ウィンドウを作る (メインウィンドウとは独立)。
/// メッセージ専用ウィンドウ (HWND_MESSAGE)にしないのは、エクスプローラー再起動の通知 (TaskbarCreated)がブロードキャストで、
/// メッセージ専用ウィンドウには届かないため。UI スレッドで作り、UI スレッドで破棄する。
/// </remarks>
public sealed class TrayIcon : IDisposable
{
    /// <summary>トレイアイコンの識別番号</summary>
    private const uint IconId = 1;

    /// <summary>トレイアイコンの操作 (クリック・右クリック等)の通知。</summary>
    private const uint CallbackMessage = PInvoke.WM_APP + 1;

    /// <summary>アイコンがキーボードで選択された通知 (NIN_SELECT に NINF_KEY を足したもの)</summary>
    private const uint NinKeySelect = PInvoke.NIN_SELECT | 1;

    /// <summary>メッセージを受けるインスタンス</summary>
    /// <remarks>ウィンドウプロシージャは static のため。トレイはアプリに 1 つ。</remarks>
    private static TrayIcon? s_current;

    /// <summary>メニューに項目を出す機能</summary>
    private readonly IEnumerable<ITrayMenuSource> _sources;
    /// <summary>アプリごとの設定 (文字・クラス名・アイコン)</summary>
    private readonly TrayIconOptions _options;
    /// <summary>TaskbarCreated (エクスプローラー再起動の通知)のメッセージ番号</summary>
    private readonly uint _taskbarCreatedMessage;
    /// <summary>UI スレッドのディスパッチャー</summary>
    private readonly DispatcherQueue _dispatcher;
    /// <summary>復旧できないエラーの報告先</summary>
    private readonly FatalErrorHandler _fatalErrors;

    /// <summary>表示中のメニューのコマンド ID と処理。</summary>
    private readonly Dictionary<int, Func<Task>> _commands = [];

    /// <summary>表示中のメニューの描画</summary>
    /// <remarks>メニューを開いている間だけ持つ。</remarks>
    private TrayMenuRenderer? _renderer;

    /// <summary>通知を受けるウィンドウのハンドル</summary>
    private HWND _hwnd;

    /// <summary>ファイルから読んだアイコン</summary>
    /// <remarks>自分で破棄する。標準のアイコンは破棄しないので持たない。</remarks>
    private HICON _icon;
    /// <summary>破棄済みか</summary>
    private bool _disposed;

    /// <summary>トレイアイコンを作る (表示は <see cref="Show"/> で行う)</summary>
    /// <param name="sources">メニューに項目を出す機能</param>
    /// <param name="options">アプリごとの設定 (ツールチップ・ウィンドウクラス名・アイコン・「終了」の文言)</param>
    /// <param name="fatalErrors">復旧できないエラーの報告先 (メッセージの処理で予想外の例外が出たとき、ログ・ダイアログ・終了を任せる)</param>
    public TrayIcon(IEnumerable<ITrayMenuSource> sources, TrayIconOptions options, FatalErrorHandler fatalErrors)
    {
        _sources = sources;
        _options = options;
        _fatalErrors = fatalErrors;
        // 異常終了のときも、アイコンがトレイに残らないようにする
        fatalErrors.BeforeExit += RemoveIconBeforeExit;
        _taskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>メインウィンドウを出すよう求められた (アイコンのクリック)。</summary>
    public event EventHandler? OpenRequested;

    /// <summary>メニューの「終了」が選ばれた。</summary>
    public event EventHandler? ExitRequested;

    /// <summary>トレイにアイコンを出す。</summary>
    public unsafe void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_hwnd.IsNull) return;

        s_current = this;
        AllowDarkMenus();

        var hInstance = (HINSTANCE)PInvoke.GetModuleHandle((PCWSTR)null);
        fixed (char* className = _options.WindowClassName)
        fixed (char* toolTip = _options.ToolTip)
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = hInstance,
                lpszClassName = className,
            };
            if (PInvoke.RegisterClassEx(windowClass) == 0)
            {
                throw new InvalidOperationException($"トレイ用のウィンドウクラスを登録できませんでした (エラー {Marshal.GetLastPInvokeError()})。");
            }

            _hwnd = PInvoke.CreateWindowEx(0, className, toolTip, 0, 0, 0, 0, 0, HWND.Null, HMENU.Null, hInstance, null);
            if (_hwnd.IsNull)
            {
                throw new InvalidOperationException($"トレイ用のウィンドウを作成できませんでした (エラー {Marshal.GetLastPInvokeError()})。");
            }
        }

        _icon = LoadTrayIcon();
        AddIcon();
    }

    /// <summary>トレイに出すアイコン</summary>
    /// <remarks>読めなければ Windows 標準のアプリアイコン (トレイから操作できなくなるのを避ける)。</remarks>
    private HICON DisplayIcon => !_icon.IsNull ? _icon : PInvoke.LoadIcon(HINSTANCE.Null, PInvoke.IDI_APPLICATION);

    /// <summary>トレイから通知を出す</summary>
    /// <param name="title">通知のタイトル</param>
    /// <param name="message">通知の本文</param>
    /// <param name="isError">エラーのアイコンで出すか (false なら情報のアイコン)</param>
    /// <remarks>Windows の通知として表示される。</remarks>
    public unsafe void ShowNotification(string title, string message, bool isError)
    {
        if (_hwnd.IsNull) return;

        var data = CreateData(NOTIFY_ICON_DATA_FLAGS.NIF_INFO);
        CopyToBuffer(title, data.szInfoTitle.AsSpan());
        CopyToBuffer(message, data.szInfo.AsSpan());
        data.dwInfoFlags = isError ? NOTIFY_ICON_INFOTIP_FLAGS.NIIF_ERROR : NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in data);
    }

    #region アイコン

    /// <summary>Shell_NotifyIcon に渡すデータを作る</summary>
    /// <param name="flags">有効にする項目を示すフラグ (<c>NIF_*</c>)</param>
    /// <returns>トレイアイコンの識別情報を入れたデータ</returns>
    private unsafe NOTIFYICONDATAW CreateData(NOTIFY_ICON_DATA_FLAGS flags) => new()
    {
        cbSize = (uint)sizeof(NOTIFYICONDATAW),
        hWnd = _hwnd,
        uID = IconId,
        uFlags = flags,
    };

    /// <summary>トレイにアイコンを登録する。</summary>
    /// <remarks>エクスプローラーがまだ起動していない等で失敗しても、起動後の TaskbarCreated で登録し直す。</remarks>
    private unsafe void AddIcon()
    {
        var data = CreateData(NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = DisplayIcon;
        CopyToBuffer(_options.ToolTip, data.szTip.AsSpan());
        if (!PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in data))
        {
            return;
        }

        // 新しい通知の形式 (クリック = NIN_SELECT、右クリック = WM_CONTEXTMENU、座標は wParam)を使う
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, in data);
    }

    /// <summary>異常終了の直前に、トレイからアイコンを消す</summary>
    /// <remarks>呼ばれるスレッドは UI スレッドとは限らないので、ウィンドウは壊さず、アイコンの登録だけを外す。</remarks>
    private void RemoveIconBeforeExit()
    {
        if (!_hwnd.IsNull)
        {
            RemoveIcon();
        }
    }

    /// <summary>トレイからアイコンを消す</summary>
    private unsafe void RemoveIcon()
    {
        var data = CreateData(0);
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in data);
    }

    /// <summary>指定されたアイコンファイルを、トレイの大きさ (DPI に合わせた小アイコン)で読む。</summary>
    /// <returns>アイコンのハンドル。パスが未指定・ファイルが無い等で読めなければ null のハンドル (呼び出し側で Windows 標準のアイコンに代える)</returns>
    private unsafe HICON LoadTrayIcon()
    {
        if (_options.IconPath is not { } path)
        {
            return HICON.Null;
        }

        var size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, PInvoke.GetDpiForSystem());
        fixed (char* iconPath = path)
        {
            return new HICON(PInvoke.LoadImage(HINSTANCE.Null, iconPath, GDI_IMAGE_TYPE.IMAGE_ICON, size, size, IMAGE_FLAGS.LR_LOADFROMFILE).Value);
        }
    }

    #endregion

    #region メッセージ

    /// <summary>通知を受けるウィンドウのウィンドウプロシージャ</summary>
    /// <param name="hwnd">ウィンドウのハンドル</param>
    /// <param name="msg">メッセージ</param>
    /// <param name="wParam">メッセージの付加情報 (wParam)</param>
    /// <param name="lParam">メッセージの付加情報 (lParam)</param>
    /// <returns>メッセージの処理結果</returns>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        var current = s_current;
        if (current is null)
        {
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
        }

        try
        {
            return current.HandleMessage(hwnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            // [UnmanagedCallersOnly] から例外が抜けるとログも残らず落ちるため、ここで受けて、ログ・ダイアログ・終了を行う
            current._fatalErrors.Report("トレイのメッセージ処理で予想外の例外が出ました", ex);
            // Report は戻らない (コンパイラーには伝わらないため、念のため投げ直す)
            throw;
        }
    }

    /// <summary>メッセージを処理する</summary>
    /// <param name="hwnd">ウィンドウのハンドル</param>
    /// <param name="msg">メッセージ</param>
    /// <param name="wParam">メッセージの付加情報 (wParam)</param>
    /// <param name="lParam">メッセージの付加情報 (lParam)</param>
    /// <returns>メッセージの処理結果</returns>
    private unsafe LRESULT HandleMessage(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // エクスプローラーが再起動するとトレイアイコンが消えるので、登録し直す
        if (msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            AddIcon();
            return new LRESULT(0);
        }

        switch (msg)
        {
            case CallbackMessage:
                switch ((uint)(lParam.Value & 0xFFFF))
                {
                    case PInvoke.NIN_SELECT:
                    case NinKeySelect:
                        // メッセージ処理の中で画面を操作しないよう、処理が戻ってから行う
                        _dispatcher.TryEnqueue(() => OpenRequested?.Invoke(this, EventArgs.Empty));
                        break;
                    case PInvoke.WM_CONTEXTMENU:
                        // 座標は wParam の下位・上位 16 ビット (符号付き。マルチモニターで負になる)
                        ShowMenu((short)(wParam.Value & 0xFFFF), (short)((wParam.Value >> 16) & 0xFFFF));
                        break;
                }
                return new LRESULT(0);

            // メニューの項目は自分で描く (メニューを開いている間だけ届く)
            case PInvoke.WM_MEASUREITEM when _renderer is not null && ((MEASUREITEMSTRUCT*)lParam.Value)->CtlType == DRAWITEMSTRUCT_CTL_TYPE.ODT_MENU:
                _renderer.Measure((MEASUREITEMSTRUCT*)lParam.Value);
                return new LRESULT(1);

            case PInvoke.WM_DRAWITEM when _renderer is not null && ((DRAWITEMSTRUCT*)lParam.Value)->CtlType == DRAWITEMSTRUCT_CTL_TYPE.ODT_MENU:
                _renderer.Draw((DRAWITEMSTRUCT*)lParam.Value);
                return new LRESULT(1);

            case PInvoke.WM_SETTINGCHANGE:
                // ダーク／ライトの切り替えをメニューに反映する
                FlushMenuThemes();
                break;
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    #endregion

    #region メニュー

    /// <summary>右クリックメニューを出し、選ばれた項目の処理を行う。</summary>
    /// <param name="x">メニューを出す位置の X (画面座標)</param>
    /// <param name="y">メニューを出す位置の Y (画面座標)</param>
    /// <remarks>メニューは開くたびに作る (各機能の最新の内容を出すため)。</remarks>
    private unsafe void ShowMenu(int x, int y)
    {
        var menu = PInvoke.CreatePopupMenu();
        if (menu.IsNull) return;

        var renderer = _renderer = new TrayMenuRenderer(x, y);
        try
        {
            _commands.Clear();
            var nextId = 1;

            foreach (var section in _sources.Select(source => source.GetItems()).Where(items => items.Count > 0))
            {
                AppendItems(menu, section, ref nextId);
                renderer.AppendSeparator(menu);
            }

            AddCommand(menu, _options.ExitText, () =>
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            }, ref nextId);
            renderer.ApplyTo(menu);

            // 前面にしておかないと、メニューの外をクリックしても閉じない (Win32 のトレイメニューの決まり)
            PInvoke.SetForegroundWindow(_hwnd);
            var flags = TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD
                | TRACK_POPUP_MENU_FLAGS.TPM_NONOTIFY | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN;
            if (PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_MENUDROPALIGNMENT) != 0)
            {
                flags |= TRACK_POPUP_MENU_FLAGS.TPM_RIGHTALIGN;
            }
            var selected = PInvoke.TrackPopupMenuEx(menu, (uint)flags, x, y, _hwnd, null).Value;
            PInvoke.PostMessage(_hwnd, PInvoke.WM_NULL, 0, 0);

            if (_commands.TryGetValue(selected, out var command))
            {
                _dispatcher.TryEnqueue(() => command().Forget());
            }
        }
        finally
        {
            // サブメニューも一緒に破棄される。描画用のブラシ・フォントはメニューが使っているので、メニューのあとに破棄する
            PInvoke.DestroyMenu(menu);
            _renderer = null;
            renderer.Dispose();
        }
    }

    /// <summary>項目をメニューに追加する</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    /// <param name="items">追加する項目</param>
    /// <param name="nextId">次に割り当てるコマンド ID。使った分だけ進む</param>
    private void AppendItems(HMENU menu, IReadOnlyList<TrayMenuItem> items, ref int nextId)
    {
        var renderer = _renderer!;
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                renderer.AppendSeparator(menu);
            }
            else if (item.Children is { Count: > 0 } children)
            {
                var submenu = PInvoke.CreatePopupMenu();
                AppendItems(submenu, children, ref nextId);
                renderer.AppendSubmenu(menu, submenu, item.Text, item.IsEnabled);
            }
            else if (item is { IsEnabled: true, Invoked: { } invoked })
            {
                AddCommand(menu, item.Text, invoked, ref nextId);
            }
            else
            {
                renderer.AppendCommand(menu, 0, item.Text, isEnabled: false);
            }
        }
    }

    /// <summary>コマンドの項目をメニューに追加する</summary>
    /// <param name="menu">追加先のメニューのハンドル</param>
    /// <param name="text">表示する文字</param>
    /// <param name="invoked">選ばれたときの処理</param>
    /// <param name="nextId">次に割り当てるコマンド ID。使った分だけ進む</param>
    private void AddCommand(HMENU menu, string text, Func<Task> invoked, ref int nextId)
    {
        var id = nextId++;
        _commands[id] = invoked;
        _renderer!.AppendCommand(menu, id, text, isEnabled: true);
    }

    #endregion

    /// <inheritdoc />
    public unsafe void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fatalErrors.BeforeExit -= RemoveIconBeforeExit;

        if (!_hwnd.IsNull)
        {
            RemoveIcon();
            PInvoke.DestroyWindow(_hwnd);
            fixed (char* className = _options.WindowClassName)
            {
                PInvoke.UnregisterClass(className, (HINSTANCE)PInvoke.GetModuleHandle((PCWSTR)null));
            }
            _hwnd = HWND.Null;
        }
        if (!_icon.IsNull)
        {
            PInvoke.DestroyIcon(_icon);
            _icon = HICON.Null;
        }
        s_current = null;
    }
}
