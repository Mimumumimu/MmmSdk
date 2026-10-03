using MmmSdk.Core.Settings;

namespace MmmSdk.Core.WindowPositions;

/// <summary>
/// ウィンドウの位置をキー単位で保存・復元する。
/// </summary>
/// <param name="store">汎用設定ストア</param>
/// <remarks>保存は汎用設定ストア（<see cref="ISettingsStore"/>）に <c>WindowPosition.&lt;キー&gt;</c> として持つ。</remarks>
public sealed class WindowPositionService(ISettingsStore store)
{
    /// <summary>キーの接頭辞</summary>
    private const string KeyPrefix = "WindowPosition.";

    /// <summary>保存した位置を取得する</summary>
    /// <param name="key">ウィンドウを区別するキー</param>
    /// <returns>保存した位置。保存がなければ null</returns>
    public WindowPosition? Load(string key)
    {
        var storeKey = KeyPrefix + key;
        return store.Contains(storeKey)
            ? store.Get(storeKey, new WindowPosition(0, 0), WindowPositionJsonContext.Readable.WindowPosition)
            : null;
    }

    /// <summary>位置を保存する。保存し終えるまで待つ。</summary>
    /// <param name="key">ウィンドウを区別するキー</param>
    /// <param name="position">保存する位置</param>
    /// <returns>保存の完了を表すタスク</returns>
    public Task SaveAsync(string key, WindowPosition position) =>
        store.SetAsync(KeyPrefix + key, position, WindowPositionJsonContext.Readable.WindowPosition);
}
