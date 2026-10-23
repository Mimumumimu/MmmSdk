using System.Text;

namespace MmmSdk.WinUI.Components.Terminal;

/// <summary>ターミナルの出力に、指定した文字列が出たことを見つける</summary>
/// <remarks>
/// 色やカーソル移動などの制御文字と空白を除いて探す。画面を描き直す CLI は、文字の途中に制御文字を挟んだり、出力のかたまりの境目で文字列を分けたりするため。
/// 大文字小文字は区別しない。
/// 見つけたら一度だけ <see cref="Matched"/> を通知し、<see cref="ITerminalSession.InputWritten"/> (利用者の入力)があるまで、探すのを止める。
/// CLI は同じ確認の枠を何度も描き直すので、止めないと、利用者が別の場所に移ったあとも、描き直しのたびに通知が来る。
/// 利用者の入力の直後は、CLI が画面全体を描き直し、前に出た文字も、もう一度出力に出る。
/// そこで、入力のあとの出力が一度途切れる (<see cref="SettleQuietMilliseconds"/>)までは、探さない。
/// 考え中のスピナーなどで出力が途切れないときは、入力から <see cref="SettleMaxMilliseconds"/> で打ち切って探す。
/// 出力はバックグラウンドのスレッドから届くので、通知もそのスレッドから行う。
/// </remarks>
public sealed class TerminalOutputWatcher : IDisposable
{
    /// <summary>入力のあと、出力がこの時間 (ミリ秒)途切れたら、描き直しが済んだとして探し始める</summary>
    public const int SettleQuietMilliseconds = 150;

    /// <summary>入力のあと、出力が途切れなくても、この時間 (ミリ秒)で探し始める</summary>
    public const int SettleMaxMilliseconds = 500;

    /// <summary>探す対象のターミナル</summary>
    private readonly ITerminalSession _session;
    /// <summary>状態を守るロック</summary>
    private readonly Lock _gate = new();
    /// <summary>制御文字と空白を除いた、これまでの出力 (文字列の途中で分かれた分を探すために、末尾だけ残す)</summary>
    private readonly StringBuilder _buffer = new();
    /// <summary>探す文字列 (空白を除いたもの)</summary>
    private string[] _patterns = [];
    /// <summary>出力の末尾に残す文字数 (最も長い文字列より 1 短い)</summary>
    private int _tailLength;
    /// <summary>制御文字を読み飛ばしている途中の状態</summary>
    private ScanState _state = ScanState.Text;
    /// <summary>見つけて通知済みで、利用者の入力を待っているか</summary>
    private bool _matched;
    /// <summary>利用者の入力のあとの、描き直しが済むのを待っているか</summary>
    private bool _settling;
    /// <summary>利用者の入力があった時刻 (<see cref="Environment.TickCount64"/>)</summary>
    private long _inputTick;
    /// <summary>最後に出力が届いた時刻で、<see cref="Environment.TickCount64"/> の値。入力の直後は、入力の時刻</summary>
    private long _lastOutputTick;

    /// <summary>ターミナルの出力と入力を見張る</summary>
    /// <param name="session">見張るターミナル</param>
    public TerminalOutputWatcher(ITerminalSession session)
    {
        _session = session;
        _session.OutputReceived += OnOutputReceived;
        _session.InputWritten += OnInputWritten;
    }

    /// <summary>文字列が見つかった</summary>
    /// <remarks>引数は、空白を除いた、見つかった文字列。バックグラウンドのスレッドから通知される。</remarks>
    public event EventHandler<string>? Matched;

    /// <summary>探す文字列を、まとめて差し替える</summary>
    /// <param name="patterns">探す文字列</param>
    /// <remarks>空白と制御文字は無視する。空になる文字列は使わない。それまでの出力と、通知済みの状態は捨てる。</remarks>
    public void SetPatterns(IEnumerable<string> patterns)
    {
        var normalized = patterns
            .Select(Normalize)
            .Where(pattern => pattern.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        lock (_gate)
        {
            _patterns = normalized;
            _tailLength = normalized.Length == 0 ? 0 : normalized.Max(pattern => pattern.Length) - 1;
            Reset();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _session.OutputReceived -= OnOutputReceived;
        _session.InputWritten -= OnInputWritten;
    }

    /// <summary>出力が届いたときの処理</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="text">届いた出力</param>
    private void OnOutputReceived(object? sender, string text)
    {
        string? found;
        lock (_gate)
        {
            if (_matched || _patterns.Length == 0)
            {
                return;
            }

            if (_settling && !FinishSettling())
            {
                return;
            }

            Append(text);
            found = Find();
            if (found is null)
            {
                Trim();
            }
            else
            {
                _matched = true;
                _buffer.Clear();
            }
        }

        if (found is not null)
        {
            Matched?.Invoke(this, found);
        }
    }

    /// <summary>利用者の入力があったときの処理 (探すのを再開する)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnInputWritten(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            Reset();
            // 入力の直後は、CLI が画面全体を描き直すので、それが済むまで探さない
            _settling = true;
            _inputTick = _lastOutputTick = Environment.TickCount64;
        }
    }

    /// <summary>入力のあとの描き直しが済んだかを調べる (出力が届くたびに呼ぶ)</summary>
    /// <returns>済んだ (この出力から探す)なら true。まだ描き直しの最中なら false</returns>
    /// <remarks>出力が <see cref="SettleQuietMilliseconds"/> 途切れたとき、または入力から <see cref="SettleMaxMilliseconds"/> たったときに、済んだとする。</remarks>
    private bool FinishSettling()
    {
        var now = Environment.TickCount64;
        var quiet = now - _lastOutputTick >= SettleQuietMilliseconds;
        var expired = now - _inputTick >= SettleMaxMilliseconds;
        _lastOutputTick = now;
        if (!quiet && !expired)
        {
            return false;
        }

        // 描き直しの最中に捨てた分の途中から、続きを読まないように、ここから読み直す
        _settling = false;
        _buffer.Clear();
        _state = ScanState.Text;
        return true;
    }

    /// <summary>出力と状態を捨てて、探すのを再開できる状態に戻す</summary>
    private void Reset()
    {
        _buffer.Clear();
        _state = ScanState.Text;
        _matched = false;
    }

    /// <summary>出力から制御文字と空白を除いて、末尾に足す</summary>
    /// <param name="text">届いた出力</param>
    /// <remarks>制御文字がかたまりの境目で途切れても読み飛ばせるよう、途中の状態を持ち越す。</remarks>
    private void Append(string text)
    {
        foreach (var c in text)
        {
            switch (_state)
            {
                case ScanState.Text:
                    if (c == '\x1b')
                    {
                        _state = ScanState.Escape;
                    }
                    else if (!char.IsControl(c) && !char.IsWhiteSpace(c))
                    {
                        _buffer.Append(c);
                    }
                    break;
                case ScanState.Escape:
                    // ESC のあとの 0x20〜0x2F は、ESC ( B のような途中の文字。それ以外の 1 文字で終わる
                    if (c == '[')
                    {
                        _state = ScanState.Csi;
                    }
                    else if (c is ']' or 'P' or 'X' or '^' or '_')
                    {
                        _state = ScanState.String;
                    }
                    else if (c is < ' ' or > '/')
                    {
                        _state = ScanState.Text;
                    }
                    break;
                case ScanState.Csi:
                    // 終わりは 0x40〜0x7E の 1 文字。その前は引数 (0x20〜0x3F)
                    if (c is >= '@' and <= '~')
                    {
                        _state = ScanState.Text;
                    }
                    break;
                case ScanState.String:
                    // BEL か ESC \ で終わる
                    if (c == '\a')
                    {
                        _state = ScanState.Text;
                    }
                    else if (c == '\x1b')
                    {
                        _state = ScanState.StringEscape;
                    }
                    break;
                case ScanState.StringEscape:
                    _state = c == '\\' ? ScanState.Text : ScanState.String;
                    break;
            }
        }
    }

    /// <summary>出力に含まれている文字列を探す</summary>
    /// <returns>見つかった文字列。無ければ null</returns>
    private string? Find()
    {
        var output = _buffer.ToString();
        return _patterns.FirstOrDefault(pattern => output.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>出力の末尾だけを残す (文字列の途中で分かれた分を、次の出力と合わせて探せるように)</summary>
    private void Trim()
    {
        if (_buffer.Length > _tailLength)
        {
            _buffer.Remove(0, _buffer.Length - _tailLength);
        }
    }

    /// <summary>探す文字列から、空白と制御文字を除く</summary>
    /// <param name="pattern">探す文字列</param>
    /// <returns>空白と制御文字を除いた文字列</returns>
    private static string Normalize(string pattern)
        => string.Concat(pattern.Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c)));

    /// <summary>制御文字を読み飛ばしている途中の状態</summary>
    private enum ScanState
    {
        /// <summary>ふつうの文字</summary>
        Text,

        /// <summary>ESC のあと</summary>
        Escape,

        /// <summary>ESC [ のあと (色やカーソル移動など)</summary>
        Csi,

        /// <summary>OSC・DCS などの文字列の制御 (BEL か ESC \ で終わる)</summary>
        String,

        /// <summary>文字列の制御の中で、ESC のあと</summary>
        StringEscape,
    }
}
