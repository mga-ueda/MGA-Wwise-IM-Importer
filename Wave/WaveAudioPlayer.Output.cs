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

            _output.Init(_provider);
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
                _output.Init(_provider);
            }
            catch
            {
                // フォールバックも失敗したら、イベント未購読の壊れたデバイスを残さない。
                DisposeOutputOnly();
                throw;
            }
        }

        _output.PlaybackStopped += OnPlaybackStopped;
        Trace(
            $"audio.output-ready api={AudioOutputSettings.ToStoredValue(_outputSettings.Api)}"
            + $" device='{_outputSettings.DeviceId}'"
            + $" type={_output.GetType().Name}");
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
        if (_output is null)
        {
            return;
        }

        _suppressPlaybackEnded = true;
        try
        {
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

    /// <summary>
    /// 元ファイルを一時領域へコピーし、そのパスを返す。
    /// 失敗時は呼び元が元ファイルを掴む状態にならないよう、コピーを破棄する。
    /// </summary>
    private static string CreatePlaybackCopy(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".wav";
        }

        var copyPath = Path.Combine(
            Path.GetTempPath(),
            $"mga-wwise-playback-{Guid.NewGuid():N}{extension}");
        try
        {
            File.Copy(sourcePath, copyPath, overwrite: true);
            return copyPath;
        }
        catch
        {
            TryDeleteFile(copyPath);
            throw;
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // 一時ファイル削除失敗は致命的ではない。
        }
        catch (UnauthorizedAccessException)
        {
            // 一時ファイル削除失敗は致命的ではない。
        }
    }

}
