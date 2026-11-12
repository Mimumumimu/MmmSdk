using Microsoft.Data.SqlClient;

namespace MmmSdk.Db.SqlServer.Components.Connections;

/// <summary>SQL Server に接続できなかったときの例外</summary>
/// <param name="message">画面に出せるメッセージ</param>
/// <param name="innerException">
/// 元の例外
/// 多くは <see cref="SqlException"/>
/// 接続プールの待ちが切れたときは <see cref="InvalidOperationException"/>
/// ログに残す
/// </param>
/// <remarks>
/// メッセージはそのまま画面に出せる。
/// 原因で処理を分けたいとき (たとえば、証明書の検証の失敗のときだけ、別の接続を試す)はメッセージの文字を調べず、<see cref="IsUntrustedCertificate"/> を使う。
/// </remarks>
public sealed class SqlServerConnectionException(string message, Exception innerException) : Exception(message, innerException)
{
    /// <summary>証明書の検証の失敗のエラー番号</summary>
    /// <remarks>信頼されていない機関が発行した証明書の場合</remarks>
    internal const int UntrustedCertificateNumber = -2146893019;

    /// <summary>サーバーの証明書を信頼できなかったことが原因か</summary>
    /// <remarks>
    /// 例は自己署名の証明書のサーバー。
    /// true のとき、<see cref="SqlServerConnectionOptions.TrustServerCertificate"/> を true にして、接続し直せば、つながることがある。
    /// </remarks>
    public bool IsUntrustedCertificate => InnerException is SqlException { Number: UntrustedCertificateNumber };
}
