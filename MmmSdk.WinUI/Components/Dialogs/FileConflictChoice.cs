namespace MmmSdk.WinUI.Components.Dialogs;

/// <summary>同じ名前のファイルが既にあるときの、ユーザーの選択</summary>
public enum FileConflictChoice
{
    /// <summary>既にあるファイルを置き換える</summary>
    Replace,
    /// <summary>置き換えずに、そのファイルは飛ばす</summary>
    Skip,
    /// <summary>ファイルごとに決める (まとめて聞くときだけ返る)</summary>
    DecideEach,
    /// <summary>保存を取りやめる</summary>
    Cancel,
}
