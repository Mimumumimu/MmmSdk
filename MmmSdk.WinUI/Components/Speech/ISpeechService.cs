namespace MmmSdk.WinUI.Components.Speech;

/// <summary>
/// テキストを日本語の音声で読み上げる。
/// </summary>
/// <remarks>
/// Windows 標準の音声合成 (<c>Windows.Media.SpeechSynthesis</c>)を使う。UI スレッドから呼ぶ。
/// </remarks>
public interface ISpeechService
{
    /// <summary>テキストを読み上げる</summary>
    /// <param name="text">読み上げるテキスト。空白だけなら何もしない</param>
    /// <returns>音声の合成と再生の開始が済んだことを表すタスク (読み上げの終わりは待たない)</returns>
    /// <remarks>
    /// 読み上げ中に呼ぶと、前の読み上げを止めて新しいほうを読む (重ならない)。
    /// 日本語の音声が入っていない PC では、何も読まずに戻る。
    /// </remarks>
    Task SpeakAsync(string text);
}
