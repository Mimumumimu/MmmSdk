using MmmSdk.Core.Components.Settings;

namespace MmmSdk.Core.Components.WindowPositions;

/// <summary>
/// ウィンドウの位置をキー単位で保存・復元する。
/// </summary>
/// <param name="store">汎用設定ストア</param>
/// <remarks>保存は汎用設定ストア (<see cref="ISettingsStore"/>)に <c>WindowPosition.&lt;キー&gt;</c> として持つ。</remarks>
public sealed class WindowPositionService(ISettingsStore store) : IWindowPositionService
{
    /// <summary>位置のキーの接頭辞</summary>
    private const string KeyPrefix = "WindowPosition.";

    /// <summary>位置と大きさのキーの接頭辞</summary>
    private const string BoundsKeyPrefix = "WindowBounds.";

    /// <inheritdoc />
    public WindowPosition? Load(string key)
    {
        // 無い・型が合わないときは null (「(0,0) に保存済み」と取り違えない)
        return store.TryGet(KeyPrefix + key, WindowPositionJsonContext.Readable.WindowPosition, out var position) ? position : null;
    }

    /// <inheritdoc />
    /// <remarks>設定ストアが読み取り専用 (読み込みに失敗している)のときは、保存しない (位置が残らなくても困らないので、呼ぶ側には知らせない)。</remarks>
    public Task SaveAsync(string key, WindowPosition position) =>
        store.SetAsync(KeyPrefix + key, position, WindowPositionJsonContext.Readable.WindowPosition);

    /// <inheritdoc />
    public WindowBounds? LoadBounds(string key)
        => store.TryGet(BoundsKeyPrefix + key, WindowPositionJsonContext.Readable.WindowBounds, out var bounds) ? bounds : null;

    /// <inheritdoc />
    /// <remarks>設定ストアが読み取り専用 (読み込みに失敗している)のときは、保存しない。</remarks>
    public Task SaveBoundsAsync(string key, WindowBounds bounds) =>
        store.SetAsync(BoundsKeyPrefix + key, bounds, WindowPositionJsonContext.Readable.WindowBounds);
}
