namespace MmmSdk.Core.Components.WindowPositions;

/// <summary>ウィンドウの位置と大きさ</summary>
/// <param name="X">左端</param>
/// <param name="Y">上端</param>
/// <param name="Width">幅</param>
/// <param name="Height">高さ</param>
/// <remarks>単位は物理ピクセル</remarks>
public sealed record WindowBounds(int X, int Y, int Width, int Height);
