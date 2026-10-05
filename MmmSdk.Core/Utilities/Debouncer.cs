namespace MmmSdk.Core.Utilities;

/// <summary>入力が止まるのを待ってから処理を 1 回だけ行う (入力のたびに呼ばれても、最後の 1 回だけが動く)</summary>
/// <param name="delay">最後の呼び出しから処理までの待ち時間</param>
/// <remarks>
/// 待ったあとの処理 (<c>work</c>)はスレッドプールで行い、結果の受け取り (<c>apply</c>)は呼び出し元のコンテキスト (UI スレッドから呼べば UI スレッド)で行う。
/// 状態を持つので、使い道ごとに 1 つ作る。スレッドセーフではないので、同じスレッド (UI スレッド)から使う。
/// </remarks>
public sealed class Debouncer(TimeSpan delay)
{
    /// <summary>今の予約を取り消すためのソース</summary>
    private CancellationTokenSource? _current;

    /// <summary>予約中の処理を取り消す (動いていれば結果も捨てる)</summary>
    public void Cancel()
    {
        _current?.Cancel();
        _current?.Dispose();
        _current = null;
    }

    /// <summary>前回の予約を取り消して、待ってから処理を行い、結果を受け取る</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="work">待ったあとにスレッドプールで行う処理 (ファイルの存在確認など、UI スレッドで行いたくないもの)</param>
    /// <param name="apply">処理の結果を受け取る (取り消されていないときだけ呼ぶ)</param>
    /// <returns>予約した処理が終わる (取り消される)まで完了しないタスク</returns>
    /// <remarks>取り消されたときは例外にせず、何もせずに完了する。<c>work</c> の例外は、そのまま呼び出し元へ伝わる。</remarks>
    public async Task RunAsync<T>(Func<T> work, Action<T> apply)
    {
        Cancel();
        var source = _current = new CancellationTokenSource();
        try
        {
            await Task.Delay(delay, source.Token);
            var result = await Task.Run(work, source.Token);
            if (!source.IsCancellationRequested)
            {
                apply(result);
            }
        }
        catch (OperationCanceledException)
        {
            // 次の入力で予約し直された
        }
    }
}
