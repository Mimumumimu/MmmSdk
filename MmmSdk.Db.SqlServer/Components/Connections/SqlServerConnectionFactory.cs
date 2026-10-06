using Microsoft.Data.SqlClient;

namespace MmmSdk.Db.SqlServer.Components.Connections;

/// <summary>
/// SQL Server への接続を作る。
/// </summary>
/// <remarks>
/// 接続は操作のたびに開いて閉じる (実体の接続は ADO.NET の接続プールが使い回すので、開くコストは小さい)。
/// 呼ぶ側が <c>await using</c> で閉じる。到達確認の Ping はせず、<see cref="SqlServerConnectionOptions.ConnectTimeoutSeconds"/> で待つ。
/// 接続の失敗は、画面に出せるメッセージつきの <see cref="SqlServerConnectionException"/> にして渡す (握りつぶさない)。
/// </remarks>
public sealed class SqlServerConnectionFactory
{
    /// <summary>証明書の検証の失敗 (信頼されていない機関が発行した証明書)のエラー番号</summary>
    private const int UntrustedCertificate = -2146893019;

    private readonly string _connectionString;
    private readonly SqlServerAuthentication _authentication;

    /// <summary>設定から、接続を作るファクトリを作る</summary>
    /// <param name="options">接続の設定</param>
    /// <exception cref="ArgumentException">サーバー・データベース名が空、または SQL Server 認証でユーザー名・パスワードが空。</exception>
    public SqlServerConnectionFactory(SqlServerConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Server, nameof(options.Server));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Database, nameof(options.Database));

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = options.Server,
            InitialCatalog = options.Database,
            ConnectTimeout = options.ConnectTimeoutSeconds,
            TrustServerCertificate = options.TrustServerCertificate,
            Encrypt = SqlConnectionEncryptOption.Mandatory,
        };
        if (options.Authentication == SqlServerAuthentication.Windows)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.UserName, nameof(options.UserName));
            ArgumentException.ThrowIfNullOrEmpty(options.Password, nameof(options.Password));
            builder.UserID = options.UserName;
            builder.Password = options.Password;
        }

        _connectionString = builder.ConnectionString;
        _authentication = options.Authentication;
    }

    /// <summary>接続を作って開く</summary>
    /// <param name="cancellationToken">取り消し</param>
    /// <returns>開いた接続。呼ぶ側が閉じる</returns>
    /// <exception cref="SqlServerConnectionException">接続できなかった (到達できない・認証の失敗・証明書の検証の失敗・データベースを開けないなど)。メッセージは画面に出せる。</exception>
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw new SqlServerConnectionException(Describe(ex), ex);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        return connection;
    }

    /// <summary>接続の失敗を、画面に出せる 1 行のメッセージにする</summary>
    /// <param name="ex">接続の失敗</param>
    /// <returns>原因ごとのメッセージ。分類できないときは、元のメッセージつき</returns>
    private string Describe(SqlException ex) => ex.Number switch
    {
        UntrustedCertificate => "サーバーの証明書を信頼できません。自己署名の証明書のサーバーに接続するときは、証明書を信頼する設定にしてください。",
        18456 => _authentication == SqlServerAuthentication.Windows
            ? "Windows 認証に失敗しました。このアカウントに、サーバーへのログインがあるか確認してください。"
            : "ログインできません。ユーザー名とパスワードを確認してください。",
        4060 => "データベースを開けません。データベース名と、ユーザーの権限を確認してください。",
        -2 or 258 => "サーバーから時間内に応答がありません。サーバー名と、ネットワークを確認してください。",
        -1 or 2 or 40 or 53 or 10060 or 10061 => "サーバーに接続できません。サーバー名・ポートと、ネットワーク (ファイアウォール・VPN)を確認してください。",
        _ => $"データベースに接続できません ({ex.Message})。",
    };
}
