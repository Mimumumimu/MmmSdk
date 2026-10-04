# MmmSdk

MmmSdk は、複数の WinUI 3 デスクトップアプリ（[MmmTool](https://github.com/Mimumumimu/MmmTool) など）で共有する汎用部品のライブラリです。
JSON ファイルへの保存、アプリ共通の設定ストア、ウィンドウ位置の保存、パスを開く処理、デスクトップ通知のダイアログ、確認ダイアログ・ファイル/フォルダー選択・擬似モーダル、多重起動の防止を提供します。

アプリ固有のもの（データ構造・業務ロジック・画面）は含みません。アプリ側が SDK を参照し、SDK はアプリを参照しません。

## 主な機能

- JSON ファイルの読み書き（一時ファイル経由で置き換えるため、書き込み途中で失敗してもファイルが壊れない）
- 壊れた JSON ファイルの自動退避（`名前.broken-yyyyMMdd-HHmmss.json` に名前を変えて残し、空として読み込む）
- アプリ共通の汎用設定ストア（キー → 任意の型の値。1 ファイルに集約・スレッドセーフ）
- ウィンドウ位置の保存・復元、画面外に出たウィンドウの判定
- URL・ファイル・フォルダー・実行ファイルを既定のアプリで開く処理と、パスの種類の判定（環境変数の展開つき）
- デスクトップ通知のダイアログ（フォーカスを奪わない常に最前面のウィンドウ。ドラッグ移動・位置保存・クリックで閉じる・本文のリンク）
- 確認ダイアログ・ファイル/フォルダー選択（親ウィンドウを自動で決める）と、ウィンドウを親の上に擬似モーダルで出す部品
- 同一 EXE の多重起動の防止
- アプリの初期化（XAML の読み込み）より前でも出せる標準のメッセージボックス
- エラーのログ（日付ごとのファイル）と、復旧できないエラーの処理（ログ → ダイアログ → 終了）。未処理の例外の最後の受け皿
- ターミナル（ConPTY でシェルを動かし、WebView2 上の xterm.js で描く画面）と、既定のシェルの決定（PowerShell 7 → Windows PowerShell）・シェル別のコマンド作り
- ConPTY（Windows 擬似コンソール）にプロセスをつないで起動する部品（ターミナルの土台）
- タスクトレイのアイコンと右クリックメニュー（Win32 を直接使う。メニューは自前描画でダーク/ライト対応）
- 時刻の入力欄（`TimeInputBox`）・押せる領域（`LinkArea`）・IME のオン/オフ、添付ファイルの一時保存先の管理
- ビジュアルツリーから要素を探す処理（コントロールのテンプレート内の要素に触るため）

## 構成

| プロジェクト | 対象 |
| --- | --- |
| `MmmSdk.Core` | `net10.0`（WinUI に依存しない。トリミング・AOT 互換の分析を有効にしている。`Paths`（`PathOpener` / `PathTarget`）だけは Windows 前提） |
| `MmmSdk.WinUI` | `net10.0-windows10.0.19041.0`（WinUI 3） |

プロジェクトの直下は、「Components / Controls / Utilities」の 3 つの層に分けています。名前空間はフォルダーと同じです。

| 層 | 入れるもの | 探し方 |
| --- | --- | --- |
| `Components/<部品>/` | 何をするための部品かを名前で言えるもの（保存・設定・通知・トレイ・ターミナル など）。その部品の型（サービス・画面・データ・その部品専用の static や拡張メソッド・その部品の中で使うコントロール）を一式まとめて置く | やりたいことの名前（「通知」「トレイ」「保存」）で探す |
| `Controls/` | どの部品にも属さない、XAML に置いて使う汎用のコントロール（`MmmSdk.WinUI` のみ） | XAML に置く部品はここ（部品の一部のものは、その部品のフォルダーにある） |
| `Utilities/` | どの部品にも属さない、汎用の小さな道具（拡張メソッド・小さなヘルパー）。サブフォルダーは作らず平置き | 「`Window` の拡張メソッド」「`Task` の扱い」などの道具はここ |

迷ったら「その型は、ある部品の一部か」で決めています。一部なら、その部品のフォルダーにあります（例: `ReadableJsonOptions` は `Storage`、`ScreenGeometry` は `WindowPositions`、`SettingsStoreExtensions` は `Settings`、`TerminalControl` は `Terminal`、`ThumbnailImage` は `Attachments`）。`Core` と `WinUI` で同じテーマの部品は、同じフォルダー名です（`Attachments`・`Notifications`）。Win32 の宣言（`Interop/`。`MmmSdk.WinUI` のみ）は土台なので、層の外にあります。DI への登録（`AddMmmSdkCore` / `AddMmmSdkWinUI`）はプロジェクトの直下です。

#### MmmSdk.Core

| 名前空間 | 内容 |
| --- | --- |
| `MmmSdk.Core.Components.Storage` | JSON ファイルの読み書き（`IJsonFileStore` / `JsonFileStore`）・共通の書式（`ReadableJsonOptions`）・読み込み結果（`DataLoadResult`）・読み書きの失敗（`DataFileException`） |
| `MmmSdk.Core.Components.Settings` | 汎用設定ストア（`ISettingsStore`・拡張メソッド `SettingsStoreExtensions`）。JSON での実装は `MmmSdk.Core.Components.Settings.Json`（`JsonSettingsStore`） |
| `MmmSdk.Core.Components.WindowPositions` | ウィンドウ位置の保存・復元（`IWindowPositionService` / `WindowPositionService` / `WindowPosition`）と、画面との位置関係の計算（`ScreenGeometry`。見えているか・作業領域に収める） |
| `MmmSdk.Core.Components.Paths` | URL・ファイル・フォルダーを開く処理（`IPathOpener` / `PathOpener` / `PathOpenException`）と種類の判定（`PathTarget`） |
| `MmmSdk.Core.Components.Notifications` | 通知の項目（`NotificationItem`） |
| `MmmSdk.Core.Components.Shells` | シェルの決定（`ShellInfo` / `ShellKind` / `ShellLocator`）と、シェル別のコマンド作り（`ShellCommands`） |
| `MmmSdk.Core.Components.Scheduling` | 毎分 00 秒に処理を呼ぶ（`MinuteScheduler`） |
| `MmmSdk.Core.Components.Logging` | エラーログの追記（`ErrorLog`。`yyyy-MM-dd.log`） |
| `MmmSdk.Core.Components.Attachments` | 添付ファイルの一時保存先（`AttachmentStore`） |
| `MmmSdk.Core.Components.SingleInstance` | 多重起動の防止（`SingleInstanceGuard`） |
| `MmmSdk.Core.Utilities` | 待たずに走らせるタスクの失敗を未処理例外にする（`Forget`）・入力が止まるのを待ってから処理を 1 回だけ行う（`Debouncer`）・「最近使った順」のリストの操作（`AddRecent`） |
| `MmmSdk.Core` | DI への登録（`AddMmmSdkCore`） |

#### MmmSdk.WinUI

| 名前空間 | 内容 |
| --- | --- |
| `MmmSdk.WinUI.Components.Notifications` | 通知ダイアログ（`INotificationDialogService` / `NotificationDialogService`・`NotificationWindow`・`NotificationWindowViewModel`） |
| `MmmSdk.WinUI.Components.Dialogs` | 確認ダイアログ（`IDialogService`）・親の決定（`IDialogHost`。実装は `DialogService`）・ファイル/フォルダー選択（`IFilePickerService` / `IFolderPickerService`） |
| `MmmSdk.WinUI.Components.Windowing` | ウィンドウを親の上に擬似モーダルで出す（`PseudoModal`）・位置と大きさの自動保存（`WindowBoundsKeeper`） |
| `MmmSdk.WinUI.Components.Tray` | タスクトレイ（`TrayIcon`・`TrayIconOptions`・`ITrayMenuSource`・`TrayMenuItem`）。DI 登録は `AddMmmSdkTray`（`SdkWinUIServiceCollectionExtensions` の中） |
| `MmmSdk.WinUI.Components.Terminal` | ターミナル（`ITerminalSession` / `PseudoConsoleSession`・`TerminalControl`。xterm.js で描く）と、ConPTY にプロセスをつないで起動する部分（`PseudoConsole`） |
| `MmmSdk.WinUI.Components.Attachments` | 添付の画像を JPEG に変換する（`IImageConverter` / `ImageConverter`）・サムネイル（`ThumbnailImage.FromFile`。`x:Bind` から呼ぶ） |
| `MmmSdk.WinUI.Components.Errors` | 画面に出すエラー 1 件の状態（`ErrorState`。`InfoBar` に結び付ける）・復旧できないエラーの最後の受け皿（`FatalErrorHandler`） |
| `MmmSdk.WinUI.Controls` | `TimeInputBox`（時刻の入力欄）・`LinkArea`（押せる領域） |
| `MmmSdk.WinUI.Utilities` | Window の拡張メソッド（前面に出す `SetForeground`・トレイや最小化から戻して前面に出す `BringToFront`・DPI 倍率 `GetDpiScale`・タイトルバー `UseCustomTitleBar`・`UseFixedPresenter`・`ResizeClientDip`・`MoveCentered`。`WindowExtensions`）・作業領域に収める計算（`WindowPlacement`）・標準のメッセージボックス（`NativeMessageBox`）・IME のオン/オフ（`ImeControl`）・ビジュアルツリーの検索（`VisualTreeSearch`） |
| `MmmSdk.WinUI` | DI への登録（`AddMmmSdkWinUI` / `AddMmmSdkTray`） |

## 技術スタック

- .NET 10
- WinUI 3（Windows App SDK 2.5）… `MmmSdk.WinUI` のみ
- CommunityToolkit.Mvvm … `MmmSdk.WinUI` のみ
- WebView2 + [xterm.js](https://xtermjs.org/) 6.0.0 / addon-fit 0.11.0（ターミナル描画。MIT。`MmmSdk.WinUI/Components/Terminal/Assets/` に同梱。ライセンスファイルも同じ場所）
- Microsoft.Extensions.DependencyInjection.Abstractions（DI 登録用の拡張メソッド）
- System.Text.Json（ソース生成。トリミング・AOT でも動く形。`MmmSdk.Core` は `IsTrimmable` / `IsAotCompatible` を有効にして、ビルドが検査する）

## 必要環境

- Windows 10 (1809) 以上（x64）… `MmmSdk.WinUI` を使う場合
- .NET 10 SDK
- Visual Studio 2026 以降（「WinUI アプリケーション開発」ワークロード）

## アプリへの組み込み方

### 1. サブモジュールとして追加

アプリのリポジトリに、Git サブモジュールとして取り込みます。

```powershell
git submodule add https://github.com/Mimumumimu/MmmSdk.git external/MmmSdk
```

アプリを clone する人は `git clone --recurse-submodules` で取得します（取りこぼしたときは `git submodule update --init --recursive`）。

### 2. プロジェクト参照

アプリの csproj から参照します（UI に依存しない層からは `MmmSdk.Core` だけを参照します）。

```xml
<ItemGroup>
  <ProjectReference Include="..\external\MmmSdk\MmmSdk.WinUI\MmmSdk.WinUI.csproj" />
</ItemGroup>
```

### 3. DI への登録

Generic Host などの `IServiceCollection` に登録します。`AddMmmSdkCore` を先に呼んでください（通知ダイアログが位置の保存とリンクを開く処理を使います）。

```csharp
services.AddMmmSdkCore(Path.Combine(AppContext.BaseDirectory, "Data")); // JSON の保存先フォルダー
services.AddMmmSdkWinUI();
```

| メソッド | 登録するもの |
| --- | --- |
| `AddMmmSdkCore(dataDirectory)` | `IJsonFileStore`・`ISettingsStore`・`IWindowPositionService`・`IPathOpener`（すべて Singleton。実装の型ではなくインターフェースで受け取る。ViewModel のテストでモックに差し替えられる） |
| `AddMmmSdkTray(options)` | `TrayIconOptions`・`TrayIcon`（Singleton）。トレイを使うアプリだけが呼ぶ。`TrayIcon` は `FatalErrorHandler` を受け取るので、アプリが先に `AddSingleton(fatalErrors)` で登録しておく |
| `AddMmmSdkWinUI()` | `INotificationDialogService`・`Func<NotificationWindow>`（通知ウィンドウを作る処理）・`DialogService`（`IDialogService` と `IDialogHost` が同じインスタンスを返す）・`IFilePickerService`・`IFolderPickerService`・`IImageConverter`（すべて Singleton）、`NotificationWindow`・`NotificationWindowViewModel`（Transient） |

## 使い方

### JSON ファイルの読み書き（`JsonFileStore`）

アプリ固有のデータも、DI から受け取った `IJsonFileStore` で同じフォルダーに保存できます。型の情報は、アプリ側で `JsonSerializable` 登録した `JsonTypeInfo<T>` を渡します（ソース生成）。

SDK と同じ書式（インデントあり・日本語や記号をエスケープしない・コメントと末尾のカンマを許す・プロパティ名の大文字小文字を区別しない）にするには、Context を `ReadableJsonOptions.Create()` で作ります。設定は Context に結び付くので、Context ごとに `Create()` で新しく作ってください。

```csharp
[JsonSerializable(typeof(LinkMenu))]
internal sealed partial class MyJsonContext : JsonSerializerContext
{
    public static MyJsonContext Readable { get; } = new(ReadableJsonOptions.Create());
}
```

```csharp
var result = await store.ReadAsync("Links.json", MyJsonContext.Readable.LinkMenu);
var menu = result.Value ?? new LinkMenu();   // ファイルが無い・空・壊れていたときは null
if (result.RecoveryMessage is { } message)
{
    // 壊れていたファイルを退避した。画面で知らせる
}

await store.WriteAsync("Links.json", menu, MyJsonContext.Readable.LinkMenu);
```

- 読み込みの結果は `DataLoadResult<T>`（`Value` と `RecoveryMessage`）です
- JSON として読めないファイルは `名前.broken-yyyyMMdd-HHmmss.json`（同じ秒に重なったら `-2` 以降）へ名前を変えて退避し、`Value` は null、`RecoveryMessage` にユーザー向けのメッセージが入ります。退避したファイルは自動では消しません
- 0 バイト・空白だけのファイルは、退避せずに null として扱います
- ロック・権限などで読み書きできないときは `DataFileException`（メッセージはそのまま画面に出せる形）
- 読み込み・書き込みはファイル操作ごとに、ファイル名ごとのロックを取るので、別スレッドから同時に呼べます（別のファイルは待たせません）

### 汎用設定ストア（`ISettingsStore`）

アプリ共通の小さな設定を `Data/AppSettings.json` の 1 ファイルにまとめて保存します。キーには機能ごとの接頭辞を付けて衝突を避けます。

```csharp
var minutes = settings.Get("Reminder.SnoozeIntervalMinutes", 15);         // 無い・型が合わないときは既定値
await settings.SetAsync("Reminder.SnoozeIntervalMinutes", 30);            // 保存し終えるまで待つ

// string / bool / int / long / double は型ごとの専用メソッド（`SettingsStoreExtensions` の拡張メソッド。それ以外の型は、ビルドで誤りになる）。それ以外の型は JsonTypeInfo を渡す
var options = settings.Get("Foo.Options", new FooOptions(), MyJsonContext.Default.FooOptions);
```

- `Get` は同期です。最初のアクセスで 1 度だけファイルを読み、以後はメモリから返します。起動時の準備で `EnsureLoadedAsync` を呼んでおくと、最初の読み込みを非同期で済ませられます（UI スレッドを止めません）
- ファイルが無い・空・壊れているときは空の設定として扱い、例外は出しません。壊れていたときは退避して `RecoveryMessage` に、読めなかったときは `LoadError` に理由が残ります
- 例外を出さずに「ある・型が合う」を確かめたいときは `TryGet(key, out value)` を使います（`int.TryParse` と同じ形）。`Get` は既定値を返す版です
- 読めなかった（`LoadError`）ときは `IsReadOnly` が true になり（一時的なロックだったときのために、保存のたびに 1 度だけ読み直し、読めれば、そのまま保存します）、元のデータを上書きで消さないよう、`SetAsync` / `RemoveAsync` は何も保存せず `false` を返します。保存できたときだけ `true` です（例外にはしません）
- ほかに `Contains(key)` / `RemoveAsync(key)` があります

### ウィンドウ位置の保存（`IWindowPositionService`）

```csharp
var position = positions.Load("Notification");                    // 保存が無ければ null
await positions.SaveAsync("Notification", new WindowPosition(x, y));

// 全モニターの作業領域に対して、ウィンドウが面積の 50% 以上見えているか
var visible = ScreenGeometry.IsVisibleEnough(windowRect, workAreas, 0.5);
```

位置は設定ストアに `WindowPosition.<キー>` として保存されます。

位置と大きさ（`WindowBounds`）は `LoadBounds(key)` / `SaveBoundsAsync(key, bounds)` で、`WindowBounds.<キー>` として保存されます。WinUI のウィンドウには、これを使って復元・保存を自動で行う `WindowBoundsKeeper`（`MmmSdk.WinUI.Components.Windowing`）があります。

```csharp
// コンストラクターで 1 回呼ぶだけ（ウィンドウが閉じたら自分で後始末する）
WindowBoundsKeeper.Attach(this, positions, "MainWindow", defaultWidthDip: 1280, defaultHeightDip: 720);
```

保存が無い・画面外のときは既定の大きさ（DIP を DPI に合わせる）で出します。変更は 1 秒まとめて保存し、最小化・最大化・非表示の間は保存しません。

### パスを開く（`IPathOpener` / `PathTarget`）

```csharp
try
{
    await opener.OpenAsync(@"%USERPROFILE%\Documents");   // URL・ファイル・フォルダー・実行ファイル
}
catch (PathOpenException ex)
{
    // ex.Message をそのまま表示できる
}

var kind = PathTarget.Classify(path);   // Empty / Url / Folder / Executable / File / NotFound
```

- 環境変数を展開し、前後の空白・引用符を取り除いてから開きます
- シェル実行はバックグラウンドで行います。実行ファイルは、そのファイルのフォルダーを作業フォルダーにして起動します
- `Classify` はファイルの有無を調べるので、ネットワーク上のパスでは時間がかかることがあります（UI スレッドから呼ばない）

### 通知ダイアログ（`INotificationDialogService`）

UI スレッドから呼びます。ウィンドウはアプリ内で常に 1 枚で、新しい通知が来ると内容を差し替えて再表示します。

```csharp
notifications.Show("リマインダー",
[
    new NotificationItem("定例ミーティング", "https://example.com/meeting"),
    new NotificationItem("日報を書く"),
],
onClicked: () => { /* 本文（リンク以外）をクリックして閉じたときだけ呼ばれる */ });

notifications.Show("お知らせ", "メッセージだけの簡易通知");
```

- フォーカスを奪わず、常に最前面に表示します（Alt+Tab には出ません）
- ドラッグで移動、クリックで閉じます。本文のリンクをクリックすると `PathOpener` で開きます
- 表示位置は `positionKey`（既定は `"Notification"`）ごとに保存・復元します。保存位置が画面外になっていたら、プライマリモニターの作業領域の右下に出します
- 表示されたときに、ウィンドウ全体を数回点滅させて知らせます

### 確認ダイアログ・ファイル/フォルダー選択・擬似モーダル（`MmmSdk.WinUI.Components.Dialogs`）

UI スレッドから呼びます。ダイアログの親は `IDialogHost`（実装は `DialogService`）が決めます。アプリは具象型ではなく、確認ダイアログは `IDialogService`、親の決定は `IDialogHost` に依存します（いちばん手前のモーダルウィンドウ → 最後に操作したウィンドウ → 最初に登録したウィンドウ）。

```csharp
// 起動時に、親の候補にするウィンドウを登録する（最初に登録したウィンドウが、最後の手段の親になる）
dialogs.TrackWindow(mainWindow);

if (await dialogs.ConfirmAsync("削除", "削除しますか？", "削除", "キャンセル")) { /* 「削除」が押された */ }  // 既定のボタンはキャンセル

var file = await filePicker.PickFileAsync();       // キャンセルなら null
var folder = await folderPicker.PickFolderAsync(); // キャンセルなら null
```

アプリ固有のウィンドウを擬似モーダルで開くときは、ウィンドウに `PseudoModal` を付け、`IDialogHost.ShowModalAsync` で開きます（親は `Owner`）。

```csharp
var modal = new PseudoModal(window);                       // ウィンドウのコンストラクターで作る
await dialogs.ShowModalAsync(window, owner =>
{
    modal.SetOwner(owner);                                 // 表示の前に親を設定
    modal.Show();                                          // 親を操作できなくして表示
    // …閉じるまで待つ。コードから閉じるときは modal.Close()…
    return Task.FromResult(true);
});
```

ウィンドウの前面表示・DPI 倍率・タイトルバー・大きさ・位置合わせは、`Window` の拡張メソッド（`MmmSdk.WinUI.Utilities`）です。

```csharp
window.Activate();
window.SetForeground();            // 前面に出す
var scale = window.GetDpiScale();  // 100% で 1.0。表示の前でも取れる

window.UseCustomTitleBar(titleBarArea, iconPath);                        // アイコン + タイトルバーを自分で描く
window.UseFixedPresenter(isDialog: true, isResizable: false);            // 最大化・最小化できないウィンドウ
window.ResizeClientDip(360, 440, scale);                                 // 論理サイズ（DIP）と倍率で大きさを決める
window.MoveCentered(new RectInt32(x, y, width, height));                 // 範囲の中央に置く（作業領域に収める）
```

### 標準のメッセージボックス（`NativeMessageBox`）

WinUI のウィンドウやアプリの初期化より前でも出せます。多重起動の案内など、画面を作る前に知らせたいときに使います（閉じられるまで待ちます）。

```csharp
NativeMessageBox.ShowInformation("MyApp はすでに起動しています。", "MyApp");
NativeMessageBox.ShowError("起動できませんでした。", "MyApp");
```

### エラーのログと、復旧できないエラーの処理（`ErrorLog` / `FatalErrorHandler`）

エラーの扱いは 3 段階です。

1. **予測できる失敗**（ファイル・JSON・OS・COM など）は、範囲を絞った `catch` で受けて、画面に出す（`ErrorState` の InfoBar など）。使い続けられる
2. **復旧が難しい失敗・予想外の失敗（バグ）**は、`FatalErrorHandler.Report` で、ログ → 後始末（トレイのアイコンを外すなど）→ ダイアログ → 終了
3. **`try/catch` を書けない場所**（`async void`・タイマー・待たれないタスク）は、`AttachTo` が付ける安全網が、2 と同じ処理を行う

```csharp
// アプリの最初（Host を作る前）に作って付ける。DI にも登録して、トレイなどの SDK の部品に渡す
var fatalErrors = new FatalErrorHandler(new ErrorLog(Path.Combine(AppContext.BaseDirectory, "Logs")), "MyApp");
fatalErrors.AttachTo(this);                       // this は Application
services.AddSingleton(fatalErrors);

// 復旧できない失敗を自分で見つけたとき
catch (Exception ex)
{
    fatalErrors.Report("起動に失敗しました", ex);   // 戻らない
}
```

- `ErrorLog`（`MmmSdk.Core.Components.Logging`）は `yyyy-MM-dd.log` に、時刻・場所・バージョン・OS・例外（内部例外とスタックトレース）を追記します。落ちる直前に呼ばれるので、同期で書いて閉じてから戻ります。書けなかったとき（権限・ディスク）は、例外にせず `null` を返します
- ダイアログは WinUI ではなく標準のメッセージボックス（`NativeMessageBox`）なので、XAML が壊れていても出ます
- `AttachTo` は、UI スレッドの未処理例外（`Application.UnhandledException`）・どのスレッドの未処理例外（`AppDomain`）・待たれないタスクの例外（`TaskScheduler.UnobservedTaskException`）を集めます。取り消し（`OperationCanceledException`）だけのタスクは報告しません
- 待たずに走らせるタスクは、`_ = SomeAsync();` で捨てずに `SomeAsync().Forget()`（`MmmSdk.Core.Utilities`）にします。捨てると、失敗が誰にも見えず、ガベージコレクションのときに初めて分かります。`Forget` は、失敗した時点で未処理例外にして、上の安全網が受けます。起きると分かっている失敗は、タスクの中で受けて画面に出してください
- `BeforeExit` イベントで、終了の直前の後始末ができます（`TrayIcon` はこれでトレイのアイコンを外します）。呼ばれるスレッドは決まっていません
- 複数のスレッドから同時に報告されたときは、最初の 1 つだけが報告し、ほかは終了を待ちます
- `ErrorLog` で書けないこと（スタックオーバーフロー・ネイティブ側の破損）は、.NET のハンドラー自体が動かないため、残せません

### 画面に出すエラー（`ErrorState`）

ViewModel が `ErrorState` を 1 つ持ち、`InfoBar` に結び付けます。閉じるボタンは TwoWay の結び付けで `IsOpen` を false にするので、閉じる処理は書かなくてよい。

```csharp
public ErrorState Error { get; } = new();

Error.Show("保存できませんでした");     // 表示する
Error.Set(loadError ?? recoveryMessage); // あれば表示、null なら消す
Error.Clear();                           // 消す
```

```xml
<InfoBar IsOpen="{x:Bind ViewModel.Error.IsOpen, Mode=TwoWay}"
         Message="{x:Bind ViewModel.Error.Message, Mode=OneWay}" Severity="Error" />
```

### 多重起動の防止（`SingleInstanceGuard`）

アプリを区別する名前を渡します。EXE のパスごとに判定するので、別のフォルダーの EXE（Debug / Release など）は同時に起動できます。

```csharp
var guard = new SingleInstanceGuard("MyApp");   // アプリ起動の最初に作って、フィールドで持ち続ける
if (!guard.IsFirstInstance) { /* すでに起動している。メッセージを出して終了する */ }
```

### 毎分 00 秒の処理（`MinuteScheduler`）

開始直後に 1 回、以後はシステム時刻の毎分 00 秒に、処理を呼びます。固定間隔ではなく、次の 00 秒までの残り時間をその都度計算する単発タイマーを掛け直すので、時計のずれに追従します。タイマーが 00 秒より少し早く来ても、同じ分に 2 回は呼びません。

```csharp
var scheduler = new MinuteScheduler(TimeProvider.System);
scheduler.Start(async now => { /* 毎分 00 秒の処理。now は現在の日時 */ });   // タイマーのスレッドで動く
var minute = MinuteScheduler.TruncateToMinute(now);                           // 秒以下を切り捨てる
scheduler.Dispose();                                                          // 止める
```

### ターミナル・シェル（`MmmSdk.WinUI.Components.Terminal` / `MmmSdk.Core.Components.Shells`）

シェルを動かして画面に出す部品です。起動するシェルは `ShellInfo(Path, Kind)` で表し、既定は `ShellLocator.Default`（PATH 上の `pwsh.exe`、無ければ Windows PowerShell）です。

```csharp
services.AddTransient<ITerminalSession, PseudoConsoleSession>();   // 利用側ごとに 1 つ。Host の破棄時にシェルも終了する
```

```xml
<!-- xmlns:terminal="using:MmmSdk.WinUI.Components.Terminal" -->
<terminal:TerminalControl Session="{x:Bind ViewModel.Terminal}" />
```

```csharp
session.WorkingDirectory = directory;                       // Start の前に設定する
session.Shell = new ShellInfo(path, ShellKind.PowerShell);  // 既定以外のシェルを使うとき
if (ShellCommands.TryChangeDirectory(session.Shell, directory, out var command))
{
    session.Submit(command);                                // 貼り付けとして入力し、Enter で確定する
}
```

- xterm.js のファイルは、参照するアプリの出力フォルダー（`Assets/Terminal/`）へ自動でコピーされます
- 画面側の WebView2 ランタイムが無いときは、ターミナルの場所に理由が文字で出ます（ほかの機能は使えます）
- 既定のシェルの探索は、最初に読むときにディスクへ触れます。UI スレッドで初めて読まないよう、起動時の準備で `_ = ShellLocator.Default` をバックグラウンドから読んでください

### ConPTY（`MmmSdk.WinUI.Components.Terminal`）

`PseudoConsole.Start` で、擬似コンソールにつないだプロセス（シェルなど）を起動します。端末の描画やキー入力の解釈は持ちません（出力は端末のエスケープシーケンスを含んだ UTF-8 のバイト列のままです）。

```csharp
using var console = PseudoConsole.Start("pwsh.exe", workingDirectory, columns: 120, rows: 30);

console.Input.Write(Encoding.UTF8.GetBytes("dir\r"));      // プロセスへの入力
var read = console.Output.Read(buffer);                    // プロセスからの出力（別のタスクで読み続ける）
console.Resize(160, 40);                                   // 端末の大きさを変える
ThreadPool.RegisterWaitForSingleObject(console.ExitHandle, (_, _) => { /* プロセスが終了した */ }, null, Timeout.Infinite, true);
```

使い終わるときは、出力を読み続けたまま `Close()`（プロセスがまだ動いていれば終了する）→ 読み取りが終わるのを待つ → `Dispose()` の順です。起動に失敗したときは、作った分を片付けてから `Win32Exception`（または `COMException`）を投げます。

### タスクトレイ（`MmmSdk.WinUI.Components.Tray`）

トレイを使うアプリだけが登録します（`AddMmmSdkWinUI` には含まれません）。`TrayIcon` は UI スレッドで解決します。

```csharp
services.AddMmmSdkTray(new TrayIconOptions(
    ToolTip: "MyApp",                  // ツールチップ。エラー通知のタイトルにも使う
    WindowClassName: "MyApp_Tray",     // 通知を受ける非表示ウィンドウのクラス名（アプリごとに別の名前）
    ExitText: "終了",                  // メニュー末尾の終了項目（アプリの言語で渡す）
    IconPath: Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")));   // 省略すると標準のアイコン

// 機能ごとにメニューの項目を足す（Singleton で登録した順に並ぶ）
services.AddSingleton<ITrayMenuSource, MyTrayMenuSource>();
```

```csharp
public sealed class MyTrayMenuSource : ITrayMenuSource
{
    // メニューを開くたびに呼ばれる
    public IReadOnlyList<TrayMenuItem> GetItems() =>
    [
        TrayMenuItem.Command("開く", () => OpenAsync()),
        TrayMenuItem.Submenu("リンク", [TrayMenuItem.Command("例", () => OpenAsync()), TrayMenuItem.Separator]),
        TrayMenuItem.Disabled("説明（押せない）"),
    ];
}
```

```csharp
var tray = provider.GetRequiredService<TrayIcon>();
tray.OpenRequested += (_, _) => ShowMainWindow();    // アイコンの左クリック
tray.ExitRequested += (_, _) => ExitApp();           // メニューの終了
tray.Show();                                         // アイコンを出す
tray.ShowNotification("タイトル", "本文", isError: false);   // バルーン通知
```

- 項目の処理の予測できる失敗は、処理の中で受けて、その機能のやり方で知らせてください（受けなかった例外は、バグとして、上の安全網がログ → ダイアログ → 終了で受けます）
- 終了時は `TrayIcon` を `Dispose` する（DI の破棄で行われる）。各機能の後始末のあとに消したいときは、機能より先に解決しておく（DI は作った順の逆に破棄する）

### コントロール・IME（`MmmSdk.WinUI.Controls` / `Utilities`）

```xml
<!-- xmlns:controls="using:MmmSdk.WinUI.Controls" -->
<controls:TimeInputBox Hour="{x:Bind ViewModel.Hour, Mode=TwoWay}" Minute="{x:Bind ViewModel.Minute, Mode=TwoWay}" />

<controls:LinkArea IsLinkEnabled="{x:Bind HasLink}" Tapped="OnLinkTapped">
    <TextBlock Text="件名" />
</controls:LinkArea>
```

```csharp
ImeControl.TurnOn();    // 日本語を打つ欄にフォーカスが来たとき
ImeControl.TurnOff();   // 英数字を打つ欄にフォーカスが来たとき
```

### 添付ファイルの一時保存（`AttachmentStore`）

アプリごとの名前を渡して登録します（`AddMmmSdkCore` には含まれません）。

```csharp
services.AddSingleton(provider => new AttachmentStore("MyApp", provider.GetRequiredService<TimeProvider>()));

var path = await store.AddFileAsync(sourcePath);          // コピーして添付（連番付き）
var path2 = await store.AddAsync(bytes, "image.jpg");     // データをファイルとして添付
store.Remove(path);                                       // 取り除く（今のセッションの添付だけ。空ならフォルダごと削除）
store.CloseSession();                                     // 送信済み。次の添付は新しいセッションへ
```

### ビジュアルツリーの検索（`VisualTreeSearch`）

```csharp
var scrollViewer = VisualTreeSearch.FindDescendant<ScrollViewer>(treeView);              // 型で探す
var delete = VisualTreeSearch.FindDescendant<Button>(numberBox, "DeleteButton");         // 型と名前で探す
```

## ドキュメント

| ファイル | 内容 |
| --- | --- |
| [docs/storage.md](docs/storage.md) | JSON の読み書き・シリアライザの設定・壊れたファイルの扱い・汎用設定ストア |
| [docs/notification-dialog.md](docs/notification-dialog.md) | 通知ダイアログの見た目と挙動・ウィンドウ位置の保存・パスを開く処理 |
| [docs/dialogs.md](docs/dialogs.md) | 確認ダイアログ・ファイル/フォルダー選択・擬似モーダル・多重起動の防止 |
| [docs/tray.md](docs/tray.md) | タスクトレイのアイコン・メニューの仕組み |
| [docs/conpty.md](docs/conpty.md) | ConPTY（`PseudoConsole`）の仕組みと後始末の順序 |
| [docs/terminal.md](docs/terminal.md) | シェルの決定・ターミナルのセッションと画面（xterm.js） |
| [docs/controls.md](docs/controls.md) | `TimeInputBox`・`LinkArea`・IME・添付の一時保存・添付の画像 |

## バージョン

現在のバージョンは 0.1.0 です（`Directory.Build.props` の `Version`。ファイル・アセンブリのバージョンは 0.1.0.0 になります）。アプリとは別に上げます。

## ビルド

```powershell
dotnet build .\MmmSdk.slnx
```

ビルドの共通設定はリポジトリ直下にまとめています。アプリにサブモジュールとして取り込んだときも、SDK のプロジェクトはこちらの設定を使います（アプリ側の設定は混ざりません）。

| ファイル | 内容 |
| --- | --- |
| `Directory.Build.props` | 全プロジェクト共通の設定（Nullable・XML ドキュメントコメントの検査・コードスタイルのビルド時検査など） |
| `Directory.Packages.props` | NuGet パッケージのバージョン（中央パッケージ管理。csproj にはバージョンを書きません） |
| `.editorconfig` | コードスタイル。未使用の using などはビルド時に警告になります |

アプリと共通のパッケージ（Windows App SDK など）のバージョンを上げるときは、SDK を先に上げてから、アプリ側を同じバージョンにします。

Win32 API の呼び出しは [CsWin32](https://github.com/microsoft/CsWin32)（`Microsoft.Windows.CsWin32`）が生成します（ビルド時だけ使い、配布物には入りません）。使う API は `MmmSdk.WinUI/NativeMethods.txt` に書きます。

SDK 単体では実行できません。動作はアプリに組み込んで確認します。

## 更新の手順（アプリ側から）

サブモジュールの中で作業するときは、先に `master` ブランチに切り替えます（detached HEAD のまま作業しない）。SDK の変更を先にコミット・push し、そのあとアプリ側で「新しいコミットを指す」変更をコミットします。

```powershell
cd external/MmmSdk
git switch master
# …変更をコミットして push…
cd ../..
git add external/MmmSdk
git commit
```

## ライセンス

このプロジェクトは [MIT License](./LICENSE.txt) のもとで公開されています。

同梱しているサードパーティのライセンス:

- xterm.js（MIT License）… [`MmmSdk.WinUI/Components/Terminal/Assets/xterm.LICENSE.txt`](./MmmSdk.WinUI/Components/Terminal/Assets/xterm.LICENSE.txt)
- @xterm/addon-fit（MIT License）… [`MmmSdk.WinUI/Components/Terminal/Assets/addon-fit.LICENSE.txt`](./MmmSdk.WinUI/Components/Terminal/Assets/addon-fit.LICENSE.txt)
