using Microsoft.Data.Sqlite;
using MmmSdk.Core.Components.Storage;

namespace MmmSdk.Db.Sqlite.Components.Connections;

/// <summary>
/// SQLite のデータベースファイルを開いて、処理を実行する入口。アプリ全体で 1 つ。
/// </summary>
/// <param name="options">ファイルの場所と、版を上げるスクリプト</param>
/// <remarks>
/// 最初に開くとき、ファイルが無ければ作り、<c>PRAGMA user_version</c> より新しい版のスクリプトを順に 1 つのトランザクションで実行する。
/// DB として開けない (<c>SQLITE_NOTADB</c>・<c>SQLITE_CORRUPT</c>)ときは、同じフォルダの <c>名前.broken-yyyyMMdd-HHmmss.db</c> へ名前を変えて退避し
/// (同じ秒なら <c>-2</c> 以降)、空の DB を作り直して <see cref="RecoveryMessage"/> に知らせる (自動では消さない)。
/// 接続は処理のたびに開いて閉じる。接続プールは使わない (閉じた接続がファイルを掴んだままにならず、退避やバックアップのコピーが妨げられないため)。
/// 失敗は、画面に出せるメッセージの <see cref="DataFileException"/> にして渡す。
/// </remarks>
public sealed class SqliteDatabase(SqliteDatabaseOptions options)
{
    /// <summary>SQLITE_BUSY</summary>
    private const int SqliteBusy = 5;

    /// <summary>SQLITE_LOCKED</summary>
    private const int SqliteLocked = 6;

    /// <summary>SQLITE_READONLY</summary>
    private const int SqliteReadOnly = 8;

    /// <summary>SQLITE_CORRUPT</summary>
    private const int SqliteCorrupt = 11;

    /// <summary>SQLITE_FULL</summary>
    private const int SqliteFull = 13;

    /// <summary>SQLITE_CANTOPEN</summary>
    private const int SqliteCantOpen = 14;

    /// <summary>SQLITE_NOTADB</summary>
    private const int SqliteNotADb = 26;

    /// <summary>準備 (作成・退避・版の更新)を 1 度だけ行うためのロック</summary>
    private readonly SemaphoreSlim _prepareLock = new(1, 1);

    /// <summary>準備が済んだか</summary>
    private volatile bool _prepared;

    /// <summary>壊れたファイルを退避して作り直したときのメッセージ。通常は null</summary>
    public string? RecoveryMessage { get; private set; }

    /// <summary>データベースのファイルのパス</summary>
    public string FilePath => options.FilePath;

    /// <summary>接続を開いて、処理を実行する</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の結果</returns>
    /// <exception cref="DataFileException">開けない・実行中に失敗した (メッセージは画面に出せる)。</exception>
    public async Task<T> RunAsync<T>(Func<SqliteConnection, Task<T>> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        try
        {
            await PrepareAsync(cancellationToken).ConfigureAwait(false);
            await using var connection = Open();
            return await work(connection).ConfigureAwait(false);
        }
        catch (SqliteException ex)
        {
            throw new DataFileException(Describe(ex), ex);
        }
        catch (IOException ex)
        {
            throw new DataFileException($"データベースのファイルを読み書きできません ({ex.Message})。", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new DataFileException($"データベースのファイルを読み書きできません ({ex.Message})。", ex);
        }
    }

    /// <summary>接続を開いて、結果を返さない処理を実行する</summary>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の完了を表すタスク</returns>
    /// <exception cref="DataFileException">開けない・実行中に失敗した (メッセージは画面に出せる)。</exception>
    public Task RunAsync(Func<SqliteConnection, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return RunAsync(async connection =>
        {
            await work(connection).ConfigureAwait(false);
            return 0;
        }, cancellationToken);
    }

    /// <summary>接続を開き、書き込みのトランザクションの中で処理を実行する</summary>
    /// <param name="work">開いた接続とトランザクションで行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の完了を表すタスク</returns>
    /// <remarks>
    /// 書き込みの許可を最初に取る (<c>BEGIN IMMEDIATE</c>)ので、読んでから書く処理の途中で、ほかの書き込みに割り込まれない。
    /// 処理が例外になったときは取り消す。
    /// </remarks>
    /// <exception cref="DataFileException">開けない・実行中に失敗した (メッセージは画面に出せる)。</exception>
    public Task RunInTransactionAsync(Func<SqliteConnection, SqliteTransaction, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        return RunAsync(async connection =>
        {
            await using var transaction = connection.BeginTransaction(deferred: false);
            await work(connection, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken);
    }

    /// <summary>接続を作って開く</summary>
    /// <returns>開いた接続。呼ぶ側が閉じる</returns>
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(ConnectionString());
        try
        {
            connection.Open();
        }
        catch
        {
            connection.Dispose();
            throw;
        }
        return connection;
    }

    /// <summary>接続文字列を作る</summary>
    /// <returns>接続文字列 (プールを使わない)</returns>
    private string ConnectionString() => new SqliteConnectionStringBuilder
    {
        DataSource = options.FilePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = false,
        DefaultTimeout = options.BusyTimeoutSeconds,
    }.ConnectionString;

    /// <summary>最初に 1 度だけ、フォルダとファイルを用意し、壊れていれば退避して作り直し、版を上げる</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>準備の完了を表すタスク</returns>
    private async Task PrepareAsync(CancellationToken cancellationToken)
    {
        if (_prepared)
        {
            return;
        }

        await _prepareLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_prepared)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.FilePath))!);
            try
            {
                await MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode is SqliteNotADb or SqliteCorrupt)
            {
                RecoveryMessage = MoveBrokenFile();
                await MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
            _prepared = true;
        }
        finally
        {
            _prepareLock.Release();
        }
    }

    /// <summary>版を確かめ、新しい版のスクリプトを順に実行し、あるべき形があれば、足りないものを足す</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実行の完了を表すタスク</returns>
    private async Task MigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = Open();
        var current = await ReadVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        var pending = options.Migrations.Where(m => m.Version > current).OrderBy(m => m.Version).ToList();
        if (pending.Count == 0 && options.SchemaSql is null && options.ColumnRenames.Count == 0 && options.ColumnReplacements.Count == 0)
        {
            return;
        }

        await using var transaction = connection.BeginTransaction(deferred: false);
        foreach (var migration in pending)
        {
            await ExecuteAsync(connection, transaction, migration.Sql, cancellationToken).ConfigureAwait(false);
            // PRAGMA はパラメーターを受け付けないので、整数をそのまま書く
            await ExecuteAsync(connection, transaction, $"PRAGMA user_version = {migration.Version}", cancellationToken).ConfigureAwait(false);
        }
        // 古い名前の列だけ名前を変える。あるべき形と比べる前に行う (比べると、新しい名前の空の列を足してしまうため)
        await SqliteSchemaRepair.RenameColumnsAsync(connection, transaction, options.ColumnRenames, cancellationToken).ConfigureAwait(false);
        if (options.SchemaSql is { } schemaSql)
        {
            // 版の更新のあとで、あるべき形と比べる。版の番号に頼らないので、書き足したスクリプトが流れなかった DB も整う
            await SqliteSchemaRepair.RepairAsync(connection, transaction, schemaSql, cancellationToken).ConfigureAwait(false);
        }
        // 新しい列が足されたあとで、古い列の値を移して、古い列を落とす
        await SqliteSchemaRepair.ReplaceColumnsAsync(connection, transaction, options.ColumnReplacements, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>データベースの版を読む</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>版 (新しいファイルは 0)。壊れたファイルは、ここで例外になる</returns>
    private static async Task<int> ReadVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>SQL を実行する</summary>
    /// <param name="connection">開いた接続</param>
    /// <param name="transaction">トランザクション</param>
    /// <param name="sql">SQL</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実行の完了を表すタスク</returns>
    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>壊れたファイルを、同じフォルダの別の名前へ移す</summary>
    /// <returns>画面に出せるメッセージ</returns>
    private string MoveBrokenFile()
    {
        // 接続プールを使わないので、閉じた接続がファイルを掴んだままにはならない
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.FilePath))!;
        var name = Path.GetFileNameWithoutExtension(options.FilePath);
        var extension = Path.GetExtension(options.FilePath);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = Path.Combine(directory, $"{name}.broken-{stamp}{extension}");
        for (var n = 2; File.Exists(target); n++)
        {
            target = Path.Combine(directory, $"{name}.broken-{stamp}-{n}{extension}");
        }

        File.Move(options.FilePath, target);
        return $"データベースのファイルが壊れていたため、「{Path.GetFileName(target)}」へ名前を変えて残し、新しく作り直しました。";
    }

    /// <summary>失敗を、画面に出せる 1 行のメッセージにする</summary>
    /// <param name="ex">SQLite の失敗</param>
    /// <returns>原因ごとのメッセージ</returns>
    private static string Describe(SqliteException ex) => ex.SqliteErrorCode switch
    {
        SqliteBusy or SqliteLocked => "データベースのファイルが、ほかの処理で使われています。少し待ってから、もう一度操作してください。",
        SqliteReadOnly or SqliteCantOpen => $"データベースのファイルを開けません ({ex.Message})。",
        SqliteFull => "ディスクの空きが足りないため、データベースに書き込めません。",
        _ => $"データベースの読み書きに失敗しました ({ex.Message})。",
    };
}
