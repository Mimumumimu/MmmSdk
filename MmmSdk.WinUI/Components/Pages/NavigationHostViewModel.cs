using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Features;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーの項目と選択を持つ ViewModel</summary>
public sealed partial class NavigationHostViewModel : ObservableObject
{
    /// <summary>各機能が登録したページ (登録順)</summary>
    private readonly IReadOnlyList<NavigationPage> _pages;

    /// <summary>機能のオン・オフ</summary>
    private readonly FeatureService _features;

    /// <summary>登録されたページから、サイドバーの項目を作る</summary>
    /// <param name="pages">各機能が登録したページ</param>
    /// <param name="features">機能のオン・オフ</param>
    /// <remarks>オンの機能の項目だけを並べる。最初は上部の先頭の項目を選択した状態にする。</remarks>
    public NavigationHostViewModel(IEnumerable<NavigationPage> pages, FeatureService features)
    {
        _pages = [.. pages];
        _features = features;
        MenuItems = [];
        FooterItems = [];
        Refresh();
        SelectedItem = MenuItems.FirstOrDefault();
    }

    /// <summary>サイドバー上部の項目</summary>
    public ObservableCollection<NavigationItem> MenuItems { get; }

    /// <summary>サイドバー下部の項目</summary>
    public ObservableCollection<NavigationItem> FooterItems { get; }

    /// <summary>選択中の項目</summary>
    [ObservableProperty]
    public partial NavigationItem? SelectedItem { get; set; }

    #region 移動の履歴 (戻る・進む)

    /// <summary>履歴に残す項目の最大数 (古いものから捨てる)</summary>
    private const int MaxHistory = 100;

    /// <summary>選んだ順の履歴</summary>
    private readonly List<NavigationItem> _history = [];

    /// <summary>履歴の中の、いま選んでいる位置</summary>
    private int _position = -1;

    /// <summary>戻る・進むで選択を変えている最中か (履歴に足さないため)</summary>
    private bool _isTraversing;

    /// <summary>戻る・進むで、ページを移ったとき</summary>
    /// <remarks>移る前のページのフォーカスがあった要素は、画面から外れる。フォーカスの行き先を、画面の側で決めるために知らせる。</remarks>
    public event EventHandler? HistoryTraversed;

    /// <summary>ひとつ前に開いたページへ戻れるか</summary>
    public bool CanGoBack => FindTarget(-1) >= 0;

    /// <summary>戻る前に開いていたページへ進めるか</summary>
    public bool CanGoForward => FindTarget(1) >= 0;

    /// <summary>ひとつ前に開いたページへ戻る</summary>
    /// <remarks>機能をオフにして、サイドバーから消えた項目と、別のウィンドウに外している項目は飛ばす。</remarks>
    public void GoBack() => Traverse(-1);

    /// <summary>戻る前に開いていたページへ進む</summary>
    /// <remarks>機能をオフにして、サイドバーから消えた項目と、別のウィンドウに外している項目は飛ばす。</remarks>
    public void GoForward() => Traverse(1);

    /// <summary>選択中の項目を外すとき、メインの表示領域に出す別の項目を選ぶ</summary>
    /// <param name="detached">外す項目</param>
    /// <remarks>履歴をひとつ前、無ければひとつ先、それも無ければ、サイドバーの先頭にある、外していない項目。1 つも無いときは、選択を外す。</remarks>
    public void SelectAnotherThan(NavigationItem detached)
    {
        if (SelectedItem != detached)
        {
            return;
        }

        if (CanGoBack)
        {
            GoBack();
        }
        else if (CanGoForward)
        {
            GoForward();
        }
        else
        {
            SelectedItem = MenuItems.Concat(FooterItems).FirstOrDefault(i => i != detached && !i.IsDetached);
        }
    }

    /// <summary>選択が変わったら、履歴に足す</summary>
    /// <param name="value">新しい選択</param>
    /// <remarks>戻る・進むの途中で別のページを選んだら、その先の履歴は捨てる。ブラウザーと同じ動き。</remarks>
    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (!_isTraversing && value is { IsDetached: false } && (_position < 0 || _history[_position] != value))
        {
            _history.RemoveRange(_position + 1, _history.Count - _position - 1);
            _history.Add(value);
            if (_history.Count > MaxHistory)
            {
                _history.RemoveAt(0);
            }
            _position = _history.Count - 1;
        }
        NotifyHistoryChanged();
    }

    /// <summary>履歴を、向きに沿って 1 つ動かす</summary>
    /// <param name="direction">戻るなら -1、進むなら 1</param>
    private void Traverse(int direction)
    {
        var target = FindTarget(direction);
        if (target < 0)
        {
            return;
        }

        _isTraversing = true;
        try
        {
            _position = target;
            SelectedItem = _history[target];
        }
        finally
        {
            _isTraversing = false;
        }
        NotifyHistoryChanged();
        HistoryTraversed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>向きに沿って、いまサイドバーに出ている項目の位置を探す</summary>
    /// <param name="direction">戻るなら -1、進むなら 1</param>
    /// <returns>履歴の中の位置。無ければ -1</returns>
    private int FindTarget(int direction)
    {
        for (var i = _position + direction; i >= 0 && i < _history.Count; i += direction)
        {
            if (_history[i] != SelectedItem && !_history[i].IsDetached && (MenuItems.Contains(_history[i]) || FooterItems.Contains(_history[i])))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>戻る・進むが使えるかの変化を知らせる</summary>
    private void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    #endregion

    /// <summary>機能のオン・オフ・並び順に合わせて、サイドバーの項目を作り直す</summary>
    /// <remarks>
    /// 選択中の項目が消えるときは、先に上部の先頭の項目へ移す (ページの切り替えは、選択の変更で行われる)。
    /// 子の項目は、サイドバーに出ている親の項目の直後に並べる (親がオフなら、子も出さない)。
    /// </remarks>
    public void Refresh()
    {
        var top = TopItems();
        var footer = EnabledItems(NavigationArea.Footer);

        if (SelectedItem is { } selected && !top.Contains(selected) && !footer.Contains(selected))
        {
            SelectedItem = top.FirstOrDefault(i => !i.IsDetached);
        }

        Sync(MenuItems, top, SelectedItem);
        Sync(FooterItems, footer, SelectedItem);
        NotifyHistoryChanged();
    }

    /// <summary>上部の項目を、親の項目と、その直後の子の項目を並べて取得する</summary>
    /// <returns>サイドバーの上部に出す項目 (親 → その子の順)</returns>
    private List<NavigationItem> TopItems()
        => [.. EnabledItems(NavigationArea.Top).SelectMany(parent => ChildItems(parent).Prepend(parent))];

    /// <summary>場所ごとの、オンの機能の項目 (子の項目を除く)を取得する</summary>
    /// <param name="area">サイドバーの中の場所</param>
    /// <returns>並び順の項目 (機能の並びは利用者が決めた順。同じ値は登録順)</returns>
    private List<NavigationItem> EnabledItems(NavigationArea area)
        => [.. _pages.Where(p => p.ParentKey is null && p.Area == area && _features.IsEnabled(p.FeatureKey))
            .OrderBy(p => _features.OrderOf(p.FeatureKey, p.Order))
            .Select(p => p.Item)];

    /// <summary>親の項目の下に出す、オンの機能の子の項目を取得する</summary>
    /// <param name="parent">親の項目</param>
    /// <returns>並び順の項目 (機能の並びは利用者が決めた順。同じ値は登録順)</returns>
    private List<NavigationItem> ChildItems(NavigationItem parent)
        => [.. _pages.Where(p => p.ParentKey == parent.Key && _features.IsEnabled(p.FeatureKey))
            .OrderBy(p => _features.OrderOf(p.FeatureKey, p.Order))
            .Select(p => p.Item)];

    /// <summary>一覧を、足りない項目を足し、余る項目を消し、順序が違うときは、選択中の項目のまわりに並べ直して合わせる</summary>
    /// <param name="target">合わせる一覧 (画面にバインドしているので、作り直さず、項目ごとに足し引きする)</param>
    /// <param name="desired">あるべき項目 (順序つき)</param>
    /// <param name="selected">選択中の項目 (外すと選択が外れるので、残したまま、まわりを並べ直す)</param>
    /// <remarks>
    /// 並べ替えに <c>Move</c> は使わない (<c>NavigationView</c> が、移動の通知を画面に反映しなかったため)。
    /// 追加と削除の通知だけで並べ直す。
    /// </remarks>
    private static void Sync(ObservableCollection<NavigationItem> target, List<NavigationItem> desired, NavigationItem? selected)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(target[i]))
            {
                target.RemoveAt(i);
            }
        }

        // 足りない項目を足したあとの並びが、あるべき並びと同じなら、何もしない
        var missing = desired.Where(d => !target.Contains(d)).ToList();
        if (missing.Count == 0 && target.SequenceEqual(desired))
        {
            return;
        }

        var keep = selected is not null && desired.Contains(selected) && target.Contains(selected) ? selected : null;
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (target[i] != keep)
            {
                target.RemoveAt(i);
            }
        }

        for (var i = 0; i < desired.Count; i++)
        {
            if (desired[i] != keep)
            {
                target.Insert(i, desired[i]);
            }
        }
    }
}
