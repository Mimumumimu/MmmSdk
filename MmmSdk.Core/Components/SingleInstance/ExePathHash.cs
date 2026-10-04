using System.Security.Cryptography;
using System.Text;

namespace MmmSdk.Core.Components.SingleInstance;

/// <summary>実行中の EXE のパスから作るハッシュ（多重起動の判定を EXE ごとに分けるために使う）</summary>
internal static class ExePathHash
{
    /// <summary>実行中の EXE のパスの SHA-256（16 進数の文字列）</summary>
    /// <remarks>パスの大文字小文字は区別しない。Debug / Release など別パスの EXE は、別の値になる。</remarks>
    public static string Current { get; } = Create();

    /// <summary>ハッシュを作る</summary>
    /// <returns>EXE のパスの SHA-256（16 進数）</returns>
    private static string Create()
    {
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exePath.ToUpperInvariant())));
    }
}
