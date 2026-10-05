namespace MmmSdk.WinUI.Components.Clipboards;

/// <summary>クリップボードのテキストの読み書き</summary>
/// <remarks>ViewModel から UI 型に触れずに使うための口。UI スレッドから呼ぶ。</remarks>
public interface IClipboardService
{
    /// <summary>クリップボードにテキストを載せる</summary>
    /// <param name="text">載せるテキスト</param>
    /// <exception cref="System.Runtime.InteropServices.COMException">クリップボードを他のアプリが使っているなどで、書けなかった。</exception>
    void SetText(string text);

    /// <summary>クリップボードのテキストを取り出す</summary>
    /// <returns>クリップボードのテキスト。テキストが無ければ null</returns>
    /// <exception cref="System.Runtime.InteropServices.COMException">クリップボードを他のアプリが使っているなどで、読めなかった。</exception>
    Task<string?> GetTextAsync();
}
