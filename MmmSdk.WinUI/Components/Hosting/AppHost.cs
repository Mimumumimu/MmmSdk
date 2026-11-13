using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using MmmSdk.Core.Components.Logging;
using MmmSdk.Core.Components.SingleInstance;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Utilities;

namespace MmmSdk.WinUI.Components.Hosting;

/// <summary>アプリの起動と終了の骨格</summary>
/// <remarks>
/// アプリごとに違うのは DI の登録と起動の途中でする画面の準備だけ。
/// 多重起動の確認・エラーログと未処理の例外の受け皿・Host の組み立て・起動の失敗の報告・終了の順序をここに集める。
/// <see cref="Application"/> を継承する基底クラスにしないのは App.xaml の根の要素を変えずに済ませるため。
/// App のコンストラクターで、この順に呼ぶ。
/// <c>new AppHost</c> → <c>InitializeComponent</c> → <see cref="Build"/>。
/// 未処理の例外の受け皿は <c>InitializeComponent</c> より前に付ける。
/// </remarks>
public sealed class AppHost
{
    /// <summary>アプリケーション</summary>
    private readonly Application _application;

    /// <summary>多重起動の防止</summary>
    /// <remarks>プロセス終了までフィールドで持つ</remarks>
    private readonly SingleInstanceGuard _instanceGuard;

    /// <summary>エラーを書くログ</summary>
    private readonly ErrorLog _errorLog;

    /// <summary>DI とライフタイムを扱う Host</summary>
    /// <remarks><see cref="Build"/> を呼ぶまで null</remarks>
    private IHost? _host;

    /// <summary>終了の処理を始めたか</summary>
    private bool _exiting;

    /// <summary>復旧できないエラーの報告先</summary>
    public FatalErrorHandler FatalErrors { get; }

    /// <summary>DI のサービスの取り出し口</summary>
    /// <remarks><see cref="Build"/> を呼ぶまで使えない</remarks>
    public IServiceProvider Services => _host?.Services
        ?? throw new InvalidOperationException($"{nameof(Build)} を呼ぶ前に {nameof(Services)} は使えません。");

    /// <summary>多重起動の確認と未処理の例外の受け皿</summary>
    /// <param name="application">アプリケーション</param>
    /// <param name="appName">多重起動の判定・ダイアログに出すアプリの名前</param>
    /// <param name="logDirectory">エラーのログを書くフォルダー</param>
    /// <remarks>
    /// 2 つ目の起動はメッセージを出してプロセスごと終了する。
    /// 未処理の例外の受け皿は Host を作る前の失敗も受けられるよう、最初に付ける。
    /// </remarks>
    public AppHost(Application application, string appName, string logDirectory)
    {
        _application = application;
        _instanceGuard = new SingleInstanceGuard(appName);
        if (!_instanceGuard.IsFirstInstance)
        {
            NativeMessageBox.ShowInformation($"{appName} はすでに起動しています。", appName);
            Environment.Exit(0);
        }

        _errorLog = new ErrorLog(logDirectory);
        FatalErrors = new FatalErrorHandler(_errorLog, appName);
        FatalErrors.AttachTo(application);
    }

    /// <summary>DI を登録して Host を組み立てる</summary>
    /// <param name="configureServices">アプリごとの DI の登録</param>
    /// <remarks>
    /// 時刻・エラーログ・復旧できないエラーの報告先をここで登録し、設定 (appsettings.json・環境変数)とログの既定は起動時に読まないので無効にする。
    /// <see cref="ExitAsync"/> が <c>IDialogHost</c> を DI から取るので、<paramref name="configureServices"/> の中で <c>AddMmmSdkWinUI</c> を呼んでおくこと。
    /// </remarks>
    public void Build(Action<IServiceCollection> configureServices)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(FatalErrors);
        builder.Services.AddSingleton(_errorLog);
        configureServices(builder.Services);
        _host = builder.Build();
    }

    /// <summary>Host を始める</summary>
    /// <returns>Host の開始の完了を表すタスク</returns>
    public Task StartHostAsync() => (_host ?? throw new InvalidOperationException($"{nameof(Build)} を呼ぶ前に開始できません。")).StartAsync();

    /// <summary>起動の処理の実行と失敗の報告</summary>
    /// <param name="start">起動の処理</param>
    /// <returns>起動の完了を表すタスク</returns>
    /// <remarks>
    /// <c>OnLaunched</c> から呼ぶ。
    /// 起動が途中で止まると、ウィンドウもトレイも出ないまま動き続けて気づけない。
    /// 復旧できないので、ログ・ダイアログ・終了にする。
    /// 読み込みの失敗など起動を止めたくない例外は呼ぶ側の起動の処理の中で受け止める。
    /// </remarks>
    public async Task LaunchAsync(Func<Task> start)
    {
        try
        {
            await start();
        }
        catch (Exception ex)
        {
            FatalErrors.Report("起動に失敗しました", ex);
        }
    }

    /// <summary>アプリを完全に終了する</summary>
    /// <param name="mainWindow">メインウィンドウ (まだ作っていなければ null)</param>
    /// <param name="prepareExit">開いているウィンドウを閉じる前にするアプリごとの準備 (無ければ null)</param>
    /// <returns>終了処理の完了を表すタスク</returns>
    /// <remarks>
    /// 準備 → 開いているウィンドウを閉じる → Host を止める → Host を破棄する → アプリの終了 の順に行う。
    /// 2 回目以降の呼び出しは何もしない。
    /// 終了の入口はトレイの「終了」・画面からの依頼・メインウィンドウの <c>Closed</c> など複数ある。
    /// Host の停止を待つ間は UI スレッドが空くので、別の入口から続けて呼ばれうる。
    /// 破棄済みの Host を触らないためのもの。
    /// 開いているウィンドウは破棄済みのサービスを触らないよう Host が生きているうちに閉じる。
    /// メインウィンドウは <see cref="Application.Exit"/> が閉じる。
    /// UI スレッドから呼ぶ。
    /// </remarks>
    public async Task ExitAsync(Window? mainWindow, Action? prepareExit = null)
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        prepareExit?.Invoke();
        Services.GetRequiredService<IDialogHost>().CloseAll(mainWindow);
        await _host!.StopAsync();
        _host.Dispose();
        _application.Exit();
    }
}
