using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmSdk.WinUI.Components.Errors;

/// <summary>画面に出すエラー 1 件の状態（メッセージと、表示中か）</summary>
/// <remarks>
/// ViewModel が <c>Error</c> として 1 つ持ち、XAML の <c>InfoBar</c> に結び付ける
/// （<c>IsOpen="{x:Bind ViewModel.Error.IsOpen, Mode=TwoWay}"</c>、<c>Message="{x:Bind ViewModel.Error.Message, Mode=OneWay}"</c>）。
/// <c>InfoBar</c> の閉じるボタンは TwoWay の結び付けで <see cref="IsOpen"/> を false にするので、閉じる処理は書かなくてよい。
/// </remarks>
public sealed partial class ErrorState : ObservableObject
{
    /// <summary>エラーメッセージ。無ければ null</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>エラーを表示中か</summary>
    /// <remarks>ユーザーが <c>InfoBar</c> を閉じると false になる（メッセージは残る）。</remarks>
    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    /// <summary>エラーを表示する</summary>
    /// <param name="message">表示するエラーメッセージ</param>
    public void Show(string message) => Set(message);

    /// <summary>エラーを設定する（null なら消す）</summary>
    /// <param name="message">表示するエラーメッセージ。無ければ null</param>
    /// <remarks>読み込み結果のエラー（あるときだけ表示）をそのまま渡せる。</remarks>
    public void Set(string? message)
    {
        Message = message;
        IsOpen = message is not null;
    }

    /// <summary>エラーを消す</summary>
    public void Clear() => Set(null);
}
