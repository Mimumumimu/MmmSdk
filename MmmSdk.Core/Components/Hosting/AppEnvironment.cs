namespace MmmSdk.Core.Components.Hosting;

/// <summary>アプリの名前と、データ・アイコンの置き場所 (ホストが 1 つ登録し、機能 (プラグイン)が使う)</summary>
/// <param name="Name">アプリの名前 (添付の一時フォルダーなど、アプリごとに分ける場所の名前に使う)</param>
/// <param name="DataDirectory">JSON などの保存先フォルダー</param>
/// <param name="IconPath">アプリのアイコン (.ico)のパス (ウィンドウのタイトルバーに使う)</param>
/// <remarks>機能は、ホストの型 (アプリ本体のクラス)を参照できないため、この値を DI から受け取る。</remarks>
public sealed record AppEnvironment(string Name, string DataDirectory, string IconPath);
