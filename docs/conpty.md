# ConPTY（PseudoConsole）

ターミナル画面の土台になる汎用部品（`MmmSdk.WinUI.ConPty`）。端末の描画・キー入力の解釈は持たない。呼び出し側は、出力（端末のエスケープシーケンスを含む UTF-8 のバイト列）を端末のエミュレーターに渡す。

## 置き場所
- 画面（UI）は持たないが、Windows の API を呼ぶので、Windows に依存しない `MmmSdk.Core` ではなく `MmmSdk.WinUI` に置く
- Win32 の宣言は CsWin32 が生成する（`NativeMethods.txt`）。宣言は internal で、公開するのは `PseudoConsole` だけ

## PseudoConsole
- `Start(commandLine, workingDirectory, columns, rows)`: 入出力のパイプ 2 本を作り、`CreatePseudoConsole` で擬似コンソールを作って、プロセスを `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE` でつないで起動する
  - 親（このアプリ）の標準ハンドルは子へ引き継がない（`STARTF_USESTDHANDLES`）
  - ConPTY 側の端（入力の読み取り側・出力の書き込み側）は、作成後に ConPTY が持つので、こちらでは閉じる
  - 失敗したときは、作った分（パイプ・擬似コンソール）をすべて解放してから、`Win32Exception`（パイプ・プロセス）または `COMException`（擬似コンソール）を投げる。呼び出し側は未起動のまま
- `Input`（書き込み）/ `Output`（読み取り）: UTF-8。出力は読み続けないと、擬似コンソールを閉じる処理が戻らないことがある
- `ExitHandle`: プロセス終了で通知される待機ハンドル。`ThreadPool.RegisterWaitForSingleObject` などで待つ。ハンドルの所有は `PseudoConsole`
- `Resize(columns, rows)`: 端末の大きさを変える（1 〜 32767 に収める。閉じたあとは何もしない）

## 後始末の順序
1. 呼び出し側は、出力を読み続けたまま `Close()` を呼ぶ。入力を閉じ、擬似コンソールを別スレッドで閉じて、最大 3 秒だけ待つ（プロセスがまだ動いていれば終了する）
2. 出力が末尾（読み取りが 0 バイト）になるのを待つ
3. `Dispose()` で、出力・プロセスのハンドルを解放する（`Close()` が済んでいなければ先に行う）

同期で待つのは意図的。`IAsyncDisposable` にすると、DI コンテナが `ConfigureAwait(false)` で待つため、後から破棄されるトレイアイコンなどの後始末が UI スレッドの外で動いてしまう。

## アプリでの使い方
`PseudoConsoleSession`（`ITerminalSession` の実装。[terminal.md](terminal.md)）が使う。出力の読み取り・デコード（UTF-8 の多バイト文字が読み取りの切れ目で分かれても復元する）・終了の通知・入力の確定はそちらが受け持つ。`commandLine` の実行ファイルのパスは、引用符で囲んで渡す（`ShellInfo.CommandLine` がそうしている）。
