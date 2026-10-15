namespace MmmSdk.Core.Components.Features;

/// <summary>機能 (プラグイン)の登録中だけ、その機能の並び順の値を持つ</summary>
/// <remarks>
/// <see cref="FeatureServiceCollectionExtensions.AddFeaturePlugin{TPlugin}(Microsoft.Extensions.DependencyInjection.IServiceCollection, int, int?)"/> が、機能の登録 (<see cref="IFeaturePlugin.Register"/>)の間だけ値を入れる。
/// サイドバー・設定の部品・機能の一覧・トレイメニューの登録が、この値を並び順として記録する。
/// 機能ごとに並び順の値を渡さなくてよいようにするための仕組み (並びはホストが 1 か所で決める)。登録は 1 つのスレッドで順に行う前提。
/// </remarks>
public static class FeatureRegistrationScope
{
    /// <summary>登録中の機能の並び順の値</summary>
    [ThreadStatic]
    private static int s_order;

    /// <summary>登録中の機能のトレイメニューの並び順の値</summary>
    [ThreadStatic]
    private static int s_trayOrder;

    /// <summary>登録中の機能の並び順の値 (機能の登録の外では 0)</summary>
    public static int CurrentOrder => s_order;

    /// <summary>登録中の機能のトレイメニューの並び順の値 (機能の登録の外では 0)</summary>
    public static int CurrentTrayOrder => s_trayOrder;

    /// <summary>機能の登録を始める</summary>
    /// <param name="order">機能の並び順の値</param>
    /// <param name="trayOrder">トレイメニューの並び順の値</param>
    /// <returns>登録の終わりに破棄して、並び順の値を元に戻すためのもの</returns>
    internal static IDisposable Begin(int order, int trayOrder)
    {
        var scope = new Scope(s_order, s_trayOrder);
        s_order = order;
        s_trayOrder = trayOrder;
        return scope;
    }

    /// <summary>破棄すると、並び順の値を元に戻す</summary>
    /// <param name="previous">元の並び順の値</param>
    /// <param name="previousTray">元のトレイメニューの並び順の値</param>
    private sealed class Scope(int previous, int previousTray) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            s_order = previous;
            s_trayOrder = previousTray;
        }
    }
}
