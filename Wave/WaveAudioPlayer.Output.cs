using NAudio.Wave;
using MgaWwiseIMImporter.UI;

namespace MgaWwiseIMImporter.Wave;

internal sealed partial class WaveAudioPlayer
{
    public void ApplyOutputSettings(AudioOutputSettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _outputSettings = settings;
        if (_provider is null)
        {
            Trace(
                $"audio.output-settings api={AudioOutputSettings.ToStoredValue(settings.Api)}"
                + $" device='{settings.DeviceId}' (deferred)");
            return;
        }

        var progress = Progress;
        var wasPlaying = _isPlaying;
        DisposeOutputOnly();
        InitOutputDevice();
        Seek(progress);
        if (wasPlaying)
        {
            Play();
        }
    }

    private void InitOutputDevice()
    {
        if (_provider is null)
        {
            return;
        }

        // AsioOut はコンストラクタで SynchronizationContext.Current を掴む。
        // 背景スレッドで Init すると再生不能／フォールバックになるため UI スレッドへ延期する。
        if (_outputSettings.Api == AudioOutputApi.Asio
            && SynchronizationContext.Current is null)
        {
            Trace(
                $"audio.output-defer api=Asio device='{_outputSettings.DeviceId}'"
                + " (requires UI SynchronizationContext)");
            return;
        }

        try
        {
            _output = AudioOutputFactory.Create(_outputSettings, out var fallbackMessage);
            if (!string.IsNullOrEmpty(fallbackMessage))
            {
                Trace($"audio.output-fallback {fallbackMessage}");
                Diagnostic?.Invoke(this, fallbackMessage);
                // 要求設定は保持する（次回 UI スレッドでの再試行・ダイアログ表示のため）
            }

            BindOutputProvider(_output);
        }
        catch (Exception ex)
        {
            DisposeOutputOnly();
            var message =
                $"Output init failed ({AudioOutputSettings.ToStoredValue(_outputSettings.Api)}"
                + $" '{_outputSettings.DeviceId}'): {ex.Message}; falling back to WaveOut default.";
            Trace($"audio.output-fallback {message}");
            Diagnostic?.Invoke(this, message);
            try
            {
                _output = AudioOutputFactory.Create(AudioOutputSettings.Default, out _);
                BindOutputProvider(_output);
            }
            catch
            {
                // フォールバックも失敗したら、イベント未購読の壊れたデバイスを残さない。
                DisposeOutputOnly();
                throw;
            }
        }

        _output.PlaybackStopped += OnPlaybackStopped;
        if (_output is AsioOut asioResettable)
        {
            // ドライバ設定変更（バッファサイズ等）の通知。放置するとドライバが
            // 不正状態のままになる（無音のまま瞬時に読み尽くす等）ため必ず対応する。
            _outputSyncContext = SynchronizationContext.Current;
            asioResettable.DriverResetRequest += OnAsioDriverResetRequest;
        }

        var asioInfo = _output is AsioOut asio
            ? $" framesPerBuffer={asio.FramesPerBuffer} sampleRate={_provider?.WaveFormat.SampleRate ?? 0}"
            : string.Empty;
        Trace(
            $"audio.output-ready api={AudioOutputSettings.ToStoredValue(_outputSettings.Api)}"
            + $" device='{_outputSettings.DeviceId}'"
            + $" type={_output.GetType().Name}{asioInfo}");
    }

    private void BindOutputProvider(IWavePlayer output)
    {
        if (_provider is null)
        {
            return;
        }

        if (output is AsioOut)
        {
            _asioAdapter = new AsioCallbackAdapter(_provider, message => Trace(message));
            output.Init(_asioAdapter);
        }
        else
        {
            _asioAdapter = null;
            output.Init(_provider);
        }
    }

    /// <summary>
    /// 出力デバイスが未初期化なら現在の設定で初期化する（UI スレッドから呼ぶこと）。
    /// </summary>
    public void EnsureOutputDevice()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_provider is null || _output is not null)
        {
            return;
        }

        InitOutputDevice();
    }

    private void DisposeOutputOnly()
    {
        _isPlaying = false;
        _asioAdapter = null;
        if (_output is null)
        {
            return;
        }

        ExitLowLatencyGc();
        _suppressPlaybackEnded = true;
        try
        {
            if (_output is AsioOut asioOut)
            {
                asioOut.DriverResetRequest -= OnAsioDriverResetRequest;
            }

            _output.PlaybackStopped -= OnPlaybackStopped;
            _output.Stop();
            _output.Dispose();
        }
        finally
        {
            _suppressPlaybackEnded = false;
            _output = null;
        }
    }

    /// <summary>
    /// ASIO ドライバからのリセット要求（コントロールパネルでのバッファサイズ／
    /// サンプルレート変更等）。ドライバのコールバックスレッドから届くため、
    /// UI スレッドへ移してデバイスを作り直す。再生位置は維持し、再生は停止する。
    /// </summary>
    private void OnAsioDriverResetRequest(object? sender, EventArgs e)
    {
        var context = _outputSyncContext;
        if (context is null)
        {
            return;
        }

        context.Post(
            _ =>
            {
                if (_disposed || _output is not AsioOut)
                {
                    return;
                }

                Trace("audio.asio-reset-request (driver settings changed; reinitializing device)");
                var progress = Progress;
                RecreateOutputDevice();
                Seek(progress);
            },
            null);
    }

    /// <summary>
    /// 出力デバイスだけを破棄して作り直し、ドライバ／ハードの先読みを捨てる。
    /// リーダー位置と Provider 状態は維持する。
    /// </summary>
    private void RecreateOutputDevice()
    {
        if (_provider is null)
        {
            return;
        }

        DisposeOutputOnly();
        InitOutputDevice();
    }

}
