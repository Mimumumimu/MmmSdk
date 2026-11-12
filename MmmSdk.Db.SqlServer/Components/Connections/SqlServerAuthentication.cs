namespace MmmSdk.Db.SqlServer.Components.Connections;

/// <summary>SQL Server への認証の方式</summary>
public enum SqlServerAuthentication
{
    /// <summary>SQL Server 認証</summary>
    /// <remarks>ユーザー名とパスワードで認証する</remarks>
    Sql,

    /// <summary>Windows 認証</summary>
    /// <remarks>今の Windows ユーザーで接続する</remarks>
    Windows,
}
