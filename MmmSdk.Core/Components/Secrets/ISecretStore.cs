namespace MmmSdk.Core.Components.Secrets;

/// <summary>
/// API キーなどの秘密を、設定ファイルとは別の安全な場所に保存する。
/// </summary>
/// <remarks>
/// 名前ごとに 1 件の文字列を持つ。設定ファイル (<c>ISettingsStore</c>)は手で開いて見られるので、秘密はこちらに保存する。
/// 実装は OS の資格情報の保管庫を使う (<c>MmmSdk.WinUI.Components.Secrets.CredentialSecretStore</c>)。
/// 名前はアプリごとに決め、ほかのアプリの項目と衝突しないようにする (例: <c>MmmTool.Backlog</c>)。
/// </remarks>
public interface ISecretStore
{
    /// <summary>秘密を取得する</summary>
    /// <param name="name">秘密の名前</param>
    /// <returns>保存されている値。無ければ null</returns>
    /// <exception cref="SecretStoreException">保管庫を読めなかった。</exception>
    string? Get(string name);

    /// <summary>秘密を保存する (同じ名前があれば上書きする)</summary>
    /// <param name="name">秘密の名前</param>
    /// <param name="value">保存する値</param>
    /// <exception cref="SecretStoreException">保管庫に書けなかった。</exception>
    void Set(string name, string value);

    /// <summary>秘密を削除する</summary>
    /// <param name="name">秘密の名前</param>
    /// <returns>削除したら true。もともと無ければ false</returns>
    /// <exception cref="SecretStoreException">保管庫から消せなかった。</exception>
    bool Remove(string name);
}
