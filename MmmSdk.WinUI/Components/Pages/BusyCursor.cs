using Microsoft.UI.Dispatching;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary><see cref="IBusyCursor"/> の実装 (始めた数を数える)</summary>
public sealed class BusyCursor : IBusyCursor
{
    /// <summary>続いている処理待ちの数</summary>
    private int _count;

    /// <inheritdoc />
    public bool IsBusy => _count > 0;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public IDisposable Begin()
    {
        _count++;
        if (_count == 1)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        return new Scope(this);
    }

    /// <inheritdoc />
    public Task RunAsync(Action work)
        => RunAsync(() =>
        {
            work();
            return Task.CompletedTask;
        });

    /// <inheritdoc />
    public async Task RunAsync(Func<Task> work)
    {
        using var scope = Begin();
        await WaitForRenderAsync();
        await work();

        // 表の行・見出しは、処理が終わったあとのレイアウトで作られる。その描画が済んでから、砂時計を終える
        await WaitForRenderAsync();
        await WaitForRenderAsync();
    }

    /// <summary>画面の描画が済むまで待つ (UI スレッドで呼ぶ)</summary>
    /// <returns>待ちの完了を表すタスク</returns>
    /// <remarks>低い優先度で呼ばれるので、すでに待っている描画・入力のあとに動く。</remarks>
    private static Task WaitForRenderAsync()
    {
        var source = new TaskCompletionSource();
        var queue = DispatcherQueue.GetForCurrentThread();
        if (queue is null || !queue.TryEnqueue(DispatcherQueuePriority.Low, () => source.SetResult()))
        {
            source.SetResult();
        }
        return source.Task;
    }

    /// <summary>処理待ちを 1 つ終える</summary>
    private void End()
    {
        _count--;
        if (_count == 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>1 回の <see cref="Begin"/> に対応する入れ物</summary>
    /// <param name="owner">数えている <see cref="BusyCursor"/></param>
    private sealed class Scope(BusyCursor owner) : IDisposable
    {
        /// <summary>もう終えたか</summary>
        private bool _disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            owner.End();
        }
    }
}
