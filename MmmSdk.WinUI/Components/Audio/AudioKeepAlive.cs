using MmmSdk.Core.Utilities;
using Windows.Media.Audio;
using Windows.Media.Devices;
using Windows.Media.Render;

namespace MmmSdk.WinUI.Components.Audio;

/// <summary>
/// 出力ノードだけの <see cref="AudioGraph"/> を動かして、音声機器を眠らせない。アプリ全体で 1 つ。
/// </summary>
/// <remarks>
/// 入力ノードを持たせないので、音声エンジンが無音を流し続け、アプリのコールバックは無い (CPU をほぼ使わない)。
/// グラフは出力機器に結び付くので、既定の出力機器が変わったとき・グラフが続けられなくなったとき (機器の取り外しなど)は、作り直す。
/// 機器の変更は、流している間だけ受け取る (止めている間は、何にも触れない)。
/// 開始・停止・作り直しは 1 つずつ順に行う。
/// </remarks>
public sealed class AudioKeepAlive : IAudioKeepAlive, IDisposable
{
    /// <summary>1 区間の長さの希望 (サンプル数。長くして、エンジンの起床の回数を減らす。機器が許す範囲に丸められる)</summary>
    private const uint DesiredSamplesPerQuantum = 4800;

    /// <summary>開始・停止・作り直しを 1 つずつ行うための排他</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>今の無音のグラフ (流していないときは null)</summary>
    private AudioGraph? _graph;

    /// <summary>流してほしい状態か (機器が無くて流せていなくても、true の間は、機器が現れたら流す)</summary>
    private bool _wanted;

    /// <summary>破棄したか</summary>
    private bool _disposed;

    /// <inheritdoc />
    public async Task<bool> StartAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_wanted)
        {
            _wanted = true;
            MediaDevice.DefaultAudioRenderDeviceChanged += OnDefaultAudioRenderDeviceChanged;
        }
        return await RebuildAsync(onlyIfMissing: true);
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (_wanted)
        {
            _wanted = false;
            MediaDevice.DefaultAudioRenderDeviceChanged -= OnDefaultAudioRenderDeviceChanged;
        }
        await RebuildAsync(onlyIfMissing: false);
    }

    /// <summary>機器の変更の受け取りをやめ、グラフを破棄する</summary>
    /// <remarks>グラフの破棄は止めることを含む。続けられなくなったグラフでも例外にならないよう、<c>Stop</c> は呼ばない。</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _wanted = false;
        MediaDevice.DefaultAudioRenderDeviceChanged -= OnDefaultAudioRenderDeviceChanged;
        CloseGraph();
    }

    /// <summary>グラフを作り直す (流してほしくなければ、閉じるだけ)</summary>
    /// <param name="onlyIfMissing">true なら、すでに流れているときは何もしない</param>
    /// <returns>無音を流している状態になったら true</returns>
    private async Task<bool> RebuildAsync(bool onlyIfMissing)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return false;
            }
            if (onlyIfMissing && _graph is not null)
            {
                return true;
            }

            CloseGraph();
            if (!_wanted)
            {
                return false;
            }
            return await TryOpenGraphAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>出力ノードだけのグラフを作って動かす</summary>
    /// <returns>動かせたら true。出力機器が無いなど、作れなかったら false</returns>
    /// <remarks>作れないのは機器の状態による予測できる失敗なので、例外にしない (機器が変わったら、また試す)。</remarks>
    private async Task<bool> TryOpenGraphAsync()
    {
        var settings = new AudioGraphSettings(AudioRenderCategory.Media)
        {
            QuantumSizeSelectionMode = QuantumSizeSelectionMode.ClosestToDesired,
            DesiredSamplesPerQuantum = (int)DesiredSamplesPerQuantum,
        };
        var graphResult = await AudioGraph.CreateAsync(settings);
        if (graphResult.Status != AudioGraphCreationStatus.Success)
        {
            return false;
        }

        var graph = graphResult.Graph;
        var outputResult = await graph.CreateDeviceOutputNodeAsync();
        if (outputResult.Status != AudioDeviceNodeCreationStatus.Success)
        {
            graph.Dispose();
            return false;
        }

        graph.UnrecoverableErrorOccurred += OnUnrecoverableErrorOccurred;
        graph.Start();
        _graph = graph;
        return true;
    }

    /// <summary>グラフを止めて破棄する (無ければ何もしない)</summary>
    private void CloseGraph()
    {
        if (_graph is not { } graph)
        {
            return;
        }
        _graph = null;
        graph.UnrecoverableErrorOccurred -= OnUnrecoverableErrorOccurred;
        graph.Dispose();
    }

    /// <summary>既定の出力機器が変わったら、流し直す</summary>
    /// <param name="sender">イベントの発生元 (使わない)</param>
    /// <param name="args">変更の内容</param>
    /// <remarks>通知は UI スレッドとは限らないが、グラフの操作は排他の中で行うので、どのスレッドでもよい。</remarks>
    private void OnDefaultAudioRenderDeviceChanged(object sender, DefaultAudioRenderDeviceChangedEventArgs args)
    {
        if (args.Role == AudioDeviceRole.Default && _wanted)
        {
            RebuildAsync(onlyIfMissing: false).Forget();
        }
    }

    /// <summary>グラフが続けられなくなったら、作り直す</summary>
    /// <param name="sender">続けられなくなったグラフ</param>
    /// <param name="args">原因 (使わない)</param>
    private void OnUnrecoverableErrorOccurred(AudioGraph sender, AudioGraphUnrecoverableErrorOccurredEventArgs args)
    {
        if (_wanted)
        {
            RebuildAsync(onlyIfMissing: false).Forget();
        }
    }
}
