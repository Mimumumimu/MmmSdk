# 確認ダイアログ・ファイル/フォルダー選択・クリップボード・擬似モーダル・多重起動の防止

アプリから移した汎用部品。アプリ固有の画面・文言は持たない (`MmmSdk.WinUI.Components.Dialogs` / `MmmSdk.Core.Components.SingleInstance`)。

## DialogService (ダイアログの親の決定)

- ダイアログ・モーダルウィンドウの親を決める。優先順位は、いちばん手前のモーダルウィンドウ → 最後に操作した普通のウィンドウ → 最初に登録したウィンドウ。どれも無ければ `InvalidOperationException`
- 普通のウィンドウは、見えているもの (`AppWindow.IsVisible`)だけを親にする。× でトレイへ退避した、隠れたウィンドウの上に出すと、ダイアログが見えないため。見えているものが 1 つも無いときだけ、隠れたウィンドウ (最後に操作したもの → 最初に登録したもの)を返す。このときは `ContentDialog` が見えないまま待ち続けるので、呼ぶ側が、先に見えるウィンドウを出してから呼ぶ
- 確認ダイアログ (`ConfirmAsync`)は `SemaphoreSlim(1)` で順番に開く (`ContentDialog` は、同じ画面に 2 つ同時に開くと例外になる)。親は、順番が来てから決める
- 普通のウィンドウは `TrackWindow` で登録する。アクティブになるたびに「最後に操作したウィンドウ」を更新し、閉じたら候補から外す
- モーダルウィンドウは `ShowModalAsync` で開く間だけ覚える (その上に開くダイアログの親にするため。一覧の上に入力画面・確認を重ねられる)
- `ConfirmAsync(title, message, primaryText, closeText)`: 確認ダイアログ。ボタンの文言は、実行・取りやめとも、アプリが言語に合わせて渡す (SDK は日本語を埋め込まない)。取り消しにくい操作の確認用に、既定のボタンを置かない (Enter で誤って実行しない。キャンセルを既定にすると、キャンセルが強調色になって主な操作に見えるので、どちらも強調しない)。コードで作る `ContentDialog` には既定のスタイルが当たらないので、`DefaultContentDialogStyle` を明示している
- `AskFileConflictAsync(title, message, replaceText, skipText, closeText, decideEachText = null)`: 保存先に同じ名前のファイルがあるときの扱いを聞く。選択肢は縦に並べたボタン (Windows のファイルのコピーの確認と同じ形)で、結果は `FileConflictChoice`(`Replace` / `Skip` / `DecideEach` / `Cancel`)。`decideEachText` を渡したときだけ「ファイルごとに決める」の選択肢が出て、`DecideEach` が返りうる (複数のファイルをまとめて聞くとき)。1 つのファイルについて聞くときは省く。取りやめるボタン・Esc は `Cancel`。置き換えは取り消しにくいので、`ConfirmAsync` と同じく既定のボタンを置かない。既定のボタンが無いと最初の選択肢 (置き換える)にフォーカスが当たり、Enter・Space 1 回で置き換えてしまうので、開いたら取りやめるボタンへフォーカスを移す (`Opened`。強調色は付けない)。文言は、すべてアプリが渡す。`ConfirmAsync` と同じロックで順番に開く
- 口は 2 つ。確認ダイアログは `IDialogService`、親の決定 (`Owner` / `TrackWindow` / `ShowModalAsync` / `Attach`)は `IDialogHost`。どちらも同じ `DialogService` が実装し、DI では同じインスタンスを返す。ViewModel は `IDialogService`、ウィンドウを開くアプリ側のサービスは `IDialogHost` を受け取る (具象型には依存しない)
- `IDialogHost.Attach(dialog)`: 開く前の `ContentDialog` を今の親の上に載せる。親の画面 (`XamlRoot`)と、親のテーマ (`ActualTheme`)を渡す。`ConfirmAsync`・`AskFileConflictAsync` と、アプリ側のダイアログ (DI から作る XAML のダイアログ)は、すべてこれを通して開く
- UI スレッドから呼ぶ

### 決定の理由: ダイアログに親のテーマを渡す
- `ContentDialog` は `XamlRoot` を渡しただけでは、親のテーマを引き継がない。背景・タイトル (ダイアログの枠)はライト、`{ThemeResource ...}` を付けた文字・入力欄・一覧の項目はダークと、テーマが食い違い、ダークモードで文字が読めなくなった
- 親のテーマ (`Owner.Content` の `ActualTheme`)を `RequestedTheme` に明示して渡す。開くたびに読むので、OS のテーマを切り替えたあとに開くダイアログにも反映される。ダイアログを開く 1 か所 (`Attach`)に集め、新しいダイアログが渡し忘れないようにする (`Owner.Content.XamlRoot` を直接代入しない)
- 最終形への近づき方: ダイアログの見た目 (テーマ)の決定を SDK の 1 か所に持つ。アプリのダイアログは色を決め打ちせず、ThemeResource だけで書く

## ファイル/フォルダー選択

- `IFilePickerService.PickFileAsync` / `IFolderPickerService.PickFolderAsync`。選ばれたパスを返し、キャンセルなら null
- `IFilePickerService.PickFilesAsync`: 複数のファイルを選べる。選ばれたパスの一覧を返し、キャンセルなら空
- `IFilePickerService.PickSaveFileAsync(suggestedFileName)`: 保存先を選ぶ (名前を付けて保存)。最初に入れておくファイル名を渡す。保存の種類は、そのファイル名の拡張子 1 つだけ (拡張子が無いときは、拡張子なしを表す `.`)。同じ名前のファイルがあるときの上書きの確認は、ダイアログが出す。キャンセルなら null
- Windows App SDK のピッカー。アンパッケージでも、親のウィンドウ ID (`IDialogHost.Owner`)を渡すだけで使える

## クリップボード (`MmmSdk.WinUI.Components.Clipboards`)

- `IClipboardService`: クリップボードのテキストの読み書き。ViewModel から UI 型 (`Windows.ApplicationModel.DataTransfer`)に触れずに使うための口。UI スレッドから呼ぶ
- `SetText(text)`: テキストを載せる。他のアプリがクリップボードを使っているときなどは `COMException`
- `GetTextAsync()`: テキストを取り出す。テキストが無ければ null。読めなかったときは `COMException`
- 名前空間を `Clipboard` にしないのは、`Windows.ApplicationModel.DataTransfer.Clipboard` の型名を隠さないため (フォルダ名は `Clipboards`)

## NativeMessageBox (標準のメッセージボックス。`MmmSdk.WinUI.Utilities`)

- `ShowInformation(text, caption)`: Windows 標準の情報メッセージボックス。閉じられるまで待つ
- `ShowError(text, caption)`: 同じく標準のエラー (赤い×のアイコン)。`FatalErrorHandler` が、復旧できないエラーを知らせるのに使う
- WinUI のウィンドウ・アプリの初期化 (XAML の読み込み)より前でも出せる。多重起動の案内のように、画面を作る前に知らせたいときに使う (`ContentDialog` は画面が無いと出せない)
- 親ウィンドウは持たない (デスクトップが親)

## PseudoModal (擬似モーダル。`MmmSdk.WinUI.Components.Windowing`)

- ウィンドウを親の上に出し、閉じるまで親を操作できなくする。`OverlappedPresenter.IsModal` では親を操作できてしまったため、Win32 のモーダルと同じく `EnableWindow` で親を無効にしている
- `SetOwner`(表示の前)で親と表示倍率を取り、オーナー設定 (親より常に手前・一緒に最小化)をする。`Show` で親を無効にして表示、`CenterOnOwner` で親の中央へ (作業領域からはみ出す分は内側へ寄せる)
- 親を戻すのは、コードから閉じる (`Close`)・× / Alt+F4 (`AppWindow.Closing`)・閉じたあと (`Closed`)のどれでも。何度戻してもよい (無効のまま閉じると、別のアプリが前面に来るため、消える前に戻す)。閉じたあとは親を前面に出す

## SingleInstanceGuard (多重起動の防止)

- Mutex 名は `Local\{appName}_{EXE パスの SHA256}`。アプリごとに `appName` を変える
- EXE のパスで区別するので、Debug / Release など別パスの EXE は同時に起動できる
- Mutex はプロセス終了まで保持する (GC で解放されないようフィールドで持つ)。呼び出し側も、Guard をアプリのフィールドで持ち続けること

## VisualTreeSearch (`MmmSdk.WinUI.Utilities`)

- ビジュアルツリーの子孫から、型 (と名前)で要素を探す。コントロールのテンプレート内の要素 (TreeView 内の ScrollViewer、NumberBox の消去ボタンなど)に触るために使う
- テンプレートが適用される前 (`Loaded` より前)は見つからない

## WindowExtensions (`MmmSdk.WinUI.Utilities`)
- `SetForeground`: ウィンドウを前面に出す (`Activate` のあとに呼ぶ)。別のアプリが前面にあると、Windows の制限で前面にならないことがある
- `GetDpiScale`: ウィンドウがあるモニターの DPI 倍率 (100% で 1.0)。`XamlRoot` は表示するまで無いので、表示前に大きさを決めるときに使う
- アプリ側にも同じ P/Invoke を持たなくて済むようにするための公開口 (SDK の `NativeMethods` は internal)
- `UseCustomTitleBar`: アプリのアイコンを付け、タイトルバーを自分で描く (コンテンツをタイトルバーまで広げ、ドラッグできる領域を指定する)
- `UseFixedPresenter`: 最大化・最小化できない重ね合わせ型のウィンドウにする (ダイアログ用の見た目か・大きさを変えられるか・最小の大きさ)
- `ResizeClientDip`: クライアント領域の大きさを、論理サイズ (DIP)と DPI 倍率から決める (中身をちょうど収めたいときは切り上げ)
- `MoveCentered`: 指定した範囲 (親ウィンドウや作業領域)の中央に置き、はみ出す分はその範囲にいちばん近いモニターの作業領域の内側へ寄せる。収める計算は `WindowPlacement.ClampToWorkArea`(`PseudoModal.CenterOnOwner` と通知ウィンドウも同じ計算を使う)
