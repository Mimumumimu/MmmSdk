# MmmSdk

複数のアプリ（MmmTool など）で共有する汎用部品。個人利用が前提だが、将来公開する可能性がある。アプリ固有のもの（Entities・業務ロジック・画面）は置かない。アプリ側が SDK を参照し、SDK はアプリを知らない。

- 各チャットの最初の応答の前に、`/winui3-mvvm` スキルを Skill ツールで読み込み、そのルールに従う（コメントの書き方など）。
- 構成: `MmmSdk.Core`（`net10.0`。Windows / WinUI を参照しない）と `MmmSdk.WinUI`（WinUI 3 のクラスライブラリ。Core を参照）
- プロジェクトの中は部品ごとのフォルダ（アプリの機能別フォルダと同じ考え方。層ごとの `Views/` `Services/` 等は作らない）。名前空間はフォルダどおり：`MmmSdk.Core.Storage`（JsonFileStore・ReadableJsonOptions・DataLoadResult・DataFileException）/ `.Settings`（ISettingsStore。JSON 実装と `SettingsJsonContext` は `.Settings.Json`）/ `.WindowPositions`（WindowPositionService・WindowPosition・`WindowPositionJsonContext`・ScreenGeometry）/ `.Paths`（PathOpener・PathOpenException・PathTarget）/ `.Notifications`（NotificationItem）/ `.SingleInstance`（SingleInstanceGuard）、`MmmSdk.WinUI.Notifications`（通知ダイアログの Window・ViewModel・サービス）/ `.Dialogs`（DialogService・ファイル/フォルダー選択・PseudoModal）/ `.VisualTree`（VisualTreeSearch）。DI 登録はプロジェクト直下（`MmmSdk.Core` / `MmmSdk.WinUI`）
- DI は拡張メソッドで登録する：`AddMmmSdkCore(dataDirectory)`（JsonFileStore・ISettingsStore・WindowPositionService・PathOpener）→ `AddMmmSdkWinUI()`（通知ダイアログ・DialogService・ファイル/フォルダー選択。Core を先に登録すること）
- ビルドの共通設定はリポジトリ直下の `Directory.Build.props`（バージョン・Nullable・ImplicitUsings・XML コメントの検査・コードスタイルのビルド時検査）/ `Directory.Packages.props`（中央パッケージ管理。csproj にバージョンを書かない）/ `.editorconfig`（`root = true`）。アプリに取り込まれても MSBuild は近いこちらを使うので、アプリの設定とは混ざらない。アプリと共通のパッケージは SDK を先に上げる
- バージョンは `Directory.Build.props` の `Version`（現在 0.1.0。ファイル・アセンブリのバージョンは自動で 0.1.0.0）。アプリとは別に上げる
- Win32 の P/Invoke は各プロジェクトの `Interop/NativeMethods.cs`（internal）に置く。アプリの NativeMethods とは別
- JSON のソース生成の Context は部品ごと（`SettingsJsonContext` / `WindowPositionJsonContext`。どちらも internal）。アプリ固有の型の登録はアプリ側の Context で行い、`ISettingsStore` / `JsonFileStore` には `JsonTypeInfo<T>` を渡す。シリアライザの設定は `ReadableJsonOptions.Create()`（public）に 1 か所だけ書き、SDK とアプリの Context がそれを使う（設定は Context に結び付くので Context ごとに作る）
- アプリ側の取り込み方: アプリ側は Git サブモジュール（`external/MmmSdk`）として取り込み、プロジェクト参照でつなぐ。SDK の変更は、サブモジュールの中でコミット・push（先に）→ アプリ側で参照先の更新をコミット。サブモジュールの中で作業するときは、先に master ブランチに切り替える（detached HEAD にしない）
- コミット・push は、ユーザーが明示的に指示するまで行わない

## ドキュメント
- `README.md`: 利用者向け（機能・組み込み方・使い方）
- `docs/storage.md`: JSON の読み書き・シリアライザの設定・壊れたファイルの扱い・汎用設定ストア
- `docs/notification-dialog.md`: 通知ダイアログの見た目と挙動・ウィンドウ位置の保存・パスを開く処理
- `docs/dialogs.md`: 確認ダイアログ・ファイル/フォルダー選択・擬似モーダル・多重起動の防止
- 機能の区切りで、変更した部分の README・docs と、下の「未実装・残りの作業」を更新してから終える

## 未実装・残りの作業
実装済みの部品は `docs/` と `README.md` を見る。ここには、これからやることだけを書く（実装したら消し、説明は docs に移す）。

- 今のところなし（アプリ側で必要になった汎用の部品が出たら、ここに足す）
