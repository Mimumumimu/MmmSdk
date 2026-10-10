# MmmSdk

複数のアプリ (MmmTool など)で共有する汎用部品。個人利用が前提だが、将来公開する可能性がある。アプリ固有のもの (Entities・業務ロジック・画面)は置かない。アプリ側が SDK を参照し、SDK はアプリを知らない。

- 各チャットの最初の応答の前に、`/winui3-mvvm` スキルを Skill ツールで読み込み、そのルールに従う (コメントの書き方など)。
- 構成: `MmmSdk.Core`(`net10.0`。Windows / WinUI を参照しない)と `MmmSdk.WinUI`(WinUI 3 のクラスライブラリ。Core を参照)と、外部のドライバーを持つ部品の独立したプロジェクト `MmmSdk.Db.<製品名>`(今は `MmmSdk.Db.SqlServer`。`net10.0`)
- プロジェクトの直下は「Components / Controls / Utilities」の 3 つの層に分け、その中を部品ごとにする (アプリの機能別フォルダと同じ考え方。層ごとの `Views/` `Services/` 等は作らない)。プロジェクトは、UI の有無 (Core / WinUI)と、依存先の違い (リフレクションを使うドライバーなど、Core のトリミング・AOT 対応の宣言と合わないものは `MmmSdk.Db.<製品名>` に分ける)でだけ分け、フォルダの見やすさのためには増やさない (CsWin32 が作る型は internal なので、WinUI を複数のプロジェクトに分けると宣言が散らばる。[architecture.md](docs/architecture.md))。名前空間はフォルダどおり
  - 層の基準: `Components/<部品>/` = 何をするための部品かを名前で言えるもの (保存・設定・通知・トレイ・ターミナル など)。その部品の型 (サービス・画面・データ・その部品専用の static や拡張メソッド・その部品の中で使うコントロール)を一式まとめて置く / `Controls/` = どの部品にも属さない、XAML に置いて使う汎用のコントロール (WinUI のみ) / `Utilities/` = どの部品にも属さない、汎用の小さな道具 (拡張メソッド・小さなヘルパー。static に限らない。サブフォルダは作らず平置き)。迷ったら「その型は、ある部品の一部か」で決め、一部ならその部品のフォルダに置く (例: `ReadableJsonOptions` は Storage、`ScreenGeometry` は WindowPositions、`SettingsStoreExtensions` は Settings、`TerminalControl` は Terminal、`ThumbnailImage` は Attachments)。Core と WinUI で同じテーマの部品は同じフォルダ名にする。`Interop/` は土台なので層の外。DI 登録 (`Sdk*ServiceCollectionExtensions.cs`)はプロジェクト直下
  - `MmmSdk.Core.Components.Storage`(JsonFileStore・ReadableJsonOptions・DataLoadResult・DataFileException・LoadStatus)/ `.Settings`(ISettingsStore・SettingsStoreExtensions。インターフェースは型情報つきのメソッドだけで、基本型の専用メソッドは拡張メソッド。JSON 実装と `SettingsJsonContext` は `.Settings.Json`)/ `.WindowPositions`(WindowPositionService・WindowPosition・`WindowPositionJsonContext`・ScreenGeometry。画面との位置関係の純粋な計算)/ `.Secrets`(ISecretStore・SecretStoreException。秘密の保存の口。実装は WinUI 側の `.Secrets`)/ `.Paths`(PathOpener・PathOpenException・PathTarget)/ `.Notifications`(NotificationItem)/ `.SingleInstance`(SingleInstanceGuard)/ `.Attachments`(AttachmentStore)/ `.Shells`(ShellInfo・ShellKind・ShellLocator・ShellCommands・WslPath・WslDistribution。シェルの決定と、シェル別のコマンド作り・パスの変換。WSL も含む)/ `.Features`(IFeaturePlugin・FeatureRegistrationScope(`AddFeaturePlugin` の `order` を登録の間だけ持つ。サイドバー・トレイ・設定の並び順の値)・FeatureInfo・IFeatureStatus・IStartupTask・StartupTaskRegistration・IFeatureDisableConfirmation・`AddFeaturePlugin`/`AddFeature`/`AddAlwaysOnFeature`(オフにできない機能。並び順だけ使う)/`AddStartupTask`。`Json/FeatureJsonContext`(機能の並び順の配列用。internal)。機能 (プラグイン)がホストへ自分を登録する口と、オン・オフと並び順の管理 `FeatureService`(並び順は設定ストアの `Feature.Order`。`OrderOf`・`SetOrderAsync`・`ResetOrderAsync`・`OrderChanged`)・`FeatureChangeResult`(`AddFeatureService` で登録。UI に依存しない部分。切り替えの確認・画面への反映はホストが持つ))/ `.Hosting`(AppEnvironment。アプリの名前・データのフォルダー・アイコンのパス。ホストが 1 つ登録し、機能が DI から受け取る)/ `.Scheduling`(MinuteScheduler)/ `.Logging`(ErrorLog)、`MmmSdk.Core.Utilities`(Forget・Debouncer・RecentListExtensions・FileSizeFormatter)
  - `MmmSdk.WinUI.Components.Notifications`(通知ダイアログの Window・ViewModel・サービス)/ `.Dialogs`(IDialogService・IDialogHost・DialogService・ファイル/フォルダー選択・FileConflictChoice)/ `.Speech`(ISpeechService・SpeechService。Windows 標準の音声合成で日本語を読み上げる)/ `.Audio`(IAudioKeepAlive・AudioKeepAlive。無音を流し続けて、音声機器が休止するのを防ぐ)/ `.Clipboards`(IClipboardService・ClipboardService。クリップボードのテキストの読み書き。名前空間を `Clipboard` にすると `DataTransfer.Clipboard` の型名を隠すので複数形)/ `.Attachments`(IImageConverter・ImageConverter・ThumbnailImage。添付の画像の変換とサムネイル)/ `.Secrets`(CredentialSecretStore。資格情報マネージャーで ISecretStore を実装する。CsWin32 の `CredRead` など)/ `.Windowing`(PseudoModal・WindowBoundsKeeper・DefaultWindowPlacement)/ `.Errors`(ErrorState・FatalErrorHandler)/ `.Terminal`(ITerminalSession・PseudoConsoleSession・TerminalControl・PseudoConsole。xterm.js のアセットは `Components/Terminal/Assets/` にあり、csproj が参照するアプリの出力フォルダーへ配る。PseudoConsole は ConPTY にプロセスをつないで起動する。UI は持たないが、Core は Windows に依存しないので WinUI 側に置く)/ `.Pages`(NavigationPage・NavigationItem・NavigationArea・SettingsSection・IReleasablePage・IPageCache・`AddNavigationPage`/`AddSettingsSection`。機能 (プラグイン)がサイドバーのページと設定の部品を登録する口)/ `.Tray`(TrayIcon・TrayIconOptions・ITrayMenuSource・TrayMenuItem・`AddTrayMenuSource`。`TrayMenuRenderer` は internal。DI 登録は `AddMmmSdkWinUI` と同じ `SdkWinUIServiceCollectionExtensions` の `AddMmmSdkTray`)、`MmmSdk.WinUI.Controls`(TimeInputBox・LinkArea・TreeGridView。TreeGridView は ITreeGridRow・TreeGridColumn・TreeGridRowEventArgs・TreeGridRowPresenter(表の中の部品)と一組)、`MmmSdk.WinUI.Utilities`(WindowExtensions・WindowPlacement・NativeMessageBox・ImeControl・VisualTreeSearch)、`MmmSdk.Db.SqlServer.Components.Connections`(SqlServerConnectionFactory・SqlServerConnectionOptions・SqlServerAuthentication・SqlServerConnectionException。SQL Server への接続。表・SQL は持たない。DI 登録は持たない)。DI 登録はプロジェクト直下 (`MmmSdk.Core` / `MmmSdk.WinUI`)
- DI は拡張メソッドで登録する：`AddMmmSdkCore(dataDirectory)`(IJsonFileStore・ISettingsStore・IWindowPositionService・IPathOpener。実装の型ではなくインターフェースで受け取る)→ `AddMmmSdkWinUI()`(通知ダイアログ・DialogService・ファイル/フォルダー選択・クリップボード・秘密の保存・読み上げ・無音の出力。Core を先に登録すること)。トレイを使うアプリは別に `AddMmmSdkTray(options)`
- ビルドの共通設定はリポジトリ直下の `Directory.Build.props`(バージョン・Nullable・ImplicitUsings・XML コメントの検査・コードスタイルのビルド時検査・全プロジェクトを x64 専用にする `Platforms` と既定の `Platform`。Core も含めて AnyCPU を使わない。出力先に x64・win-x64 の段を作らない `AppendPlatformToOutputPath` / `AppendRuntimeIdentifierToOutputPath`。RID の段があると、アプリの VS の「発行」が RID を渡したときだけ出力先が変わり、DLL を見つけられずに失敗する)/ `Directory.Packages.props`(中央パッケージ管理。csproj にバージョンを書かない)/ `.editorconfig`(`root = true`)。アプリに取り込まれても MSBuild は近いこちらを使うので、アプリの設定とは混ざらない。アプリと共通のパッケージは SDK を先に上げる
- バージョンは `Directory.Build.props` の `Version`(現在 0.1.0。ファイル・アセンブリのバージョンは自動で 0.1.0.0)。アプリとは別に上げる
- Win32 の P/Invoke は CsWin32 (`Microsoft.Windows.CsWin32`。ソース生成。生成される型は internal)で書く。使う API の名前を `MmmSdk.WinUI/NativeMethods.txt` に足し (用途ごとにまとめる)、呼び出しは `PInvoke.<API>`(`Windows.Win32`)。設定は `NativeMethods.json`(`allowMarshaling: false`・`useSafeHandles: false`。ハンドルは `HWND` などの構造体)。`Interop/NativeMethods*.cs` には、生成された API を組み合わせる小さな補助 (IME・メニューのダーク対応・フォーカスを奪わない表示など)だけを置く (internal。アプリは Win32 の宣言を持たないので、SDK の 1 か所にだけある)。GDI (メニュー項目のフォント・描画)も CsWin32 で、手書きの `LibraryImport` は SDK に残っていない
- Core と WinUI の置き分け: BCL だけで書ける処理は、Windows 専用でも Core に置く (`[SupportedOSPlatform("windows")]` を付ける。例: `Paths`・`SingleInstance`)。P/Invoke (CsWin32)や WinUI の型が要るものは WinUI に置く (例: `Terminal`(PseudoConsole)・`Tray`・`NativeMessageBox`)。Core はアプリの Core からも呼べる形を保つ
- JSON のソース生成の Context は部品ごと (`SettingsJsonContext` / `WindowPositionJsonContext` / `FeatureJsonContext`。いずれも internal)。アプリ固有の型の登録はアプリ側の Context で行い、`ISettingsStore` / `JsonFileStore` には `JsonTypeInfo<T>` を渡す。シリアライザの設定は `ReadableJsonOptions.Create()`(public)に 1 か所だけ書き、SDK とアプリの Context がそれを使う (設定は Context に結び付くので Context ごとに作る)
- アプリ側の取り込み方: アプリ側は Git サブモジュール (`external/MmmSdk`)として取り込み、プロジェクト参照でつなぐ。SDK の変更は、サブモジュールの中でコミット・push (先に)→ アプリ側で参照先の更新をコミット。サブモジュールの中で作業するときは、先に master ブランチに切り替える (detached HEAD にしない)
- コミット・push は、ユーザーが明示的に指示するまで行わない

## 設計の基本姿勢
常に**最終形から逆算して**、今の形を決める。置き場所だけでなく、道具 (ライブラリ・コード生成・仕組み)の選び方、名前、インターフェースの切り方、すべてが対象。

- 最終形: 汎用の部品はすべてこの SDK に集まり、複数のアプリが使える。アプリ固有のもの (Entities・業務ロジック・画面)は持たない。Win32 の宣言は、この SDK の 1 か所 (CsWin32)にある
- 判断や見送りの理由に**使ってはいけない言い方**: 「今は〜だけ」「使うアプリが 1 つだけ」「他のアプリで使う予定がない」「優先度が低い」「宣言・件数が少ない」「増えたら (倍になったら)見直す」「必要になってから」。これらを書きそうになったら、最終形に合わせて今やる
- 例外は、ユーザーがまだ決めていないことだけ
- 画像の変換 (`ImageConverter`)と添付ファイルの大きさに、上限は設けない (ユーザーの決定。意図した仕様)。レビューで「大きな画像を元の解像度のまま展開する・添付に上限が無い」と指摘・提案しない (理由は `docs/controls.md`)
- 添付の一時フォルダ (`AttachmentStore`)は、同じアプリの別ビルド (Debug / Release など)と共有し、古いものの掃除で、同時に動いている別ビルドの 1 日より古いセッションも消える (ユーザーの決定。意図した仕様)。レビューで「別ビルドのフォルダを消しうる」と指摘・提案しない (理由は `docs/controls.md`)
- 判断を書くときは、「最終形にどう近づくか」を 1 行で書く。レビューで「現状維持」「対応不要」と判定した項目には、必ず理由 (なぜ今の形・場所が最終形に合っているか)を書く。「未対応」の項目にも、いつ・なぜやらないかを書く

## ドキュメント
- `README.md`: 利用者向け (機能・組み込み方・使い方)
- `docs/architecture.md`: フォルダの層・プロジェクトの分け方・CsWin32 の決定の理由
- `docs/storage.md`: JSON の読み書き・シリアライザの設定・壊れたファイルの扱い・汎用設定ストア・秘密の保存
- `docs/notification-dialog.md`: 通知ダイアログの見た目と挙動・ウィンドウ位置の保存・パスを開く処理
- `docs/dialogs.md`: 確認ダイアログ・ファイル/フォルダー選択・クリップボード・擬似モーダル・多重起動の防止
- `docs/speech.md`: 読み上げ (Windows 標準の音声合成)の仕組みと決定の理由
- `docs/audio.md`: 音声機器を眠らせない無音の出力 (`AudioGraph`)の仕組みと決定の理由
- `docs/tray.md`: タスクトレイのアイコン・メニューの仕組み
- `docs/conpty.md`: ConPTY (`PseudoConsole`)の仕組みと後始末の順序
- `docs/terminal.md`: シェルの決定・ターミナルのセッションと画面 (xterm.js)
- `docs/controls.md`: `TimeInputBox`・`LinkArea`・`TreeGridView`・IME・添付の一時保存・添付の画像
- `docs/db-sqlserver.md`: SQL Server への接続 (`MmmSdk.Db.SqlServer`)の使い方・接続の決まり・失敗のメッセージ・決定の理由
- 機能の区切りで、変更した部分の README・docs を更新してから終える。これからやることが残るときは、末尾に「未実装・残りの作業」の見出しを作って書く (実装したら消し、説明は docs に移す)
