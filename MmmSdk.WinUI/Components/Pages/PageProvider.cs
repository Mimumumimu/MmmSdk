using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Features;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーの項目に対応するページを作って持つ</summary>
/// <param name="pages">登録されたページ</param>
/// <param name="features">機能のオン・オフ</param>
/// <param name="scopeFactory">ページごとのスコープを作る</param>
/// <remarks>
/// ページは初回の表示で、ページごとのスコープから DI で作り、以後は同じインスタンスを使う (入力中の内容やターミナルを保つため)。
/// 機能がオフの間はページを作らず、オフにしたときは、作ったページを捨てる (<see cref="EvictDisabledPages"/>)。
/// スコープごとに作るのは、捨てたページが持つ <c>IDisposable</c>(ターミナルのセッションなど)を、スコープの破棄で解放するため
/// (ルートのプロバイダーから作ると、オン・オフのたびに増えて、Host の破棄まで残る)。残りのスコープは、この <c>Dispose</c>(Host の破棄)で破棄する。
/// </remarks>
public sealed class PageProvider(IEnumerable<NavigationPage> pages, FeatureService features, IServiceScopeFactory scopeFactory) : IPageCache, IDisposable
{
    /// <summary>作ったページと、それを作ったスコープ (項目のキー → ページ・スコープ)</summary>
    private readonly Dictionary<string, (Page Page, IServiceScope Scope)> _cache = [];

    /// <summary>ページを捨てる直前 (引数は、項目のキー)</summary>
    /// <remarks>ページを別のウィンドウに外しているとき、先にウィンドウを閉じるために知らせる。</remarks>
    public event EventHandler<string>? PageEvicting;

    /// <summary>項目に対応するページを取得する</summary>
    /// <param name="item">サイドバーの項目</param>
    /// <returns>ページ。登録されていない項目・機能がオフの項目なら null</returns>
    public Page? GetPage(NavigationItem item)
    {
        if (_cache.TryGetValue(item.Key, out var cached))
        {
            return cached.Page;
        }

        if (pages.FirstOrDefault(p => p.Item.Key == item.Key) is not { } registration
            || !features.IsEnabled(registration.FeatureKey))
        {
            return null;
        }

        var scope = scopeFactory.CreateScope();
        var page = (Page)scope.ServiceProvider.GetRequiredService(registration.PageType);
        _cache[item.Key] = (page, scope);
        return page;
    }

    /// <summary>別のウィンドウに外したページと、それを作ったスコープ (項目のキー → ページ・スコープ)</summary>
    /// <remarks>メインの表示領域のページ (<see cref="_cache"/>)とは別に作る。WebView2 を持つページは、作られたウィンドウから別のウィンドウへ移すと使えなくなるため。</remarks>
    private readonly Dictionary<string, (Page Page, IServiceScope Scope)> _detached = [];

    /// <inheritdoc />
    public TPage? GetCreatedPage<TPage>()
        where TPage : Page
    {
        var key = typeof(TPage).Name;
        return _cache.TryGetValue(key, out var cached) ? cached.Page as TPage
            : _detached.TryGetValue(key, out var detached) ? detached.Page as TPage
            : null;
    }

    /// <summary>別のウィンドウに出すページを、新しく作る</summary>
    /// <param name="item">サイドバーの項目</param>
    /// <returns>新しいページ。登録されていない項目・機能がオフの項目・すでに外している項目なら null</returns>
    /// <remarks>
    /// メインの表示領域のページとは別に、専用のスコープから作る。ウィンドウを閉じるときに <see cref="ReleaseDetachedPage"/> で破棄すること。
    /// 同じ機能のページが 2 つ生きたままにならないよう、呼ぶ前に、メインのページを <see cref="Evict"/> で捨てておく。
    /// </remarks>
    public Page? CreateDetachedPage(NavigationItem item)
    {
        if (_detached.ContainsKey(item.Key)
            || pages.FirstOrDefault(p => p.Item.Key == item.Key) is not { } registration
            || !features.IsEnabled(registration.FeatureKey))
        {
            return null;
        }

        var scope = scopeFactory.CreateScope();
        var page = (Page)scope.ServiceProvider.GetRequiredService(registration.PageType);
        _detached[item.Key] = (page, scope);
        return page;
    }

    /// <summary>別のウィンドウに出していたページを破棄する</summary>
    /// <param name="key">項目のキー</param>
    /// <remarks>ウィンドウから外したあとに呼ぶこと。後始末の流れは <see cref="Evict"/> と同じ。</remarks>
    public void ReleaseDetachedPage(string key)
    {
        if (_detached.Remove(key, out var detached))
        {
            (detached.Page as IReleasablePage)?.Release();
            detached.Scope.Dispose();
        }
    }

    /// <summary>メインの表示領域のために作ったページを捨てる</summary>
    /// <param name="key">項目のキー</param>
    /// <remarks>
    /// 画面の後始末が要るページ (<see cref="IReleasablePage"/>)は、先に <c>Release</c> を呼び、そのあと、ページを作ったスコープを破棄する。ターミナルのセッションなどを解放するため。
    /// 画面は、このページから別のページへ移したあとに呼ぶこと。表示中のページを捨てないため。
    /// 次に表示するときは、新しく作る。
    /// </remarks>
    public void Evict(string key)
    {
        if (_cache.Remove(key, out var cached))
        {
            (cached.Page as IReleasablePage)?.Release();
            cached.Scope.Dispose();
        }
    }

    /// <summary>オフになった機能のページを捨てる</summary>
    /// <remarks>
    /// ページを別のウィンドウに外しているときは、先にそのウィンドウを閉じるよう、<see cref="PageEvicting"/> で知らせる。
    /// 再びオンにしたときは、新しく作る。
    /// </remarks>
    public void EvictDisabledPages()
    {
        foreach (var registration in pages.Where(p => !features.IsEnabled(p.FeatureKey)))
        {
            PageEvicting?.Invoke(this, registration.Item.Key);
            Evict(registration.Item.Key);
        }
    }

    /// <summary>作ったページのスコープをすべて破棄する (Host の破棄時)</summary>
    public void Dispose()
    {
        foreach (var (_, scope) in _cache.Values.Concat(_detached.Values))
        {
            scope.Dispose();
        }
        _cache.Clear();
        _detached.Clear();
    }
}
