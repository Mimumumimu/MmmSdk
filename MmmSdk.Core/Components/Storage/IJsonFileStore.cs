using System.Text.Json.Serialization.Metadata;

namespace MmmSdk.Core.Components.Storage;

/// <summary>
/// データフォルダ内の JSON ファイルを読み書きする (保存先を差し替えたり、テストでモックにしたりするための口)。
/// </summary>
/// <remarks>書き込みは一時ファイルに書いてから置き換え、途中で失敗しても元のファイルを壊さない (<see cref="JsonFileStore"/>)。</remarks>
public interface IJsonFileStore
{
    /// <summary>ファイルが存在するか</summary>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <returns>存在すれば true</returns>
    bool Exists(string fileName);

    /// <summary>同期で読み込む</summary>
    /// <typeparam name="T">読み込む値の型</typeparam>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <returns>読み込んだ値と、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>
    /// ファイルが無い・空 (空白だけ)なら値は null。JSON として読めないファイルは退避して値を null で返す (壊れたファイルの退避)。
    /// 小さなファイルを起動時などに UI スレッドで読む用途向け。
    /// </remarks>
    /// <exception cref="DataFileException">ファイルを読めなかった・退避できなかった (ロック・権限など)。</exception>
    DataLoadResult<T?> Read<T>(string fileName, JsonTypeInfo<T> typeInfo);

    /// <summary>読み込む</summary>
    /// <typeparam name="T">読み込む値の型</typeparam>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込んだ値と、壊れたファイルを退避したときのメッセージ</returns>
    /// <remarks>ファイルが無い・空 (空白だけ)なら値は null。JSON として読めないファイルは退避して値を null で返す (壊れたファイルの退避)。</remarks>
    /// <exception cref="DataFileException">ファイルを読めなかった・退避できなかった (ロック・権限など)。</exception>
    Task<DataLoadResult<T?>> ReadAsync<T>(string fileName, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default);

    /// <summary>書き込む。一時ファイルに書いてから置き換える</summary>
    /// <typeparam name="T">書き込む値の型</typeparam>
    /// <param name="fileName">データフォルダ内のファイル名</param>
    /// <param name="value">書き込む値</param>
    /// <param name="typeInfo">値の型のソース生成メタデータ</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <exception cref="DataFileException">保存できなかった (ロック・権限など)。</exception>
    Task WriteAsync<T>(string fileName, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken = default);
}
