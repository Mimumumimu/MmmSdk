# SQL Server への接続

`MmmSdk.Db.SqlServer`(`net10.0`)の設計。SQL Server への接続を作る汎用の部品で、表・SQL・業務の知識は持たない。外部のドライバー `Microsoft.Data.SqlClient` を参照する (Microsoft の現行の推奨ドライバーで、`System.Data.SqlClient` は非推奨。暗号化が既定で必須なので、安全な設定がそのまま既定になる)。独立したプロジェクトにするのは、`MmmSdk.Core` がトリミング・AOT 対応を宣言していて、SqlClient (リフレクションを使う)を混ぜるとその宣言が合わなくなるため (SQL Server を使わないアプリには SqlClient の DLL が入らず、ほかの DB のドライバーも `MmmSdk.Db.<製品名>` として並べられる。[architecture.md](architecture.md))。

## 型
名前空間は `MmmSdk.Db.SqlServer.Components.Connections`。

| 型 | 内容 |
| --- | --- |
| `SqlServerConnectionOptions` | 接続の設定。`Server`・`Database`・`Authentication`・`UserName`・`Password`・`TrustServerCertificate`・`ConnectTimeoutSeconds` |
| `SqlServerAuthentication` | 認証の方式。`Sql`(ユーザー名とパスワード。既定)/ `Windows`(今の Windows ユーザー) |
| `SqlServerConnectionFactory` | 設定から接続を作って開く (`OpenAsync`) |
| `SqlServerConnectionException` | 接続できなかったときの例外。メッセージはそのまま画面に出せる。元の `SqlException` は `InnerException` |

DI への登録は持たない。設定 (サーバー名・DB 名・パスワードなど)はアプリが読んで組み立てて渡すので、ファクトリはアプリの `Add<機能>()` が作って登録する。ファクトリのインターフェースは持たない (保存先の差し替えは、アプリの Repository のインターフェースで行う。ファクトリは SQL Server 専用の実装の部品で、差し替える対象ではない)。

## 使い方
```csharp
var options = new SqlServerConnectionOptions
{
    Server = "サーバー名",
    Database = "DB 名",
    UserName = "ユーザー名",
    Password = secretStore.Get("<アプリ>.Database"),   // パスワードは ISecretStore (資格情報マネージャー)に保存して読む
    TrustServerCertificate = true,                     // 自己署名の証明書のサーバーのときだけ
};
var factory = new SqlServerConnectionFactory(options);

try
{
    await using var connection = await factory.OpenAsync(cancellationToken);
    // connection を使って、パラメータ化した SQL を実行する
}
catch (SqlServerConnectionException ex)
{
    // ex.Message を画面 (InfoBar など)に出し、ex.InnerException をログに残す
}
```

## 接続の決まり
- 通信の暗号化は常に必須 (`Encrypt` は必須のまま。暗号化を切る設定を持たせると、安全でない接続が簡単に作れてしまうため、設定にするのは `TrustServerCertificate` だけ)。証明書を検証できないサーバー (自己署名)に接続するときだけ、`TrustServerCertificate` を true にする (通信は暗号化されたまま、証明書の発行元を信頼するだけ)
- 認証は `Sql` か `Windows`。`Sql` のときは、ユーザー名とパスワードが空だと `ArgumentException`(呼ぶ側のバグなので、握りつぶさない)
- 接続を待つ秒数 (`ConnectTimeoutSeconds`)の既定は 5 秒。到達確認の Ping はしない
- 接続は、操作のたびに開いて閉じる (`await using`)。実体の接続は ADO.NET の接続プールが使い回す
- `SqlServerConnectionOptions` は、パスワードを持つので `class`(`record` の `ToString` は全項目を文字にするため、ログに出ると漏れる)。パスワードは設定ファイルに書かない

## 接続の失敗
`OpenAsync` の `SqlException` を、次のメッセージの `SqlServerConnectionException` にして渡す。取り消し (`OperationCanceledException`)など、`SqlException` 以外はそのまま渡す。メッセージにして渡すのは、画面に出す文言を、呼ぶ側がエラー番号を調べずに使えるようにするため (`DataFileException`・`SecretStoreException` と同じ形)。分類できない失敗は、元のメッセージつきで渡し、隠さない。

| 原因 | エラー番号 | メッセージ |
| --- | --- | --- |
| 証明書を信頼できない | -2146893019 | サーバーの証明書を信頼できません。自己署名の証明書のサーバーに接続するときは、証明書を信頼する設定にしてください。 |
| ログインの失敗 (SQL 認証) | 18456 | ログインできません。ユーザー名とパスワードを確認してください。 |
| ログインの失敗 (Windows 認証) | 18456 | Windows 認証に失敗しました。このアカウントに、サーバーへのログインがあるか確認してください。 |
| データベースを開けない | 4060 | データベースを開けません。データベース名と、ユーザーの権限を確認してください。 |
| 時間切れ | -2・258 | サーバーから時間内に応答がありません。サーバー名と、ネットワークを確認してください。 |
| サーバーに届かない | -1・2・40・53・10060・10061 | サーバーに接続できません。サーバー名・ポートと、ネットワーク (ファイアウォール・VPN)を確認してください。 |
| そのほか | | データベースに接続できません (元のメッセージ)。 |

証明書の検証の失敗のときだけ、検証を省いて接続し直したい (呼ぶ側が決める)ときは、メッセージの文字ではなく、`SqlServerConnectionException.IsUntrustedCertificate` で見分ける (アプリが、エラー番号や文字を調べずに済み、失敗の知識がこの部品に集まる。ほかの原因で処理を分けるときも、同じ形でプロパティを足す)。
