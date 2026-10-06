namespace MmmSdk.Db.SqlServer.Components.Connections;

/// <summary>
/// SQL Server への認証の方式。
/// </summary>
public enum SqlServerAuthentication
{
    /// <summary>SQL Server 認証 (ユーザー名とパスワード)</summary>
    Sql,

    /// <summary>Windows 認証 (今の Windows ユーザーで接続する)</summary>
    Windows,
}
