# コントロール・IME・添付の一時保存・添付の画像

アプリから移した汎用部品。アプリ固有の知識は持たない。

## TimeInputBox（`MmmSdk.WinUI.Controls`）
時刻（24 時間）の入力欄。1 つの枠に「時 : 分」の 2 区画を持ち、ボタンは置かない。`Hour`（0〜23）・`Minute`（0〜59）は依存関係プロパティで、`x:Bind` の TwoWay で結べる。

- 数字だけを受け付ける。時を 2 桁打つと分へ移る
- ←→ で区画を移動し、↑↓ / マウスホイールで ±1（端まで行ったら反対の端へ回る）
- フォーカスで全選択し、IME をオフにする（`ImeControl.TurnOff`）
- 区画を離れたときに確定する（空なら元の値、範囲外は最大値）。常に 2 桁表示
- 区画は、枠・消去ボタンを持たない最小テンプレートの `TextBox`。外側の枠が、標準の入力欄の見た目（ポインタ上・フォーカス中の背景と、アクセント色の下線）を受け持つ。枠の見た目は XAML のビジュアルステート（Normal / PointerOver / Focused。色は `ThemeResource`）なので、テーマの切り替えに追従する
- `Hour` / `Minute` に外から範囲外の値（負の値・24 時など）が来たら、範囲内に収め直す（表示と値がずれないため）
- 決めた理由: `TimePicker` はドラムの「適用」と画面の「OK」が両方目立って押し間違えやすい。`NumberBox` を 2 つ並べると、▲▼ と ✕ でごちゃごちゃする

## LinkArea（`MmmSdk.WinUI.Controls`）
押すとリンクを開く領域（`Grid` 派生）。`IsLinkEnabled` のときだけ、マウスを乗せると手の形のカーソルと背景で押せることを示す。押したときの処理は `Tapped` で受ける側が行う。

- カーソルの形（`ProtectedCursor`）は派生クラスからしか変えられないため、`Grid` を派生させている
- 文字の無い所でもマウス・タップを受けるため、背景は透明で塗る
- マウスを乗せたときの背景は、`SubtleFillColorSecondaryBrush` をコードで引く（`Grid` 派生でコードだけの部品のため、ビジュアルステートを持てない）。テーマが切り替わったら（`ActualThemeChanged`）引き直す。制約: アプリの `Application.RequestedTheme` が起動時のままだと、引き直しても前のテーマの色になる可能性があり、実機（Windows のテーマを実行中に切り替える）で確かめていない。確かめて、ずれるなら、`UserControl`（XAML）にして `ThemeResource` を使う形に直す

## ImeControl（`MmmSdk.WinUI.Utilities`）
フォーカスのあるウィンドウの IME（日本語入力）をオン・オフする。`TurnOn` は日本語を打つ欄（日本語入力・漢字変換の状態）、`TurnOff` は英数字を打つ欄（直接入力の状態）にフォーカスが来たときに呼ぶ。Win32 の `ImmSetOpenStatus` を使う。

## AttachmentStore（`MmmSdk.Core.Components.Attachments`）
添付ファイルの一時保存先（`%TEMP%\<appName>\session_日時\`）の管理。保存先ごとに 1 つ。`appName` はアプリごとに別の名前にする。

- 保存先は 2 つから選ぶ: Windows の一時フォルダ（コンストラクター）と、WSL の既定のディストリビューションの /tmp（`AttachmentStore.ForWsl(appName, timeProvider)`。`\\wsl.localhost\<名前>\tmp\<appName>\session_日時\`）
  - WSL で動く CLI に渡すときは、保存先のパスを `ShellCommands.TryConvertPath`（`WslPath`）で `/tmp/<appName>/...` にして渡す
  - WSL の /tmp では、終了時の削除と、古い一時フォルダの掃除をしない（ユーザーの決定）。WSL の /tmp は、systemd が有効なら WSL の起動のたびに空になるため。送信前に取り除いた添付（`Remove`）は、Windows と同じくすぐ消す
  - ディストリビューション名は最初の添付のときに調べる（`WslDistribution`）。見つからない（WSL が入っていない）ときは、その添付が `IOException` で失敗する

- 保存するのは、元がファイルではないもの（貼り付けた画像など）だけ（`AddAsync`）。ディスク上にあるファイルは、コピーせずに元のパスを使う想定なので、ファイルをコピーする口は持たない（ローカルで動く CLI は元の場所のファイルを直接読めるうえ、置き場所も手がかりになるため。コピーを持つと、取り除くときに元のファイルと取り違えて消す心配も出る）
- 最初の添付でセッションフォルダを作り、以降は連番（`001_名前`）を付けて保存する（同名でも衝突しない）
- 添付を全部取り除いたら、フォルダごと削除して初期化する（`Remove`）。`Remove` は今のセッションフォルダの中のファイルだけを消す（外のパスは `ArgumentException`。送信済みのファイルも消さない）
- 保存するファイル名は、フォルダの部分と使えない文字を取り除く（セッションフォルダの外に保存されないように）
- 送信済みのファイルは、CLI が後から読むので、アプリ終了まで残す（`CloseSession` で次の添付は新しいセッションへ）
- `Dispose` で、このインスタンスが作ったセッションフォルダを削除する（WSL の /tmp では削除しない）
- フォルダは、同じアプリの別ビルド（Debug / Release など、別の場所の EXE）と共有する。EXE ごとには分けない（パスを短く保つため）
- 前回までの残り（異常終了など）は、初回の添付時に、1 日より古い `session_*` だけ消す（作ったばかりのものは消さない。WSL の /tmp では消さない）
  - フォルダを共有するので、同時に動いている別ビルドの、1 日より古いセッションも消える。これは想定内（ユーザーの決定）。添付は CLI が読み込めば用済みの一時ファイルで、元は読み込んだらすぐ消す仕様だったものを緩めて残しているだけのため。レビューで「別ビルドのフォルダを消しうる」と指摘しない
- セッションのフォルダ名が重なったら（同じミリ秒に作り直したとき・別ビルドと同じミリ秒に作ったとき）、接尾辞（`_2`）を付けて、前のファイルを上書きしない
- 登録は、アプリ側で `TimeProvider` と一緒に行う（`AddMmmSdkCore` には含めない）

```csharp
services.AddSingleton(provider => new AttachmentStore("MyApp", provider.GetRequiredService<TimeProvider>()));
```

## 添付の画像（`MmmSdk.WinUI.Components.Attachments`）
- `IImageConverter.ToJpegAsync(stream)`: 画像（PNG・BMP など）を JPEG に変換する（実装は `ImageConverter`。`AddMmmSdkWinUI` が Singleton で登録する）。JPEG は透過を持てないので、アルファは無視する。画像の大きさ（解像度・容量）に上限は設けない（意図した仕様。大きな画像も元の解像度のまま変換する。添付ファイル（`AttachmentStore`）の大きさにも上限は無い）。貼り付けたスクリーンショットなどを、添付のファイルにするときに使う
- `ThumbnailImage.FromFile(path)`: サムネイルを作る（XAML の `x:Bind` から関数として呼ぶ。パスが空なら null）。ファイルを開いたままにしない（削除できなくなるため）よう、中身をメモリに読み込んでから表示する。表示サイズに合わせて高さ 144 でデコードする。読み込みの失敗（`IOException`・`UnauthorizedAccessException`・`COMException`）は、空のまま表示する
