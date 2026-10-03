using System.Text.Json.Serialization;
using MmmSdk.Core.Storage;

namespace MmmSdk.Core.WindowPositions;

/// <summary>ウィンドウ位置の JSON 読み書き用のシリアライザ設定</summary>
/// <remarks>発行時のトリミングでも動くよう、JSON の読み書きはソース生成で行う。設定の中身は <see cref="ReadableJsonOptions"/> を参照。</remarks>
[JsonSerializable(typeof(WindowPosition))]
internal sealed partial class WindowPositionJsonContext : JsonSerializerContext
{
    /// <summary>手で読み書きしやすい形の設定</summary>
    public static WindowPositionJsonContext Readable { get; } = new(ReadableJsonOptions.Create());
}
