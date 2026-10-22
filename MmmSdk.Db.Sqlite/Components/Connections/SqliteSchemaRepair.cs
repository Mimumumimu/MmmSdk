using Microsoft.Data.Sqlite;

namespace MmmSdk.Db.Sqlite.Components.Connections;

/// <summary>
/// データベースの形を、あるべき形 (<see cref="SqliteDatabaseOptions.SchemaSql"/>)と比べて、足りないものを足す。
/// </summary>
/// <remarks>
/// 足すのは、足りない列と、足りない表・索引だけ。型の変更・削除は直せないので、番号つきのスクリプトで行う。
/// 列の名前の変更は、比べる前に、<see cref="SqliteDatabaseOptions.ColumnRenames"/> で行う。
/// 足りない列は、あるべき形の <c>CREATE TABLE</c> に書いた定義のまま、<c>ALTER TABLE ... ADD COLUMN</c> で表の末尾に付ける。
/// 版の番号 (<c>PRAGMA user_version</c>)には頼らない。どの版の DB でも、何度流しても、同じ結果になる。
/// </remarks>
internal static class SqliteSchemaRepair
{
    /// <summary>列の定義ではない、表の制約の書き出しの語</summary>
    private static readonly HashSet<string> ConstraintKeywords = new(StringComparer.OrdinalIgnoreCase) { "CONSTRAINT", "PRIMARY", "UNIQUE", "CHECK", "FOREIGN" };

    /// <summary>古い名前の列がある表だけ、列の名前を変える</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">書き込みのトランザクション</param>
    /// <param name="renames">列の名前の変更</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変更の完了を表すタスク</returns>
    /// <remarks>表が無い・古い名前の列が無い・新しい名前の列がすでにある場合は、何もしない。値は、列を作り直さずに引き継がれる。</remarks>
    public static async Task RenameColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<SqliteColumnRename> renames, CancellationToken cancellationToken)
    {
        foreach (var rename in renames)
        {
            var actual = await ReadColumnNamesAsync(connection, transaction, rename.Table, cancellationToken).ConfigureAwait(false);
            if (actual.Contains(rename.OldName) && !actual.Contains(rename.NewName))
            {
                await ExecuteAsync(connection, transaction, $"ALTER TABLE {Quote(rename.Table)} RENAME COLUMN {Quote(rename.OldName)} TO {Quote(rename.NewName)}", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>あるべき形と実際の DB を比べて、足りないものを足す</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">書き込みのトランザクション</param>
    /// <param name="schemaSql">あるべき表と索引の SQL (<c>CREATE TABLE IF NOT EXISTS</c> と <c>CREATE INDEX IF NOT EXISTS</c> だけ)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>点検の完了を表すタスク</returns>
    /// <remarks>先に、すでにある表の足りない列を足し、そのあとで、あるべき形の SQL を流して、足りない表と索引を作る。列を使う索引は、列を足してからでないと作れないため。</remarks>
    public static async Task RepairAsync(SqliteConnection connection, SqliteTransaction transaction, string schemaSql, CancellationToken cancellationToken)
    {
        foreach (var (table, createSql) in await ReadExpectedTablesAsync(schemaSql, cancellationToken).ConfigureAwait(false))
        {
            var actual = await ReadColumnNamesAsync(connection, transaction, table, cancellationToken).ConfigureAwait(false);
            if (actual.Count == 0)
            {
                // 表が無い。あとで、あるべき形の SQL が作る
                continue;
            }

            foreach (var (name, definition) in ColumnDefinitions(createSql))
            {
                if (!actual.Contains(name))
                {
                    await ExecuteAsync(connection, transaction, $"ALTER TABLE {Quote(table)} ADD COLUMN {definition}", cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await ExecuteAsync(connection, transaction, schemaSql, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>あるべき形の SQL を、メモリ上の空の DB に流して、表の名前と <c>CREATE TABLE</c> の文を読む</summary>
    /// <param name="schemaSql">あるべき表と索引の SQL</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>表の名前と、<c>CREATE TABLE</c> の文の組</returns>
    private static async Task<List<(string Table, string CreateSql)>> ReadExpectedTablesAsync(string schemaSql, CancellationToken cancellationToken)
    {
        await using var memory = new SqliteConnection("Data Source=:memory:");
        await memory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(memory, null, schemaSql, cancellationToken).ConfigureAwait(false);

        var tables = new List<(string, string)>();
        await using var command = memory.CreateCommand();
        command.CommandText = "SELECT name, sql FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY rowid";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            tables.Add((reader.GetString(0), reader.GetString(1)));
        }
        return tables;
    }

    /// <summary>実際の DB の表が持つ列の名前を読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション</param>
    /// <param name="table">表の名前</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>列の名前で、大文字小文字は区別しない。表が無いときは空</returns>
    private static async Task<HashSet<string>> ReadColumnNamesAsync(SqliteConnection connection, SqliteTransaction transaction, string table, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({Quote(table)})";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            names.Add(reader.GetString(1));
        }
        return names;
    }

    /// <summary><c>CREATE TABLE</c> の文から、列の定義を 1 つずつ取り出す</summary>
    /// <param name="createSql"><c>CREATE TABLE</c> の文 (表の中にコメントを含まないこと)</param>
    /// <returns>列の名前と、<c>ADD COLUMN</c> にそのまま使える定義の組。表の制約 (<c>PRIMARY KEY</c> など)は含まない</returns>
    private static IEnumerable<(string Name, string Definition)> ColumnDefinitions(string createSql)
    {
        var open = createSql.IndexOf('(');
        if (open < 0)
        {
            yield break;
        }

        var depth = 0;
        var start = open + 1;
        var quote = '\0';
        for (var i = open; i < createSql.Length; i++)
        {
            var c = createSql[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    if (i + 1 < createSql.Length && createSql[i + 1] == quote)
                    {
                        // 文字の中の引用符は、2 つ重ねる
                        i++;
                    }
                    else
                    {
                        quote = '\0';
                    }
                }
                continue;
            }

            switch (c)
            {
                case '\'' or '"' or '`':
                    quote = c;
                    break;
                case '[':
                    quote = ']';
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth == 0)
                    {
                        if (ToColumn(createSql[start..i]) is { } last)
                        {
                            yield return last;
                        }
                        yield break;
                    }
                    break;
                case ',' when depth == 1:
                    if (ToColumn(createSql[start..i]) is { } column)
                    {
                        yield return column;
                    }
                    start = i + 1;
                    break;
            }
        }
    }

    /// <summary>表の中の 1 つの項目から、列の名前と定義を取り出す</summary>
    /// <param name="segment">カンマで区切った 1 つの項目</param>
    /// <returns>列の名前と定義。列ではない (表の制約)ときは null</returns>
    private static (string Name, string Definition)? ToColumn(string segment)
    {
        var text = segment.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        string name;
        if (text[0] is '"' or '`' or '[')
        {
            var close = text[0] == '[' ? ']' : text[0];
            var end = text.IndexOf(close, 1);
            if (end < 0)
            {
                return null;
            }
            name = text[1..end];
        }
        else
        {
            var end = text.IndexOfAny([' ', '\t', '\r', '\n', '(']);
            name = end < 0 ? text : text[..end];
            if (ConstraintKeywords.Contains(name))
            {
                return null;
            }
        }
        return (name, text);
    }

    /// <summary>名前を、SQL の識別子として引用する</summary>
    /// <param name="name">表の名前</param>
    /// <returns>二重引用符で囲んだ名前</returns>
    private static string Quote(string name) => $"\"{name.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    /// <summary>SQL を実行する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション (使わないなら null)</param>
    /// <param name="sql">SQL</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実行の完了を表すタスク</returns>
    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
