using System.Runtime.InteropServices;
using System.Text;
using MmmSdk.Core.Components.Secrets;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Credentials;

namespace MmmSdk.WinUI.Components.Secrets;

/// <summary>
/// 秘密を Windows の資格情報マネージャー (汎用資格情報)に保存する。
/// </summary>
/// <remarks>
/// 項目の名前 (ターゲット名)とユーザー名には、渡された名前をそのまま使う。保存先は今の Windows ユーザーの、この PC の中だけ
/// (<c>CRED_PERSIST_LOCAL_MACHINE</c>。ほかの PC に同期しない)。値は UTF-16 で保存する。
/// 1 件の大きさには OS の上限がある (<c>CRED_MAX_CREDENTIAL_BLOB_SIZE</c>。512 × 5 バイト = 1280 文字)。
/// </remarks>
public sealed unsafe class CredentialSecretStore : ISecretStore
{
    /// <inheritdoc />
    public string? Get(string name)
    {
        if (!PInvoke.CredRead(name, CRED_TYPE.CRED_TYPE_GENERIC, out var credential))
        {
            var error = Marshal.GetLastPInvokeError();
            return error == (int)WIN32_ERROR.ERROR_NOT_FOUND ? null : throw Failure("読み込めません", error);
        }

        try
        {
            return Encoding.Unicode.GetString(credential->CredentialBlob, (int)credential->CredentialBlobSize);
        }
        finally
        {
            PInvoke.CredFree(credential);
        }
    }

    /// <inheritdoc />
    public void Set(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var blob = Encoding.Unicode.GetBytes(value);
        fixed (char* target = name)
        fixed (byte* data = blob)
        {
            var credential = new CREDENTIALW
            {
                Type = CRED_TYPE.CRED_TYPE_GENERIC,
                TargetName = target,
                UserName = target,
                CredentialBlob = data,
                CredentialBlobSize = (uint)blob.Length,
                Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
            };
            if (!PInvoke.CredWrite(&credential, 0))
            {
                throw Failure("保存できません", Marshal.GetLastPInvokeError());
            }
        }
    }

    /// <inheritdoc />
    public bool Remove(string name)
    {
        fixed (char* target = name)
        {
            if (PInvoke.CredDelete(target, CRED_TYPE.CRED_TYPE_GENERIC))
            {
                return true;
            }
        }

        var error = Marshal.GetLastPInvokeError();
        return error == (int)WIN32_ERROR.ERROR_NOT_FOUND ? false : throw Failure("削除できません", error);
    }

    /// <summary>資格情報マネージャーの失敗を、画面に出せる例外にする</summary>
    /// <param name="action">できなかった操作 (「保存できません」など)</param>
    /// <param name="error">Win32 のエラーコード</param>
    /// <returns>画面に出せるメッセージを持つ例外</returns>
    private static SecretStoreException Failure(string action, int error)
        => new($"Windows の資格情報マネージャーに{action}でした (エラー {error}: {Marshal.GetPInvokeErrorMessage(error)})。");
}
