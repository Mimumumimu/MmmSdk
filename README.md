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
- タスクトレイのアイコンと右クリックメニュー（Win32 を直接使う。メニューは自前描画でダーク/ライト対応）
- 時刻の入力欄（`TimeInputBox`）・押せる領域（`LinkArea`）・IME のオン/オフ、添付ファイルの一時保存先の管理
- ビジュアルツリーから要素を探す処理（コントロールのテンプレート内の要素に触るため）

## 構成

| プロジェクト | 対象 |
| --- | --- |
| `MmmSdk.Core` | `net10.0`（Windows / WinUI に依存しない） |
| `MmmSdk.WinUI` | `net10.0-windows10.0.19041.0`（WinUI 3） |

プロジェクトの中は、部品ごとのフォルダーに分けています。名前空間はフォルダーと同じです。

| 名前空間 | 内容 |
| --- | --- |
| `MmmSdk.Core.Storage` | JSON ファイルの読み書き（`JsonFileStore`）・共通の書式（`ReadableJsonOptions`）・読み込み結果（`DataLoadResult`）・読み書きの失敗（`DataFileException`） |
| `MmmSdk.Core.Settings` | 汎用設定ストア（`ISettingsStore`）。JSON での実装は `MmmSdk.Core.Settings.Json`（`JsonSettingsStore`） |
| `MmmSdk.Core.WindowPositions` | ウィンドウ位置の保存・復元（`WindowPositionService` / `WindowPosition`）と、画面との位置関係の計算（`ScreenGeometry`） |
| `MmmSdk.Core.Paths` | URL・ファイル・フォルダーを開く処理（`PathOpener` / `PathOpenException`）と種類の判定（`PathTarget`） |
| `MmmSdk.Core.Notifications` | 通知の項目（`NotificationItem`） |
| `MmmSdk.Core.Attachments` | 添付ファイルの一時保存先（`AttachmentStore`） |
| `MmmSdk.Core.SingleInstance` | 多重起動の防止（`SingleInstanceGuard`） |
| `MmmSdk.WinUI.Notifications` | 通知ダイアログ（`INotificationDialogService` / `NotificationDialogService`・`NotificationWindow`・`NotificationDialogViewModel`） |
| `MmmSdk.WinUI.Dialogs` | 確認ダイアログ（`IDialogService`）・親の決定（`IDialogHost`。実装は `DialogService`）・ファイル/フォルダー選択（`IFilePickerService` / `IFolderPickerService`）・擬似モーダル（`PseudoModal`） |
| `MmmSdk.WinUI.Controls` | `TimeInputBox`（時刻の入力欄）・`LinkArea`（押せる領域） |
| `MmmSdk.WinUI.Input` | IME のオン/オフ（`ImeControl`） |
| `MmmSdk.WinUI.Tray` | タスクトレイ（`TrayIcon`・`TrayIconOptions`・`ITrayMenuSource`・`TrayMenuItem`）。DI 登録は `AddMmmSdkTray` |
| `MmmSdk.WinUI.Windowing` | Window の拡張メソッド（前面に出す `SetForeground`・DPI 倍率 `GetDpiScale`） |
| `MmmSdk.WinUI.VisualTree` | ビジュアルツリーの検索（`VisualTreeSearch`） |
| `MmmSdk.Core` / `MmmSdk.WinUI` | DI への登録（`AddMmmSdkCore` / `AddMmmSdkWinUI`） |

## 技術スタック

- .NET 10
- WinUI 3（Windows App SDK 2.5）… `MmmSdk.WinUI` のみ
- CommunityToolkit.Mvvm … `MmmSdk.WinUI` のみ
- Microsoft.Extensions.DependencyInjection.Abstractions（DI 登録用の拡張メソッド）
- System.Text.Json（ソース生成。トリミングしても動く形）

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
| `AddMmmSdkCore(dataDirectory)` | `JsonFileStore`・`ISettingsStore`・`WindowPositionService`・`PathOpener`（すべて Singleton） |
| `AddMmmSdkTray(options)` | `TrayIconOptions`・`TrayIcon`（Singleton）。トレイを使うアプリだけが呼ぶ |
| `AddMmmSdkWinUI()` | `INotificationDialogService`・`DialogService`（`IDialogService` と `IDialogHost` が同じインスタンスを返す）・`IFilePickerService`・`IFolderPickerService`（すべて Singleton）、`NotificationWindow`・`NotificationDialogViewModel`（Transient） |

## 使い方

### JSON ファイルの読み書き（`JsonFileStore`）

アプリ固有のデータも、DI から受け取った `JsonFileStore` で同じフォルダーに保存できます。型の情報は、アプリ側で `JsonSerializable` 登録した `JsonTypeInfo<T>` を渡します（ソース生成）。

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
- 読み込み・書き込みはファイル操作ごとにロックを取るので、別スレッドから同時に呼べます

### 汎用設定ストア（`ISettingsStore`）

アプリ共通の小さな設定を `Data/AppSettings.json` の 1 ファイルにまとめて保存します。キーには機能ごとの接頭辞を付けて衝突を避けます。

```csharp
var minutes = settings.Get("Reminder.SnoozeIntervalMinutes", 15);         // 無い・型が合わないときは既定値
await settings.SetAsync("Reminder.SnoozeIntervalMinutes", 30);            // 保存し終えるまで待つ

// string / bool / int / long / double 以外は JsonTypeInfo を渡す
var options = settings.Get("Foo.Options", new FooOptions(), MyJsonContext.Default.FooOptions);
```

- `Get` は同期です。最初のアクセスで 1 度だけファイルを読み、以後はメモリから返します
- ファイルが無い・空・壊れているときは空の設定として扱い、例外は出しません。壊れていたときは退避して `RecoveryMessage` に、読めなかったときは `LoadError` に理由が残ります
- ほかに `Contains(key)` / `RemoveAsync(key)` があります

### ウィンドウ位置の保存（`WindowPositionService`）

```csharp
var position = positions.Load("Notification");                    // 保存が無ければ null
await positions.SaveAsync("Notification", new WindowPosition(x, y));

// 全モニターの作業領域に対して、ウィンドウが面積の 50% 以上見えているか
var visible = ScreenGeometry.IsVisibleEnough(windowRect, workAreas, 0.5);
```

位置は設定ストアに `WindowPosition.<キー>` として保存されます。

### パスを開く（`PathOpener` / `PathTarget`）

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

### 確認ダイアログ・ファイル/フォルダー選択・擬似モーダル（`MmmSdk.WinUI.Dialogs`）

UI スレッドから呼びます。ダイアログの親は `IDialogHost`（実装は `DialogService`）が決めます。アプリは具象型ではなく、確認ダイアログは `IDialogService`、親の決定は `IDialogHost` に依存します（いちばん手前のモーダルウィンドウ → 最後に操作したウィンドウ → 最初に登録したウィンドウ）。

```csharp
// 起動時に、親の候補にするウィンドウを登録する（最初に登録したウィンドウが、最後の手段の親になる）
dialogs.TrackWindow(mainWindow);

if (await dialogs.ConfirmAsync("削除", "削除しますか？", "削除")) { /* 「削除」が押された */ }  // 既定のボタンはキャンセル

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

ウィンドウを前面に出す・DPI 倍率を取る処理は、`Window` の拡張メソッド（`MmmSdk.WinUI.Windowing`）です。

```csharp
window.Activate();
window.SetForeground();            // 前面に出す
var scale = window.GetDpiScale();  // 100% で 1.0。表示の前でも取れる
```

### 多重起動の防止（`SingleInstanceGuard`）

アプリを区別する名前を渡します。EXE のパスごとに判定するので、別のフォルダーの EXE（Debug / Release など）は同時に起動できます。

```csharp
var guard = new SingleInstanceGuard("MyApp");   // アプリ起動の最初に作って、フィールドで持ち続ける
if (!guard.IsFirstInstance) { /* すでに起動している。メッセージを出して終了する */ }
```

### タスクトレイ（`MmmSdk.WinUI.Tray`）

トレイを使うアプリだけが登録します（`AddMmmSdkWinUI` には含まれません）。`TrayIcon` は UI スレッドで解決します。

```csharp
services.AddMmmSdkTray(new TrayIconOptions(
    ToolTip: "MyApp",                  // ツールチップ。エラー通知のタイトルにも使う
    WindowClassName: "MyApp_Tray",     // 通知を受ける非表示ウィンドウのクラス名（アプリごとに別の名前）
    IconPath: Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"),   // 省略すると標準のアイコン
    ExitText: "終了"));                // メニュー末尾の終了項目（既定は「終了」）

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

- 項目の処理が例外を投げたら、トレイの通知（エラー）でメッセージを知らせます
- 終了時は `TrayIcon` を `Dispose` する（DI の破棄で行われる）。各機能の後始末のあとに消したいときは、機能より先に解決しておく（DI は作った順の逆に破棄する）

### コントロール・IME（`MmmSdk.WinUI.Controls` / `Input`）

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
store.Remove(path);                                       // 取り除く（空ならフォルダごと削除）
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
| [docs/controls.md](docs/controls.md) | `TimeInputBox`・`LinkArea`・IME・添付の一時保存 |

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
