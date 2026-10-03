# コントロール・IME・添付の一時保存・添付の画像

アプリから移した汎用部品。アプリ固有の知識は持たない。

## TimeInputBox（`MmmSdk.WinUI.Controls`）
時刻（24 時間）の入力欄。1 つの枠に「時 : 分」の 2 区画を持ち、ボタンは置かない。`Hour`（0〜23）・`Minute`（0〜59）は依存関係プロパティで、`x:Bind` の TwoWay で結べる。

- 数字だけを受け付ける。時を 2 桁打つと分へ移る
- ←→ で区画を移動し、↑↓ / マウスホイールで ±1（端まで行ったら反対の端へ回る）
- フォーカスで全選択し、IME をオフにする（`ImeControl.TurnOff`）
- 区画を離れたときに確定する（空なら元の値、範囲外は最大値）。常に 2 桁表示
- 区画は、枠・消去ボタンを持たない最小テンプレートの `TextBox`。外側の枠が、標準の入力欄の見た目（ポインタ上・フォーカス中の背景と、アクセント色の下線）を受け持つ
- 決めた理由: `TimePicker` はドラムの「適用」と画面の「OK」が両方目立って押し間違えやすい。`NumberBox` を 2 つ並べると、▲▼ と ✕ でごちゃごちゃする

## LinkArea（`MmmSdk.WinUI.Controls`）
押すとリンクを開く領域（`Grid` 派生）。`IsLinkEnabled` のときだけ、マウスを乗せると手の形のカーソルと背景で押せることを示す。押したときの処理は `Tapped` で受ける側が行う。

- カーソルの形（`ProtectedCursor`）は派生クラスからしか変えられないため、`Grid` を派生させている
- 文字の無い所でもマウス・タップを受けるため、背景は透明で塗る

## ImeControl（`MmmSdk.WinUI.Input`）
フォーカスのあるウィンドウの IME（日本語入力）をオン・オフする。`TurnOn` は日本語を打つ欄（日本語入力・漢字変換の状態）、`TurnOff` は英数字を打つ欄（直接入力の状態）にフォーカスが来たときに呼ぶ。Win32 の `ImmSetOpenStatus` を使う。

## AttachmentStore（`MmmSdk.Core.Attachments`）
添付ファイルの一時保存先（`%TEMP%\<appName>\session_日時\`）の管理。アプリ全体で 1 つ。`appName` はアプリごとに別の名前にする。

- 最初の添付でセッションフォルダを作り、以降は連番（`001_名前`）を付けて保存する（同名でも衝突しない）
- 添付を全部取り除いたら、フォルダごと削除して初期化する（`Remove`）。`Remove` は今のセッションフォルダの中のファイルだけを消す（外のパスは `ArgumentException`。送信済みのファイルも消さない）
- 保存するファイル名は、フォルダの部分と使えない文字を取り除く（セッションフォルダの外に保存されないように）
- 送信済みのファイルは、CLI が後から読むので、アプリ終了まで残す（`CloseSession` で次の添付は新しいセッションへ）
- `Dispose` で、このインスタンスが作ったセッションフォルダを削除する
- 前回までの残り（異常終了など）は、初回の添付時に、1 日より古いものだけ消す（同時起動している別ビルドのフォルダを消さないため）
- 登録は、アプリ側で `TimeProvider` と一緒に行う（`AddMmmSdkCore` には含めない）

```csharp
services.AddSingleton(provider => new AttachmentStore("MyApp", provider.GetRequiredService<TimeProvider>()));
```

## 添付の画像（`MmmSdk.WinUI.Attachments`）
- `IImageConverter.ToJpegAsync(stream)`: 画像（PNG・BMP など）を JPEG に変換する（実装は `ImageConverter`。`AddMmmSdkWinUI` が Singleton で登録する）。JPEG は透過を持てないので、アルファは無視する。貼り付けたスクリーンショットなどを、添付のファイルにするときに使う
- `ThumbnailImage.FromFile(path)`: サムネイルを作る（XAML の `x:Bind` から関数として呼ぶ。パスが空なら null）。ファイルを開いたままにしない（削除できなくなるため）よう、中身をメモリに読み込んでから表示する。表示サイズに合わせて高さ 144 でデコードする。読み込みの失敗（`IOException`・`UnauthorizedAccessException`・`COMException`）は、空のまま表示する
