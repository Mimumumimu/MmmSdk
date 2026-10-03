namespace MmmSdk.Core.Components.WindowPositions;

/// <summary>
/// ウィンドウの位置（と、位置と大きさ）をキー単位で保存・復元する（モックで差し替えられるようにするための口）。
/// </summary>
public interface IWindowPositionService
{
    /// <summary>保存した位置を取得する</summary>
    /// <param name="key">ウィンドウを区別するキー</param>
    /// <returns>保存した位置。保存がなければ null</returns>
    WindowPosition? Load(string key);

    /// <summary>位置を保存する。保存し終えるまで待つ。</summary>
    /// <param name="key">ウィンドウを区別するキー</param>
    /// <param name="position">保存する位置</param>
    /// <returns>保存の完了を表すタスク</returns>
    Task SaveAsync(string key, WindowPosition position);

    /// <summary>保存した位置と大きさを取得する</summary>
    /// <param name="key">ウィンドウを区別するキー</param>
    /// <returns>保存した位置と大きさ。保存がなければ null</returns>
    WindowBounds? LoadBounds(string key);

    /// <summary>位置と大きさを保存する。保存し終えるまで待つ。</summary>
    /// <param name="key">ウィンドウを区別するキー</param>
    /// <param name="bounds">保存する位置と大きさ</param>
    /// <returns>保存の完了を表すタスク</returns>
    Task SaveBoundsAsync(string key, WindowBounds bounds);
}
