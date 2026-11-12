namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>同じ名前のファイルが既にあるときのユーザーの選択</summary>
public enum FileConflictChoice
{
    /// <summary>既にあるファイルを置き換える</summary>
    Replace,
    /// <summary>置き換えずにそのファイルは飛ばす</summary>
    Skip,
    /// <summary>ファイルごとに決める</summary>
    /// <remarks>まとめて聞くときだけ返る</remarks>
    DecideEach,
    /// <summary>保存を取りやめる</summary>
    Cancel,
}
