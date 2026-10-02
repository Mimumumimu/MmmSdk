using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MmmSdk.Core.Services;

namespace MmmSdk.Core.Repositories.Json;

/// <summary>SDK の JSON 読み書き用のシリアライザ設定。</summary>
/// <remarks>発行時のトリミングでも動くよう、JSON の読み書きはソース生成で行う。アプリ固有の型はアプリ側の Context で登録する。</remarks>
[JsonSerializable(typeof(WindowPosition))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
internal sealed partial class SdkJsonContext : JsonSerializerContext
{
    /// <summary>手で読み書きしやすい形の設定</summary>
    /// <remarks>インデントあり・日本語や記号を非エスケープ・コメント可・プロパティ名の大文字小文字を区別しない。</remarks>
    public static SdkJsonContext Readable { get; } = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    });
}
