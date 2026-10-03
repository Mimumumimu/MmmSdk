using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Console;
using Windows.Win32.System.Threading;

namespace MmmSdk.WinUI.Components.Terminal;

/// <summary>
/// ConPTY（Windows 擬似コンソール）にプロセスをつないで起動し、入出力のパイプを渡す。
/// </summary>
/// <remarks>
/// 端末の描画・入力の解釈は持たない（出力は端末のエスケープシーケンスを含んだバイト列のまま）。
/// 使い終わったら、出力を読み続けたまま <see cref="Close"/> でシェルを終わらせ、読み取りが終わってから <see cref="Dispose"/> する。
/// </remarks>
public sealed class PseudoConsole : IDisposable
{
    /// <summary>端末の大きさの上限（列・行）</summary>
    private const int MaxSize = short.MaxValue;

    /// <summary>擬似コンソールを閉じるのを待つ時間</summary>
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(3);

    /// <summary>擬似コンソールのハンドル</summary>
    private readonly HPCON _handle;
    /// <summary>プロセスのハンドル</summary>
    private readonly SafeWaitHandle _process;
    /// <summary>閉じたか</summary>
    private bool _closed;
    /// <summary>破棄済みか</summary>
    private bool _disposed;

    /// <summary>擬似コンソールと起動したプロセスを受け取る</summary>
    /// <param name="handle">擬似コンソールのハンドル</param>
    /// <param name="input">プロセスへの入力パイプ</param>
    /// <param name="output">プロセスからの出力パイプ</param>
    /// <param name="process">プロセスのハンドル</param>
    private PseudoConsole(HPCON handle, FileStream input, FileStream output, SafeWaitHandle process)
    {
        _handle = handle;
        Input = input;
        Output = output;
        _process = process;
        ExitHandle = new ProcessWaitHandle(process);
    }

    /// <summary>プロセスへの入力（書き込み用）</summary>
    /// <remarks>書き込む内容は、端末が受け取るキー入力の文字列（UTF-8）。<see cref="Close"/> で閉じる。</remarks>
    public Stream Input { get; }

    /// <summary>プロセスからの出力（読み取り用）</summary>
    /// <remarks>端末のエスケープシーケンスを含む UTF-8 のバイト列。プロセスが終わり擬似コンソールを閉じると、末尾（読み取りが 0 バイト）になる。</remarks>
    public Stream Output { get; }

    /// <summary>プロセスが終了すると通知される待機ハンドル</summary>
    /// <remarks><see cref="ThreadPool.RegisterWaitForSingleObject(WaitHandle, WaitOrTimerCallback, object?, int, bool)"/> などで待つ。ハンドルの所有はこのクラスで、<see cref="Dispose"/> まで有効。</remarks>
    public WaitHandle ExitHandle { get; }

    /// <summary>擬似コンソールを作り、プロセスをつないで起動する</summary>
    /// <param name="commandLine">プロセスを起動するコマンドライン</param>
    /// <param name="workingDirectory">プロセスの作業ディレクトリ</param>
    /// <param name="columns">端末の桁数</param>
    /// <param name="rows">端末の行数</param>
    /// <returns>起動した擬似コンソール</returns>
    /// <remarks>失敗したときは、作った分をすべて解放してから例外を投げる。</remarks>
    /// <exception cref="Win32Exception">パイプの作成またはプロセスの起動に失敗した</exception>
    /// <exception cref="COMException">擬似コンソールを作成できなかった</exception>
    public static unsafe PseudoConsole Start(string commandLine, string workingDirectory, int columns, int rows)
    {
        if (!PInvoke.CreatePipe(out var inputReadHandle, out var inputWriteHandle, null, 0))
        {
            throw new Win32Exception();
        }
        var inputRead = new SafeFileHandle((nint)inputReadHandle.Value, ownsHandle: true);
        var inputWrite = new SafeFileHandle((nint)inputWriteHandle.Value, ownsHandle: true);

        if (!PInvoke.CreatePipe(out var outputReadHandle, out var outputWriteHandle, null, 0))
        {
            var error = new Win32Exception();
            inputRead.Dispose();
            inputWrite.Dispose();
            throw error;
        }
        var outputRead = new SafeFileHandle((nint)outputReadHandle.Value, ownsHandle: true);
        var outputWrite = new SafeFileHandle((nint)outputWriteHandle.Value, ownsHandle: true);

        HPCON handle;
        try
        {
            // ConPTY 側の端（入力の読み取り側・出力の書き込み側）は、作成後に ConPTY が保持するので閉じてよい
            using (inputRead)
            using (outputWrite)
            {
                var result = PInvoke.CreatePseudoConsole(
                    ToCoord(columns, rows), new HANDLE(inputRead.DangerousGetHandle()), new HANDLE(outputWrite.DangerousGetHandle()), 0, out handle);
                Marshal.ThrowExceptionForHR(result.Value);
            }
        }
        catch
        {
            inputWrite.Dispose();
            outputRead.Dispose();
            throw;
        }

        var input = new FileStream(inputWrite, FileAccess.Write, 0);
        var output = new FileStream(outputRead, FileAccess.Read, 0);
        try
        {
            return new PseudoConsole(handle, input, output, StartProcess(handle, commandLine, workingDirectory));
        }
        catch
        {
            // プロセスを起動できなかったときは、擬似コンソールもパイプも片付けて、投げ直す
            input.Dispose();
            PInvoke.ClosePseudoConsole(handle);
            output.Dispose();
            throw;
        }
    }

    /// <summary>端末の大きさを変える</summary>
    /// <param name="columns">端末の桁数</param>
    /// <param name="rows">端末の行数</param>
    /// <remarks>閉じたあとは何もしない。</remarks>
    public void Resize(int columns, int rows)
    {
        if (!_closed)
        {
            PInvoke.ResizePseudoConsole(_handle, ToCoord(columns, rows));
        }
    }

    /// <summary>入力を閉じ、擬似コンソールを閉じる（プロセスがまだ動いていれば終了する）</summary>
    /// <remarks>
    /// 出力を読み続けていないと擬似コンソールを閉じる処理が戻らないことがあるため、別スレッドで閉じて、最大 3 秒だけ待つ。
    /// 呼ぶ側は出力を読み続けたまま呼び、読み取りが末尾になるのを待ってから <see cref="Dispose"/> する。
    /// 同期で待つのは意図的（<c>IAsyncDisposable</c> にすると、DI コンテナが <c>ConfigureAwait(false)</c> で待つため、
    /// 後から破棄されるトレイアイコンなどの後始末が UI スレッドの外で動いてしまう）。
    /// </remarks>
    public void Close()
    {
        if (_closed) return;
        _closed = true;

        Input.Dispose();
        var handle = _handle;
        Task.Run(() => PInvoke.ClosePseudoConsole(handle)).Wait(CloseTimeout);
    }

    /// <summary>擬似コンソールを閉じ（まだなら）、パイプとプロセスのハンドルを解放する</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Close();
        Output.Dispose();
        ExitHandle.Dispose();
        _process.Dispose();
    }

    /// <summary>プロセスを擬似コンソールにつないで起動する</summary>
    /// <param name="console">接続する擬似コンソールのハンドル</param>
    /// <param name="commandLine">起動するコマンドライン</param>
    /// <param name="workingDirectory">作業ディレクトリ</param>
    /// <returns>起動したプロセスのハンドル</returns>
    /// <exception cref="Win32Exception">プロセスを起動できなかった</exception>
    private static unsafe SafeWaitHandle StartProcess(HPCON console, string commandLine, string workingDirectory)
    {
        nuint size = 0;
        PInvoke.InitializeProcThreadAttributeList(default, 1, 0, &size);
        var buffer = Marshal.AllocHGlobal((nint)size);
        try
        {
            var attributeList = new LPPROC_THREAD_ATTRIBUTE_LIST((void*)buffer);
            if (!PInvoke.InitializeProcThreadAttributeList(attributeList, 1, 0, &size))
            {
                throw new Win32Exception();
            }

            try
            {
                if (!PInvoke.UpdateProcThreadAttribute(attributeList, 0, PInvoke.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, (void*)console.Value, (nuint)nint.Size, null, null))
                {
                    throw new Win32Exception();
                }

                var startupInfo = new STARTUPINFOEXW { lpAttributeList = attributeList };
                startupInfo.StartupInfo.cb = (uint)sizeof(STARTUPINFOEXW);
                // 親（このアプリ）の標準ハンドルを子へ引き継がせない
                startupInfo.StartupInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;

                // CreateProcess はコマンドラインを書き換えることがあるので、終端を付けた書き込み可能なバッファを渡す
                var commandLineBuffer = (commandLine + '\0').ToCharArray();
                PROCESS_INFORMATION processInfo;
                fixed (char* commandLinePtr = commandLineBuffer)
                fixed (char* workingDirectoryPtr = workingDirectory)
                {
                    if (!PInvoke.CreateProcess(null, commandLinePtr, null, null, false, PROCESS_CREATION_FLAGS.EXTENDED_STARTUPINFO_PRESENT, null,
                            workingDirectoryPtr, &startupInfo.StartupInfo, &processInfo))
                    {
                        throw new Win32Exception();
                    }
                }

                PInvoke.CloseHandle(processInfo.hThread);
                return new SafeWaitHandle((nint)processInfo.hProcess.Value, ownsHandle: true);
            }
            finally
            {
                PInvoke.DeleteProcThreadAttributeList(attributeList);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>列数・行数から端末サイズの構造体を作る</summary>
    /// <param name="columns">端末の桁数</param>
    /// <param name="rows">端末の行数</param>
    /// <returns>端末サイズ（1 〜 <see cref="MaxSize"/> に収める）</returns>
    private static COORD ToCoord(int columns, int rows)
        => new() { X = (short)Math.Clamp(columns, 1, MaxSize), Y = (short)Math.Clamp(rows, 1, MaxSize) };

    /// <summary>プロセスのハンドルを待機に使うためのラッパー</summary>
    private sealed class ProcessWaitHandle : WaitHandle
    {
        /// <summary>ハンドルを借りて待機用にする</summary>
        /// <param name="handle">プロセスのハンドル</param>
        public ProcessWaitHandle(SafeWaitHandle handle)
        {
            // 待機用に借りるだけで、ハンドルの所有は PseudoConsole 側
            SafeWaitHandle = new SafeWaitHandle(handle.DangerousGetHandle(), ownsHandle: false);
        }
    }
}
