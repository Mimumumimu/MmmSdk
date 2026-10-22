namespace MmmSdk.Db.Sqlite.Components.Connections;

/// <summary>
/// 列の名前の変更。古い名前の列がある DB だけ、新しい名前に変える。
/// </summary>
/// <param name="Table">表の名前</param>
/// <param name="OldName">変える前の列の名前</param>
/// <param name="NewName">変えたあとの列の名前 (あるべき形の <see cref="SqliteDatabaseOptions.SchemaSql"/> に書いた名前)</param>
public sealed record SqliteColumnRename(string Table, string OldName, string NewName);
