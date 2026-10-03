using System.Security.Cryptography;
using System.Text;

namespace MmmSdk.Core.SingleInstance;

/// <summary>同一 EXE の二重起動を防ぐ</summary>
/// <remarks>Mutex 名を EXE パスのハッシュから作るため、Debug / Release など別パスの EXE は同時に起動できる。</remarks>
public sealed class SingleInstanceGuard
{
    /// <summary>多重起動防止用の Mutex。</summary>
    /// <remarks>プロセス終了まで保持する（GC で解放されないようフィールドで持つ）。</remarks>
    private readonly Mutex _mutex;

    /// <summary>この起動が、最初のインスタンスか</summary>
    public bool IsFirstInstance { get; }

    /// <summary>Mutex を取得して、最初の起動かどうかを判定する</summary>
    /// <param name="appName">アプリを区別する名前（Mutex 名の先頭に付ける。アプリごとに別の名前にする）</param>
    public SingleInstanceGuard(string appName)
    {
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        var exeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exePath.ToUpperInvariant())));
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{appName}_{exeHash}", out var createdNew);
        IsFirstInstance = createdNew;
    }
}
