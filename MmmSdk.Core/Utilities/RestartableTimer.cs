namespace MmmSdk.Core.Utilities;

/// <summary>一定の間隔で待ち、待ちの途中で数え直せるタイマー (<see cref="PeriodicTimer"/> の代わり)</summary>
/// <param name="interval">待つ間隔</param>
/// <remarks>
/// 定期的な取り込みの途中で、利用者が手動で取り込んだときに、直後に自動の取り込みが重ならないよう、<see cref="Restart"/> で間隔を数え直す。
/// 間隔は、前の待ちが終わった時点から数える (処理に時間がかかっても、次の待ちが短くならない)。
/// スレッドセーフではないので、同じスレッド (UI スレッド)から使う。
/// </remarks>
public sealed class RestartableTimer(TimeSpan interval)
{
    /// <summary>今の待ちを数え直すためのソース (待っていないときは null)</summary>
    private CancellationTokenSource? _waiting;

    /// <summary>次の間隔が来るまで待つ</summary>
    /// <param name="cancellationToken">待ちを止めるためのトークン</param>
    /// <returns>間隔が来たら true</returns>
    /// <exception cref="OperationCanceledException">トークンが取り消された。</exception>
    public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            using var waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _waiting = waiting;
            try
            {
                await Task.Delay(interval, waiting.Token);
                return true;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // 数え直しが求められた (間隔を最初から待ち直す)
            }
            finally
            {
                // 取り消された待ちの後始末が、あとから始まった待ちの印を消さないよう、自分の待ちのときだけ外す
                if (_waiting == waiting)
                {
                    _waiting = null;
                }
            }
        }
    }

    /// <summary>待っている間隔を、最初から数え直す (待っていないときは何もしない)</summary>
    public void Restart() => _waiting?.Cancel();
}
