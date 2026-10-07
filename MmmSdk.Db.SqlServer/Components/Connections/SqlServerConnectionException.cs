using Microsoft.Data.SqlClient;

namespace MmmSdk.Db.SqlServer.Components.Connections;

/// <summary>
/// SQL Server に接続できなかったときの例外。メッセージはそのまま画面に出せる。
/// </summary>
/// <param name="message">画面に出せるメッセージ</param>
/// <param name="innerException">元の例外 (<see cref="SqlException"/>)。ログに残す</param>
/// <remarks>原因で処理を分けたいとき (たとえば、証明書の検証の失敗のときだけ、別の接続を試す)は、メッセージの文字を調べず、<see cref="IsUntrustedCertificate"/> を使う。</remarks>
public sealed class SqlServerConnectionException(string message, SqlException innerException) : Exception(message, innerException)
{
    /// <summary>証明書の検証の失敗 (信頼されていない機関が発行した証明書)のエラー番号</summary>
    internal const int UntrustedCertificateNumber = -2146893019;

    /// <summary>サーバーの証明書を信頼できなかったことが原因か (自己署名の証明書のサーバーなど)</summary>
    /// <remarks>true のとき、<see cref="SqlServerConnectionOptions.TrustServerCertificate"/> を true にして、接続し直せば、つながることがある。</remarks>
    public bool IsUntrustedCertificate => innerException.Number == UntrustedCertificateNumber;
}
