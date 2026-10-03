using System.Drawing;
using Microsoft.UI.Windowing;
using MmmSdk.Core.Components.WindowPositions;
using Windows.Graphics;

namespace MmmSdk.WinUI.Utilities;

/// <summary>ウィンドウを作業領域の中に収める計算</summary>
public static class WindowPlacement
{
    /// <summary>ウィンドウ全体が作業領域に収まるよう、位置をずらす</summary>
    /// <param name="position">ウィンドウの左上の位置（物理ピクセル）</param>
    /// <param name="size">ウィンドウの大きさ（物理ピクセル）</param>
    /// <param name="workArea">収める作業領域</param>
    /// <returns>作業領域に収まるようにずらした左上の位置</returns>
    /// <remarks>計算は <see cref="ScreenGeometry.ClampToWorkArea"/>（型の変換だけをここで行う）。</remarks>
    public static PointInt32 ClampToWorkArea(PointInt32 position, SizeInt32 size, RectInt32 workArea)
    {
        var clamped = ScreenGeometry.ClampToWorkArea(
            new Point(position.X, position.Y),
            new Size(size.Width, size.Height),
            new Rectangle(workArea.X, workArea.Y, workArea.Width, workArea.Height));
        return new PointInt32(clamped.X, clamped.Y);
    }

    /// <summary>ウィンドウの矩形が、全モニターの作業領域に対して、十分に見えているか</summary>
    /// <param name="window">ウィンドウの矩形（物理ピクセル）</param>
    /// <param name="minRatio">見えている割合の下限（0〜1。面積に対する割合）</param>
    /// <returns>十分に見えていれば true（保存した位置が、今のモニター構成で画面外にならないかの判定に使う）</returns>
    public static bool IsVisibleEnough(RectInt32 window, double minRatio)
    {
        // FindAll の返り値は foreach で列挙すると InvalidCastException になることがあるので、Count とインデクサで回す
        var displays = DisplayArea.FindAll();
        var workAreas = new List<Rectangle>(displays.Count);
        for (var i = 0; i < displays.Count; i++)
        {
            var work = displays[i].WorkArea;
            workAreas.Add(new Rectangle(work.X, work.Y, work.Width, work.Height));
        }

        return ScreenGeometry.IsVisibleEnough(new Rectangle(window.X, window.Y, window.Width, window.Height), workAreas, minRatio);
    }
}
