namespace MmmSdk.Db.Sqlite.Components.Connections;

/// <summary>
/// データベースの版を 1 つ上げるスクリプト。
/// </summary>
/// <param name="Version">この版 (1 から始まる連番。実行後の <c>PRAGMA user_version</c> になる)</param>
/// <param name="Sql">実行する SQL (何度流しても壊れない形にする。<c>IF NOT EXISTS</c> など)</param>
public sealed record SqliteMigration(int Version, string Sql);
