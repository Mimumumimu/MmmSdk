using Microsoft.Data.SqlClient;

namespace MmmSdk.Db.SqlServer.Components.Connections;

/// <summary>
/// SQL Server に接続できなかったときの例外。メッセージはそのまま画面に出せる。
/// </summary>
/// <param name="message">画面に出せるメッセージ</param>
/// <param name="innerException">元の例外 (<see cref="SqlException"/>)。ログに残す</param>
public sealed class SqlServerConnectionException(string message, SqlException innerException) : Exception(message, innerException);
