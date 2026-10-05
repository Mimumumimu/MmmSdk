using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using MmmSdk.Core.Components.Storage;

namespace MmmSdk.Core.Components.Settings.Json;

/// <summary>
/// 汎用設定ストアの JSON ファイル実装。全設定を <c>Data/AppSettings.json</c> の 1 ファイルに集約する。
/// </summary>
/// <param name="store">JSON ファイルの読み書き</param>
/// <remarks>
/// 内部では「キー → JSON 要素」の辞書をメモリに持ち、取得時に目的の型へ変換する。最初のアクセスで 1 度だけ読み込む (<see cref="EnsureLoadedAsync"/> で先に非同期で読んでおくと、最初のアクセスで UI スレッドを止めない)。
/// ファイルが無い・空・壊れているときは空として扱う。壊れていたファイルは退避してから作り直す (<see cref="RecoveryMessage"/> に残す)。
/// ロック・権限などで読めなかったときは空として扱うが、元のファイルを上書きしないよう、保存はしない (<see cref="IsReadOnly"/>)。一時的なロックだったかもしれないので、保存のたびに 1 度だけ読み直し、読めれば、そのまま保存する。
/// 保存は、変更後のコピーをファイルに書き、成功してからメモリの辞書を差し替える (保存に失敗したとき、メモリだけが新しい状態にならない)。
/// </remarks>
public sealed class JsonSettingsStore(IJsonFileStore store) : ISettingsStore
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "AppSettings.json";

    /// <summary>辞書の読み書きを守るロック</summary>
    private readonly Lock _gate = new();

    /// <summary>保存の順序を守るロック (古い内容が後から書かれないように)</summary>
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>キー → 値の辞書。最初のアクセスまでは null</summary>
    /// <remarks>書き換えず、保存に成功したときに、新しい辞書へ差し替える。</remarks>
    private Dictionary<string, JsonElement>? _values;

    /// <summary>読み込みの結果 (失敗したか・壊れたファイルを退避したか)</summary>
    private readonly LoadStatus _status = new();

    /// <inheritdoc />
    public string? LoadError
    {
        get
        {
            lock (_gate)
            {
                EnsureLoaded();
                return _status.LoadError;
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
                return _status.RecoveryMessage;
            }
        }
    }

    /// <inheritdoc />
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_values is not null) return;
        }

        try
        {
            var result = await store.ReadAsync(FileName, SettingsJsonContext.Readable.DictionaryStringJsonElement, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                // 読んでいる間に、別のスレッドが (同期で)読み込み済みなら、それを使う
                if (_values is null)
                {
                    ApplyLoaded(result);
                }
            }
        }
        catch (DataFileException ex)
        {
            lock (_gate)
            {
                if (_values is null)
                {
                    ApplyFailed(ex);
                }
            }
        }
    }

    /// <inheritdoc />
    public bool IsReadOnly => LoadError is not null;

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
    /// <returns>保存したら true。読み取り専用 (読み込みに失敗している)ため保存しなかったら false</returns>
    /// <remarks>書き換えたあとの内容を保存の順番どおりに書く。何も変わらなかったときも書くが、実害はない。</remarks>
    /// <exception cref="DataFileException">保存に失敗した (ロック・権限など)。このときメモリの辞書は変えない。</exception>
    private async Task<bool> UpdateAsync(Action<Dictionary<string, JsonElement>> change, CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, JsonElement> next;
            lock (_gate)
            {
                var values = EnsureLoaded();
                if (_status.HasFailed)
                {
                    // 最初の読み込みが、一時的なロックなどで失敗していたかもしれないので、保存のときに 1 度だけ読み直す
                    Load();
                    if (_status.HasFailed)
                    {
                        // 読めなかっただけの既存の設定を、空の辞書で上書きしてしまわないよう、書かない
                        return false;
                    }

                    values = _values!;
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
        if (_values is null)
        {
            Load();
        }

        return _values!;
    }

    /// <summary>ファイルを読み込んで、辞書と読み込みの結果を更新する。呼ぶ側は <c>_gate</c> を取っておくこと</summary>
    /// <remarks>
    /// 読めなかったとき (<see cref="DataFileException"/>)は、結果を失敗にして、辞書は空のまま (すでに読めていた辞書は残す)にする。
    /// 壊れたファイルを退避したときのメッセージは、読み直しても残す (最初の読み込みで起きた退避を、画面で知らせ続けるため)。
    /// </remarks>
    private void Load()
    {
        try
        {
            ApplyLoaded(store.Read(FileName, SettingsJsonContext.Readable.DictionaryStringJsonElement));
        }
        catch (DataFileException ex)
        {
            ApplyFailed(ex);
        }
    }

    /// <summary>読み込めた結果を、辞書と読み込みの結果に反映する。呼ぶ側は <c>_gate</c> を取っておくこと</summary>
    /// <param name="result">読み込んだ辞書と、壊れたファイルを退避したときのメッセージ</param>
    private void ApplyLoaded(DataLoadResult<Dictionary<string, JsonElement>?> result)
    {
        _values = result.Value ?? [];
        _status.Succeeded(result.RecoveryMessage, keepPreviousRecoveryMessage: true);
    }

    /// <summary>読めなかったことを、読み込みの結果に反映する。呼ぶ側は <c>_gate</c> を取っておくこと</summary>
    /// <param name="exception">読めなかった原因</param>
    private void ApplyFailed(DataFileException exception)
    {
        _status.Failed(exception);
        _values ??= [];
    }
}
