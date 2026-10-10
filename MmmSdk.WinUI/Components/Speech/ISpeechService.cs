namespace MmmSdk.WinUI.Components.Speech;

/// <summary>
/// テキストを日本語の音声で読み上げる。
/// </summary>
/// <remarks>
/// Windows 標準の音声合成 (<c>Windows.Media.SpeechSynthesis</c>)を使う。UI スレッドから呼ぶ。
/// </remarks>
public interface ISpeechService
{
    /// <summary>読み上げの音量 (%)。保存値を範囲内に収めて返す (無ければ既定の 50)</summary>
    /// <remarks>
    /// アプリの再生音量で、Windows の音量 (ミキサー・全体)とは別。声と速さは変わらない (Windows の音声の設定に従う)。
    /// 保存値は、読み上げのたびに読んで使う。
    /// </remarks>
    int VolumePercent { get; }

    /// <summary>設定を保存できない状態か (設定ファイルを読めなかったため、音量を保存できない)</summary>
    bool IsVolumeReadOnly { get; }

    /// <summary>読み上げの音量 (%)を保存して、すぐ反映する</summary>
    /// <param name="percent">音量 (%)。範囲外は 0〜100 に収める</param>
    /// <returns>保存したら true。設定を保存できない状態 (<see cref="IsVolumeReadOnly"/>)で保存しなかったら false</returns>
    /// <remarks>読み上げ中なら、その音量も変わる。</remarks>
    Task<bool> SetVolumePercentAsync(int percent);

    /// <summary>テキストを読み上げる</summary>
    /// <param name="text">読み上げるテキスト。空白だけなら何もしない</param>
    /// <returns>音声の合成と再生の開始が済んだことを表すタスク (読み上げの終わりは待たない)</returns>
    /// <remarks>
    /// 読み上げ中に呼ぶと、前の読み上げを止めて新しいほうを読む (重ならない)。
    /// 日本語の音声が入っていない PC では、何も読まずに戻る。
    /// </remarks>
    Task SpeakAsync(string text);
}
