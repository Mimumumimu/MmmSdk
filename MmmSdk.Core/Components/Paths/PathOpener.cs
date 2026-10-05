using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace MmmSdk.Core.Components.Paths;

/// <summary>
/// リンクのパス (URL・ファイル・フォルダ・実行ファイル)を既定のアプリで開く。
/// </summary>
/// <remarks>Windows 前提 (シェル実行で開く。<see cref="PathTarget"/> も Windows 前提)。Windows 以外から実装の型を使うと、ビルドで警告になる (呼び出す側は <see cref="IPathOpener"/> で受け取る)。</remarks>
[SupportedOSPlatform("windows")]
public sealed class PathOpener : IPathOpener
{
    /// <inheritdoc />
    public Task OpenAsync(string path) => Task.Run(() =>
    {
        var target = PathTarget.Expand(path);
        if (target.Length == 0)
        {
            throw new PathOpenException("パスが空です。");
        }

        var startInfo = new ProcessStartInfo(target) { UseShellExecute = true };

        // 実行ファイルは、自分のフォルダを作業フォルダにして起動する (隣のファイルを相対パスで読むものがあるため)
        if (File.Exists(target) && Path.GetDirectoryName(target) is { Length: > 0 } directory)
        {
            startInfo.WorkingDirectory = directory;
        }

        try
        {
            using var _ = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            throw new PathOpenException($"「{path}」を開けませんでした。{ex.Message}", ex);
        }
    });
}
