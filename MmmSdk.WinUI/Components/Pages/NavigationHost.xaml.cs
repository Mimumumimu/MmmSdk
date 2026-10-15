using System.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Features;

namespace MmmSdk.WinUI.Components.Pages;

/// <summary>サイドバーと、選んだ項目のページを出す部品 (アプリのウィンドウに載せて使う)</summary>
/// <remarks>
/// 登録されたページ (<see cref="NavigationPage"/>)と機能のオン・オフ・並び順に合わせて項目を作り直し、オフになった機能のページを捨てる。
/// ウィンドウ (タイトルバー・位置の保存・トレイへの退避)は、アプリが持つ。
/// </remarks>
public sealed partial class NavigationHost : UserControl
{
    /// <summary>項目に対応するページ</summary>
    private readonly PageProvider _pages;

    /// <summary>サイドバーの項目と選択</summary>
    public NavigationHostViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">サイドバーの項目と選択</param>
    /// <param name="pages">項目に対応するページ</param>
    /// <param name="features">機能のオン・オフ</param>
    public NavigationHost(NavigationHostViewModel viewModel, PageProvider pages, FeatureService features)
    {
        ViewModel = viewModel;
        _pages = pages;

        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        features.Changed += OnFeatureChanged;
        features.OrderChanged += (_, _) => ViewModel.Refresh();
        NavigateTo(ViewModel.SelectedItem);
    }

    /// <summary>機能のオン・オフが切り替わったら、サイドバーの項目を作り直し、オフにした機能のページを捨てる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="featureKey">切り替わった機能のキー</param>
    /// <remarks>表示中のページが消えるときは、項目の更新の中で別のページへ移る。ページを捨てるのは、そのあと (表示中のページを捨てないため)。</remarks>
    private void OnFeatureChanged(object? sender, string featureKey)
    {
        ViewModel.Refresh();
        _pages.EvictDisabledPages();
    }

    /// <summary>選択が変わったら、そのページへ移動する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationHostViewModel.SelectedItem))
        {
            NavigateTo(ViewModel.SelectedItem);
        }
    }

    /// <summary>項目に対応するページを表示する</summary>
    /// <param name="item">表示するナビゲーション項目</param>
    private void NavigateTo(NavigationItem? item)
    {
        if (item is null || _pages.GetPage(item) is not { } page)
        {
            return;
        }

        ContentFrame.Content = page;
    }
}
