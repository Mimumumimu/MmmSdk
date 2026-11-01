using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Features;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーの項目に対応するページを作って持つ</summary>
/// <param name="pages">登録されたページ</param>
/// <param name="features">機能のオン・オフ</param>
/// <param name="scopeFactory">ページごとのスコープを作る</param>
/// <remarks>
/// ページは初回の表示で、ページごとのスコープから DI で作り、以後は同じインスタンスを使う。入力中の内容やターミナルを保つため。
/// 機能がオフの間はページを作らず、オフにしたときは、作ったページを捨てる。捨てるのは <see cref="EvictDisabledPages"/>。
/// スコープごとに作るのは、捨てたページが持つ <c>IDisposable</c>(ターミナルのセッションなど)を、スコープの破棄で解放するため。
/// ルートのプロバイダーから作ると、オン・オフのたびに増えて、Host の破棄まで残る。残りのスコープは、この <c>Dispose</c> で破棄する。呼ばれるのは Host の破棄のとき。
/// 別のウィンドウへ移すとき、<see cref="IHandoverPage"/> を持つページは、スコープを捨てず、同じスコープから、<see cref="RebuildAsDetached"/> と <see cref="RebuildAsMain"/> で作り直す。ViewModel やシェルなど、スコープが持つものを続けるため。
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

    /// <summary>メインの表示領域のページが、画面だけを作り直して引き継げるページなら、それを返す</summary>
    /// <param name="key">項目のキー</param>
    /// <returns>引き継げるページ。ページが無い・引き継げないページなら null</returns>
    public IHandoverPage? FindMainHandoverPage(string key)
        => _cache.TryGetValue(key, out var cached) ? cached.Page as IHandoverPage : null;

    /// <summary>メインのページを、別のウィンドウに出すページへ作り直す</summary>
    /// <param name="key">項目のキー</param>
    /// <returns>新しいページ。メインのページが無い・登録されていない項目なら null</returns>
    /// <remarks>
    /// ページを作ったスコープは捨てず、同じスコープから新しいページを作る。ViewModel やシェルなど、スコープが持つものは、そのまま続く。
    /// 古いページは、呼ぶ前に <see cref="IHandoverPage.TakeHandover"/> で、画面を閉じておくこと。
    /// </remarks>
    public Page? RebuildAsDetached(string key) => Rebuild(key, _cache, _detached);

    /// <summary>別のウィンドウに出していたページを、メインのページへ作り直す</summary>
    /// <param name="key">項目のキー</param>
    /// <returns>新しいページ。別のウィンドウのページが無い・登録されていない項目なら null</returns>
    /// <remarks>
    /// ページを作ったスコープは捨てず、同じスコープから新しいページを作る。
    /// 古いページは、呼ぶ前に <see cref="IHandoverPage.TakeHandover"/> で、画面を閉じておくこと。
    /// </remarks>
    public Page? RebuildAsMain(string key) => Rebuild(key, _detached, _cache);

    /// <summary>ページを、同じスコープから作り直して、持ち場を移す</summary>
    /// <param name="key">項目のキー</param>
    /// <param name="from">古いページを持っている側</param>
    /// <param name="to">新しいページを持たせる側</param>
    /// <returns>新しいページ。古いページが無い・登録されていない項目なら null</returns>
    private Page? Rebuild(
        string key,
        Dictionary<string, (Page Page, IServiceScope Scope)> from,
        Dictionary<string, (Page Page, IServiceScope Scope)> to)
    {
        if (pages.FirstOrDefault(p => p.Item.Key == key) is not { } registration || !from.Remove(key, out var old))
        {
            return null;
        }

        var page = (Page)old.Scope.ServiceProvider.GetRequiredService(registration.PageType);
        to[key] = (page, old.Scope);
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
