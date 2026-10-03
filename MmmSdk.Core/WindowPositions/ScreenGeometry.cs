using System.Drawing;

namespace MmmSdk.Core.WindowPositions;

/// <summary>
/// ウィンドウと画面の位置関係の計算。状態を持たず、設定ストアにも依存しない。
/// </summary>
public static class ScreenGeometry
{
    /// <summary>ウィンドウ全体が作業領域に収まるよう、位置をずらす</summary>
    /// <param name="position">ウィンドウの左上の位置（物理ピクセル）</param>
    /// <param name="size">ウィンドウの大きさ（物理ピクセル）</param>
    /// <param name="workArea">収める作業領域</param>
    /// <returns>作業領域に収まるようにずらした左上の位置</returns>
    /// <remarks>上下左右のはみ出しを内側へ寄せる。作業領域より大きいときは左上に合わせる。</remarks>
    public static Point ClampToWorkArea(Point position, Size size, Rectangle workArea)
    {
        var x = Math.Min(position.X, workArea.X + workArea.Width - size.Width);
        var y = Math.Min(position.Y, workArea.Y + workArea.Height - size.Height);
        return new Point(Math.Max(x, workArea.X), Math.Max(y, workArea.Y));
    }

    /// <summary>ウィンドウが、いずれかの作業領域に十分に見えているか</summary>
    /// <param name="window">ウィンドウの矩形（物理ピクセル）。</param>
    /// <param name="workAreas">全モニタの作業領域。</param>
    /// <param name="minRatio">見えるとみなす面積割合（0〜1）。</param>
    /// <returns>十分に見えていれば true</returns>
    /// <remarks>全作業領域とウィンドウ矩形の重なる面積を合計し、ウィンドウ面積に対する割合が <paramref name="minRatio"/> 以上なら見えるとみなす。</remarks>
    public static bool IsVisibleEnough(Rectangle window, IReadOnlyList<Rectangle> workAreas, double minRatio)
    {
        var windowArea = (long)window.Width * window.Height;
        if (windowArea <= 0) return false;

        long visible = 0;
        foreach (var area in workAreas)
        {
            var overlap = Rectangle.Intersect(window, area);
            visible += (long)overlap.Width * overlap.Height;
        }

        return (double)visible / windowArea >= minRatio;
    }
}
