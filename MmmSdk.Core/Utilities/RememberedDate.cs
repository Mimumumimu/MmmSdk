namespace MmmSdk.Core.Utilities;

/// <summary>画面で選んだ日付を、選んだ日の間だけ覚える入れ物</summary>
/// <remarks>
/// ページを作り直しても、見ている日付を引き継ぐために使う。画面の状態は、ページの外の状態クラスが持つ。
/// アプリを起動したまま日をまたぐと、覚えた日付は古くなる。古い日付が「今日」のつもりの入力に残らないよう、日をまたいだあとは使わず、今日にする。
/// 画面のスレッドだけから使う。
/// </remarks>
public sealed class RememberedDate
{
    /// <summary>覚えた日付</summary>
    private DateOnly _value;

    /// <summary>日付を選んだ日</summary>
    private DateOnly _chosenOn;

    /// <summary>日付を覚えているか</summary>
    private bool _hasValue;

    /// <summary>画面で最初に出す日付を決める</summary>
    /// <param name="today">今日の日付</param>
    /// <returns>今日に選んだ日付があれば、その日付。無い・日をまたいだときは今日</returns>
    public DateOnly Resolve(DateOnly today) => _hasValue && _chosenOn == today ? _value : today;

    /// <summary>選んだ日付を覚える</summary>
    /// <param name="value">選んだ日付</param>
    /// <param name="today">今日の日付 (選んだ日として覚える)</param>
    public void Set(DateOnly value, DateOnly today)
    {
        _value = value;
        _chosenOn = today;
        _hasValue = true;
    }
}
