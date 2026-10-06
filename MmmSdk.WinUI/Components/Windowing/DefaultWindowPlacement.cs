namespace MmmSdk.WinUI.Components.Windowing;

/// <summary>保存した位置が無いときの、ウィンドウの初期位置</summary>
public enum DefaultWindowPlacement
{
    /// <summary>位置は Windows に任せる</summary>
    System,

    /// <summary>主モニターの作業領域の右下 (端から少し離す)</summary>
    PrimaryBottomRight,
}
