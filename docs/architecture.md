# 構成

SDK のプロジェクト・フォルダ・Win32 呼び出しの決め方と、その理由。置き場所の基準の一覧は `CLAUDE.md`。

## フォルダを「Components / Controls / Utilities」の層に分ける
- プロジェクトの直下に「Components / Controls / Utilities」の層を 1 段置く。名前空間はフォルダどおり。部品ごとのフォルダだけを直下に並べると、画面を持つ部品 (通知・トレイ・ダイアログ・ターミナル)と、小さな道具 (`Debouncer`・`VisualTreeSearch`・`ImeControl` など)が同じ段に並んで見分けにくい。層で分ければ、「何をする部品か」と「道具」を別の段に置け、層と部品名の 2 段で探せる。部品が増えても、直下の見分けやすさが保たれる
- どの層に入れるかの基準は `CLAUDE.md` の「構成」。同じテーマの型は 1 つの部品のフォルダにまとめる (ConPTY とターミナルは `Components/Terminal/`)
- ターミナルの資材は `Components/Terminal/Assets/` で、出力先 (`Assets/Terminal`)は csproj の `Link` で決まっていて、変えない

### プロジェクトの分け方
プロジェクトは、次の 2 つの基準でだけ分ける。見やすさのために分けることはしない (見やすさは、層とフォルダで足りる)。

- **UI の有無**: `MmmSdk.Core`(Windows / WinUI に依存しない)と `MmmSdk.WinUI`(WinUI 3)
- **依存先の違い**: 外部のドライバーなどを参照する部品は、`MmmSdk.Db.<製品名>` のように独立したプロジェクトにする (今は `MmmSdk.Db.SqlServer`・`MmmSdk.Db.Sqlite`。[db-sqlserver.md](db-sqlserver.md)・[db-sqlite.md](db-sqlite.md))。理由は 2 つ。`MmmSdk.Core` はトリミング・AOT 対応を宣言しているので、リフレクションを使うドライバーを混ぜると宣言が合わなくなる。ドライバーを使わないアプリには、その DLL が入らない

`MmmSdk.WinUI` を、さらに複数のプロジェクトに分けない。CsWin32 が作る型は internal なので、WinUI 側を複数のプロジェクトに分けると、各プロジェクトに CsWin32 を持たせる (宣言が散らばる)か、Win32 の型を公開する Interop プロジェクトを作るしかなく、下の「Win32 の P/Invoke は CsWin32 で生成する」(Win32 の宣言は SDK の 1 か所)を崩す。分ける基準の「依存先の違い」「別パッケージでの配布」にも当たらない。

### アプリ側に同じ層を作らない理由
アプリは「機能」で分け、SDK は「部品」で分けていて、分ける軸がもともと違う。

## Win32 の P/Invoke は CsWin32 で生成する
- `Microsoft.Windows.CsWin32`(ソース生成)で作る。呼び出しは `PInvoke.<API>`(`Windows.Win32`)。使う API の名前を `NativeMethods.txt` に書く。設定は `NativeMethods.json`(`allowMarshaling: false`・`useSafeHandles: false`)。生成された API を組み合わせる小さな補助 (IME・メニューのダーク対応など)だけを、`Interop/NativeMethods*.cs` に残す
- 理由
  - 型 (`HWND`・`HMENU`・`HICON` など)と列挙体 (`SYSTEM_METRICS_INDEX` など)で、取り違えと値の写し間違いをコンパイル時に止められる
  - 構造体・定数を Windows のメタデータから作るので、手書きの誤りがなくなる。使う API の一覧が `NativeMethods.txt` に残る
  - 宣言は SDK (`MmmSdk.WinUI`)に置く。アプリ側の ConPTY・メッセージボックスも SDK に移し、アプリには Win32 の宣言を持たない。SDK が public にするのは、クラス・拡張メソッドの口だけ (宣言そのものは internal のまま)
- 影響
  - ConPTY は画面を持たないが、Windows の API を呼ぶので、Windows に依存しない `MmmSdk.Core` ではなく `MmmSdk.WinUI`(`Components/Terminal/PseudoConsole`)に置いた。標準のメッセージボックスは `Utilities/NativeMessageBox`
  - CsWin32 はビルド時だけ使う (`PrivateAssets="all"`)ので、配布物には入らない。バージョンは SDK の `Directory.Packages.props` にだけ書く (アプリは直接使わない)
  - `POINT` は .NET では `System.Drawing.Point` に割り当てられる。`NIN_KEYSELECT` は定義がないので、`TrayIcon` に定数 (`NIN_SELECT | 1`)で持つ
  - ウィンドウプロシージャのコールバックは `[UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]` で、CsWin32 の `WNDPROC` に合わせる


## 機能 (プラグイン)がホストへ入る口 (`Features` / `Pages` / `Hosting` / `Tray`)
- アプリの機能を、機能ごとのライブラリ (プラグイン)に分けられるよう、機能がホスト (exe)へ入るための型を SDK に置く。機能はホストを参照できないため、機能もホストも SDK のこれらの型を参照する
- `MmmSdk.Core.Components.Features`: 入口 `IFeaturePlugin`(`Register(IServiceCollection)`。ホストは `AddFeaturePlugin<TPlugin>(order)` で呼ぶ。`order` は、サイドバー・トレイ・設定の並び順の値で、登録の間だけ `FeatureRegistrationScope` が持つ)・機能の登録 `FeatureInfo` / `AddFeature` / `AddAlwaysOnFeature`(オフにできない機能。並び順だけ使う)。`AddFeature` の `availableWhen` は、機能が動く前提 (例: サーバーモード)を満たさない間、機能の一覧にも出さず常にオフにする条件 (利用者のオン・オフとは別。サイドバー・トレイ・設定の部品・起動時の準備が、`IsEnabled` で一斉に外れる)・起動時の準備 `IStartupTask` / `StartupTaskRegistration` / `AddStartupTask`・オフにする前の確認 `IFeatureDisableConfirmation`・オン・オフを調べる口 `IFeatureStatus`・オン・オフの管理 `FeatureService`(状態の設定ストアへの保存・起動時の準備の実行・切り替えの通知・利用者が決めた並び順 (設定ストアの `Feature.Order`。キーの配列。載っていない機能は登録の順で後ろに付く。`OrderOf` が画面の並びの値を返し、`SetOrderAsync` / `ResetOrderAsync` で保存・既定へ戻し、`OrderChanged` で通知する。トレイは使わない)。ホストが `AddFeatureService()` で登録する。保存が無い機能は、登録の既定値。切り替えの確認・画面への反映はホストが持つ)・`FeatureChangeResult`
- `MmmSdk.Core.Components.Hosting`: `AppEnvironment`(アプリの名前・データのフォルダー・アイコンのパス。ホストが 1 つ登録し、機能が DI から受け取る)
- `MmmSdk.WinUI.Components.Pages`: サイドバーのページ `NavigationPage` / `NavigationItem` / `NavigationArea` / `AddNavigationPage` / `AddNavigationChildPage` (別のページの子の項目として、親の直後に字下げして並べる。開閉は無く、常に出す。子は 1 段だけで、サイドバーの上部だけ。親がオフの間は出さない。親は、同じ機能のページを型で指す。`NavigationView` の入れ子の項目は使わない (親を押すたびに子の開閉が切り替わり、「親を押すとページが開く」動きと合わないため))・設定の部品 `SettingsSection` / `AddSettingsSection`・ページの後始末 `IReleasablePage`・ホストが作ったページを調べる口 `IPageCache`・サイドバーの本体 `NavigationHost` (UserControl。`NavigationView` と、選んだ項目のページを出す `Frame`)・項目の一覧と選択を持つ `NavigationHostViewModel`・ページを DI のスコープごとに作って保つ `PageProvider`。登録された機能のオン・オフ・並び順 (`FeatureService`)に合わせて項目を作り直し、オフになった機能のページを捨てる。ウィンドウ (タイトルバー・トレイへの退避)は、アプリが持つ。アプリごとに同じ部品を書き写さず、機能 (バッチ処理)が増えても、ホストは機能の名前を持たずに済むようにするため、MmmTool と MmmBatch で共有する
- `MmmSdk.WinUI.Components.Tray`: トレイメニューの項目の登録 `AddTrayMenuSource`(機能がオフの間は項目を出さない包みを内部に持つ)
- 登録の拡張メソッドの型引数には、トリミング・AOT 対応のため `DynamicallyAccessedMembers(PublicConstructors)` を付ける (Core の `IsAotCompatible` の宣言に合わせる)
- 最終形にどう近づくか: 機能とホストの境界をここに 1 か所に持つので、機能の追加・削除はホストの 1 行で済み、実行時に DLL を読み込むローダーを足しても、機能側は作り直さない
- 機能のオン・オフの管理 `FeatureService`・`FeatureChangeResult` も SDK に置く。設定ストア・機能の登録・起動時の準備だけで動き、UI にも特定の機能にも依存しないため、アプリごとに同じ管理を書き直さずに済む。ホストが持つのは、切り替えの結果を画面へ反映すること (サイドバー・ページのキャッシュ・設定の画面)と、オフにする前の確認の画面。保存するキーは `Feature.<キー>.Enabled`
- 最終形にどう近づくか: 共通の部品はすべて SDK に集まる形に近づく。オン・オフの管理の部分をアプリから SDK へ移し、別のアプリが機能 (プラグイン)を載せるとき、`AddFeatureService()` を呼ぶだけで同じ管理を使える

## 部品の一覧
名前空間ごとの中身。型の細かい一覧はコードを見る。DI 登録はプロジェクト直下 (`MmmSdk.Core` / `MmmSdk.WinUI`)。

### MmmSdk.Core
- `MmmSdk.Core.Components.Storage`: JsonFileStore・ReadableJsonOptions・DataLoadResult・DataFileException・LoadStatus
- `.Settings`: ISettingsStore・SettingsStoreExtensions・SettingsStoreStartup (設定の先読み。ホストが最初の起動時の準備として登録する)。インターフェースは型情報つきのメソッドだけで、基本型の専用メソッドは拡張メソッド。JSON 実装と `SettingsJsonContext` は `.Settings.Json`
- `.WindowPositions`: WindowPositionService・WindowPosition・`WindowPositionJsonContext`・ScreenGeometry (画面との位置関係の純粋な計算)
- `.Secrets`: ISecretStore・SecretStoreException (秘密の保存の口。実装は WinUI 側の `.Secrets`)
- `.Paths`: PathOpener・PathOpenException・PathTarget
- `.Holidays`: JapaneseHolidays (日本の祝日の判定)。内閣府の「国民の祝日」の一覧 (`syukujitsu.csv`。ダウンロードしたままの Shift_JIS のファイル。UTF-8 も読める。外へは通信しない)を、アプリの `Assets/Holidays/` から読む。組み込みリソースにせず、ファイルのまま `Assets/Holidays/` へ配る (SDK の csproj。利用者が、最新の一覧に置き換えられるように。置き換えたら、次の起動から使う)。ファイルが無い・読めないときと、一覧の最後の年より先は、祝日として扱わない。公共データ利用規約 (第 1.0 版)が出典の記載を求めるので、`syukujitsu.LICENSE.txt` を同じ場所へ配り、アプリの同梱ライブラリのライセンス全文に載せる (ファイルは変換せず、そのまま置くので、加工の記載は要らない)
- `.Notifications`: NotificationItem
- `.SingleInstance`: SingleInstanceGuard
- `.Attachments`: AttachmentStore
- `.Shells`: ShellInfo・ShellKind・ShellLocator・ShellCommands・WslPath・WslDistribution (シェルの決定と、シェル別のコマンド作り・パスの変換。WSL も含む)
- `.Features` / `.Hosting`: 機能 (プラグイン)がホストへ自分を登録する口と、オン・オフと並び順の管理 (`FeatureService`)。上の「機能 (プラグイン)がホストへ入る口」。`Json/FeatureJsonContext` は機能の並び順の配列用 (internal)
- `.Scheduling`: MinuteScheduler
- `.Logging`: ErrorLog
- `MmmSdk.Core.Utilities`: Forget・Debouncer・RecentListExtensions・FileSizeFormatter

### MmmSdk.WinUI
- `MmmSdk.WinUI.Components.Notifications`: 通知ダイアログの Window・ViewModel・サービス
- `.Dialogs`: IDialogService・IDialogHost・DialogService・ファイル/フォルダー選択・FileConflictChoice
- `.Speech`: ISpeechService・SpeechService (Windows 標準の音声合成で日本語を読み上げる)
- `.Audio`: IAudioKeepAlive・AudioKeepAlive (無音を流し続けて、音声機器が休止するのを防ぐ)
- `.Clipboards`: IClipboardService・ClipboardService・ClipboardFormats (クリップボードのテキストの読み書きと、載っている中身の種類の判定。名前空間を `Clipboard` にすると `DataTransfer.Clipboard` の型名を隠すので複数形)
- `.Attachments`: IImageConverter・ImageConverter・ThumbnailImage (添付の画像の変換とサムネイル)
- `.Secrets`: CredentialSecretStore (資格情報マネージャーで ISecretStore を実装する。CsWin32 の `CredRead` など)
- `.Windowing`: PseudoModal・WindowBoundsKeeper・DefaultWindowPlacement
- `.Errors`: ErrorState・FatalErrorHandler
- `.Terminal`: ITerminalSession・PseudoConsoleSession・TerminalControl・PseudoConsole。xterm.js のアセットは `Components/Terminal/Assets/` にあり、csproj が参照するアプリの出力フォルダーへ配る。PseudoConsole は ConPTY にプロセスをつないで起動する。UI は持たないが、Core は Windows に依存しないので WinUI 側に置く
- `.Pages`: 機能 (プラグイン)がサイドバーのページと設定の部品を登録する口 (上の「機能 (プラグイン)がホストへ入る口」)。`IBusyCursor` は、ウィンドウ全体のマウスカーソルを砂時計にする口で、`NavigationHost`(サイドバーとページの全体)が、その状態に合わせてカーソルを変える (サイドバーを押した直後は、マウスがページの外にあるため、ページだけでは変えない)。`RunAsync` は、砂時計を描かせてから、UI スレッドで長く動く処理を始める
- `.Tray`: TrayIcon・TrayIconOptions・ITrayMenuSource・TrayMenuItem・`AddTrayMenuSource`。`TrayMenuRenderer` は internal。DI 登録は `AddMmmSdkWinUI` と同じ `SdkWinUIServiceCollectionExtensions` の `AddMmmSdkTray`
- `MmmSdk.WinUI.Controls`: TimeInputBox・LinkArea・TreeGridView (TreeGridView は ITreeGridRow・TreeGridColumn・TreeGridRowEventArgs・TreeGridRowPresenter (表の中の部品)と一組)
- `MmmSdk.WinUI.Utilities`: WindowExtensions・WindowPlacement・NativeMessageBox・ImeControl・TextBoxExtensions・VisualTreeSearch・WarningBubble

### DB の接続
- `MmmSdk.Db.SqlServer.Components.Connections`: SqlServerConnectionFactory・SqlServerConnectionOptions・SqlServerAuthentication・SqlServerConnectionException。SQL Server への接続。表・SQL は持たない。DI 登録は持たない
- `MmmSdk.Db.Sqlite.Components.Connections`: SqliteDatabase・SqliteDatabaseOptions・SqliteMigration。SQLite のファイルを開き、版を上げ、壊れたファイルを退避する。表・SQL は持たない。DI 登録は持たない
