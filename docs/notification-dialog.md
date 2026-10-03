# 通知ダイアログとウィンドウ位置の保存

`MmmSdk.WinUI.Components.Notifications`（`INotificationDialogService` / `NotificationWindow`）と `MmmSdk.Core.Components.WindowPositions`（`WindowPositionService`）の設計。

## 概要
- `INotificationDialogService.Show(title, items, onClicked?, positionKey)` / `Show(title, message)`。UI スレッドから呼ぶ
- ウィンドウはアプリ内で常に 1 枚。新しい通知は内容を差し替えて再表示する。ユーザーが閉じたら破棄し、次の通知で作り直す
- 項目は `NotificationItem(Text, LinkPath?)`。リンクを開く処理は `IPathOpener`。開けなかったときは何も表示しない（通知ダイアログに失敗の表示は持たせない方針）

## 見た目
- 幅 400 固定。高さは本文に合わせて 160〜480（超えたらスクロール）
- Mica。タイトルは `SubtitleTextBlockStyle`
- 本文は `TextBlock.Inlines`（Run / Hyperlink / LineBreak）をコードで組み立てる。リンクの直後に全角スペースの Run を置く（クリックの当たり判定のため）
- 表示されたとき、アンバー（`#FFB300`）の全面オーバーレイの不透明度を 0 → 0.55（0.09 秒）→ 0（0.18 秒）で 3 回繰り返して知らせる。オーバーレイは操作を邪魔しないよう当たり判定を持たせない

## 挙動
- 常に最前面（`SetWindowPos` の `HWND_TOPMOST` + `SWP_NOACTIVATE`）。フォーカスを奪わない（`WS_EX_NOACTIVATE`）。Alt+Tab に出さない。サイズ変更・最大化・最小化はできない
- 非アクティブで表示するとバインドが評価されないことがあるので、表示のたびに `Bindings.Update()` を呼ぶ
- ドラッグは `SetTitleBar` を使わず、ルートのポインタイベント（`handledEventsToo`）で自前実装する
  - カーソルの画面座標で移動量を測り、`SM_CXDRAG` / `SM_CYDRAG` 以上でドラッグ開始（その時点でキャプチャ。リンクの押下を横取りしないため）
  - それ未満で離したらクリックとみなして閉じる
  - リンクのクリックは `Hyperlink.Click` でフラグを立て、閉じる判定は Low 優先度で 1 サイクル遅らせて確認する
  - キャプションボタンの領域（`TitleBar.RightInset`）とスクロールバーでの押下は対象外
- `onClicked` は本文クリックで閉じたときだけ呼ぶ（×・Alt+F4・差し替えでは呼ばない）。通知ごとに設定し直し、渡さなければクリアする
- 差し替え時は、高さの変化で画面に収まらなくなるとき以外は位置を動かさない

## 位置の保存と復元（`IWindowPositionService` / `WindowPositionService`）
- 位置の保存に失敗（`DataFileException`。ロック・権限・ディスク）しても、通知は使えるので、その失敗だけを受けて続ける（`async void` の例外は受け皿が無く、アプリごと落ちるため）。メインウィンドウ用の `WindowBoundsKeeper` も同じ。
- 位置は設定ストアに `WindowPosition.<キー>` として保存する。画面内にいるかの判定は `WindowPlacement.IsVisibleEnough`（メインウィンドウの `WindowBoundsKeeper` と共通）。キー（`positionKey`）は通知の種類ごとに指定でき、既定は `Notification`
- 保存するタイミング
  - ドラッグの終了（離した通知とキャプチャ喪失のどちらからでも 1 回）
  - クリックで閉じる直前（`Window.Close()` では `AppWindow.Closing` が発火しないため、明示的に保存する）
  - × / Alt+F4 で閉じるとき（`Closing`）
- 復元は、表示するときにキーが変わっていた場合だけ行う
  - 保存位置のウィンドウが、全モニターの作業領域に対して面積の 50% 以上見えていれば、その位置（判定は `ScreenGeometry.IsVisibleEnough`。状態を持たず、設定ストアにも依存しない計算）
  - 見えなければ、プライマリモニターの作業領域の右下（余白 16px）
- そのうえで毎回の表示で、はみ出していれば最寄りのモニターの作業領域の内側へ寄せる（`KeepInWorkArea`）。寄せた位置は保存しない
- `DisplayArea.FindAll()` は `foreach` せず、Count とインデクサで回す

## パスを開く（`IPathOpener` / `PathOpener` / `PathTarget`）
- `OpenAsync` は環境変数を展開し、前後の空白・引用符を取り除いてから、シェル実行をバックグラウンドで行う。実行ファイルはそのファイルのフォルダーを作業フォルダーにして起動する
- `PathTarget.Classify` は URL / フォルダー / 実行ファイル / ファイル / 見つからない / 空 を判定する。ファイルの有無を調べるので、ネットワーク上のパスでは時間がかかる（UI スレッドから呼ばない）

## 通知ウィンドウの作り方
`NotificationDialogService` は、通知ウィンドウを作る処理（`Func<NotificationWindow>`）を DI から受け取る（`IServiceProvider` を持たない）。ウィンドウはユーザーが閉じたら破棄し、次の通知で作り直す。`AddMmmSdkWinUI` が、`NotificationWindow` を Transient で登録し、作る処理を渡す。
