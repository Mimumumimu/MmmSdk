namespace MmmSdk.Db.Sqlite.Components.Connections;

/// <summary>
/// SQLite のデータベースファイルの設定。
/// </summary>
public sealed class SqliteDatabaseOptions
{
    /// <summary>データベースのファイルのパス (フォルダーが無ければ作る)</summary>
    public required string FilePath { get; init; }

    /// <summary>版を上げるスクリプト (連番で、抜けがないこと。並びは問わない)</summary>
    public IReadOnlyList<SqliteMigration> Migrations { get; init; } = [];

    /// <summary>ロックを待つ秒数 (既定は 30 秒)</summary>
    public int BusyTimeoutSeconds { get; init; } = 30;
}
