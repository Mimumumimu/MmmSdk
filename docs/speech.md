# 読み上げ

テキストを日本語の音声で読み上げる部品 (`MmmSdk.WinUI.Components.Speech`)。アプリ固有の文言・読む内容は持たない。

## 概要
- `ISpeechService.SpeakAsync(text)`: テキストを読み上げる。音声の合成と再生の開始までを待ち、読み上げの終わりは待たない。UI スレッドから呼ぶ
- 空白だけのテキストは何もしない
- 読み上げ中に呼ぶと、前の読み上げを止めて新しいほうを読む (重ならない)。合成の途中で次の依頼が来たときは、前の合成結果を捨てる (通し番号で見分ける)
- 日本語の音声が入っていない PC では、何も読まずに戻る (予測できる失敗なので、先に音声を探して確かめる。例外にしない)
- 合成・再生の失敗 (予想外の失敗)は例外のまま呼び出し元へ返す。待たずに走らせるときは `Forget()` を付けて、アプリの受け皿 (`FatalErrorHandler`)に渡す

## 仕組み (`SpeechService`)
- Windows 標準の音声合成 `Windows.Media.SpeechSynthesis.SpeechSynthesizer` で音声のストリームを作り、`Windows.Media.Playback.MediaPlayer` で再生する。追加のパッケージ・ライセンスは要らない
- 声は、OS の既定の音声が日本語ならそれ、そうでなければ入っている日本語の音声 (言語タグが `ja` で始まるもの)の先頭
- 合成器と再生器は 1 つを使い回す。前の音声の `MediaSource` とストリームは、新しい音声に差し替えたあと・破棄のときに解放する
- DI は `AddMmmSdkWinUI` で Singleton。`IDisposable` なので、ホストの破棄で再生器と合成器も破棄される

## 決定の理由

### 通知ダイアログとは別の部品にする
- 通知の表示と声は別の関心で、声は通知以外の機能からも使える。`INotificationDialogService.Show` の形は変えず、読み上げたいアプリが、通知を出した隣で `ISpeechService` を呼ぶ

### AquesTalk ではなく、Windows 標準の音声合成にする
- AquesTalk は、個人利用の無償配布でも開発ライセンスの取得・配布許諾・クレジット表記が要り、ライブラリやプラグインとしての配布 (SDK に入れて複数のアプリで共有する形)と、商用・法人での利用は別の契約が要る。SDK を共有・公開する最終形に合わない
- Windows 標準 (`Windows.Media.SpeechSynthesis`)は、ライセンスの制約がなく、WinUI 3 から追加のパッケージなしで呼べる
