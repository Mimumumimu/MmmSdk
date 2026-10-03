using System.Diagnostics.CodeAnalysis;
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
/// ロック・権限などで読めなかったときは空として扱うが、元のファイルを上書きしないよう、保存はしない（<see cref="IsReadOnly"/>）。
/// 保存は、変更後のコピーをファイルに書き、成功してからメモリの辞書を差し替える（保存に失敗したとき、メモリだけが新しい状態にならない）。
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
    /// <remarks>書き換えず、保存に成功したときに、新しい辞書へ差し替える。</remarks>
    private Dictionary<string, JsonElement>? _values;

    /// <summary>読み込みに失敗したときのメッセージ。最初のアクセスまでは null</summary>
    private string? _loadError;

    /// <summary>壊れたファイルを退避したときのメッセージ。最初のアクセスまでは null</summary>
    private string? _recoveryMessage;

    /// <inheritdoc />
    public string? LoadError
    {
        get
        {
            lock (_gate)
            {
                EnsureLoaded();
                return _loadError;
            }
        }
    }

    /// <inheritdoc />
    public string? RecoveryMessage
    {
        get
        {
            lock (_gate)
            {
                EnsureLoaded();
                return _recoveryMessage;
            }
        }
    }

    /// <inheritdoc />
    public bool IsReadOnly => LoadError is not null;

    /// <inheritdoc />
    public string Get(string key, string defaultValue) => TryGet(key, out string? value) ? value : defaultValue;

    /// <inheritdoc />
    public bool Get(string key, bool defaultValue) => TryGet(key, out bool value) ? value : defaultValue;

    /// <inheritdoc />
    public int Get(string key, int defaultValue) => TryGet(key, out int value) ? value : defaultValue;

    /// <inheritdoc />
    public long Get(string key, long defaultValue) => TryGet(key, out long value) ? value : defaultValue;

    /// <inheritdoc />
    public double Get(string key, double defaultValue) => TryGet(key, out double value) ? value : defaultValue;

    /// <inheritdoc />
    public bool TryGet(string key, [MaybeNullWhen(false)] out string value) => TryGet(key, GetBuiltInTypeInfo<string>(), out value);

    /// <inheritdoc />
    public bool TryGet(string key, out bool value) => TryGet(key, GetBuiltInTypeInfo<bool>(), out value);

    /// <inheritdoc />
    public bool TryGet(string key, out int value) => TryGet(key, GetBuiltInTypeInfo<int>(), out value);

    /// <inheritdoc />
    public bool TryGet(string key, out long value) => TryGet(key, GetBuiltInTypeInfo<long>(), out value);

    /// <inheritdoc />
    public bool TryGet(string key, out double value) => TryGet(key, GetBuiltInTypeInfo<double>(), out value);

    /// <inheritdoc />
    public Task<bool> SetAsync(string key, string value, CancellationToken cancellationToken = default)
        => SetAsync(key, value, GetBuiltInTypeInfo<string>(), cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetAsync(string key, bool value, CancellationToken cancellationToken = default)
        => SetAsync(key, value, GetBuiltInTypeInfo<bool>(), cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetAsync(string key, int value, CancellationToken cancellationToken = default)
        => SetAsync(key, value, GetBuiltInTypeInfo<int>(), cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetAsync(string key, long value, CancellationToken cancellationToken = default)
        => SetAsync(key, value, GetBuiltInTypeInfo<long>(), cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetAsync(string key, double value, CancellationToken cancellationToken = default)
        => SetAsync(key, value, GetBuiltInTypeInfo<double>(), cancellationToken);

    /// <inheritdoc />
    public T Get<T>(string key, T defaultValue, JsonTypeInfo<T> typeInfo)
        => TryGet(key, typeInfo, out var value) ? value : defaultValue;

    /// <inheritdoc />
    public bool TryGet<T>(string key, JsonTypeInfo<T> typeInfo, [MaybeNullWhen(false)] out T value)
    {
        JsonElement element;
        lock (_gate)
        {
            if (!EnsureLoaded().TryGetValue(key, out element))
            {
                value = default;
                return false;
            }
        }

        try
        {
            var deserialized = element.Deserialize(typeInfo);
            if (deserialized is null)
            {
                value = default;
                return false;
            }

            value = deserialized;
            return true;
        }
        catch (JsonException)
        {
            // 手で書き換えた値が型に合わないとき。「無い」ものとして扱う
            value = default;
            return false;
        }
    }

    /// <inheritdoc />
    public Task<bool> SetAsync<T>(string key, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default)
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
        var saved = await UpdateAsync(values => removed = values.Remove(key), cancellationToken).ConfigureAwait(false);
        return saved && removed;
    }

    /// <summary>辞書のコピーを書き換えて保存し、成功したらメモリの辞書を差し替える</summary>
    /// <param name="change">辞書のコピーを書き換える処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用（読み込みに失敗している）ため保存しなかったら false</returns>
    /// <exception cref="DataFileException">保存に失敗した（ロック・権限など）。このときメモリの辞書は変えない。</exception>
    /// <remarks>書き換えたあとの内容を保存の順番どおりに書く。何も変わらなかったときも書くが、実害はない。</remarks>
    private async Task<bool> UpdateAsync(Action<Dictionary<string, JsonElement>> change, CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, JsonElement> next;
            lock (_gate)
            {
                var values = EnsureLoaded();
                if (_loadError is not null)
                {
                    // 読めなかっただけの既存の設定を、空の辞書で上書きしてしまわないよう、書かない
                    return false;
                }

                next = new Dictionary<string, JsonElement>(values);
            }

            change(next);
            await store.WriteAsync(FileName, next, SettingsJsonContext.Readable.DictionaryStringJsonElement, cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                _values = next;
            }
            return true;
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
            _recoveryMessage = result.RecoveryMessage;
        }
        catch (DataFileException ex)
        {
            _loadError = ex.Message;
            _values = [];
        }

        return _values;
    }

    /// <summary>基本型の <see cref="JsonTypeInfo{T}"/> を返す</summary>
    /// <typeparam name="T">基本型（string / bool / int / long / double）。呼ぶのは、型ごとの専用のメソッドだけ</typeparam>
    /// <returns>型のソース生成メタデータ</returns>
    /// <exception cref="InvalidOperationException">基本型以外で呼んだとき（このクラスのプログラムの誤り）。</exception>
    private static JsonTypeInfo<T> GetBuiltInTypeInfo<T>()
        => SettingsJsonContext.Readable.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"{typeof(T)} は JsonTypeInfo を渡さずに使えません。");
}
