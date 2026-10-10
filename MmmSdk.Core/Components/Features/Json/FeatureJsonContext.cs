using System.Text.Json.Serialization;
using MmmSdk.Core.Components.Storage;

namespace MmmSdk.Core.Components.Features.Json;

/// <summary>機能の並び順 (機能のキーの配列)を設定ストアへ読み書きするためのシリアライザ設定</summary>
/// <remarks>設定の中身は <see cref="ReadableJsonOptions"/> を参照。</remarks>
[JsonSerializable(typeof(string[]))]
internal sealed partial class FeatureJsonContext : JsonSerializerContext
{
    /// <summary>手で読み書きしやすい形の設定</summary>
    public static FeatureJsonContext Readable { get; } = new(ReadableJsonOptions.Create());
}
