namespace MmmSdk.Db.Sqlite.Components.Connections;

/// <summary>
/// 列の置き換え。古い列と新しい列が両方ある DB だけ、古い列の値から新しい列の値を作り、古い列を落とす。
/// </summary>
/// <param name="Table">表の名前</param>
/// <param name="OldName">置き換える古い列の名前</param>
/// <param name="NewName">新しい列の名前 (あるべき形の <see cref="SqliteDatabaseOptions.SchemaSql"/> に書いた名前)</param>
/// <param name="NewValueSql">新しい列に入れる値を、古い列から求める SQL の式 (例: <c>CASE "Old" WHEN 1 THEN 2 ELSE 0 END</c>)</param>
/// <remarks>
/// 列の名前の変更 (<see cref="SqliteColumnRename"/>)では、古い列の制約 (<c>CHECK</c> など)を引き継いでしまい、値の範囲を広げられない。
/// そのとき、新しい列をあるべき形の点検で足し、この置き換えで、値を移してから古い列を落とす。
/// </remarks>
public sealed record SqliteColumnReplacement(string Table, string OldName, string NewName, string NewValueSql);
