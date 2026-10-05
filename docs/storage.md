# 保存 (JSON・設定ストア・壊れたファイルの扱い)

`MmmSdk.Core.Components.Storage` / `MmmSdk.Core.Components.Settings` の設計。

## JSON ファイルの読み書き (`IJsonFileStore` / `JsonFileStore`)
- 保存先のフォルダは `AddMmmSdkCore(dataDirectory)` で渡す。アプリは `AppContext.BaseDirectory/Data` を渡している
- 型の情報は呼ぶ側が `JsonTypeInfo<T>`(ソース生成)で渡す。トリミングしても動く形を保つため、リフレクションによるシリアライズは使わない
- 書き込みは一時ファイルに書いてから置き換える (書き込み途中で失敗しても元のファイルが壊れない)
- 読み込みも書き込みと同じロックを取る (読み込みと退避の間に書き込みが割り込まないように)。ロックはファイル名ごと (別のファイルは待たせない。大文字小文字は区別しない)。別スレッドから同時に呼べる

## シリアライザの設定 (`ReadableJsonOptions`)
- インデントあり・日本語や記号を非エスケープ・コメントと末尾のカンマを許す・プロパティ名は camelCase で書き、読み込みでは大文字小文字を区別しない・null は書かない
- 設定は 1 か所 (`ReadableJsonOptions.Create()`)にだけ書き、SDK とアプリのソース生成の Context がそれを使う
- 設定は Context に渡すと読み取り専用になり、その Context に結び付く。共有の static インスタンスを使い回さず、Context ごとに `Create()` で新しく作る

## 壊れたファイルの扱い
`JsonFileStore.Read` / `ReadAsync` は `DataLoadResult<T?>`(`Value` と `RecoveryMessage`)を返す。

| ファイルの状態 | 結果 |
| --- | --- |
| 無い | `Value` = null |
| 0 バイト・空白だけ・`null` | 退避せず `Value` = null |
| JSON として読めない | 同じフォルダに `名前.broken-yyyyMMdd-HHmmss.json`(同じ秒なら `-2` 以降)へ名前を変えて退避し、`Value` = null と `RecoveryMessage`(そのまま画面に出せる文) |
| ロック・権限などで読めない | 退避できないので `DataFileException`(メッセージはそのまま画面に出せる) |

- 退避したファイルは自動では消さない
- 呼ぶ側は「ファイルが無いとき」と同じに作り直す。`RecoveryMessage` は画面の InfoBar などで知らせる
- 書き込みは、一時ファイルに書いて、ディスクまで書き出し (`Flush(true)`)てから置き換える (電源断で、空や途中までのファイルに置き換わらないように)
- 書き込みが失敗 (取り消しを含む)したら、一時ファイル (`.tmp`)を削除してから例外を投げる。削除できなくても、元の例外を優先する
- ファイル名には、フォルダを含まない名前だけを受け付ける (区切り・ドライブ・`..`・`:`(代替データストリーム)を含むと `ArgumentException`。データフォルダの外を読み書きしないため。呼ぶ側のバグなので、握りつぶさず落とす)
- 読めなかった (`DataFileException`)ときは、元のデータを空で上書きして消さないよう、保存を止める運用にする (アプリ側のサービスが担当)

## 汎用設定ストア (`ISettingsStore` / `JsonSettingsStore`)
- `Data/AppSettings.json` の 1 ファイル (キー → JSON 要素の辞書)。ローカル専用。`AddMmmSdkCore` が Singleton で登録する
- `Get(key, 既定値)` は同期。最初のアクセスで 1 度だけ読み込み、以後はメモリから返す。起動時の準備で `EnsureLoadedAsync` を呼んでおくと、最初の読み込みを非同期で済ませられる (UI スレッドを止めない。読めなかったときも例外にせず `LoadError` に残す)
- `SetAsync` / `RemoveAsync` は保存完了まで待つ。保存は専用のロックで順序を守る (古い内容が後から書かれないように)
- インターフェース (`ISettingsStore`)は、型情報つき (`JsonTypeInfo<T>`)の `Get` / `TryGet` / `SetAsync` と、`Contains` / `RemoveAsync` と、状態 (`LoadError` / `IsReadOnly` / `RecoveryMessage`)だけ。保存先を替えるときは、これだけを実装する
- `string` / `bool` / `int` / `long` / `double` は、型ごとの専用のメソッド (`SettingsStoreExtensions` の拡張メソッド。型情報つきのメソッドを呼ぶだけ)で、型情報なしで使える。それ以外の型を渡すと、実行時ではなく、ビルドで誤りになる。それ以外は呼ぶ側が `JsonSerializable` 登録した `JsonTypeInfo<T>` を渡す (ソース生成を維持するため)
- 型が合わない・無いキーは既定値を返す。例外を出さずに確かめたいときは `TryGet(key, out value)`(`bool` を返す)
- 読めなかった (`LoadError`)ときは `IsReadOnly` が true。`SetAsync` / `RemoveAsync` は保存せず `false` を返す (保存できたときだけ `true`)。元のデータを空で上書きして消さないため
  - 読み込みの結果は `LoadStatus` で持つ。最初の読み込みが一時的なロックなどで失敗しても、プロセスが終わるまで読み取り専用のままにならないよう、保存のたびに 1 度だけ読み直す。読めれば、読み取り専用を解いて、そのまま保存する (読めなければ、保存せずに `false`)。壊れたファイルを退避したときのメッセージは、読み直しても残す
- 保存は、コピーした辞書を変更して書き出し、成功してからメモリの辞書を入れ替える (書き込みに失敗しても、メモリの内容がファイルとずれない)
- `JsonFileStore` はロックを持ったまま await するため、SDK.Core の await には `ConfigureAwait(false)` を付ける (UI スレッドから呼ばれても、戻りを待つ UI スレッドとのデッドロックが起きない)
- ファイルが無い・空・壊れているときは空として扱う (壊れていたときは上の手順で退避してから。`RecoveryMessage`)。ロック・権限で読めなかったときは `LoadError` に残し、空として扱って保存は試みる
- 機能ごとの設定はキーの接頭辞 (`Reminder.` / `WindowPosition.` 等)で衝突を避ける

## 読み込み結果の記録 (`LoadStatus`)
保存ファイルを読むサービスが 1 つ持つ。`LoadError`(読めなかった)と `RecoveryMessage`(壊れたファイルを退避した)を覚え、画面に出す。

- 読み込みに失敗した (ロック・権限などで読めなかった)ときは、元のデータを上書きで消さないよう、サービスが `HasFailed` を見て保存を止める。止め方は呼び出しの性質で変える。ユーザーの操作による保存は `ThrowIfSaveBlocked` で例外にして画面で知らせ、補助的な設定の保存は黙って行わない
- 壊れていた (JSON として読めなかった)ファイルは退避済みなので、失敗ではなく `RecoveryMessage` で知らせるだけで、保存は止めない
- 読み直せるサービスは、読み直しに成功したら失敗を消す (`Succeeded` / `ClearFailure`)。最初の読み込みで起きた退避は、読み直しても知らせ続けたいときは `keepPreviousRecoveryMessage`
- 複数のファイルをまとめて読むサービスは、結果をまとめて記録する (`Record`。複数のメッセージは改行でつなぐ)
