using System.Text.Json.Serialization.Metadata;

namespace MmmSdk.Core.Repositories;

/// <summary>
/// キーに対して任意の型の値を保存・取得する、アプリ共通の汎用設定ストア。
/// </summary>
/// <remarks>
/// 機能ごとの設定サービスは、キーに接頭辞を付けて委譲すると衝突しない（例: <c>WindowPosition.&lt;キー&gt;</c>、<c>Reminder.&lt;項目&gt;</c>）。
/// 値は JSON で持つので、string / bool / int / long / double はそのまま使える。それ以外の型は、
/// 使う側が <c>JsonSerializable</c> で登録した <see cref="JsonTypeInfo{T}"/> を渡す。
/// 読み書きはスレッドセーフ。ファイルが無い・空・壊れているときは空の設定として扱い、例外は投げない。
/// 壊れていたファイルは退避してから作り直す（<see cref="RecoveryMessage"/>）。
/// </remarks>
public interface ISettingsStore
{
    /// <summary>ファイルを読めなかったときのメッセージ。正常なら null。</summary>
    /// <remarks>ロック・権限などで読めなかったとき。このときも空の設定として扱い、保存は試みる。</remarks>
    string? LoadError { get; }

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。通常は null。</summary>
    string? RecoveryMessage { get; }

    /// <summary>値を取得する。無い・型が合わないときは既定値を返す（基本型用）。</summary>
    /// <typeparam name="T">値の型（string / bool / int / long / double）</typeparam>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    T Get<T>(string key, T defaultValue);

    /// <summary>値を取得する。無い・型が合わないときは既定値を返す。</summary>
    /// <typeparam name="T">値の型</typeparam>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">値が無い・型が合わないときに返す値</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <returns>保存されている値。無い・型が合わないときは既定値</returns>
    T Get<T>(string key, T defaultValue, JsonTypeInfo<T> typeInfo);

    /// <summary>値を保存する（基本型用）。保存し終えるまで待つ。</summary>
    /// <typeparam name="T">値の型（string / bool / int / long / double）</typeparam>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);

    /// <summary>値を保存する。保存し終えるまで待つ。</summary>
    /// <typeparam name="T">値の型</typeparam>
    /// <param name="key">設定のキー</param>
    /// <param name="value">保存する値</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    Task SetAsync<T>(string key, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default);

    /// <summary>キーが存在するか</summary>
    /// <param name="key">設定のキー</param>
    /// <returns>存在すれば true</returns>
    bool Contains(string key);

    /// <summary>キーを削除する。存在して削除したときは true。</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>存在して削除したとき true</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);
}
