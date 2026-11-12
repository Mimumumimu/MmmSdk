namespace MmmSdk.Db.Sqlite.Components.Connections;

/// <summary>SQLite のデータベースファイルの設定</summary>
public sealed class SqliteDatabaseOptions
{
    /// <summary>データベースのファイルのパス</summary>
    /// <remarks>フォルダーが無ければ作る</remarks>
    public required string FilePath { get; init; }

    /// <summary>版を上げるスクリプト</summary>
    /// <remarks>連番で抜けがなければよく、並びは問わない。</remarks>
    public IReadOnlyList<SqliteMigration> Migrations { get; init; } = [];

    /// <summary>あるべき表と索引の SQL</summary>
    /// <remarks>
    /// <c>CREATE TABLE IF NOT EXISTS</c> と <c>CREATE INDEX IF NOT EXISTS</c> だけで書く。
    /// 初期データは含めない。
    /// 最初に開くとき、版の更新のあとで、実際の DB と比べて、足りない列・表・索引を足す。
    /// 表の中にコメントは書かない。
    /// </remarks>
    public string? SchemaSql { get; init; }

    /// <summary>列の名前の変更</summary>
    /// <remarks>
    /// 無ければ空。
    /// 版を上げるスクリプトのあと、あるべき形との比較の前に、古い名前の列があって新しい名前の列が無い表だけ、<c>ALTER TABLE ... RENAME COLUMN</c> で名前を変える。
    /// 列があるかどうかを見て変えるので、新しい DB・変え済みの DB・古い名前の列を持たない DB では何もしない。
    /// 比較の前に変えないと、比較が新しい名前の空の列を足してしまい、古い列の値が引き継がれない。
    /// </remarks>
    public IReadOnlyList<SqliteColumnRename> ColumnRenames { get; init; } = [];

    /// <summary>列の置き換え</summary>
    /// <remarks>
    /// 無ければ空。
    /// あるべき形との比較のあと、古い列と新しい列が両方ある表だけ、新しい列の値を古い列から作り、<c>ALTER TABLE ... DROP COLUMN</c> で古い列を落とす。
    /// 古い列を落とすので、1 度行えば、次からは何もしない。
    /// 比較のあとに行うのは新しい列を、あるべき形の定義 (制約つき)で足してから、値を移すため。
    /// 古い列を、索引・ビュー・トリガーが使っていると、落とせない。
    /// </remarks>
    public IReadOnlyList<SqliteColumnReplacement> ColumnReplacements { get; init; } = [];

    /// <summary>ロックを待つ秒数</summary>
    public int BusyTimeoutSeconds { get; init; } = 30;
}
