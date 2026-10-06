namespace MmmSdk.Db.SqlServer.Components.Connections;

/// <summary>
/// SQL Server への接続の設定。
/// </summary>
/// <remarks>
/// パスワードを持つので、<c>record</c> ではなく <c>class</c> にしている (<c>record</c> の <c>ToString</c> は全項目を文字にするため、ログに出ると漏れる)。
/// パスワードは設定ファイルに書かず、呼ぶ側が <c>ISecretStore</c> などから読んで渡す。
/// </remarks>
public sealed class SqlServerConnectionOptions
{
    /// <summary>サーバー名 (<c>ホスト名</c>・<c>ホスト名\インスタンス名</c>・<c>ホスト名,ポート</c>)</summary>
    public required string Server { get; init; }

    /// <summary>データベース名</summary>
    public required string Database { get; init; }

    /// <summary>認証の方式 (既定は SQL Server 認証)</summary>
    public SqlServerAuthentication Authentication { get; init; } = SqlServerAuthentication.Sql;

    /// <summary>ユーザー名 (SQL Server 認証のとき)</summary>
    public string? UserName { get; init; }

    /// <summary>パスワード (SQL Server 認証のとき)</summary>
    public string? Password { get; init; }

    /// <summary>
    /// サーバーの証明書を検証せずに信頼するか (既定は false)。
    /// </summary>
    /// <remarks>通信の暗号化は常に必須。自己署名の証明書のサーバーに接続するときだけ true にする。</remarks>
    public bool TrustServerCertificate { get; init; }

    /// <summary>接続を待つ秒数 (既定は 5 秒)</summary>
    public int ConnectTimeoutSeconds { get; init; } = 5;
}
