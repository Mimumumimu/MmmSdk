namespace MmmSdk.Core.Components.Paths;

/// <summary>リンクのパスが指す先の種類。</summary>
public enum PathTargetKind
{
    /// <summary>パスが空。</summary>
    Empty,

    /// <summary>URL</summary>
    Url,

    /// <summary>フォルダ</summary>
    Folder,

    /// <summary>実行ファイル (.exe・.bat 等)。</summary>
    Executable,

    /// <summary>ファイル</summary>
    File,

    /// <summary>ファイルにもフォルダにも見つからない。</summary>
    NotFound,
}
