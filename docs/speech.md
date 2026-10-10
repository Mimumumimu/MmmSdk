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
- 音量: `ISpeechService.VolumePercent`(0〜100 の %。既定 50)を、設定ストアの `Speech.VolumePercent` に保存する (`SetVolumePercentAsync`。設定ファイルを読めなかったときは保存せず false を返す。`IsVolumeReadOnly`)。読み上げのたびに保存値を読んで、再生器の `MediaPlayer.Volume` に入れる。保存すると、再生中の音量もすぐ変わる。アプリの再生音量で、Windows の音量 (ミキサー・全体)とは別。実際の音量は、この値と Windows の音量を掛けたもの。声と速さは、これまでどおり Windows の音声の設定に従う
- `SpeechService` は `ISettingsStore` を受け取る (`AddMmmSdkCore` が登録済み)
- DI は `AddMmmSdkWinUI` で Singleton。`IDisposable` なので、ホストの破棄で再生器と合成器も破棄される

## 決定の理由

### 通知ダイアログとは別の部品にする
- 通知の表示と声は別の関心で、声は通知以外の機能からも使える。`INotificationDialogService.Show` の形は変えず、読み上げたいアプリが、通知を出した隣で `ISpeechService` を呼ぶ

### 音量は、再生器の音量で持ち、保存も SDK の部品が持つ
- 合成側の音量 (`SpeechSynthesizerOptions.AudioVolume`)は、作った音声に焼き込まれるので、再生中は変えられない。再生器の音量 (`MediaPlayer.Volume`)なら、変えたときすぐ効く
- 保存まで `SpeechService` に持たせる。読み上げを使うアプリや機能が増えても、音量を入れ直す呼び出しを、それぞれに書かなくて済み、どこから読んでも同じ音量になる (最終形: 読み上げの部品が、声以外の設定も一式持つ。機能のオン・オフと並び順を `FeatureService` が設定ストアに持つのと同じ形)。設定の画面は、アプリが作って `SetVolumePercentAsync` を呼ぶ
- 既定を 50% にしたのは、100% では Windows の音量を上げた PC でうるさかったため

### AquesTalk ではなく、Windows 標準の音声合成にする
- AquesTalk は、個人利用の無償配布でも開発ライセンスの取得・配布許諾・クレジット表記が要り、ライブラリやプラグインとしての配布 (SDK に入れて複数のアプリで共有する形)と、商用・法人での利用は別の契約が要る。SDK を共有・公開する最終形に合わない
- Windows 標準 (`Windows.Media.SpeechSynthesis`)は、ライセンスの制約がなく、WinUI 3 から追加のパッケージなしで呼べる
