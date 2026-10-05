using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;
using MmmSdk.Core.Components.Settings.Json;

namespace MmmSdk.Core.Components.Settings;

/// <summary>基本型 (string / bool / int / long / double)を、型情報なしで読み書きする <see cref="ISettingsStore"/> の拡張メソッド</summary>
/// <remarks>
/// 型情報つきのメソッド (<see cref="ISettingsStore"/>)を呼ぶだけ。基本型の型情報は <c>SettingsJsonContext</c>(internal)が持つので、保存先の実装は、これらを実装し直さなくてよい。
/// 基本型以外を渡すと、実行時ではなく、ビルドで誤りになる。
/// </remarks>
public static class SettingsStoreExtensions
{
    /// <summary>文字列の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    public static string Get(this ISettingsStore store, string key, string defaultValue)
        => store.Get(key, defaultValue, BuiltInTypeInfo<string>());

    /// <summary>文字列の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true (無い・型が合わないときは false)</returns>
    public static bool TryGet(this ISettingsStore store, string key, [MaybeNullWhen(false)] out string value)
        => store.TryGet(key, BuiltInTypeInfo<string>(), out value);

    /// <summary>文字列の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用 (読み込みに失敗している)ため保存しなかったら false</returns>
    public static Task<bool> SetAsync(this ISettingsStore store, string key, string value, CancellationToken cancellationToken = default)
        => store.SetAsync(key, value, BuiltInTypeInfo<string>(), cancellationToken);

    /// <summary>真偽値の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    public static bool Get(this ISettingsStore store, string key, bool defaultValue)
        => store.Get(key, defaultValue, BuiltInTypeInfo<bool>());

    /// <summary>真偽値の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true (無い・型が合わないときは false)</returns>
    public static bool TryGet(this ISettingsStore store, string key, out bool value)
        => store.TryGet(key, BuiltInTypeInfo<bool>(), out value);

    /// <summary>真偽値の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用 (読み込みに失敗している)ため保存しなかったら false</returns>
    public static Task<bool> SetAsync(this ISettingsStore store, string key, bool value, CancellationToken cancellationToken = default)
        => store.SetAsync(key, value, BuiltInTypeInfo<bool>(), cancellationToken);

    /// <summary>整数 (int)の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    public static int Get(this ISettingsStore store, string key, int defaultValue)
        => store.Get(key, defaultValue, BuiltInTypeInfo<int>());

    /// <summary>整数 (int)の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true (無い・型が合わないときは false)</returns>
    public static bool TryGet(this ISettingsStore store, string key, out int value)
        => store.TryGet(key, BuiltInTypeInfo<int>(), out value);

    /// <summary>整数 (int)の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用 (読み込みに失敗している)ため保存しなかったら false</returns>
    public static Task<bool> SetAsync(this ISettingsStore store, string key, int value, CancellationToken cancellationToken = default)
        => store.SetAsync(key, value, BuiltInTypeInfo<int>(), cancellationToken);

    /// <summary>整数 (long)の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    public static long Get(this ISettingsStore store, string key, long defaultValue)
        => store.Get(key, defaultValue, BuiltInTypeInfo<long>());

    /// <summary>整数 (long)の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true (無い・型が合わないときは false)</returns>
    public static bool TryGet(this ISettingsStore store, string key, out long value)
        => store.TryGet(key, BuiltInTypeInfo<long>(), out value);

    /// <summary>整数 (long)の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用 (読み込みに失敗している)ため保存しなかったら false</returns>
    public static Task<bool> SetAsync(this ISettingsStore store, string key, long value, CancellationToken cancellationToken = default)
        => store.SetAsync(key, value, BuiltInTypeInfo<long>(), cancellationToken);

    /// <summary>小数の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    public static double Get(this ISettingsStore store, string key, double defaultValue)
        => store.Get(key, defaultValue, BuiltInTypeInfo<double>());

    /// <summary>小数の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true (無い・型が合わないときは false)</returns>
    public static bool TryGet(this ISettingsStore store, string key, out double value)
        => store.TryGet(key, BuiltInTypeInfo<double>(), out value);

    /// <summary>小数の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="store">設定ストア</param>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用 (読み込みに失敗している)ため保存しなかったら false</returns>
    public static Task<bool> SetAsync(this ISettingsStore store, string key, double value, CancellationToken cancellationToken = default)
        => store.SetAsync(key, value, BuiltInTypeInfo<double>(), cancellationToken);

    /// <summary>基本型の <see cref="JsonTypeInfo{T}"/> を返す</summary>
    /// <typeparam name="T">基本型 (string / bool / int / long / double)。呼ぶのは、型ごとの専用のメソッドだけ</typeparam>
    /// <returns>型のソース生成メタデータ</returns>
    /// <exception cref="InvalidOperationException">基本型以外で呼んだとき (このクラスのプログラムの誤り)。</exception>
    private static JsonTypeInfo<T> BuiltInTypeInfo<T>()
        => SettingsJsonContext.Readable.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException($"{typeof(T)} は JsonTypeInfo を渡さずに使えません。");
}
