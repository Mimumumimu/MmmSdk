namespace MmmSdk.Core.Repositories;

/// <summary>
/// 保存データの読み込み結果。
/// </summary>
/// <remarks>
/// 保存ファイルが壊れていて退避・作り直しをしたときは <see cref="RecoveryMessage"/> にその旨が入る（そのままユーザーへ表示できる形）。
/// </remarks>
/// <param name="Value">読み込んだ値</param>
/// <param name="RecoveryMessage">壊れたファイルを退避して作り直したときのメッセージ。通常は null</param>
public sealed record DataLoadResult<T>(T Value, string? RecoveryMessage = null);
