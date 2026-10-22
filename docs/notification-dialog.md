# 通知ダイアログとウィンドウ位置の保存

`MmmSdk.WinUI.Components.Notifications`(`INotificationDialogService` / `NotificationWindow`)と `MmmSdk.Core.Components.WindowPositions`(`WindowPositionService`)の設計。

## 概要
- `INotificationDialogService.Show(NotificationSection)`。UI スレッドから呼ぶ。機能の通知は、この形で出す
  - `NotificationSection(SourceKey, Order, Title, Items, OnClicked?)` は、通知の送り元 (機能)ごとの節。見出しと項目の並びを持つ
  - 汎用の `Show(title, items, onClicked?)` と `Show(title, message, onClicked?)` は、送り元 `General` (並びの値 1000)の節として出す。エラーの知らせなど、機能の通知ではないものに使う
- ウィンドウはアプリ内で常に 1 枚。通知の管理はこのサービスが持ち、各機能は、自分の節を出すだけにする
  - 送り元が通知すると、その送り元の節だけが入れ替わる。ほかの送り元の節は残る (複数の機能が、同じ窓に通知しても、互いを消さないため)
  - 節は、並びの値の小さい順に上から並ぶ。同じ値は、送り元のキーの順。並びの値は、送り元が決める。ノートは 2、リマインダーは 4 で、サイドバーの並びと同じにして、ノートを上に固定する
  - 通知する送り元が無ければ、窓を出さない。どちらか 1 つだけなら、その節だけを出す
  - ユーザーが閉じたら、節も捨てて破棄し、次の通知で作り直す
- 項目は `NotificationItem(Text, LinkPath?, Detail?)`。`Text` は SemiBold。`Detail` があれば、`Text` の下に、字下げして左に細い線 (`ControlStrongStrokeColorDefaultBrush`)を付け、小さく薄い字 (13、`TextFillColorSecondaryBrush`)で、改行のまま出す。同じ左端に並べると、件名と補足の上下の関係が見えないため。リンクを開く処理は `IPathOpener`。開けなかったときは何も表示しない (通知ダイアログに失敗の表示は持たせない方針)

## 見た目
- 幅 400 固定。高さは本文に合わせて 160〜480 (超えたらスクロール)
- Mica。窓全体のタイトルは持たず、節ごとの見出し (`SubtitleTextBlockStyle`)を出す。上の 28 は、右上のウィンドウボタンと見出しが重ならないための空き。節の間には、区切り線を引く
- 節は、見出しと項目を縦に並べた枠で、押した位置の節を知るため、送り元のキーを `Tag` に持つ。項目ごとの `TextBlock` を縦に並べ、件名の `TextBlock.Inlines`(Run / Hyperlink)をコードで組み立てる。補足は別の `TextBlock`。リンクの直後に全角スペースの Run を置く (クリックの当たり判定のため)
- 表示されたとき、アンバーの全面オーバーレイの不透明度を 0 → 0.55 (0.09 秒)→ 0 (0.18 秒)で 3 回繰り返して知らせる。オーバーレイは操作を邪魔しないよう当たり判定を持たせない

## 挙動
- 常に最前面 (`SetWindowPos` の `HWND_TOPMOST` + `SWP_NOACTIVATE`)。フォーカスを奪わない (`WS_EX_NOACTIVATE`)。Alt+Tab に出さない。サイズ変更・最大化・最小化はできない
- 非アクティブで表示するとバインドが評価されないことがあるので、表示のたびに `Bindings.Update()` を呼ぶ
- ドラッグは `SetTitleBar` を使わず、ルートのポインタイベント (`handledEventsToo`)で自前実装する
  - カーソルの画面座標で移動量を測り、`SM_CXDRAG` / `SM_CYDRAG` 以上でドラッグ開始 (その時点でキャプチャ。リンクの押下を横取りしないため)
  - それ未満で離したらクリックとみなして閉じる
  - リンクのクリックは `Hyperlink.Click` でフラグを立て、閉じる判定は Low 優先度で 1 サイクル遅らせて確認する
  - キャプションボタンの領域 (`TitleBar.RightInset`)とスクロールバーでの押下は対象外
- 節をクリックしたら、その節だけを捨てて、その節の `OnClicked` を呼ぶ。ほかの節が残っていれば、明滅せずに、残りでウィンドウを作り直す。無ければウィンドウを閉じる
  - 節の外 (余白・区切り線)のクリックは、何もしない
  - ×・Alt+F4 は、全部の節を閉じる。このとき、どの節の `OnClicked` も呼ばない。同じ送り元の次の通知による入れ替えでも、呼ばない
  - `OnClicked` は、ウィンドウを閉じたあと、または作り直したあとに呼ぶ
- 節が入れ替わる・減るときは、高さの変化で画面に収まらなくなるとき以外は位置を動かさない

## 位置の保存と復元 (`IWindowPositionService` / `WindowPositionService`)
- 位置の保存に失敗 (`DataFileException`。ロック・権限・ディスク)しても、通知は使えるので、その失敗だけを受けて続ける (`async void` の例外は受け皿が無く、アプリごと落ちるため)。メインウィンドウ用の `WindowBoundsKeeper` も同じ。
- 位置は設定ストアに `WindowPosition.<キー>` として保存する。画面内にいるかの判定は `WindowPlacement.IsVisibleEnough`(メインウィンドウの `WindowBoundsKeeper` と共通)。キーは `Notification` の 1 つ (窓が 1 枚で、送り元ごとに位置を持たないため)
- 保存するタイミング
  - ドラッグの終了 (離した通知とキャプチャ喪失のどちらからでも 1 回)
  - クリックで閉じる直前 (`Window.Close()` では `AppWindow.Closing` が発火しないため、明示的に保存する)
  - × / Alt+F4 で閉じるとき (`Closing`)
- 復元は、表示するときにキーが変わっていた場合だけ行う
  - 保存位置のウィンドウが、全モニターの作業領域に対して面積の 50% 以上見えていれば、その位置 (判定は `ScreenGeometry.IsVisibleEnough`。状態を持たず、設定ストアにも依存しない計算)
  - 見えなければ、プライマリモニターの作業領域の右下 (余白 16px)
- そのうえで毎回の表示で、はみ出していれば最寄りのモニターの作業領域の内側へ寄せる (`KeepInWorkArea`)。寄せた位置は保存しない
- `DisplayArea.FindAll()` は `foreach` せず、Count とインデクサで回す

## パスを開く (`IPathOpener` / `PathOpener` / `PathTarget`)
- `OpenAsync` は環境変数を展開し、前後の空白・引用符を取り除いてから、シェル実行をバックグラウンドで行う。実行ファイルはそのファイルのフォルダーを作業フォルダーにして起動する
- `PathTarget.Classify` は URL / フォルダー / 実行ファイル / ファイル / 見つからない / 空 を判定する。ファイルの有無を調べるので、ネットワーク上のパスでは時間がかかる (UI スレッドから呼ばない)

## 通知ウィンドウの作り方
`NotificationDialogService` は、通知ウィンドウを作る処理 (`Func<NotificationWindow>`)を DI から受け取る (`IServiceProvider` を持たない)。ウィンドウはユーザーが閉じたら破棄し、次の通知で作り直す。`AddMmmSdkWinUI` が、`NotificationWindow` を Transient で登録し、作る処理を渡す。

## リンクのクリック判定 (意図した仕様)
通知ウィンドウはクリックで閉じるが、本文のリンク (`Hyperlink`)をクリックしたときは閉じない。`Hyperlink.Click` でフラグを立て、閉じる判定を Low 優先度で 1 サイクル遅らせて、リンクのクリックかどうかを決めている (上の「挙動」)。この方式は変えない (ユーザーの決定)。レビューで「発火順に依存していて将来壊れうる」と指摘・提案しない。
- 実機で問題なく動いている
- 押下位置から判定する方式は、WinUI 3 の `TextBlock` に使える API が無い
- `RichTextBlock` へ替えると、見た目・操作 (テキスト選択・カーソル形状・クリックで閉じる)が壊れる
