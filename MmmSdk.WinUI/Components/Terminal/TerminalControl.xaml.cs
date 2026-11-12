using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.WebViews;

namespace MmmSdk.WinUI.Components.Terminal;

/// <summary>端末の描画と入出力のつなぎ</summary>
/// <remarks>
/// WebView2 上の xterm.js で端末を描画する
/// <see cref="ITerminalSession"/> と入出力をつなぐ
/// </remarks>
public sealed partial class TerminalControl : UserControl
{
    /// <summary>Assets/Terminal を読むための仮想ホスト名</summary>
    /// <remarks>出力フォルダーの Assets/Terminal (SDK が xterm.js などを配る場所)を WebView2 から読むために使う</remarks>
    private const string HostName = "terminal.mmmsdk.invalid";

    /// <summary>セッションの依存関係プロパティ</summary>
    public static readonly DependencyProperty SessionProperty = DependencyProperty.Register(
        nameof(Session), typeof(ITerminalSession), typeof(TerminalControl), new PropertyMetadata(null, OnSessionChanged));

    /// <summary>UI スレッドのディスパッチャー</summary>
    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>UI スレッドへ渡す前の出力バッファ</summary>
    /// <remarks>シェル出力は細切れに届くため、UI スレッドへ渡す前にまとめる。</remarks>
    private readonly StringBuilder _pendingOutput = new();
    /// <summary>出力バッファの排他用ロック</summary>
    private readonly Lock _pendingLock = new();
    /// <summary>UI スレッドへの受け渡しを予約済みか</summary>
    private bool _flushQueued;
    /// <summary>xterm.js へ送ったが描画の受け取りがまだ返っていない文字数</summary>
    /// <remarks>
    /// UI スレッドだけで読み書きする
    /// xterm.js の書き込み待ちがあふれて出力が捨てられるのを防ぐ
    /// </remarks>
    private int _unconfirmedChars;
    /// <summary>シェルを起動し直している最中か</summary>
    /// <remarks>
    /// 終了待ちの間に押されたキーで、二重に起動し直さないための印。
    /// UI スレッドだけで読み書きする。
    /// </remarks>
    private bool _isRestarting;

    /// <summary>画面への出力を止めているか</summary>
    /// <remarks>
    /// 別のウィンドウへ引き継ぐために止める
    /// 止める処理は <see cref="FreezeAndCaptureAsync"/>
    /// 止めている間に届いた出力は <see cref="_pendingOutput"/> にためる
    /// UI スレッドだけで読み書きする
    /// </remarks>
    private bool _frozen;
    /// <summary>画面の内容の取り出しを待っている依頼の番号</summary>
    /// <remarks>返事と突き合わせるために使う</remarks>
    private int _captureId;
    /// <summary>画面の内容の取り出しを待っているもの</summary>
    private TaskCompletionSource<(string Data, int Columns, int Rows)>? _captureRequest;
    /// <summary>取り出した画面の内容</summary>
    /// <remarks>取り出していなければ null</remarks>
    private (string Data, int Columns, int Rows)? _captured;
    /// <summary>前の画面から引き継いだ内容</summary>
    /// <remarks>端末の準備ができたとき、書き戻す。</remarks>
    private TerminalHandover? _initialHandover;

    /// <summary>画面の内容の取り出しを待つ上限</summary>
    /// <remarks>
    /// 端末の画面が応答しないときにウィンドウを移す操作が止まり続けないため。
    /// 超えたときは内容を引き継がずに、出力だけを引き継ぐ。
    /// </remarks>
    private const int CaptureTimeoutMilliseconds = 3000;

    /// <summary>描画の受け取りを待たずに送ってよい文字数の上限</summary>
    private const int MaxUnconfirmedChars = 1024 * 1024;

    /// <summary>WebView の初期化を始めたか</summary>
    private bool _webViewInitialized;
    /// <summary>xterm.js の準備ができたか</summary>
    private bool _webViewReady;
    /// <summary>閉じたか</summary>
    private bool _closed;

    /// <summary>xterm.js 側の現在の端末の列数</summary>
    /// <remarks>再起動時に使う</remarks>
    private int _columns;
    /// <summary>xterm.js 側の現在の端末の行数</summary>
    private int _rows;

    /// <summary>コントロールを作る</summary>
    public TerminalControl()
    {
        InitializeComponent();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Loaded += OnLoaded;
    }

    /// <summary>自分でフォーカスを取るか</summary>
    /// <remarks>
    /// 表示したときと端末の準備ができたときに取る
    /// 端末のほかに入力欄を持つ画面が表示したときのフォーカスを入力欄に置きたいときに false にする
    /// WebView2 にフォーカスがあるあいだはウィンドウ全体のショートカット (Alt+矢印など)が効かないため
    /// <see cref="FocusTerminal"/> を直接呼ぶ移動はこの設定にかかわらず行う
    /// </remarks>
    public bool AutoFocus { get; set; } = true;

    /// <summary>シェルのセッション</summary>
    public ITerminalSession? Session
    {
        get => (ITerminalSession?)GetValue(SessionProperty);
        set => SetValue(SessionProperty, value);
    }

    /// <summary>WebView2 のデータの保存先フォルダー</summary>
    /// <remarks>
    /// キャッシュなどを入れる。
    /// 読み込まれる前に設定する。
    /// WebView2 の初期化のときに 1 回だけ読む。
    /// null のときは WebView2 の既定で、EXE の隣の <c>&lt;EXE 名&gt;.WebView2</c>。
    /// 同じプロセスの WebView2 は同じフォルダーなら同じ設定で作る必要があるので、アプリの中で 1 か所に決めて渡す。
    /// </remarks>
    public string? UserDataFolder { get; set; }

    /// <summary>前の画面から引き継いだ内容</summary>
    /// <remarks>
    /// <see cref="TakeHandover"/> の戻り値を渡す。
    /// <see cref="Session"/> を渡す前に設定する。
    /// 端末の準備ができたとき、内容を書き戻してから、続きの出力を描く。
    /// 届いていなかった出力はすぐ出力のたまりの先頭に入れる。
    /// 設定できるのは準備の前に 1 回だけ。
    /// </remarks>
    public TerminalHandover? InitialHandover
    {
        get => _initialHandover;
        set
        {
            if (_webViewReady || _initialHandover is not null || value is null)
            {
                return;
            }

            _initialHandover = value;
            lock (_pendingLock)
            {
                _pendingOutput.Insert(0, value.PendingOutput);
            }
        }
    }

    /// <summary>別のウィンドウへ引き継ぐために画面への出力を止めて、画面の内容を取り出す</summary>
    /// <returns>取り出しの完了を表すタスク</returns>
    /// <remarks>
    /// 画面は表示したままでよい。
    /// 止めている間に届いた出力はためておく。
    /// 端末の準備がまだのとき・閉じているときは取り出す内容が無いので、出力を止めるだけ。
    /// 終わったら、<see cref="TakeHandover"/> を呼ぶこと。
    /// UI スレッドから呼ぶ。
    /// </remarks>
    public async Task FreezeAndCaptureAsync()
    {
        _frozen = true;
        if (!_webViewReady || _closed)
        {
            return;
        }

        var request = new TaskCompletionSource<(string Data, int Columns, int Rows)>(TaskCreationOptions.RunContinuationsAsynchronously);
        _captureRequest = request;
        var id = ++_captureId;
        PostMessage("capture", null, id: id);

        if (await Task.WhenAny(request.Task, Task.Delay(CaptureTimeoutMilliseconds)) == request.Task)
        {
            _captured = request.Task.Result;
        }
        _captureRequest = null;
    }

    /// <summary>引き継ぎの情報を受け取って、この端末を閉じる</summary>
    /// <returns>
    /// 引き継ぎの情報
    /// 新しい <see cref="TerminalControl"/> の <see cref="InitialHandover"/> へ渡す
    /// </returns>
    /// <remarks>
    /// <see cref="FreezeAndCaptureAsync"/> のあとに呼ぶ。
    /// シェル (セッション)は終了しない。
    /// 端末の準備の前で、前の画面から引き継いだ内容を書き戻せていないときはその内容と届いた出力を、そのまま次へ渡す。
    /// UI スレッドから呼ぶ。
    /// 新しい端末へセッションをつなぐまで、同じ処理の中で続けること。
    /// 続けないとあいだに届いた出力が捨てられる。
    /// </remarks>
    public TerminalHandover TakeHandover()
    {
        string pending;
        lock (_pendingLock)
        {
            pending = _pendingOutput.ToString();
            _pendingOutput.Clear();
        }

        var handover = _captured is { } captured
            ? new TerminalHandover(captured.Data, captured.Columns, captured.Rows, pending)
            : _initialHandover is { } initial
                ? new TerminalHandover(initial.Snapshot, initial.Columns, initial.Rows, pending)
                : new TerminalHandover(null, 0, 0, pending);
        _captured = null;
        Close();
        return handover;
    }

    /// <summary>セッションが差し替わったらイベントの購読を付け替える</summary>
    /// <param name="d">変更されたコントロール</param>
    /// <param name="e">変更の情報</param>
    private static void OnSessionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TerminalControl)d;
        if (e.OldValue is ITerminalSession oldSession)
        {
            oldSession.OutputReceived -= control.OnOutputReceived;
            oldSession.Exited -= control.OnSessionExited;
            oldSession.SubmitRequested -= control.OnSubmitRequested;
        }
        if (e.NewValue is ITerminalSession newSession)
        {
            newSession.OutputReceived += control.OnOutputReceived;
            newSession.Exited += control.OnSessionExited;
            newSession.SubmitRequested += control.OnSubmitRequested;

            // 端末の準備ができたあとに渡されたときは、ここで起動する (準備の前なら、準備ができたときに起動する)
            if (control._webViewReady && !newSession.IsStarted)
            {
                control.StartSessionAsync(restart: false).Forget();
            }
        }
    }

    /// <summary>送信を求められたら端末に貼り付けとして渡す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="request">送信の依頼</param>
    private void OnSubmitRequested(object? sender, SubmitRequest request)
    {
        if (_webViewReady)
        {
            PostMessage("submit", request.Text, request.ReadyMarker);
        }
        else
        {
            // 端末の表示がまだ準備できていなければ、そのまま書き込んで確定する
            Session?.Write(request.Text);
            Session?.Write("\r");
        }
    }

    /// <summary>端末を閉じる</summary>
    /// <remarks>
    /// WebView2 のプロセスを解放する。
    /// 画面から取り除いて、もう使わないときに呼ぶ。
    /// 例はタブを閉じる・ページを捨てる。
    /// 呼ばないと、WebView2 のプロセスがアプリを終了するまで残る。
    /// セッションを外し、WebView2 を閉じる。
    /// シェル (セッション)は終了しない。
    /// 終了するのはセッションの <see cref="IDisposable.Dispose"/>。
    /// 閉じたあとは再利用できない。
    /// 何度呼んでもよい。
    /// UI スレッドから呼ぶ。
    /// </remarks>
    public void Close()
    {
        if (_closed)
        {
            return;
        }
        _closed = true;

        Session = null;
        _webViewReady = false;
        WebView.Close();
    }

    /// <summary>読み込み時に WebView を初期化して、xterm.js のページを開く</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_closed)
        {
            return;
        }

        if (_webViewInitialized)
        {
            // ページを再表示したとき
            if (AutoFocus)
            {
                FocusTerminal();
            }
            return;
        }
        _webViewInitialized = true;

        try
        {
            await WebView2Setup.EnsureAsync(WebView, UserDataFolder);
        }
        catch (COMException ex)
        {
            // WebView2 ランタイムが無い・起動できないとき。ターミナルだけが使えないので、他の機能は使えるよう、ここに理由を出す
            ShowInitializationError(ex);
            return;
        }
        // 初期化の待ちの間に閉じられたときは、何もしない (閉じた WebView2 には、CoreWebView2 が無い)
        if (_closed)
        {
            return;
        }
        var core = WebView.CoreWebView2;

        // ブラウザとしての機能 (再読み込み・検索・印刷・ズーム・右クリックメニュー等)を止め、端末として振る舞わせる
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        // ページからアプリ側のオブジェクトを呼べないようにする (メッセージのやり取りだけを使う)
        core.Settings.AreHostObjectsAllowed = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif
        core.NewWindowRequested += (_, args) => args.Handled = true;
        // 仮想ホスト (同梱のターミナルの画面)以外へ移動させない (リンクなどで、外のページに切り替わらないように)
        core.NavigationStarting += (_, args) => args.Cancel = !IsTerminalPageUri(args.Uri);
        core.WebMessageReceived += OnWebMessageReceived;
        core.SetVirtualHostNameToFolderMapping(
            HostName, Path.Combine(AppContext.BaseDirectory, "Assets", "Terminal"), CoreWebView2HostResourceAccessKind.DenyCors);
        core.Navigate($"https://{HostName}/index.html");
    }

    /// <summary>同梱のターミナルの画面の URI か</summary>
    /// <param name="uri">移動先の URI</param>
    /// <returns>仮想ホストの https の URI なら true</returns>
    /// <remarks>画面は仮想ホストで配る</remarks>
    private static bool IsTerminalPageUri(string uri)
        => Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps
            && string.Equals(parsed.Host, HostName, StringComparison.OrdinalIgnoreCase);

    /// <summary>WebView2 の初期化失敗の理由を表示する</summary>
    /// <param name="exception">初期化の失敗</param>
    /// <remarks>COMException 以外が出たときは未処理例外の受け皿 (ログ・ダイアログ・終了)が受ける</remarks>
    private void ShowInitializationError(COMException exception)
    {
        WebView.Visibility = Visibility.Collapsed;
        InitializationErrorText.Text =
            $"ターミナルを表示できません。WebView2 を初期化できませんでした。\nWebView2 ランタイムがインストールされているか確認してください。\n\n{exception.Message}";
        InitializationErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>xterm.js からのメッセージを処理する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">受け取ったメッセージの情報</param>
    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!Uri.TryCreate(args.Source, UriKind.Absolute, out var source) || source.Host != HostName)
        {
            return;
        }

        using var json = JsonDocument.Parse(args.WebMessageAsJson);
        var message = json.RootElement;
        switch (message.GetProperty("type").GetString())
        {
            case "ready":
                _columns = message.GetProperty("cols").GetInt32();
                _rows = message.GetProperty("rows").GetInt32();
                OnTerminalReady();
                break;
            case "input":
                OnInput(message.GetProperty("data").GetString() ?? "");
                break;
            case "resize":
                _columns = message.GetProperty("cols").GetInt32();
                _rows = message.GetProperty("rows").GetInt32();
                Session?.Resize(_columns, _rows);
                break;
            case "written":
                _unconfirmedChars = Math.Max(0, _unconfirmedChars - message.GetProperty("length").GetInt32());
                FlushOutput();
                break;
            case "snapshot":
                // 待っている依頼への返事だけを受ける (待ちを諦めたあとに届いた古い返事は捨てる)
                if (_captureRequest is { } request && message.GetProperty("id").GetInt32() == _captureId)
                {
                    request.TrySetResult((
                        message.GetProperty("data").GetString() ?? "",
                        message.GetProperty("cols").GetInt32(),
                        message.GetProperty("rows").GetInt32()));
                }
                break;
        }
    }

    /// <summary>xterm.js の準備ができたときの処理</summary>
    /// <remarks>シェルを起動する</remarks>
    private void OnTerminalReady()
    {
        _webViewReady = true;

        // 前の画面から引き継いだ内容を、続きの出力より先に書き戻す (どちらも、端末の書き込みの順に処理される)
        if (_initialHandover is { Snapshot: { } snapshot } initial)
        {
            PostMessage("restore", snapshot, cols: initial.Columns, rows: initial.Rows);
        }
        _initialHandover = null;

        if (Session is { IsStarted: true } session)
        {
            session.Resize(_columns, _rows);
        }
        else
        {
            StartSessionAsync(restart: false).Forget();
        }

        FlushOutput();
        if (AutoFocus)
        {
            FocusTerminal();
        }
    }

    /// <summary>端末への入力をシェルへ送る</summary>
    /// <param name="data">端末への入力</param>
    /// <remarks>シェル終了後は再起動する</remarks>
    private void OnInput(string data)
    {
        if (_isRestarting)
        {
            return;
        }

        // シェル終了後は、押されたキーを捨てて再起動のきっかけにする
        if (Session is { HasExited: true })
        {
            StartSessionAsync(restart: true).Forget();
            return;
        }
        Session?.Write(data);
    }

    /// <summary>シェルを起動し直す</summary>
    /// <returns>起動し直しの完了を表すタスク</returns>
    /// <remarks>
    /// 起動していなければ起動する
    /// 起動するシェル (<see cref="ITerminalSession.Shell"/>)を替えたあとに呼ぶ
    /// 動いているシェル (とその中の CLI)は終了する
    /// 表示の準備がまだのとき・<see cref="Session"/> が無いときは何もしない
    /// 準備ができたときにそのときの設定で起動する
    /// </remarks>
    public Task RestartSessionAsync()
    {
        if (!_webViewReady || Session is not { } session || _isRestarting)
        {
            return Task.CompletedTask;
        }
        return StartSessionAsync(restart: session.IsStarted);
    }

    /// <summary>シェルを起動する</summary>
    /// <param name="restart">起動し直すなら true</param>
    /// <returns>起動 (または起動し直し)の完了を表すタスク</returns>
    /// <remarks>
    /// restart が true なら起動し直す
    /// 起動し直すときの終了待ちは UI スレッドを止めずに行う
    /// </remarks>
    private async Task StartSessionAsync(bool restart)
    {
        if (Session is not { } session)
        {
            return;
        }

        try
        {
            if (restart)
            {
                _isRestarting = true;
                // 出力の順序を保つため、シェルの出力と同じ経路で送る
                OnOutputReceived(this, "\r\n");
                await session.RestartAsync(_columns, _rows);
            }
            else
            {
                session.Start(_columns, _rows);
            }
        }
        catch (Exception ex) when (ex is Win32Exception or COMException or InvalidOperationException)
        {
            OnOutputReceived(this, $"\x1b[31mシェルを起動できませんでした: {session.Shell.CommandLine}\r\n{ex.Message}\x1b[0m\r\n");
        }
        finally
        {
            _isRestarting = false;
        }
    }

    /// <summary>シェルの出力をためて UI スレッドで描画する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="text">シェルの出力</param>
    private void OnOutputReceived(object? sender, string text)
    {
        lock (_pendingLock)
        {
            _pendingOutput.Append(text);
            if (_flushQueued)
            {
                return;
            }
            _flushQueued = true;
        }
        _dispatcherQueue.TryEnqueue(FlushOutput);
    }

    /// <summary>シェルが終了したことを端末に表示する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnSessionExited(object? sender, EventArgs e)
        => OnOutputReceived(sender, "\r\n\x1b[90m[プロセスが終了しました。何かキーを押すと再起動します]\x1b[0m\r\n");

    /// <summary>ためた出力を端末へ送る</summary>
    /// <remarks>端末の描画が追いついていない (<see cref="MaxUnconfirmedChars"/> 超)ときは送らず、受け取りが返ってから送る。</remarks>
    private void FlushOutput()
    {
        if (!_webViewReady || _frozen)
        {
            return;
        }

        string text;
        lock (_pendingLock)
        {
            if (_unconfirmedChars >= MaxUnconfirmedChars)
            {
                _flushQueued = false;
                return;
            }
            text = _pendingOutput.ToString();
            _pendingOutput.Clear();
            _flushQueued = false;
        }

        if (text.Length > 0)
        {
            _unconfirmedChars += text.Length;
            PostMessage("output", text);
        }
    }

    /// <summary>テキスト入力欄から直接フォーカスが来るときは見えない部品を一度経由させてから端末に移す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">フォーカスの移動の情報</param>
    /// <remarks>
    /// <para>
    /// 回避策。
    /// 根本の原因は特定できていない。
    /// WinUI または WebView2 側の不具合と考えている。
    /// 入力欄 (TextBox など)から WebView2 へ直接フォーカスが移ると、タスクバーの IME は日本語入力のままなのに、
    /// WebView2 の入力欄 (xterm.js の textarea)につながらず、英字が入る。
    /// キー入力が IME に渡らない。
    /// 別のウィンドウへ切り替えて戻るか、ほかの部品 (ツリーなど)を一度経由すると、つながる。
    /// WebView2 側のフォーカスのイベント・IME のオン・オフの状態・XAML のフォーカスの状態は直るときと直らないときで同じだった。
    /// </para>
    /// <para>
    /// そこで、入力欄からのときだけ、見えない経由先 (<c>FocusRelay</c>)を挟んでから端末に移す。
    /// フォーカスの外れ・付け直しが 1 回ずつ増える。
    /// 経由先から WebView2 へ移るときは元が入力欄ではないので、ここで再び経由させることはない。
    /// WinUI・WebView2 の更新で直ったら、この処理と <c>FocusRelay</c> を外す。
    /// 外して、入力欄 → ターミナルで日本語を打てれば確認できる。
    /// </para>
    /// </remarks>
    private void OnWebViewGettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        if (_relayDone || !_webViewReady || args.OldFocusedElement is not (TextBox or PasswordBox or RichEditBox))
        {
            return;
        }

        if (!args.TrySetNewFocusedElement(FocusRelay))
        {
            _dispatcherQueue.TryEnqueue(() => FocusRelay.Focus(FocusState.Programmatic));
        }
        _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, FocusTerminal);
    }

    /// <summary>経由の 1 段目を済ませて端末へ移すところか</summary>
    /// <remarks>
    /// 経由するのは見えない部品 <c>FocusRelay</c>。
    /// 経由の向け直しは 1 回だけにして、必ず終わるようにする。
    /// </remarks>
    private bool _relayDone;

    /// <summary>端末にキーボードフォーカスを移す</summary>
    /// <remarks>
    /// テキスト入力欄にフォーカスがあるときは直接 WebView2 へ移さず、見えない部品を一度経由してから移す。
    /// 理由は <see cref="OnWebViewGettingFocus"/> の備考。
    /// 経由は 1 回だけで、2 回目は経由できていなくても、そのまま端末へ移す。
    /// 向け直しが効かないと、移す処理が自分を呼び直して、止まらなくなるため。
    /// </remarks>
    public void FocusTerminal()
    {
        if (!_webViewReady)
        {
            return;
        }

        if (!_relayDone && Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) is TextBox or PasswordBox or RichEditBox)
        {
            FocusRelay.Focus(FocusState.Programmatic);
            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            {
                _relayDone = true;
                try
                {
                    FocusTerminal();
                }
                finally
                {
                    _relayDone = false;
                }
            });
            return;
        }

        WebView.Focus(FocusState.Programmatic);
        PostMessage("focus", null);
    }

    /// <summary>xterm.js へメッセージを送る</summary>
    /// <param name="type">メッセージの種類</param>
    /// <param name="data">
    /// メッセージの内容
    /// 無ければ null
    /// </param>
    /// <param name="marker">
    /// 送信の完了を待つ合図の文字列
    /// 無ければ null
    /// </param>
    /// <param name="id">
    /// 依頼の番号
    /// 返事と突き合わせる
    /// 無ければ null
    /// </param>
    /// <param name="cols">
    /// 端末の列数
    /// 無ければ null
    /// </param>
    /// <param name="rows">
    /// 端末の行数
    /// 無ければ null
    /// </param>
    private void PostMessage(string type, string? data, string? marker = null, int? id = null, int? cols = null, int? rows = null)
    {
        // トリミング有効の発行でも動くよう、リフレクションを使わずに JSON を組み立てる
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", type);
            if (data is not null)
            {
                writer.WriteString("data", data);
            }
            if (marker is not null)
            {
                writer.WriteString("marker", marker);
            }
            if (id is { } idValue)
            {
                writer.WriteNumber("id", idValue);
            }
            if (cols is { } colsValue)
            {
                writer.WriteNumber("cols", colsValue);
            }
            if (rows is { } rowsValue)
            {
                writer.WriteNumber("rows", rowsValue);
            }
            writer.WriteEndObject();
        }
        WebView.CoreWebView2?.PostWebMessageAsJson(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}
