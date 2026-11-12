namespace MmmSdk.Core.Components.Paths;

/// <summary>リンクのパスを既定のアプリで開く</summary>
/// <remarks>
/// 対象は URL・ファイル・フォルダ・実行ファイル
/// ViewModel などからモックで差し替えられるようにするための口
/// </remarks>
public interface IPathOpener
{
    /// <summary>開く</summary>
    /// <param name="path">
    /// 開くパス。
    /// URL・ファイル・フォルダ・実行ファイルで、環境変数を展開する。
    /// </param>
    /// <returns>開く処理の完了を表すタスク</returns>
    /// <remarks>シェル実行は呼び出し元をしばらく止めることがあるため、バックグラウンドで実行する。</remarks>
    /// <exception cref="PathOpenException">開けなかった</exception>
    Task OpenAsync(string path);
}
