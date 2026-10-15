# SQLite のデータベースファイル

`MmmSdk.Db.Sqlite`(`net10.0`)の設計。SQLite のデータベースファイルを開いて、版を上げる汎用の部品で、表・SQL・業務の知識は持たない。外部のドライバー `Microsoft.Data.Sqlite` を参照する。独立したプロジェクトにするのは、`MmmSdk.Core` がトリミング・AOT 対応を宣言していて、`Microsoft.Data.Sqlite` が含むネイティブ DLL (`e_sqlite3.dll`)がその宣言と合わないため (ドライバーを使わないアプリには、その DLL が入らない。[architecture.md](architecture.md) の「依存先の違い」。`MmmSdk.Db.SqlServer` と同じ理由)。

## 型
名前空間は `MmmSdk.Db.Sqlite.Components.Connections`。

| 型 | 内容 |
| --- | --- |
| `SqliteDatabaseOptions` | 設定。`FilePath`(ファイルのパス)・`Migrations`(版を上げるスクリプト)・`BusyTimeoutSeconds`(ロックを待つ秒数。既定は 30) |
| `SqliteMigration` | 版を上げるスクリプト 1 つ(`Version`・`Sql`) |
| `SqliteDatabase` | ファイルを開いて処理を実行する入口。`RunAsync`(接続を開いて処理)・`RunInTransactionAsync`(書き込みのトランザクションの中で処理)・`RecoveryMessage`(壊れたファイルを退避したときのメッセージ) |

DI への登録は持たない。ファイルの場所と、スクリプトの読み込みは、アプリが組み立てて渡す。失敗は、画面に出せるメッセージの `DataFileException`(`MmmSdk.Core.Components.Storage`)にして渡す。

## 使い方
```csharp
var database = new SqliteDatabase(new SqliteDatabaseOptions
{
    FilePath = Path.Combine(environment.DataDirectory, "App.db"),
    Migrations = [new SqliteMigration(1, "CREATE TABLE IF NOT EXISTS ...;")],
});

await database.RunInTransactionAsync(async (connection, transaction) =>
{
    // connection と transaction を使って、パラメーター化した SQL を実行する
});
```

## 決まり
- **開くとき**: 最初の 1 度だけ、フォルダとファイルを作り、`PRAGMA user_version` より新しい版のスクリプトを順に 1 つのトランザクションで実行して、`user_version` を更新する。スクリプトは何度流しても壊れない形(`IF NOT EXISTS`)にする
- **壊れたファイル**: DB として開けない(`SQLITE_NOTADB`・`SQLITE_CORRUPT`)ときは、同じフォルダの `名前.broken-yyyyMMdd-HHmmss.db`(同じ秒なら `-2` 以降)へ名前を変えて退避し、空の DB を作り直して `RecoveryMessage` に知らせる。自動では消さない。ロック・権限などの IO エラーは退避できないので、`DataFileException` にして止める
- **接続**: 処理のたびに開いて閉じる。接続プールは使わない(閉じた接続がファイルを掴んだままにならず、退避やバックアップのコピーが妨げられない)。ジャーナルは既定の方式(`DELETE`)で、ファイルが 1 つのまま残る
- **書き込み**: `RunInTransactionAsync` は書き込みの許可を最初に取る(`BEGIN IMMEDIATE`)。読んでから書く処理の途中で、ほかの書き込みに割り込まれない。ロックは `BusyTimeoutSeconds` まで待つ
- 外部キーは使わない(SQLite は既定でも無効のまま)
