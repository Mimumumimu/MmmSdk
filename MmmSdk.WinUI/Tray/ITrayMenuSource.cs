namespace MmmSdk.WinUI.Tray;

/// <summary>
/// トレイメニューに項目を出す機能（リンク・リマインダー等）。DI に登録した順に、区切り線で分けて並ぶ。
/// </summary>
public interface ITrayMenuSource
{
    /// <summary>メニューに出す項目。</summary>
    /// <returns>メニューに出す項目の一覧</returns>
    /// <remarks>
    /// メニューを開くたびに呼ぶので、その時点の内容を返す（保存などの変更を作り直しなしで反映するため）。
    /// 項目の処理（<see cref="TrayMenuItem.Command"/>）で予測できる失敗（パスが開けないなど）は、処理の中で受けて、その機能のやり方で知らせること。
    /// 受けなかった失敗は、バグとして、アプリの安全網（<c>FatalErrorHandler</c>）がログ → ダイアログ → 終了で受ける。
    /// </remarks>
    IReadOnlyList<TrayMenuItem> GetItems();
}
