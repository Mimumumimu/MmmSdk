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

    /// <summary>あるべき表と索引の SQL (無ければ null)</summary>
    /// <remarks>
    /// <c>CREATE TABLE IF NOT EXISTS</c> と <c>CREATE INDEX IF NOT EXISTS</c> だけで書く。初期データは含めない。
    /// 最初に開くとき、版の更新のあとで、実際の DB と比べて、足りない列・表・索引を足す。表の中にコメントは書かない。
    /// </remarks>
    public string? SchemaSql { get; init; }

    /// <summary>ロックを待つ秒数 (既定は 30 秒)</summary>
    public int BusyTimeoutSeconds { get; init; } = 30;
}
