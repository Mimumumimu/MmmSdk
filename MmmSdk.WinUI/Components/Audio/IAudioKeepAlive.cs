namespace MmmSdk.WinUI.Components.Audio;

/// <summary>
/// 音声機器を眠らせないため、無音の出力を流し続ける。
/// </summary>
/// <remarks>
/// 光デジタルなど、無音が続くと信号を止める機器で、読み上げや通知音の頭が切れるのを防ぐ。
/// 流している間は、既定の出力機器が変わったら、新しい機器へ流し直す。
/// </remarks>
public interface IAudioKeepAlive
{
    /// <summary>無音の出力を始める</summary>
    /// <returns>始められたら true。出力機器が無いなど、始められなかったら false (機器が現れたら、自動で始まる)</returns>
    /// <remarks>すでに流しているときは、何もしない。</remarks>
    Task<bool> StartAsync();

    /// <summary>無音の出力を止める</summary>
    /// <returns>止めたことを表すタスク</returns>
    Task StopAsync();
}
