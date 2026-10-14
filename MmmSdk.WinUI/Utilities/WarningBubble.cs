using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace MmmSdk.WinUI.Utilities;

/// <summary>入力した直後に、要素の下へ短く出して、自然に消える警告の吹き出し</summary>
/// <remarks>
/// 入力のじゃまをしないよう、フォーカスを取らず、クリックも受けない (吹き出しの下の要素を、そのまま押せる)。
/// 決まった秒数のあとにフェードして消える。<see cref="Hide"/> か、次の <see cref="Show"/> では、すぐ消える。
/// 色は、テーマの標準の注意の色 (<c>SystemFillColorCautionBrush</c>・<c>SystemFillColorCautionBackgroundBrush</c>)。
/// ライト・ダーク・ハイコントラストへの切り替えに、標準のとおり追従する。
/// </remarks>
public sealed class WarningBubble
{
    /// <summary>吹き出しの見た目 (色は、テーマの標準の注意の色を、出すときに引く)</summary>
    private const string BubbleXaml = """
        <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                Background="{ThemeResource SystemFillColorCautionBackgroundBrush}"
                BorderBrush="{ThemeResource SystemFillColorCautionBrush}"
                BorderThickness="1"
                CornerRadius="6"
                Padding="10,6,12,6"
                IsHitTestVisible="False"
                Opacity="0">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <FontIcon Glyph="&#xE7BA;" FontFamily="Segoe Fluent Icons" FontSize="14"
                          Foreground="{ThemeResource SystemFillColorCautionBrush}" VerticalAlignment="Center" />
                <TextBlock TextWrapping="NoWrap" VerticalAlignment="Center" />
            </StackPanel>
        </Border>
        """;

    /// <summary>吹き出しを出しておく時間の既定</summary>
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(4);

    /// <summary>フェードして出る・消えるのにかける時間</summary>
    private static readonly Duration FadeDuration = new(TimeSpan.FromMilliseconds(150));

    /// <summary>吹き出しと要素の間の余白 (ピクセル)</summary>
    private const double Gap = 2;

    /// <summary>吹き出しを出す入れ物 (出していないときは null)</summary>
    private Popup? _popup;

    /// <summary>消えるまでの時間を測るタイマー</summary>
    private DispatcherQueueTimer? _timer;

    /// <summary>吹き出しを、要素の下に出す</summary>
    /// <param name="target">出す場所の要素 (入力したセルなど。この要素の左下に合わせる)</param>
    /// <param name="message">警告の文</param>
    /// <param name="duration">出しておく時間。省略すると 4 秒</param>
    /// <remarks>すでに出ている吹き出しは、すぐ消す。要素が画面の下端・右端にかかるときは、収まる位置にずらす (下に収まらなければ、要素の上に出す)。</remarks>
    public void Show(FrameworkElement target, string message, TimeSpan? duration = null)
    {
        Hide(animate: false);
        if (target.XamlRoot is not { } root)
        {
            return;
        }

        var bubble = (Border)XamlReader.Load(BubbleXaml);
        ((TextBlock)((StackPanel)bubble.Child).Children[1]).Text = message;
        bubble.RequestedTheme = target.ActualTheme;
        AutomationProperties.SetLiveSetting(bubble, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(bubble, message);

        bubble.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = bubble.DesiredSize;
        var origin = target.TransformToVisual(null).TransformPoint(new Point(0, 0));
        var x = Math.Max(0, Math.Min(origin.X, root.Size.Width - size.Width - Gap));
        var below = origin.Y + target.ActualHeight + Gap;
        var y = below + size.Height > root.Size.Height ? Math.Max(0, origin.Y - size.Height - Gap) : below;

        _popup = new Popup
        {
            XamlRoot = root,
            Child = bubble,
            IsLightDismissEnabled = false,
            HorizontalOffset = x,
            VerticalOffset = y,
            IsOpen = true,
        };
        Fade(bubble, to: 1, completed: null);
        Announce(bubble);

        _timer = target.DispatcherQueue.CreateTimer();
        _timer.Interval = duration ?? DefaultDuration;
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => Hide(animate: true);
        _timer.Start();
    }

    /// <summary>吹き出しを消す (出ていなければ、何もしない)</summary>
    /// <param name="animate">フェードして消すなら true。すぐ消すなら false</param>
    public void Hide(bool animate = false)
    {
        _timer?.Stop();
        _timer = null;
        if (_popup is not { } popup)
        {
            return;
        }

        _popup = null;
        if (animate && popup.Child is Border bubble)
        {
            Fade(bubble, to: 0, completed: () => popup.IsOpen = false);
        }
        else
        {
            popup.IsOpen = false;
        }
    }

    /// <summary>画面の読み上げ (スクリーンリーダー)に、吹き出しが出たことを知らせる</summary>
    /// <param name="bubble">吹き出し</param>
    /// <remarks>ライブ領域の指定だけでは、中身が出たことは伝わらない。ライブ領域の変更の通知を、自分で上げる。</remarks>
    private static void Announce(Border bubble)
    {
        if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            (FrameworkElementAutomationPeer.FromElement(bubble) ?? FrameworkElementAutomationPeer.CreatePeerForElement(bubble))
                .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
    }

    /// <summary>吹き出しの不透明度を、決まった時間で変える</summary>
    /// <param name="bubble">吹き出し</param>
    /// <param name="to">変えたあとの不透明度</param>
    /// <param name="completed">変え終わったときの処理 (無ければ null)</param>
    private static void Fade(Border bubble, double to, Action? completed)
    {
        var animation = new DoubleAnimation { To = to, Duration = FadeDuration };
        Storyboard.SetTarget(animation, bubble);
        Storyboard.SetTargetProperty(animation, "Opacity");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        if (completed is not null)
        {
            storyboard.Completed += (_, _) => completed();
        }
        storyboard.Begin();
    }
}
