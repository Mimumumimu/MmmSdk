namespace MmmSdk.WinUI.Components.Tray;

/// <summary>トレイメニューに項目を出す機能</summary>
/// <remarks>
/// 例はリンク・リマインダー
/// 機能の並び順の値の順に区切り線で分けて並ぶ
/// 登録は <c>AddTrayMenuSource</c>
/// </remarks>
public interface ITrayMenuSource
{
    /// <summary>メニューに出す項目</summary>
    /// <returns>メニューに出す項目の一覧</returns>
    /// <remarks>
    /// メニューを開くたびに呼ぶので、その時点の内容を返す。
    /// 保存などの変更を作り直しなしで反映するため。
    /// 項目の処理 (<see cref="TrayMenuItem.Command"/>)で予測できる失敗 (パスが開けないなど)は処理の中で受けて、その機能のやり方で知らせること。
    /// 受けなかった失敗はバグとして、アプリの安全網 (<c>FatalErrorHandler</c>)がログ → ダイアログ → 終了で受ける。
    /// </remarks>
    IReadOnlyList<TrayMenuItem> GetItems();
}
