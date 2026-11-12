namespace MmmSdk.Core.Components.WindowPositions;

/// <summary>ウィンドウの位置</summary>
/// <param name="X">左端</param>
/// <param name="Y">上端</param>
/// <remarks>画面全体の物理ピクセル座標での左上</remarks>
public sealed record WindowPosition(int X, int Y);
