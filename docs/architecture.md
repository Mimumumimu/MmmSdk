# 構成と決定の理由

SDK のプロジェクト・フォルダ・Win32 呼び出しの決め方と、その理由。置き場所の基準の一覧は `CLAUDE.md`。

## フォルダを「Components / Controls / Utilities」の層に分ける
- 以前は「1 部品 = 1 フォルダ」で、プロジェクトの直下にフォルダが Core 13 個・WinUI 14 個並び、中身が 1〜2 ファイルのものが多かった。画面を持つ部品 (通知・トレイ・ダイアログ・ターミナル)と、小さな道具 (`FireAndForget`・`Debouncer`・`VisualTreeSearch`・`ImeControl`)が同じ段に並び、どれが何なのか見分けにくかった。同じテーマが 2 つのフォルダに分かれているものもあった (WinUI の `ConPty/` と `Terminal/`)
- プロジェクトの直下に「Components / Controls / Utilities」の層を 1 段足す。名前空間はフォルダどおり。型の名前も動きも変えない。層で分ければ、「何をする部品か」と「道具」を別の段に置け、層と部品名の 2 段で探せる。部品が増えても、直下の見分けやすさが保たれる
- どの層に入れるかの基準
  - **Components**(部品): 何をするための部品かを名前で言えるもの (保存・設定・通知・トレイ・ターミナル など)。部品ごとにサブフォルダを作り、その部品の型 (サービス・画面・データ・その部品専用の static や拡張メソッド・その部品の中で使うコントロール)を一式まとめて置く
  - **Controls**(画面部品): どの部品にも属さない、XAML に置いて使う汎用のコントロール
  - **Utilities**(道具): どの部品にも属さない、汎用の小さな道具 (拡張メソッド・小さなヘルパー。static に限らない)。サブフォルダは作らず平置き
  - 迷ったら「その型は、ある部品の一部か」で決める。一部なら、その部品のフォルダに置く (例: `ReadableJsonOptions` は Storage、`ScreenGeometry` は WindowPositions、`SettingsStoreExtensions` は Settings、`TerminalControl` は Terminal、`ThumbnailImage` は Attachments)
  - `Interop/` は土台なので層の外。DI 登録の `Sdk*ServiceCollectionExtensions.cs` もプロジェクト直下。Core と WinUI で同じテーマのフォルダは同じ名前にする (`Attachments`・`Notifications`)
- `ConPty/` と `Terminal/` は、1 つ (`Components/Terminal/`)にまとめた。ターミナルの資材は `Components/Terminal/Assets/` で、出力先 (`Assets/Terminal`)は csproj の `Link` で決まっていて、変えない

### プロジェクトの分け方
プロジェクトは、次の 2 つの基準でだけ分ける。見やすさのために分けることはしない (見やすさは、層とフォルダで足りる)。

- **UI の有無**: `MmmSdk.Core`(Windows / WinUI に依存しない)と `MmmSdk.WinUI`(WinUI 3)
- **依存先の違い**: 外部のドライバーなどを参照する部品は、`MmmSdk.Db.<製品名>` のように独立したプロジェクトにする (今は `MmmSdk.Db.SqlServer`。[db-sqlserver.md](db-sqlserver.md))。理由は 2 つ。`MmmSdk.Core` はトリミング・AOT 対応を宣言しているので、リフレクションを使うドライバーを混ぜると宣言が合わなくなる。ドライバーを使わないアプリには、その DLL が入らない

`MmmSdk.WinUI` を、さらに複数のプロジェクトに分けない。CsWin32 が作る型は internal なので、WinUI 側を複数のプロジェクトに分けると、各プロジェクトに CsWin32 を持たせる (宣言が散らばる)か、Win32 の型を公開する Interop プロジェクトを作るしかなく、下の「Win32 の P/Invoke は CsWin32 で生成する」(Win32 の宣言は SDK の 1 か所)を崩す。分ける基準の「依存先の違い」「別パッケージでの配布」にも当たらない。

### アプリ側に同じ層を作らない理由
アプリは「機能」で分け、SDK は「部品」で分けていて、分ける軸がもともと違う。

## Win32 の P/Invoke は CsWin32 で生成する
- 以前は、Win32 の宣言 (ウィンドウ・トレイ・メニュー・GDI・IME など)を `LibraryImport` で 1 つずつ手書きしていた。構造体・定数・フラグも手書きで、型は `nint` や `uint` のため、ハンドルの取り違えや、定数の値の写し間違いをコンパイラが止められなかった
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
- 移行は 4 段階で行った (すべて済み。各段階で Debug / Release のビルドと publish を警告 0 で通した): 単純な宣言 → ウィンドウ・トレイ・メニュー → GDI → アプリ側の宣言を SDK へ
