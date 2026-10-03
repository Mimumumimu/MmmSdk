using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MmmSdk.Core.Repositories.Json;

/// <summary>
/// データフォルダ内の JSON ファイルを読み書きする。書き込みは一時ファイルに書いてから置き換え、途中で失敗しても元のファイルを壊さない。
/// </summary>
public sealed class JsonFileStore(string dataDirectory)
{
    /// <summary>ファイル操作の同時実行を防ぐロック</summary>
    /// <remarks>読み込みも取る（読み込みと壊れたファイルの退避の間に、書き込みが割り込まないように）。</remarks>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <summary>ファイルが存在するか</summary>
    public bool Exists(string fileName) => File.Exists(GetPath(fileName));

    /// <summary>同期で読み込む</summary>
    /// <remarks>
    /// ファイルが無い・空（空白だけ）なら値は null。JSON として読めないファイルは退避して値を null で返す（<see cref="TryRecover"/>）。
    /// 小さなファイルを起動時などに UI スレッドで読む用途向け。
    /// </remarks>
    /// <exception cref="DataFileException">ファイルを読めなかった・退避できなかった（ロック・権限など）。</exception>
    public DataLoadResult<T?> Read<T>(string fileName, JsonTypeInfo<T> typeInfo)
    {
        _writeLock.Wait();
        try
        {
            var path = GetPath(fileName);
            if (!File.Exists(path))
            {
                return new(default);
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new DataFileException($"{fileName} を読み込めませんでした。{ex.Message}", ex);
            }
            return ParseOrRecover(fileName, bytes, typeInfo);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>読み込む</summary>
    /// <remarks>ファイルが無い・空（空白だけ）なら値は null。JSON として読めないファイルは退避して値を null で返す（<see cref="TryRecover"/>）。</remarks>
    /// <exception cref="DataFileException">ファイルを読めなかった・退避できなかった（ロック・権限など）。</exception>
    public async Task<DataLoadResult<T?>> ReadAsync<T>(string fileName, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var path = GetPath(fileName);
            if (!File.Exists(path))
            {
                return new(default);
            }

            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new DataFileException($"{fileName} を読み込めませんでした。{ex.Message}", ex);
            }
            return ParseOrRecover(fileName, bytes, typeInfo);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>書き込む。一時ファイルに書いてから置き換える</summary>
    public async Task WriteAsync<T>(string fileName, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var path = GetPath(fileName);
        var tempPath = path + ".tmp";

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(dataDirectory);
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken);
            }
            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DataFileException($"{fileName} を保存できませんでした。{ex.Message}", ex);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>JSON を値に変換する。読めなければファイルを退避する。呼ぶ側は <c>_writeLock</c> を取っておくこと</summary>
    /// <remarks>空（BOM・空白だけ）なら値は null。</remarks>
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

    /// <summary>中身が空（UTF-8 の BOM と空白だけ）か</summary>
    private static bool IsBlank(byte[] bytes)
    {
        var span = bytes.AsSpan();
        if (span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            span = span[3..];
        }
        return span.Trim((ReadOnlySpan<byte>)[(byte)' ', (byte)'\t', (byte)'\r', (byte)'\n']).IsEmpty;
    }

    /// <summary>壊れたファイルを退避する。呼ぶ側は <c>_writeLock</c> を取っておくこと</summary>
    /// <remarks>
    /// 同じフォルダに <c>名前.broken-yyyyMMdd-HHmmss.拡張子</c> で名前を変えて残す（自動では消さない）。
    /// 元のファイルは無くなるので、呼ぶ側は「ファイルが無いとき」と同じように作り直す。
    /// </remarks>
    /// <returns>ユーザーへ表示するメッセージ</returns>
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DataFileException($"{fileName} を読み込めませんでした。{error.Message}\n壊れたファイルの退避にも失敗しました。{ex.Message}", ex);
        }

        return $"{fileName} が壊れていたため、{backupName} に退避して作り直しました。\n{error.Message}";
    }

    /// <summary>ファイルのフルパスを返す</summary>
    private string GetPath(string fileName) => Path.Combine(dataDirectory, fileName);
}
