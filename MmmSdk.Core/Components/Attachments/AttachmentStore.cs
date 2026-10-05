using System.Runtime.Versioning;
using MmmSdk.Core.Components.Shells;

namespace MmmSdk.Core.Components.Attachments;

/// <summary>添付ファイルの一時保存先 (%TEMP%\アプリ名\session_日時\)の管理</summary>
/// <remarks>
/// 保存先ごとに 1 つ (Windows の %TEMP% と、WSL の /tmp (<see cref="ForWsl"/>))。
/// 保存するのは、元がファイルではないもの (貼り付けた画像など)だけ。ディスク上にあるファイルは、コピーせずに元のパスを使う想定なので、ファイルをコピーする口は持たない。
/// <list type="bullet">
/// <item>最初の添付でセッションフォルダを作り、以降は連番を付けて保存する (同名でも衝突しない)</item>
/// <item>添付を全部取り除いたらフォルダごと削除して初期化する</item>
/// <item>送信済みのファイルは CLI が後から読むため、アプリ終了まで残す (WSL の /tmp では、終了しても消さない)</item>
/// <item>フォルダは、同じアプリの別ビルド (Debug / Release など、別の場所の EXE)と共有する</item>
/// </list>
/// </remarks>
public sealed class AttachmentStore : IDisposable
{
    /// <summary>古い一時フォルダとみなす経過時間。</summary>
    /// <remarks>
    /// この時間より古いものは、前回までの残り (異常終了など)とみなして消す。
    /// フォルダを共有するので、同時に動いている別ビルドの古いセッションも消えるが、想定内とする (添付は CLI が読み込めば用済みの一時ファイルのため)。
    /// 作ったばかりのものは消さないよう、時間で線を引く。
    /// </remarks>
    private static readonly TimeSpan StaleAge = TimeSpan.FromDays(1);

    /// <summary>一時フォルダのルートを決める処理</summary>
    /// <remarks>最初の添付のときに 1 度だけ呼ぶ (WSL のディストリビューション名の取得など、作るときに済ませなくてよい処理があるため)。</remarks>
    private readonly Func<string> _resolveRoot;
    /// <summary>現在時刻の提供元</summary>
    private readonly TimeProvider _timeProvider;
    /// <summary>終了時の削除と、古い一時フォルダの掃除をするか</summary>
    /// <remarks>WSL の /tmp では、しない (WSL の再起動で空になるのに任せる。ユーザーの決定)。</remarks>
    private readonly bool _cleansUp;
    /// <summary>一時フォルダのルート</summary>
    /// <remarks>同じアプリの別ビルドと共有する (パスを短く保つため、EXE ごとには分けない)。最初の添付まで null。</remarks>
    private string? _root;
    /// <summary>このインスタンスが作ったセッションフォルダ</summary>
    /// <remarks>終了時に削除する。</remarks>
    private readonly List<string> _ownedSessions = [];
    /// <summary>今のセッションフォルダ</summary>
    /// <remarks>未作成なら null。</remarks>
    private string? _session;
    /// <summary>セッション内の連番</summary>
    private int _sequence;
    /// <summary>古い一時フォルダの掃除を済ませたか</summary>
    private bool _staleCleaned;

    /// <summary>Windows の一時フォルダ (%TEMP%\アプリ名\)に保存する一時保存先を作る</summary>
    /// <param name="appName">アプリを区別する名前 (一時フォルダの名前に使う。アプリごとに別の名前にする)</param>
    /// <param name="timeProvider">現在時刻の提供元</param>
    public AttachmentStore(string appName, TimeProvider timeProvider)
        : this(() => Path.Combine(Path.GetTempPath(), appName), timeProvider, cleansUp: true)
    {
    }

    /// <summary>一時保存先を作る</summary>
    /// <param name="resolveRoot">一時フォルダのルートを決める処理</param>
    /// <param name="timeProvider">現在時刻の提供元</param>
    /// <param name="cleansUp">終了時の削除と、古い一時フォルダの掃除をするか</param>
    private AttachmentStore(Func<string> resolveRoot, TimeProvider timeProvider, bool cleansUp)
    {
        _resolveRoot = resolveRoot;
        _timeProvider = timeProvider;
        _cleansUp = cleansUp;
    }

    /// <summary>WSL の既定のディストリビューションの /tmp (<c>\\wsl.localhost\&lt;名前&gt;\tmp\アプリ名\</c>)に保存する一時保存先を作る</summary>
    /// <param name="appName">アプリを区別する名前 (一時フォルダの名前に使う。アプリごとに別の名前にする)</param>
    /// <param name="timeProvider">現在時刻の提供元</param>
    /// <returns>WSL の /tmp に保存する一時保存先</returns>
    /// <remarks>
    /// WSL で動く CLI に渡すときは、保存先のパスを Linux の形 (<c>/tmp/アプリ名/...</c>。<see cref="WslPath"/>)にして渡す。
    /// 終了時の削除と古い一時フォルダの掃除はしない (WSL の /tmp は、systemd が有効なら WSL の起動のたびに空になるため。ユーザーの決定)。送信前に取り除いた添付は、Windows と同じくすぐ消す。
    /// ディストリビューション名は最初の添付のときに調べる。見つからない (WSL が入っていない)ときは、その添付が <see cref="IOException"/> で失敗する。
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public static AttachmentStore ForWsl(string appName, TimeProvider timeProvider)
        => new(() => WslTempRoot(appName), timeProvider, cleansUp: false);

    /// <summary>WSL の /tmp の中の、アプリの一時フォルダのルートを決める</summary>
    /// <param name="appName">アプリを区別する名前</param>
    /// <returns>Windows から見たパス (<c>\\wsl.localhost\&lt;名前&gt;\tmp\アプリ名</c>)</returns>
    /// <exception cref="IOException">WSL の既定のディストリビューションが見つからない。</exception>
    [SupportedOSPlatform("windows")]
    private static string WslTempRoot(string appName)
        => WslDistribution.TryGetDefaultName(out var distribution)
            ? Path.Combine($@"\\wsl.localhost\{distribution}\tmp", appName)
            : throw new IOException("WSL のディストリビューションが見つかりません。WSL とディストリビューション (Ubuntu など)が入っているか確認してください。");

    /// <summary>データをファイルとして添付する</summary>
    /// <param name="content">ファイルの内容</param>
    /// <param name="fileName">保存するファイル名 (フォルダの部分や使えない文字は取り除く)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存先のパス</returns>
    public async Task<string> AddAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        var destination = NextPath(fileName);
        await File.WriteAllBytesAsync(destination, content, cancellationToken).ConfigureAwait(false);
        return destination;
    }

    /// <summary>添付を取り除く</summary>
    /// <param name="filePath">取り除く添付ファイルのパス (今のセッションフォルダの中のもの)</param>
    /// <remarks>
    /// セッションに何も残らなければフォルダごと削除する。
    /// 送信前の添付を取り除くためのものなので、今のセッションフォルダの外のファイル (送信済みのファイルを含む)は消さない。
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> が今のセッションフォルダの中ではない (呼ぶ側のバグ)。</exception>
    public void Remove(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (_session is null || !string.Equals(Path.GetDirectoryName(fullPath), Path.GetFullPath(_session), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"今の添付フォルダの外のファイルは取り除けません: {filePath}", nameof(filePath));
        }

        File.Delete(fullPath);

        if (_session is not null && !Directory.EnumerateFileSystemEntries(_session).Any())
        {
            Directory.Delete(_session);
            _ownedSessions.Remove(_session);
            _session = null;
        }
    }

    /// <summary>送信済みとして今のセッションを閉じる</summary>
    /// <remarks>ファイルは残し、次の添付は新しいセッションに入れる。</remarks>
    public void CloseSession() => _session = null;

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_cleansUp)
        {
            return;
        }

        foreach (var directory in _ownedSessions)
        {
            TryDeleteDirectory(directory);
        }
        _ownedSessions.Clear();
    }

    /// <summary>次に保存するファイルのパスを決める</summary>
    /// <param name="fileName">元のファイル名</param>
    /// <returns>連番を付けた保存先のパス</returns>
    /// <remarks>ファイル名は、フォルダの部分と使えない文字を取り除いてから使う (セッションフォルダの外に保存されないように)。</remarks>
    private string NextPath(string fileName)
    {
        if (_session is null)
        {
            StartSession();
        }
        _sequence++;
        return Path.Combine(_session!, $"{_sequence:D3}_{ToSafeFileName(fileName)}");
    }

    /// <summary>ファイル名だけにして、使えない文字を置き換える</summary>
    /// <param name="fileName">元のファイル名 (フォルダを含んでいてもよい)</param>
    /// <returns>フォルダを含まない安全なファイル名。空になるときは <c>file</c></returns>
    private static string ToSafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }
        return string.IsNullOrWhiteSpace(name) || name is "." or ".." ? "file" : name;
    }

    /// <summary>セッションフォルダを作る</summary>
    private void StartSession()
    {
        _root ??= _resolveRoot();
        if (_cleansUp && !_staleCleaned)
        {
            _staleCleaned = true;
            DeleteStaleSessions(_root);
        }

        var now = _timeProvider.GetLocalNow();
        var baseName = $"session_{now:yyyyMMdd_HHmmss_fff}";
        var session = Path.Combine(_root, baseName);
        // 同じミリ秒にセッションを作っても (作り直し・フォルダを共有する別ビルド)、前のファイルを上書きしないよう、重なったら接尾辞を付ける
        for (var suffix = 2; Directory.Exists(session); suffix++)
        {
            session = Path.Combine(_root, $"{baseName}_{suffix}");
        }
        Directory.CreateDirectory(session);
        _ownedSessions.Add(session);
        _session = session;
        _sequence = 0;
    }

    /// <summary>古いセッションフォルダを削除する</summary>
    /// <param name="root">一時フォルダのルート</param>
    private void DeleteStaleSessions(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var threshold = _timeProvider.GetUtcNow().UtcDateTime - StaleAge;
        foreach (var directory in Directory.EnumerateDirectories(root, "session_*"))
        {
            if (Directory.GetLastWriteTimeUtc(directory) < threshold)
            {
                TryDeleteDirectory(directory);
            }
        }
    }

    /// <summary>フォルダを削除する</summary>
    /// <param name="directory">削除するフォルダ</param>
    /// <remarks>失敗しても無視する。</remarks>
    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 後片付けなので、使用中などで消せなければ次回起動以降に回す
        }
    }
}
