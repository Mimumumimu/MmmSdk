# 確認ダイアログ・ファイル/フォルダー選択・擬似モーダル・多重起動の防止

アプリから移した汎用部品。アプリ固有の画面・文言は持たない（`MmmSdk.WinUI.Dialogs` / `MmmSdk.Core.SingleInstance`）。

## DialogService（ダイアログの親の決定）

- ダイアログ・モーダルウィンドウの親を決める。優先順位は、いちばん手前のモーダルウィンドウ → 最後に操作した普通のウィンドウ → 最初に登録したウィンドウ。どれも無ければ `InvalidOperationException`
- 普通のウィンドウは `TrackWindow` で登録する。アクティブになるたびに「最後に操作したウィンドウ」を更新し、閉じたら候補から外す
- モーダルウィンドウは `ShowModalAsync` で開く間だけ覚える（その上に開くダイアログの親にするため。一覧の上に入力画面・確認を重ねられる）
- `ConfirmAsync`: 確認ダイアログ。取り消しにくい操作の確認用に、既定のボタンはキャンセル（Enter で誤って実行しない）。コードで作る `ContentDialog` には既定のスタイルが当たらないので、`DefaultContentDialogStyle` を明示している
- 口は 2 つ。確認ダイアログは `IDialogService`、親の決定（`Owner` / `TrackWindow` / `ShowModalAsync`）は `IDialogHost`。どちらも同じ `DialogService` が実装し、DI では同じインスタンスを返す。ViewModel は `IDialogService`、ウィンドウを開くアプリ側のサービスは `IDialogHost` を受け取る（具象型には依存しない）
- UI スレッドから呼ぶ

## ファイル/フォルダー選択

- `IFilePickerService.PickFileAsync` / `IFolderPickerService.PickFolderAsync`。選ばれたパスを返し、キャンセルなら null
- Windows App SDK のピッカー。アンパッケージでも、親のウィンドウ ID（`IDialogHost.Owner`）を渡すだけで使える

## PseudoModal（擬似モーダル）

- ウィンドウを親の上に出し、閉じるまで親を操作できなくする。`OverlappedPresenter.IsModal` では親を操作できてしまったため、Win32 のモーダルと同じく `EnableWindow` で親を無効にしている
- `SetOwner`（表示の前）で親と表示倍率を取り、オーナー設定（親より常に手前・一緒に最小化）をする。`Show` で親を無効にして表示、`CenterOnOwner` で親の中央へ（作業領域からはみ出す分は内側へ寄せる）
- 親を戻すのは、コードから閉じる（`Close`）・× / Alt+F4（`AppWindow.Closing`）・閉じたあと（`Closed`）のどれでも。何度戻してもよい（無効のまま閉じると、別のアプリが前面に来るため、消える前に戻す）。閉じたあとは親を前面に出す

## SingleInstanceGuard（多重起動の防止）

- Mutex 名は `Local\{appName}_{EXE パスの SHA256}`。アプリごとに `appName` を変える
- EXE のパスで区別するので、Debug / Release など別パスの EXE は同時に起動できる
- Mutex はプロセス終了まで保持する（GC で解放されないようフィールドで持つ）。呼び出し側も、Guard をアプリのフィールドで持ち続けること

## VisualTreeSearch

- ビジュアルツリーの子孫から、型（と名前）で要素を探す。コントロールのテンプレート内の要素（TreeView 内の ScrollViewer、NumberBox の消去ボタンなど）に触るために使う
- テンプレートが適用される前（`Loaded` より前）は見つからない

## WindowExtensions（`MmmSdk.WinUI.Windowing`）
- `SetForeground`: ウィンドウを前面に出す（`Activate` のあとに呼ぶ）。別のアプリが前面にあると、Windows の制限で前面にならないことがある
- `GetDpiScale`: ウィンドウがあるモニターの DPI 倍率（100% で 1.0）。`XamlRoot` は表示するまで無いので、表示前に大きさを決めるときに使う
- アプリ側にも同じ P/Invoke を持たなくて済むようにするための公開口（SDK の `NativeMethods` は internal）
- `UseCustomTitleBar`: アプリのアイコンを付け、タイトルバーを自分で描く（コンテンツをタイトルバーまで広げ、ドラッグできる領域を指定する）
- `UseFixedPresenter`: 最大化・最小化できない重ね合わせ型のウィンドウにする（ダイアログ用の見た目か・大きさを変えられるか・最小の大きさ）
- `ResizeClientDip`: クライアント領域の大きさを、論理サイズ（DIP）と DPI 倍率から決める（中身をちょうど収めたいときは切り上げ）
- `MoveCentered`: 指定した範囲（親ウィンドウや作業領域）の中央に置き、はみ出す分はその範囲にいちばん近いモニターの作業領域の内側へ寄せる。収める計算は `WindowPlacement.ClampToWorkArea`（`PseudoModal.CenterOnOwner` と通知ウィンドウも同じ計算を使う）
