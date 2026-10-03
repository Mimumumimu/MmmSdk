using System.Drawing;

namespace MmmSdk.Core.WindowPositions;

/// <summary>
/// ウィンドウと画面の位置関係の計算。状態を持たず、設定ストアにも依存しない。
/// </summary>
public static class ScreenGeometry
{
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
