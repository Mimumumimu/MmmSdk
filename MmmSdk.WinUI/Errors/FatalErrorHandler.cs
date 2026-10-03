using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;
using MmmSdk.Core.Logging;
using MmmSdk.WinUI.Dialogs;

namespace MmmSdk.WinUI.Errors;

/// <summary>
/// 復旧できないエラーを、ログに書き、ダイアログで知らせて、アプリを終了する
/// </summary>
/// <param name="log">エラーを書くログ</param>
/// <param name="appName">ダイアログのタイトルと本文に出すアプリ名</param>
/// <remarks>
/// 予測できる失敗は、呼び出し側が範囲を絞った catch で受けて画面に出す。ここは、復旧が難しい失敗・予想外の失敗（バグ）の最後の受け皿。
/// 順序は「ログ → ダイアログ → 終了」。ダイアログは WinUI ではなく Windows 標準（<see cref="NativeMessageBox"/>）なので、XAML が壊れていても出せる。
/// どのスレッドからでも呼べる。
/// </remarks>
public sealed class FatalErrorHandler(ErrorLog log, string appName)
{
    /// <summary>処理を始めたか（0 = 未・1 = 済）</summary>
    private int _handling;

    /// <summary>終了の直前に行う後始末（トレイアイコンを消すなど）</summary>
    /// <remarks>呼ばれるスレッドは決まっていない。失敗してもログに残すだけで、終了は止めない。</remarks>
    public event Action? BeforeExit;

    /// <summary>未処理の例外の受け皿を張る</summary>
    /// <param name="application">アプリケーション（UI スレッドの未処理例外を受ける）</param>
    /// <remarks>
    /// どこにも受け皿が無い未処理の例外を、すべてここへ集める。
    /// アプリの最初（Host を作る前）に呼ぶ。<c>try/catch</c> を書ける場所では、書いてそこで受けること。
    /// これは、書けない場所（<c>async void</c>・タイマー・待たれないタスク）の最後の安全網。
    /// </remarks>
    public void AttachTo(Application application)
    {
        application.UnhandledException += (_, e) =>
        {
            e.Handled = true;
            Report("UI スレッドの未処理例外", e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Report("未処理例外", e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject.ToString()));
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            // 取り消し（アプリの終了中のタスクなど）はエラーではないので、報告しない
            if (e.Exception.Flatten().InnerExceptions.All(inner => inner is OperationCanceledException))
            {
                return;
            }
            Report("待たれなかったタスクの例外", e.Exception);
        };
    }

    /// <summary>エラーを報告して、アプリを終了する（戻らない）</summary>
    /// <param name="context">どこで起きたか（ログに書く短い説明）</param>
    /// <param name="exception">起きた例外</param>
    /// <remarks>複数のスレッドから同時に呼ばれたときは、最初の 1 つだけが報告し、ほかは終了を待つ。</remarks>
    [DoesNotReturn]
    public void Report(string context, Exception exception)
    {
        if (Interlocked.Exchange(ref _handling, 1) != 0)
        {
            Thread.Sleep(Timeout.Infinite);
        }

        var logPath = log.Write(context, exception);
        var location = logPath is null
            ? "ログを書き込めませんでした。"
            : $"詳細は次のログを見てください。\n{logPath}";
        NativeMessageBox.ShowError(
            $"予期しないエラーが起きたため、{appName} を終了します。\n\n{exception.GetType().Name}: {exception.Message}\n\n{location}",
            appName);

        RunBeforeExit();
        Environment.Exit(1);
    }

    /// <summary>終了前の後始末を行う</summary>
    private void RunBeforeExit()
    {
        try
        {
            BeforeExit?.Invoke();
        }
        catch (Exception ex)
        {
            // すでに終了処理の最中。後始末の失敗で終了を止めないよう、ログに残して続ける
            log.Write("終了前の後始末に失敗しました", ex);
        }
    }
}
