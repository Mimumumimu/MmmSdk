# SQLite のデータベースファイル

`MmmSdk.Db.Sqlite`(`net10.0`)の設計。SQLite のデータベースファイルを開いて、版を上げる汎用の部品で、表・SQL・業務の知識は持たない。外部のドライバー `Microsoft.Data.Sqlite` を参照する。独立したプロジェクトにするのは、`MmmSdk.Core` がトリミング・AOT 対応を宣言していて、`Microsoft.Data.Sqlite` が含むネイティブ DLL (`e_sqlite3.dll`)がその宣言と合わないため (ドライバーを使わないアプリには、その DLL が入らない。[architecture.md](architecture.md) の「依存先の違い」。`MmmSdk.Db.SqlServer` と同じ理由)。

## 型
名前空間は `MmmSdk.Db.Sqlite.Components.Connections`。

| 型 | 内容 |
| --- | --- |
| `SqliteDatabaseOptions` | 設定。`FilePath`(ファイルのパス)・`Migrations`(版を上げるスクリプト)・`SchemaSql`(あるべき表と索引の SQL で、無ければ点検しない)・`ColumnRenames`(列の名前の変更)・`ColumnReplacements`(列の置き換え)・`BusyTimeoutSeconds`(ロックを待つ秒数。既定は 30) |
| `SqliteMigration` | 版を上げるスクリプト 1 つ(`Version`・`Sql`) |
| `SqliteColumnRename` | 列の名前の変更 1 つ (`Table`・`OldName`・`NewName`) |
| `SqliteColumnReplacement` | 列の置き換え 1 つ (`Table`・`OldName`・`NewName`・`NewValueSql`) |
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
- **あるべき形の点検**: `SchemaSql` があるときは、版の更新のあと、同じトランザクションの中で、実際の DB と比べて、足りないものを足す (`SqliteSchemaRepair`)
  - 足りない列は、`CREATE TABLE` に書いた定義のまま `ALTER TABLE ... ADD COLUMN` で足す。`CHECK` も含む。列は表の末尾に付く。
  - そのあと、`SchemaSql` を流して、足りない表と索引を作る。列を使う索引は、列を足してからでないと作れないため、この順にする。
  - 版の番号 (`user_version`)には頼らない。書き足したスクリプトが、流し済みの DB に流れなかった場合も、整う。
  - 直せないもの: 型の変更・列の制約の変更・削除・データの書き換え。値の範囲を広げたいときは、新しい名前の列を足して、下の「列の置き換え」で値を移す。それ以外は番号つきのスクリプトで行う。
  - `SchemaSql` は、`CREATE TABLE IF NOT EXISTS` と `CREATE INDEX IF NOT EXISTS` だけで書き、初期データを含めない。
  - `SchemaSql` の表の中に、コメントを書かない。列の定義を取り出すとき、定義の一部になるため。
  - 版のスクリプトを細かく上げる方式にしない。書き換えたスクリプトは流し済みの DB に届かないが、点検は実際の DB の形だけを見るため。
- **列の名前の変更**: `ColumnRenames` があるときは、版の更新のあと、点検の前に、同じトランザクションの中で `ALTER TABLE ... RENAME COLUMN` を行う (`SqliteSchemaRepair.RenameColumnsAsync`)
  - 古い名前の列があって、新しい名前の列が無い表だけ変える。新しい DB・変え済みの DB・古い名前の列を持たない DB では、何もしない。
  - 点検の前に行う。後だと、点検が新しい名前の空の列を足してしまい、古い列の値が引き継がれない。
  - 番号つきのスクリプトにしない。スクリプトは、列があるかどうかで分けられず、列の無い DB (リリース前に作った DB など)で失敗するため。
  - 列の名前だけを変える。古い列の `CHECK` は引き継がれるので、値の範囲を広げる変更には使わない。
- **列の置き換え**: `ColumnReplacements` があるときは、点検のあと、同じトランザクションの中で、値を移して古い列を落とす (`SqliteSchemaRepair.ReplaceColumnsAsync`)
  - 古い列と新しい列が両方ある表だけ行う。新しい列の値を `NewValueSql` の式 (古い列から求める)で書き、そのあと `ALTER TABLE ... DROP COLUMN` で古い列を落とす。
  - 古い列を落とすので、1 度行えば、次からは何もしない。
  - 点検のあとに行う。新しい列を、あるべき形の定義 (新しい `CHECK` つき)で足してから、値を移すため。名前の変更が点検の前なのは、新しい名前の空の列を足させないためで、順序が逆になる。
  - 古い列を、索引・ビュー・トリガーが使っていると、落とせない。
  - `DROP COLUMN` は SQLite 3.35 以降の機能で、Microsoft.Data.Sqlite が同梱する SQLite は、これを満たす。
- **壊れたファイル**: DB として開けない(`SQLITE_NOTADB`・`SQLITE_CORRUPT`)ときは、同じフォルダの `名前.broken-yyyyMMdd-HHmmss.db`(同じ秒なら `-2` 以降)へ名前を変えて退避し、空の DB を作り直して `RecoveryMessage` に知らせる。自動では消さない。ロック・権限などの IO エラーは退避できないので、`DataFileException` にして止める
- **接続**: 処理のたびに開いて閉じる。接続プールは使わない(閉じた接続がファイルを掴んだままにならず、退避やバックアップのコピーが妨げられない)。ジャーナルは既定の方式(`DELETE`)で、ファイルが 1 つのまま残る
- **書き込み**: `RunInTransactionAsync` は書き込みの許可を最初に取る(`BEGIN IMMEDIATE`)。読んでから書く処理の途中で、ほかの書き込みに割り込まれない。ロックは `BusyTimeoutSeconds` まで待つ
- 外部キーは使わない(SQLite は既定でも無効のまま)
