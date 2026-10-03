namespace MmmSdk.Core.Components.Storage;

/// <summary>保存ファイルの読み込み結果（失敗したか・壊れたファイルを退避したか）を覚える</summary>
/// <remarks>
/// 読み込みを行うサービスが 1 つ持ち、<see cref="LoadError"/> と <see cref="RecoveryMessage"/> を画面に出す。
/// 読み込みに失敗した（ロック・権限などで読めなかった）ときは、元のデータを上書きで消さないよう、サービスが <see cref="HasFailed"/> を見て保存を止める
/// （止め方は、呼び出しの性質に合わせてサービスごとに決める。ユーザーの操作による保存は <see cref="ThrowIfSaveBlocked"/> で例外にし、補助的な保存は黙って行わない）。
/// 壊れていた（JSON として読めなかった）ファイルは退避済みなので、失敗ではなく <see cref="RecoveryMessage"/> で知らせるだけで、保存は止めない。
/// </remarks>
public sealed class LoadStatus
{
    /// <summary>読み込みの失敗の原因。失敗していなければ null</summary>
    private DataFileException? _failure;

    /// <summary>読み込みに失敗したときのメッセージ。失敗していなければ null</summary>
    public string? LoadError { get; private set; }

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。無ければ null</summary>
    public string? RecoveryMessage { get; private set; }

    /// <summary>読み込みに失敗しているか（保存を止める判断に使う）</summary>
    public bool HasFailed => _failure is not null;

    /// <summary>読み込みに成功したことを記録する</summary>
    /// <param name="recoveryMessage">壊れたファイルを退避したときのメッセージ。無ければ null</param>
    /// <param name="keepPreviousRecoveryMessage">
    /// 退避のメッセージが無いとき、前回のメッセージを残すか（読み直しても、最初の読み込みで起きた退避を画面で知らせ続けたいとき true）
    /// </param>
    public void Succeeded(string? recoveryMessage = null, bool keepPreviousRecoveryMessage = false)
    {
        _failure = null;
        LoadError = null;
        RecoveryMessage = recoveryMessage ?? (keepPreviousRecoveryMessage ? RecoveryMessage : null);
    }

    /// <summary>読み込みに失敗したことを記録する</summary>
    /// <param name="failure">失敗の原因</param>
    /// <param name="message">画面に出すメッセージ。省略すると原因のメッセージ</param>
    public void Failed(DataFileException failure, string? message = null)
    {
        _failure = failure;
        LoadError = message ?? failure.Message;
    }

    /// <summary>複数のファイルの読み込み結果をまとめて記録する</summary>
    /// <param name="failures">失敗の原因（無ければ空）。1 件でもあれば失敗として記録する</param>
    /// <param name="recoveryMessages">壊れたファイルを退避したときのメッセージ（無ければ空）</param>
    /// <remarks>複数あるメッセージは改行でつなぐ。</remarks>
    public void Record(IReadOnlyList<DataFileException> failures, IReadOnlyList<string> recoveryMessages)
    {
        _failure = failures.Count > 0 ? failures[0] : null;
        LoadError = failures.Count > 0 ? string.Join("\n", failures.Select(failure => failure.Message)) : null;
        RecoveryMessage = recoveryMessages.Count > 0 ? string.Join("\n", recoveryMessages) : null;
    }

    /// <summary>読み込みの失敗を消す（保存や読み直しに成功して、ファイルが読める状態に戻ったとき）</summary>
    public void ClearFailure()
    {
        _failure = null;
        LoadError = null;
    }

    /// <summary>読み込みに失敗していたら、保存させずに例外にする</summary>
    /// <param name="explanation"><see cref="LoadError"/> のあとに添える、保存を止めている理由の説明</param>
    /// <exception cref="DataFileException">読み込みに失敗している。</exception>
    public void ThrowIfSaveBlocked(string explanation)
    {
        if (_failure is not null)
        {
            throw new DataFileException($"{LoadError}\n{explanation}", _failure);
        }
    }
}
