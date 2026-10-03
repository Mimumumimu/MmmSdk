using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization.Metadata;

namespace MmmSdk.Core.Settings;

/// <summary>
/// キーに対して任意の型の値を保存・取得する、アプリ共通の汎用設定ストア。
/// </summary>
/// <remarks>
/// 機能ごとの設定サービスは、キーに接頭辞を付けて委譲すると衝突しない（例: <c>WindowPosition.&lt;キー&gt;</c>、<c>Reminder.&lt;項目&gt;</c>）。
/// 値は JSON で持つので、string / bool / int / long / double はそのまま使える（型ごとの専用のメソッド。それ以外の型を渡すと、実行時ではなく、ビルドで誤りになる）。
/// それ以外の型は、使う側が <c>JsonSerializable</c> で登録した <see cref="JsonTypeInfo{T}"/> を渡す。
/// 読み書きはスレッドセーフ。ファイルが無い・空・壊れているときは空の設定として扱い、例外は投げない。
/// 壊れていたファイルは退避してから作り直す（<see cref="RecoveryMessage"/>）。
/// ロック・権限などで読めなかったときも、空の設定として扱うが、元のファイルを空で上書きしないよう、保存はしない（<see cref="IsReadOnly"/>。例外ではなく、保存の結果の false で知らせる）。
/// 値の取得は、無い・型違いのときに既定値を返す <c>Get</c> と、有無を結果で返す <c>TryGet</c>（既定値と区別したいとき）がある。
/// </remarks>
public interface ISettingsStore
{
    /// <summary>ファイルを読めなかったときのメッセージ。正常なら null。</summary>
    /// <remarks>ロック・権限などで読めなかったとき。このときは空の設定として扱い、保存はしない（<see cref="IsReadOnly"/>）。</remarks>
    string? LoadError { get; }

    /// <summary>読み取り専用か（読み込みに失敗していて、保存できない状態か）</summary>
    /// <remarks>
    /// true の間、<c>SetAsync</c> / <c>RemoveAsync</c> は何も書かず、保存しなかったことを結果（false）で返す。
    /// 空の設定でファイルを上書きして、読めなかっただけの既存の設定を失うのを防ぐ。呼ぶ側は、保存の前にこれで確かめて、画面で知らせてもよい。
    /// </remarks>
    bool IsReadOnly { get; }

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。通常は null。</summary>
    string? RecoveryMessage { get; }

    /// <summary>文字列の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    string Get(string key, string defaultValue);

    /// <summary>真偽値の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    bool Get(string key, bool defaultValue);

    /// <summary>整数（int）の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    int Get(string key, int defaultValue);

    /// <summary>整数（long）の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    long Get(string key, long defaultValue);

    /// <summary>小数の値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    double Get(string key, double defaultValue);

    /// <summary>値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <typeparam name="T">値の型</typeparam>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    T Get<T>(string key, T defaultValue, JsonTypeInfo<T> typeInfo);

    /// <summary>文字列の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true（無い・型が合わないときは false）</returns>
    bool TryGet(string key, [MaybeNullWhen(false)] out string value);

    /// <summary>真偽値の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true（無い・型が合わないときは false）</returns>
    bool TryGet(string key, out bool value);

    /// <summary>整数（int）の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true（無い・型が合わないときは false）</returns>
    bool TryGet(string key, out int value);

    /// <summary>整数（long）の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true（無い・型が合わないときは false）</returns>
    bool TryGet(string key, out long value);

    /// <summary>小数の値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true（無い・型が合わないときは false）</returns>
    bool TryGet(string key, out double value);

    /// <summary>値を取得する。無い・型が合わないときは false を返す。</summary>
    /// <typeparam name="T">値の型</typeparam>
    /// <param name="key">設定のキー</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <param name="value">保存されている値。取得できなかったときは既定値</param>
    /// <returns>取得できれば true（無い・型が合わないときは false）</returns>
    bool TryGet<T>(string key, JsonTypeInfo<T> typeInfo, [MaybeNullWhen(false)] out T value);

    /// <summary>文字列の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用（読み込みに失敗している）ため保存しなかったら false</returns>
    Task<bool> SetAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>真偽値の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用（読み込みに失敗している）ため保存しなかったら false</returns>
    Task<bool> SetAsync(string key, bool value, CancellationToken cancellationToken = default);

    /// <summary>整数（int）の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用（読み込みに失敗している）ため保存しなかったら false</returns>
    Task<bool> SetAsync(string key, int value, CancellationToken cancellationToken = default);

    /// <summary>整数（long）の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用（読み込みに失敗している）ため保存しなかったら false</returns>
    Task<bool> SetAsync(string key, long value, CancellationToken cancellationToken = default);

    /// <summary>小数の値を保存する。保存し終えるまで待つ。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用（読み込みに失敗している）ため保存しなかったら false</returns>
    Task<bool> SetAsync(string key, double value, CancellationToken cancellationToken = default);

    /// <summary>値を保存する。保存し終えるまで待つ。</summary>
    /// <typeparam name="T">値の型</typeparam>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存したら true。読み取り専用（読み込みに失敗している）ため保存しなかったら false</returns>
    Task<bool> SetAsync<T>(string key, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default);

    /// <summary>キーが存在するか</summary>
    /// <param name="key">設定のキー</param>
    /// <returns>存在すれば true</returns>
    bool Contains(string key);

    /// <summary>キーを削除する。存在して削除して保存したときは true。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>存在して削除して保存したとき true（存在しない、または読み取り専用のため保存しなかったときは false）</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
}
