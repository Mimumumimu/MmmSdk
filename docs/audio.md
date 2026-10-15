# 音声機器を眠らせない出力

無音を流し続けて、音声機器が休止するのを防ぐ部品 (`MmmSdk.WinUI.Components.Audio`)。光デジタル出力など、無音が続くと信号を止める機器で、読み上げや通知音の頭が切れるのを防ぐ。アプリ固有の設定・画面は持たない。

## 概要
- `IAudioKeepAlive.StartAsync()`: 無音の出力を始める。始められたら true。出力機器が無いときは false (例外にしない。機器が現れたら自動で始まる)。すでに流しているときは何もしない
- `IAudioKeepAlive.StopAsync()`: 止める
- 流している間は、既定の出力機器が変わったとき・グラフが続けられなくなったとき (機器の取り外しなど)に、流し直す (`AudioGraph` は既定の出力機器の変更に追従しないので、`MediaDevice.DefaultAudioRenderDeviceChanged` と `UnrecoverableErrorOccurred` で作り直す)
- DI は `AddMmmSdkWinUI` で Singleton。`IDisposable` なので、ホストの破棄でグラフも破棄される

## 仕組み (`AudioKeepAlive`)
- 出力ノード (`CreateDeviceOutputNodeAsync`)だけの `AudioGraph` を動かす。入力ノードを持たせないので、音声エンジンが無音を流し続け、アプリのコールバックは無い (CPU をほぼ使わない)
  - `MediaPlayer` で無音の WAV をループ再生する形にしない (メモリ上でヘッダーを組み立て、ループのつなぎ目を処理する必要があるため)。WASAPI を直接呼ぶ形にもしない (`AudioGraph` は、その上に作られた標準の部品)
  - 入力ノードを持たせない形で流れるのは、デジタルのゼロ。ゼロが続くだけで止まる機器があるときは、`AudioFrameInputNode` で ±1 のサンプルを流す形に替える (区間ごとのコールバックが増える)
- 描画の種類は `Media`。1 区間の長さは、長めを希望する (`ClosestToDesired`・4800 サンプル。機器が許す範囲に丸められる)
- 開始・停止・作り直しは、`SemaphoreSlim` で 1 つずつ行う。機器の変更の通知は UI スレッドとは限らないが、排他の中で操作するのでどのスレッドでもよい
