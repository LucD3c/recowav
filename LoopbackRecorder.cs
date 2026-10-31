using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

#pragma warning disable CS0618 // WasapiLoopbackCapture/WasapiOut siguen funcionando en NAudio 3

namespace RecoWav;

/// <summary>
/// Graba el audio que suena por el dispositivo de salida predeterminado (loopback WASAPI)
/// durante una duraciÃ³n fija y lo convierte a MP3.
/// </summary>
public sealed class LoopbackRecorder : IDisposable
{
    private static readonly int[] Mp3SampleRates = { 44100, 48000 };

    private WasapiLoopbackCapture? _capture;
    private WasapiOut? _silencePlayer;
    private WaveFileWriter? _writer;
    private string _tempWav = "";
    private long _targetBytes;
    private bool _stopRequested;

    public event Action<float>? LevelChanged;
    public event Action<TimeSpan>? ProgressChanged;
    /// <summary>Se dispara cuando la captura termina; el argumento es la excepciÃ³n si la hubo.</summary>
    public event Action<Exception?>? CaptureFinished;

    public TimeSpan Recorded { get; private set; }

    public void Start(TimeSpan duration)
    {
        _tempWav = Path.Combine(Path.GetTempPath(), $"recowav_{Guid.NewGuid():N}.wav");
        _stopRequested = false;
        Recorded = TimeSpan.Zero;

        _capture = new WasapiLoopbackCapture();
        var format = _capture.WaveFormat;
        _targetBytes = (long)(duration.TotalSeconds * format.AverageBytesPerSecond);
        _targetBytes -= _targetBytes % format.BlockAlign;

        _writer = new WaveFileWriter(_tempWav, format);
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;

        // El loopback no entrega datos cuando nada suena. Reproducir silencio mantiene
        // el flujo activo para que los silencios reales queden grabados y la duraciÃ³n sea exacta.
        _silencePlayer = new WasapiOut(AudioClientShareMode.Shared, 100);
        _silencePlayer.Init(new SilenceProvider(format));
        _silencePlayer.Play();

        _capture.StartRecording();
    }

    public void Stop()
    {
        if (_stopRequested) return;
        _stopRequested = true;
        _capture?.StopRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_writer == null || _stopRequested) return;

        int count = (int)Math.Min(e.BytesRecorded, _targetBytes - _writer.Length);
        if (count > 0)
        {
            _writer.Write(e.Buffer, 0, count);
            Recorded = TimeSpan.FromSeconds((double)_writer.Length / _writer.WaveFormat.AverageBytesPerSecond);
            LevelChanged?.Invoke(Peak(e.Buffer, count, _writer.WaveFormat));
            ProgressChanged?.Invoke(Recorded);
        }

        if (_writer.Length >= _targetBytes)
            Stop();
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _silencePlayer?.Stop();
        _silencePlayer?.Dispose();
        _silencePlayer = null;
        _writer?.Dispose();
        _writer = null;
        _capture?.Dispose();
        _capture = null;
        CaptureFinished?.Invoke(e.Exception);
    }

    /// <summary>Convierte la grabaciÃ³n temporal a MP3 y borra el WAV temporal.</summary>
    public void SaveMp3(string outputPath, int bitrateKbps)
    {
        try
        {
            using var reader = new WaveFileReader(_tempWav);
            ISampleProvider samples = reader.ToSampleProvider();

            // El codificador MP3 de Windows sÃ³lo admite mono/estÃ©reo a 44.1/48 kHz.
            if (samples.WaveFormat.Channels > 2)
                samples = new DownmixToStereo(samples);
            if (!Mp3SampleRates.Contains(samples.WaveFormat.SampleRate))
                samples = new WdlResamplingSampleProvider(samples, 48000);

            MediaFoundationEncoder.EncodeToMp3(samples.ToWaveProvider16(), outputPath, bitrateKbps * 1000);
        }
        finally
        {
            DeleteTemp();
        }
    }

    public void DeleteTemp()
    {
        try { if (File.Exists(_tempWav)) File.Delete(_tempWav); } catch { /* best effort */ }
    }

    private static float Peak(byte[] buffer, int count, WaveFormat format)
    {
        float max = 0;
        if (format.Encoding == WaveFormatEncoding.IeeeFloat ||
            (format is WaveFormatExtensible && format.BitsPerSample == 32))
        {
            for (int i = 0; i + 4 <= count; i += 4)
                max = Math.Max(max, Math.Abs(BitConverter.ToSingle(buffer, i)));
        }
        else if (format.BitsPerSample == 16)
        {
            for (int i = 0; i + 2 <= count; i += 2)
                max = Math.Max(max, Math.Abs(BitConverter.ToInt16(buffer, i) / 32768f));
        }
        return Math.Min(max, 1f);
    }

    public void Dispose()
    {
        _stopRequested = true;
        _capture?.StopRecording();
        _silencePlayer?.Dispose();
        _writer?.Dispose();
        _capture?.Dispose();
        DeleteTemp();
    }

    /// <summary>Mezcla audio multicanal (5.1, 7.1...) a estÃ©reo tomando canales pares/impares.</summary>
    private sealed class DownmixToStereo : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private float[] _buffer = Array.Empty<float>();

        public DownmixToStereo(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(Span<float> buffer)
        {
            int frames = buffer.Length / 2;
            int needed = frames * _channels;
            if (_buffer.Length < needed) _buffer = new float[needed];
            int read = _source.Read(_buffer.AsSpan(0, needed));
            int framesRead = read / _channels;

            for (int f = 0; f < framesRead; f++)
            {
                float l = 0, r = 0;
                int baseIdx = f * _channels;
                for (int c = 0; c < _channels; c++)
                {
                    if (c % 2 == 0) l += _buffer[baseIdx + c]; else r += _buffer[baseIdx + c];
                }
                float scale = 2f / _channels;
                buffer[f * 2] = Math.Clamp(l * scale, -1f, 1f);
                buffer[f * 2 + 1] = Math.Clamp(r * scale, -1f, 1f);
            }
            return framesRead * 2;
        }
    }
}


