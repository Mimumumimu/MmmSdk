using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MmmSdk.Core.Storage;

namespace MmmSdk.Core.Settings.Json;

/// <summary>
/// 汎用設定ストアの JSON ファイル実装。全設定を <c>Data/AppSettings.json</c> の 1 ファイルに集約する。
/// </summary>
/// <param name="store">JSON ファイルの読み書き</param>
/// <remarks>
/// 内部では「キー → JSON 要素」の辞書をメモリに持ち、取得時に目的の型へ変換する。最初のアクセスで 1 度だけ読み込む。
/// ファイルが無い・空・壊れているときは空として扱う。壊れていたファイルは退避してから作り直す（<see cref="RecoveryMessage"/> に残す）。
/// </remarks>
public sealed class JsonSettingsStore(IJsonFileStore store) : ISettingsStore
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "AppSettings.json";

    /// <summary>辞書の読み書きを守るロック</summary>
    private readonly Lock _gate = new();

    /// <summary>保存の順序を守るロック（古い内容が後から書かれないように）</summary>
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>キー → 値の辞書。最初のアクセスまでは null</summary>
    private Dictionary<string, JsonElement>? _values;

    /// <inheritdoc />
    public string? LoadError { get; private set; }

    /// <inheritdoc />
    public string? RecoveryMessage { get; private set; }

    /// <inheritdoc />
    public T Get<T>(string key, T defaultValue) => Get(key, defaultValue, GetBuiltInTypeInfo<T>());

    /// <inheritdoc />
    public T Get<T>(string key, T defaultValue, JsonTypeInfo<T> typeInfo)
    {
        JsonElement element;
        lock (_gate)
        {
            if (!EnsureLoaded().TryGetValue(key, out element))
            {
                return defaultValue;
            }
        }

        try
        {
            var value = element.Deserialize(typeInfo);
            return value is null ? defaultValue : value;
        }
        catch (JsonException)
        {
            return defaultValue;
        }
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        => SetAsync(key, value, GetBuiltInTypeInfo<T>(), cancellationToken);

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
    {
        var element = JsonSerializer.SerializeToElement(value, typeInfo);
        return UpdateAsync(values => values[key] = element, cancellationToken);
    }

    /// <inheritdoc />
    public bool Contains(string key)
    {
        lock (_gate)
        {
            return EnsureLoaded().ContainsKey(key);
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var removed = false;
        await UpdateAsync(values => removed = values.Remove(key), cancellationToken);
        return removed;
    }

    /// <summary>辞書を書き換えて保存する</summary>
    /// <param name="change">辞書を書き換える処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>書き換えたあとの内容を保存の順番どおりに書く。何も変わらなかったときも書くが、実害はない。</remarks>
    private async Task UpdateAsync(Action<Dictionary<string, JsonElement>> change, CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, JsonElement> snapshot;
            lock (_gate)
            {
                var values = EnsureLoaded();
                change(values);
                snapshot = new Dictionary<string, JsonElement>(values);
            }

            await store.WriteAsync(FileName, snapshot, SettingsJsonContext.Readable.DictionaryStringJsonElement, cancellationToken);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>未読み込みなら読み込んで辞書を返す。呼ぶ側は <c>_gate</c> を取っておくこと</summary>
    /// <returns>キー → 値の辞書</returns>
    private Dictionary<string, JsonElement> EnsureLoaded()
    {
        if (_values is not null)
        {
            return _values;
        }

        try
        {
            var result = store.Read(FileName, SettingsJsonContext.Readable.DictionaryStringJsonElement);
            _values = result.Value ?? [];
            RecoveryMessage = result.RecoveryMessage;
        }
        catch (DataFileException ex)
        {
            LoadError = ex.Message;
            _values = [];
        }

        return _values;
    }

    /// <summary>基本型の <see cref="JsonTypeInfo{T}"/> を返す</summary>
    /// <typeparam name="T">基本型（string / bool / int / long / double）</typeparam>
    /// <returns>型のソース生成メタデータ</returns>
    /// <exception cref="InvalidOperationException">基本型以外で、TypeInfo の指定が無いとき（呼び出し側のプログラムの誤り）。</exception>
    private static JsonTypeInfo<T> GetBuiltInTypeInfo<T>()
        => SettingsJsonContext.Readable.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"{typeof(T)} は JsonTypeInfo を渡さずに使えません。");
}
