using System.Text.Json;
using System.Text.Json.Serialization;
using MmmSdk.Core.Storage;

namespace MmmSdk.Core.Settings.Json;

/// <summary>汎用設定ストアの JSON 読み書き用のシリアライザ設定</summary>
/// <remarks>
/// 発行時のトリミングでも動くよう、JSON の読み書きはソース生成で行う。設定ファイル全体（キー → JSON 要素の辞書）と、
/// TypeInfo なしで使える基本型（string / bool / int / long / double）を登録する。それ以外の型は使う側の Context で登録する。
/// </remarks>
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
    /// <summary>手で読み書きしやすい形の設定</summary>
    /// <remarks>設定の中身は <see cref="ReadableJsonOptions"/> を参照。</remarks>
    public static SettingsJsonContext Readable { get; } = new(ReadableJsonOptions.Create());
}
