using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace MmmSdk.Core.Storage;

/// <summary>
/// データフォルダ内の JSON ファイルを読み書きする。書き込みは一時ファイルに書いてから置き換え、途中で失敗しても元のファイルを壊さない。
/// </summary>
/// <param name="dataDirectory">JSON ファイルを置くフォルダのパス</param>
public sealed class JsonFileStore(string dataDirectory) : IJsonFileStore
{
    /// <summary>ファイル操作の同時実行を防ぐロック</summary>
    /// <remarks>読み込みも取る（読み込みと壊れたファイルの退避の間に、書き込みが割り込まないように）。</remarks>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    /// <inheritdoc />
    public bool Exists(string fileName) => File.Exists(GetPath(fileName));

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
    /// <typeparam name="T">読み込む値の型</typeparam>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="bytes">ファイルの中身</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <returns>変換した値と、退避したときのメッセージ</returns>
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

    /// <summary>壊れたファイルを退避する。呼ぶ側は <c>_writeLock</c> を取っておくこと</summary>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="error">JSON として読めなかった原因の例外</param>
    /// <returns>ユーザーへ表示するメッセージ</returns>
    /// <remarks>
    /// 同じフォルダに <c>名前.broken-yyyyMMdd-HHmmss.拡張子</c> で名前を変えて残す（自動では消さない）。
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DataFileException($"{fileName} を読み込めませんでした。{error.Message}\n壊れたファイルの退避にも失敗しました。{ex.Message}", ex);
        }

        return $"{fileName} が壊れていたため、{backupName} に退避して作り直しました。\n{error.Message}";
    }

    /// <summary>ファイルのフルパスを返す</summary>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <returns>データフォルダと結合したパス</returns>
    private string GetPath(string fileName) => Path.Combine(dataDirectory, fileName);
}
