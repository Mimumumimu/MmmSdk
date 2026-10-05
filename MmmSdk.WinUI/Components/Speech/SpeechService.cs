using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace MmmSdk.WinUI.Components.Speech;

/// <summary>
/// Windows 標準の音声合成で、テキストを日本語の音声で読み上げる。アプリ全体で 1 つ。
/// </summary>
/// <remarks>
/// 合成器と再生器は 1 つを使い回す。新しく読むときは前の読み上げを差し替える (合成の途中で次の依頼が来たら、前の合成結果は捨てる)。
/// 声は、OS の既定の音声が日本語ならそれを、そうでなければ入っている日本語の音声の先頭を使う。
/// </remarks>
public sealed class SpeechService : ISpeechService, IDisposable
{
    /// <summary>日本語の音声の言語タグの先頭</summary>
    private const string JapanesePrefix = "ja";

    /// <summary>音声の合成器</summary>
    private readonly SpeechSynthesizer _synthesizer = new();

    /// <summary>音声の再生器</summary>
    /// <remarks>読み上げはメディアの再生ではないので、システムのメディア操作 (音量キーの表示・再生キー)とは連携させない。</remarks>
    private readonly MediaPlayer _player = new() { CommandManager = { IsEnabled = false } };

    /// <summary>今の依頼の通し番号 (合成の途中で次の依頼が来たかを調べる)</summary>
    private int _version;

    /// <summary>再生中 (または再生済み)の音声の元</summary>
    private MediaSource? _currentSource;

    /// <summary>再生中 (または再生済み)の音声のストリーム</summary>
    private SpeechSynthesisStream? _currentStream;

    /// <summary>破棄したか</summary>
    private bool _disposed;

    /// <inheritdoc />
    public async Task SpeakAsync(string text)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(text) || FindJapaneseVoice() is not { } voice)
        {
            return;
        }

        var version = ++_version;
        _synthesizer.Voice = voice;
        var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
        if (_disposed || version != _version)
        {
            stream.Dispose();
            return;
        }

        var source = MediaSource.CreateFromStream(stream, stream.ContentType);
        _player.Source = source;
        ReleaseCurrent();
        (_currentSource, _currentStream) = (source, stream);
        _player.Play();
    }

    /// <summary>再生器と合成器を破棄する</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _player.Dispose();
        ReleaseCurrent();
        _synthesizer.Dispose();
    }

    /// <summary>日本語の音声を探す</summary>
    /// <returns>既定の音声が日本語ならそれ、そうでなければ日本語の音声の先頭。無ければ null</returns>
    private static VoiceInformation? FindJapaneseVoice()
    {
        var defaultVoice = SpeechSynthesizer.DefaultVoice;
        return IsJapanese(defaultVoice) ? defaultVoice : SpeechSynthesizer.AllVoices.FirstOrDefault(IsJapanese);
    }

    /// <summary>音声が日本語か</summary>
    /// <param name="voice">調べる音声</param>
    /// <returns>日本語なら true</returns>
    private static bool IsJapanese(VoiceInformation voice)
        => voice.Language.StartsWith(JapanesePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>前の音声の元とストリームを解放する</summary>
    private void ReleaseCurrent()
    {
        _currentSource?.Dispose();
        _currentStream?.Dispose();
        (_currentSource, _currentStream) = (null, null);
    }
}
