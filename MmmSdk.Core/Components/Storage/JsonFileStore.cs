using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MmmSdk.Core.Components.Storage;

/// <summary>
/// データフォルダ内の JSON ファイルを読み書きする。書き込みは一時ファイルに書いてから置き換え、途中で失敗しても元のファイルを壊さない。
/// </summary>
/// <param name="dataDirectory">JSON ファイルを置くフォルダのパス</param>
public sealed class JsonFileStore(string dataDirectory) : IJsonFileStore
{
    /// <summary>ファイル操作の同時実行を防ぐロック (ファイル名ごと)</summary>
    /// <remarks>
    /// 同じファイルの読み込みと書き込みで取る (読み込みと壊れたファイルの退避の間に、書き込みが割り込まないように)。別のファイルは待たせない。
    /// 待つ処理 (await)をまたいで持つので、継続を元のスレッド (UI スレッド)に戻さない (<c>ConfigureAwait(false)</c>)。
    /// 戻すと、UI スレッドが同じファイルの同期の <see cref="Read{T}"/> でこのロックを待っている間に、継続が UI スレッドを待って、デッドロックする。
    /// ファイル名の大文字小文字は区別しない (Windows のファイルシステムに合わせる)。
    /// </remarks>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public bool Exists(string fileName) => File.Exists(GetPath(fileName));

    /// <inheritdoc />
    public DataLoadResult<T?> Read<T>(string fileName, JsonTypeInfo<T> typeInfo)
    {
        var fileLock = GetFileLock(fileName);
        fileLock.Wait();
        try
        {
            if (!TryGetExistingPath(fileName, out var path))
            {
                return new(default);
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex) when (IsFileAccessError(ex))
            {
                throw ReadFailed(fileName, ex);
            }
            return ParseOrRecover(fileName, bytes, typeInfo);
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<DataLoadResult<T?>> ReadAsync<T>(string fileName, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var fileLock = GetFileLock(fileName);
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!TryGetExistingPath(fileName, out var path))
            {
                return new(default);
            }

            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsFileAccessError(ex))
            {
                throw ReadFailed(fileName, ex);
            }
            return ParseOrRecover(fileName, bytes, typeInfo);
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync<T>(string fileName, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var path = GetPath(fileName);
        var tempPath = path + ".tmp";

        var fileLock = GetFileLock(fileName);
        await fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dataDirectory);
            try
            {
                var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
                try
                {
                    await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken).ConfigureAwait(false);
                    // 置き換える前に、中身をディスクまで書き出す (電源断で、空や途中までのファイルに置き換わらないように)
                    stream.Flush(flushToDisk: true);
                }
                finally
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
                File.Move(tempPath, path, overwrite: true);
            }
            catch
            {
                // 失敗 (取り消しを含む)で一時ファイルを残さない。削除できなくても、元の例外を優先する
                TryDeleteTempFile(tempPath);
                throw;
            }
        }
        catch (Exception ex) when (IsFileAccessError(ex))
        {
            throw new DataFileException($"{fileName} を保存できませんでした。{ex.Message}", ex);
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>ファイルのロックを返す (無ければ作る)</summary>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <returns>そのファイル用のロック</returns>
    /// <remarks>不正なファイル名 (フォルダを含むなど)は、ロックを作る前に <see cref="GetPath"/> で拒否する。</remarks>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> が不正 (呼ぶ側のバグ)。</exception>
    private SemaphoreSlim GetFileLock(string fileName)
    {
        GetPath(fileName);
        return _fileLocks.GetOrAdd(fileName, static _ => new SemaphoreSlim(1, 1));
    }

    /// <summary>一時ファイルを削除する。削除できなくても例外にしない</summary>
    /// <param name="tempPath">一時ファイルのフルパス</param>
    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            File.Delete(tempPath);
        }
        catch (Exception ex) when (IsFileAccessError(ex))
        {
            // 次の書き込みで同じ名前を上書きするので、残っても害はない
        }
    }

    /// <summary>ファイルがあれば、そのパスを返す</summary>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="path">ファイルのフルパス</param>
    /// <returns>ファイルがあれば true</returns>
    private bool TryGetExistingPath(string fileName, out string path)
    {
        path = GetPath(fileName);
        return File.Exists(path);
    }

    /// <summary>ファイルの読み書きの失敗 (ロック・権限など)か</summary>
    /// <param name="exception">起きた例外</param>
    /// <returns>ロック・権限などの失敗なら true</returns>
    private static bool IsFileAccessError(Exception exception) => exception is IOException or UnauthorizedAccessException;

    /// <summary>読み込みの失敗を、画面に出せるメッセージ付きの例外にする</summary>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="cause">原因の例外</param>
    /// <returns>読み込みに失敗したことを表す例外</returns>
    private static DataFileException ReadFailed(string fileName, Exception cause)
        => new($"{fileName} を読み込めませんでした。{cause.Message}", cause);

    /// <summary>JSON を値に変換する。読めなければファイルを退避する。呼ぶ側は <see cref="GetFileLock"/> のロックを取っておくこと</summary>
    /// <typeparam name="T">読み込む値の型</typeparam>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="bytes">ファイルの中身</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <returns>変換した値と、退避したときのメッセージ</returns>
    /// <remarks>空 (BOM・空白だけ)なら値は null。</remarks>
    private DataLoadResult<T?> ParseOrRecover<T>(string fileName, byte[] bytes, JsonTypeInfo<T> typeInfo)
    {
        if (IsBlank(bytes))
        {
            return new(default);
        }

        try
        {
            // Stream から読むと先頭の BOM を読み飛ばしてくれる
            using var stream = new MemoryStream(bytes, writable: false);
            return new(JsonSerializer.Deserialize(stream, typeInfo));
        }
        catch (JsonException ex)
        {
            return new(default, TryRecover(fileName, ex));
        }
    }

    /// <summary>中身が空 (UTF-8 の BOM と空白だけ)か</summary>
    /// <param name="bytes">ファイルの中身</param>
    /// <returns>空なら true</returns>
    private static bool IsBlank(byte[] bytes)
    {
        var span = bytes.AsSpan();
        if (span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            span = span[3..];
        }
        return span.Trim((ReadOnlySpan<byte>)[(byte)' ', (byte)'\t', (byte)'\r', (byte)'\n']).IsEmpty;
    }

    /// <summary>壊れたファイルを退避する。呼ぶ側は <see cref="GetFileLock"/> のロックを取っておくこと</summary>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="error">JSON として読めなかった原因の例外</param>
    /// <returns>ユーザーへ表示するメッセージ</returns>
    /// <remarks>
    /// 同じフォルダに <c>名前.broken-yyyyMMdd-HHmmss.拡張子</c> で名前を変えて残す (自動では消さない)。
    /// 元のファイルは無くなるので、呼ぶ側は「ファイルが無いとき」と同じように作り直す。
    /// </remarks>
    /// <exception cref="DataFileException">退避できなかった。</exception>
    private string TryRecover(string fileName, JsonException error)
    {
        var path = GetPath(fileName);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var baseName = $"{Path.GetFileNameWithoutExtension(fileName)}.broken-{stamp}";
        var extension = Path.GetExtension(fileName);

        var backupName = baseName + extension;
        for (var i = 2; File.Exists(GetPath(backupName)); i++)
        {
            backupName = $"{baseName}-{i}{extension}";
        }

        try
        {
            File.Move(path, GetPath(backupName));
        }
        catch (Exception ex) when (IsFileAccessError(ex))
        {
            throw new DataFileException($"{fileName} を読み込めませんでした。{error.Message}\n壊れたファイルの退避にも失敗しました。{ex.Message}", ex);
        }

        return $"{fileName} が壊れていたため、{backupName} に退避して作り直しました。\n{error.Message}";
    }

    /// <summary>ファイルのフルパスを返す</summary>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <returns>データフォルダと結合したパス</returns>
    /// <remarks>ファイル名にフォルダの区切り・ドライブ・<c>..</c> が入ると、データフォルダの外を読み書きできてしまう。<c>:</c> が入ると、代替データストリーム (<c>x.json:ads</c>)として、見えない場所に読み書きできてしまう。そのため、ファイル名だけを受け付ける。</remarks>
    /// <exception cref="ArgumentException"><paramref name="fileName"/> が空、またはフォルダや <c>:</c> を含んでいる (呼ぶ側のバグ)。</exception>
    private string GetPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName || fileName is "." or ".." || fileName.Contains(':'))
        {
            throw new ArgumentException($"ファイル名には、フォルダや「:」を含まない名前を指定してください: {fileName}", nameof(fileName));
        }
        return Path.Combine(dataDirectory, fileName);
    }
}
