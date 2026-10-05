namespace MmmSdk.WinUI.Components.Tray;

/// <summary>トレイアイコンの、アプリごとの設定</summary>
/// <param name="ToolTip">アイコンにマウスを載せたときの文字。エラー通知のタイトルにも使う</param>
/// <param name="WindowClassName">通知を受ける非表示ウィンドウのクラス名 (アプリごとに別の名前にする)</param>
/// <param name="ExitText">メニューの末尾に出す終了項目の文字 (アプリの言語で渡す)</param>
/// <param name="IconPath">アイコンファイル (.ico)のパス。未指定・読めないときは Windows 標準のアプリアイコン</param>
/// <remarks>ウィンドウクラスの登録はプロセスごとなので、別の EXE (Debug / Release 等)と名前が重なっても問題ない。</remarks>
public sealed record TrayIconOptions(string ToolTip, string WindowClassName, string ExitText, string? IconPath = null);
