using Windows.Graphics;

namespace MmmSdk.WinUI.Windowing;

/// <summary>ウィンドウを作業領域の中に収める計算</summary>
public static class WindowPlacement
{
    /// <summary>ウィンドウ全体が作業領域に収まるよう、位置をずらす</summary>
    /// <param name="position">ウィンドウの左上の位置（物理ピクセル）</param>
    /// <param name="size">ウィンドウの大きさ（物理ピクセル）</param>
    /// <param name="workArea">収める作業領域</param>
    /// <returns>作業領域に収まるようにずらした左上の位置</returns>
    /// <remarks>上下左右のはみ出しを内側へ寄せる。作業領域より大きいときは左上に合わせる。</remarks>
    public static PointInt32 ClampToWorkArea(PointInt32 position, SizeInt32 size, RectInt32 workArea)
    {
        var x = Math.Min(position.X, workArea.X + workArea.Width - size.Width);
        var y = Math.Min(position.Y, workArea.Y + workArea.Height - size.Height);
        return new PointInt32(Math.Max(x, workArea.X), Math.Max(y, workArea.Y));
    }
}
