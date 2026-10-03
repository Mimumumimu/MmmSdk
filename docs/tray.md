# タスクトレイ

アプリから移した汎用部品（`MmmSdk.WinUI.Tray`）。アプリ名・文言・アイコンは `TrayIconOptions` で受け取り、アプリ固有の知識は持たない。

## TrayIcon
- `Shell_NotifyIcon` を直接 P/Invoke する（`NOTIFYICON_VERSION_4`）。WinForms には依存しない
- 通知を受ける専用の非表示トップレベルウィンドウを UI スレッドで作る（`Show`）。メッセージ専用ウィンドウにしないのは、`TaskbarCreated`（エクスプローラーの再起動でアイコンを付け直す）がブロードキャストで、メッセージ専用ウィンドウには届かないため
- ウィンドウクラス名は `TrayIconOptions.WindowClassName`。登録はプロセスごとなので、別の EXE と重なってもよい。アプリごとに別の名前にする
- 左クリック（`NIN_SELECT`・`NIN_KEYSELECT`）で `OpenRequested`、右クリック（`WM_CONTEXTMENU`）で Win32 のポップアップメニュー。通知の中で画面を操作しないよう、`OpenRequested` は処理が戻ってから UI スレッドで呼ぶ
- アイコンは `TrayIconOptions.IconPath` から、DPI に合わせた小アイコンで読む。未指定・読めないときは Windows 標準のアプリアイコン（トレイから操作できなくなるのを避ける）
- `ShowNotification`: バルーン通知（情報 / エラー）。タイトルは 64 文字、本文は 256 文字で切り捨てる
- UI スレッドで作り、UI スレッドで破棄する。トレイはアプリに 1 つ（ウィンドウプロシージャが static のため）

## メニュー
- 項目は `ITrayMenuSource` を DI に登録した順に、区切り線で分けて並べ、末尾に `TrayIconOptions.ExitText`（終了）。メニューは開くたびに作る（`GetItems` を呼ぶ）
- `TrayMenuItem`: `Command`（クリックで処理）/ `Submenu` / `Disabled`（押せない）/ `Separator`。項目の処理の失敗は、トレイの通知（エラー）で知らせる
- 項目はオーナードロー（`TrayMenuRenderer`。internal）
  - フォントは BIZ UDゴシック 12pt（無ければ Yu Gothic UI → Segoe UI。有無は `EnumFontFamiliesEx` で調べる）。メニューを出すモニターの DPI に合わせる
  - 配色はレジストリの `AppsUseLightTheme` でダーク / ライトを切り替える（Windows 11 風の色）
  - チェック欄の余白は `MNS_NOCHECK` で無くし、代わりに文字の左を 36px あける。区切り線は文字の書き出し位置から右端まで
  - 行は詰め気味（上下 4px・区切り線の行 7px）
  - サブメニューの矢印は Segoe Fluent Icons（無ければ MDL2）で自分で描き、`ExcludeClipRect` で標準の矢印を止める
  - 枠（外周・影）は Windows が描くので、uxtheme の非公開序数 135 / 136 でシステムのダーク設定に従わせている（無い環境ではライトのまま）
