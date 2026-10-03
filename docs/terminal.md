# ターミナル（`MmmSdk.WinUI.Terminal`）・シェル（`MmmSdk.Core.Shells`）

シェルを動かして画面に出す部品。ConPTY（[conpty.md](conpty.md)）の上に、セッションと、xterm.js で描く画面を載せる。

## シェルの決定（`MmmSdk.Core.Shells`）
- `ShellInfo(Path, Kind)`: 起動するシェル 1 つ分。種類（`ShellKind` = PowerShell / Cmd）は、起動コマンドの文字列から推測せず、決める側が指定する。`CommandLine` は実行ファイルのフルパスを引用符で囲んだもの、`FileName` はフォルダーを除いた名前（シェルの中で、そのシェル自身を呼ぶときに使う）
- `ShellLocator.Default`: 既定のシェル。PATH 上の `pwsh.exe` があればそれ、無ければ Windows PowerShell（システムフォルダー内）
  - PATH の項目のうち、相対パス（`.` など）は飛ばす（カレントフォルダーの同名のファイルが選ばれるのを防ぐ）。前後の引用符は外す
  - 最初に読んだときに PATH を探し（ディスクへ触れる）、結果を保持する。UI スレッドで初めて読まないよう、アプリは起動時の準備でバックグラウンドから読んでおく
  - BCL だけで書けるので Core に置く（Windows 専用なので `[SupportedOSPlatform("windows")]`）
- `ShellCommands.TryChangeDirectory(shell, directory, out command)`: 作業ディレクトリを移すコマンドを作る
  - PowerShell は `Set-Location -LiteralPath '…'`（単一引用符なら `$` や `` ` `` が展開されない）。パス中の単一引用符は 2 つ重ねる。PowerShell は ASCII の `'` のほかに U+2018 / U+2019 / U+201A / U+201B も単一引用符として扱うので、この 4 文字も重ねる（フォルダー名に使える文字。重ねないと、文字列が途中で閉じて、残りがコマンドとして実行される）
  - cmd は `cd /d "…"`。cmd は対話入力で引用符の中でも `%名前%` を展開し、打ち消す方法もないので、`%` を含むパスは作らない（`false` を返す）

## セッション（`ITerminalSession` / `PseudoConsoleSession`）
- シェル 1 つ分。`Shell`（既定は `ShellLocator.Default`）と `WorkingDirectory` を、`Start` の前に設定する。`PseudoConsole` でシェルを起動し、出力の読み取り（`OutputReceived`。バックグラウンドスレッドから通知）・終了の通知（`Exited`）・入力（`Write`）・サイズ変更（`Resize`）を受け持つ
- `Submit(text)`: テキストを貼り付けとして入力し、続けて Enter で確定する。画面側（`TerminalControl`）を通して xterm.js の `term.paste`（ブラケットペースト）で貼り付けるので、複数行でも CLI が 1 行ずつ実行せず、ひとまとまりで受け取る。画面が無いとき（未接続）は、そのまま書き込んで確定する
- `RestartAsync`: 古いシェルの終了待ち（最大で数秒）を UI スレッドの外で行ってから、同じ設定で起動し直す
- `Dispose` は同期のまま（アプリの終了時に呼ばれ、シェルを確実に終わらせる。`IAsyncDisposable` にしない理由は [conpty.md](conpty.md)）
- 出力の UTF-8 は、状態を持つデコーダーで復元する（読み取りの切れ目で多バイト文字が分かれても壊れない）

## 画面（`TerminalControl`）
- WebView2 上の xterm.js で描く `UserControl`。`Session` に `ITerminalSession` を渡すと、入出力をつなぐ。`FocusTerminal()` でフォーカスを移す
- xterm.js・addon-fit（MIT。ライセンスファイルも同梱）は `MmmSdk.WinUI/Terminal/Assets/`。SDK の csproj が、参照するアプリの出力フォルダー（`Assets/Terminal/`）へコピーする。**アプリは何も書かなくてよい**（アセットはフォルダー内に平らに置く。サブフォルダーを足すときは csproj のリンクの書き方を直す）
- 仮想ホスト `terminal.mmmsdk.invalid` で `Assets/Terminal/` を読み込む（`DenyCors`）。守りは多重：ページの CSP（外部への通信・フレーム・フォームを禁止）・`AreHostObjectsAllowed = false`・ブラウザーの機能（再読み込み・検索・印刷・ズーム・右クリックメニュー）を止める・メッセージの送信元の検査・Release で DevTools を無効化
- Ctrl+C のコピーは、クリップボードへ書き込めたときだけ選択を解除する。拒否されたときは選択を残し、`console.error` に出す（画面には出さない）。サイズ変更の通知は 1 フレームに 1 回にまとめる
- ページの CSP は、Chromium で、違反が出ないことと、描画・サイズ変更の通知が動くことを確認済み（xterm.js が `<style>` を足すため、style だけ inline を許可）
- C# ↔ JS は JSON メッセージ（C# → JS: `output` / `submit` / `focus`。JS → C#: `ready` / `input` / `resize` / `written`）。ページは `terminal.js`
- 出力は細切れに届くので、UI スレッドへ渡す前にまとめる。xterm.js の書き込み待ちがあふれて出力が捨てられないよう、描画の受け取り（`written`）が返っていない文字数が 1M 文字を超えたら、受け取りが返るまで送らない
- シェルが終了したあとは、何かキーを押すと再起動する（押されたキーは捨てる。終了待ちの間のキーも捨てて、二重に起動し直さない）
- WebView2 を初期化できなかったとき（ランタイムが無い・起動できない。`COMException`）は、画面の場所に理由を文字で出す（ほかの機能は使い続けられる）。制約: WebView2 ランタイムが無い PC で、どの例外が出るかは、この環境では確かめられていない（`COMException` 以外は、未処理例外の受け皿が受ける）。実機で確かめて、受ける例外を直す
- シェルの起動に失敗したとき（`Win32Exception`・`COMException`・`InvalidOperationException`）は、端末に赤字で理由を出す

## アプリでの使い方
```csharp
services.AddTransient<ITerminalSession, PseudoConsoleSession>(); // 利用側ごとに 1 つ。Host の破棄時に Dispose され、シェルも終了する
```
```xml
<!-- xmlns:terminal="using:MmmSdk.WinUI.Terminal" -->
<terminal:TerminalControl Session="{x:Bind ViewModel.Terminal}" />
```
- 起動するシェルを替えるときは、`Start` の前に `session.Shell = new ShellInfo(path, kind)` を設定する
