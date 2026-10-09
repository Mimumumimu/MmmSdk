# 音声機器を眠らせない出力

無音を流し続けて、音声機器が休止するのを防ぐ部品 (`MmmSdk.WinUI.Components.Audio`)。光デジタル出力など、無音が続くと信号を止める機器で、読み上げや通知音の頭が切れるのを防ぐ。アプリ固有の設定・画面は持たない。

## 概要
- `IAudioKeepAlive.StartAsync()`: 無音の出力を始める。始められたら true。出力機器が無いときは false (例外にしない。機器が現れたら自動で始まる)。すでに流しているときは何もしない
- `IAudioKeepAlive.StopAsync()`: 止める
- 流している間は、既定の出力機器が変わったとき・グラフが続けられなくなったとき (機器の取り外しなど)に、流し直す
- DI は `AddMmmSdkWinUI` で Singleton。`IDisposable` なので、ホストの破棄でグラフも破棄される

## 仕組み (`AudioKeepAlive`)
- 出力ノード (`CreateDeviceOutputNodeAsync`)だけの `AudioGraph` を動かす。入力ノードを持たせないので、音声エンジンが無音を流し続け、アプリのコールバックは無い (CPU をほぼ使わない)
- 描画の種類は `Media`。1 区間の長さは、長めを希望する (`ClosestToDesired`・4800 サンプル。機器が許す範囲に丸められる)
- 開始・停止・作り直しは、`SemaphoreSlim` で 1 つずつ行う。機器の変更の通知は UI スレッドとは限らないが、排他の中で操作するのでどのスレッドでもよい

## 決定の理由

### `MediaPlayer` ではなく `AudioGraph` にする
- `MediaPlayer` はメディアを再生する部品で、無音の WAV をメモリ上で作ってループ再生する形になる (ヘッダーの組み立て・ループのつなぎ目が要る)。`AudioGraph` は、出力ノードだけで、エンジンが無音を流し続けるので、データを作らず、コールバックも無い。WASAPI の直接呼び出しは、`AudioGraph` がその上に作られた標準の部品なので、使わない
- `AudioGraph` は既定の出力機器の変更に追従しないので、`MediaDevice.DefaultAudioRenderDeviceChanged` と `UnrecoverableErrorOccurred` で作り直す

### 無音は、デジタルのゼロにする
- 入力ノードを持たせない形では、流れるのはゼロの音になる。機器によっては、ゼロが続くだけで止まる可能性がある。その場合は、`AudioFrameInputNode` で ±1 のサンプルを流す (区間ごとのコールバックが増える)。実機で効かなかったときに、そうする
