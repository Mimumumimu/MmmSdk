namespace MmmSdk.Core.Components.Features;

/// <summary>起動時の機能ごとの準備</summary>
/// <remarks>
/// 設定の読み込みや監視の開始などを行う。
/// アプリの起動時に、ホストが登録した順に 1 つずつ実行する。
/// 登録は <see cref="FeatureServiceCollectionExtensions.AddStartupTask{TTask}"/> で行う。
/// オフにできる機能のものはオンの間だけ実行する。
/// 起動時にオフなら実行せず、あとでオンにしたときに実行する。
/// </remarks>
public interface IStartupTask
{
    /// <summary>準備をする</summary>
    /// <returns>準備の完了を表すタスク</returns>
    Task StartAsync();
}
