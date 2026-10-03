namespace MmmSdk.Core.WindowPositions;

/// <summary>ウィンドウの位置と大きさ（物理ピクセル）</summary>
/// <param name="X">左端</param>
/// <param name="Y">上端</param>
/// <param name="Width">幅</param>
/// <param name="Height">高さ</param>
public sealed record WindowBounds(int X, int Y, int Width, int Height);
