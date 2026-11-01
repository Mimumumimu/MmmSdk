namespace MmmSdk.Core.Components.Features;

/// <summary>起動時に行う、機能ごとの準備 (設定の読み込み・監視の開始など)</summary>
/// <remarks>
/// アプリの起動時に、ホストが、登録した順に 1 つずつ実行する。
/// 登録は <see cref="FeatureServiceCollectionExtensions.AddStartupTask{TTask}"/> で行う。
/// オフにできる機能のものは、オンの間だけ実行する。起動時にオフなら実行せず、あとでオンにしたときに実行する。
/// </remarks>
public interface IStartupTask
{
    /// <summary>準備をする</summary>
    /// <returns>準備の完了を表すタスク</returns>
    Task StartAsync();
}
