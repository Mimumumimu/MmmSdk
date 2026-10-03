using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MmmSdk.Core.Storage;

/// <summary>手で読み書きしやすい JSON のシリアライザ設定</summary>
/// <remarks>
/// インデントあり・日本語や記号を非エスケープ・コメント可・プロパティ名の大文字小文字を区別しない。
/// SDK とアプリのソース生成の Context（<c>JsonSerializerContext</c>）で同じ設定を使うための共通の作り方。
/// 設定は Context に渡すと読み取り専用になり、その Context に結び付くので、Context ごとに新しく作る。
/// </remarks>
public static class ReadableJsonOptions
{
    /// <summary>設定を新しく作る</summary>
    /// <returns>作った設定（Context のコンストラクタに渡す）</returns>
    public static JsonSerializerOptions Create() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
