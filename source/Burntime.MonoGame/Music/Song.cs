using Burntime.Platform;
using Burntime.Platform.IO;
using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System;

namespace Burntime.MonoGame.Music;

internal class LoopableSong : IDisposable
{
    const int BufferMilliseconds = 200;
    const int QueuedBufferCount = 3;

    public static LoopableSong? FromFileName(string fileName, bool repeat = false,
        bool fade = false)
    {
        string? intro = null;
        string loop = fileName;

        if (fileName.Contains(':'))
        {
            var split = loop.Split(':');
            loop = split[1];
            intro = split[0];
        }

        var loopFile = FileSystem.GetFile(loop);
        if (loopFile is null)
            return null;

        if (intro is not null)
            return new LoopableSong(loopFile, FileSystem.GetFile(intro), repeat, fade);

        return new LoopableSong(loopFile, null, repeat, fade);
    }

    VorbisReader? _introReader;
    VorbisReader? _loopReader;
    VorbisReader? _activeReader;
    DynamicSoundEffectInstance? _music;
    float[]? _sampleBuffer;
    byte[]? _pcmBuffer;
    bool _loopEnabled;
    bool _fadeAtEnd;
    bool _finishedDecoding;

    public LoopableSong(Burntime.Platform.IO.File loop,
        Burntime.Platform.IO.File? intro = null, bool repeat = false,
        bool fade = false)
    {
        _loopReader = new VorbisReader(loop.Stream, true);
        if (intro is not null)
        {
            _introReader = new VorbisReader(intro.Stream, true);
            if (_introReader.SampleRate != _loopReader.SampleRate ||
                _introReader.Channels != _loopReader.Channels)
            {
                DisposeReaders();
                return;
            }
        }

        _activeReader = _introReader ?? _loopReader;
        _loopEnabled = repeat;
        _fadeAtEnd = fade && !repeat;

        int sampleCount = _loopReader.SampleRate * BufferMilliseconds / 1000 *
            _loopReader.Channels;
        _sampleBuffer = new float[sampleCount];
        _pcmBuffer = new byte[sampleCount * sizeof(short)];
        _music = new DynamicSoundEffectInstance(_loopReader.SampleRate,
            _loopReader.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
    }

    void FillBuffers()
    {
        if (_music is null || _finishedDecoding)
            return;

        while (_music.PendingBufferCount < QueuedBufferCount && FillBuffer())
        {
        }
    }

    bool FillBuffer()
    {
        if (_music is null || _activeReader is null || _sampleBuffer is null ||
            _pcmBuffer is null)
            return false;

        while (true)
        {
            VorbisReader activeReader = _activeReader!;
            long firstFrame = activeReader.SamplePosition;
            int samplesRead = activeReader.ReadSamples(_sampleBuffer, 0,
                _sampleBuffer.Length);
            if (samplesRead > 0)
            {
                if (_fadeAtEnd && ReferenceEquals(activeReader, _loopReader))
                    ApplyFadeOut(_sampleBuffer, samplesRead, firstFrame,
                        activeReader.TotalSamples, activeReader.SampleRate,
                        activeReader.Channels);

                OggSoundEffect.CastBuffer(_sampleBuffer, _pcmBuffer, samplesRead);
                _music.SubmitBuffer(_pcmBuffer, 0, samplesRead * sizeof(short));
                return true;
            }

            if (ReferenceEquals(_activeReader, _introReader))
            {
                _introReader!.Dispose();
                _introReader = null;
                _activeReader = _loopReader;
                continue;
            }

            VorbisReader? loopReader = _loopReader;
            if (_loopEnabled && loopReader is not null && loopReader.TotalSamples > 0)
            {
                loopReader.SamplePosition = 0;
                continue;
            }

            _finishedDecoding = true;
            DisposeReaders();
            return false;
        }
    }

    static void ApplyFadeOut(float[] samples, int sampleCount, long firstFrame,
        long totalFrames, int sampleRate, int channels)
    {
        long fadeFrames = System.Math.Min(
            (long)(sampleRate * OggSoundEffect.FadeSeconds),
            totalFrames);
        if (fadeFrames == 0)
            return;
        long fadeStart = totalFrames - fadeFrames;
        int frameCount = sampleCount / channels;
        for (int frame = 0; frame < frameCount; frame++)
        {
            long position = firstFrame + frame;
            if (position < fadeStart)
                continue;

            float factor = System.Math.Max(0, (float)(totalFrames - position - 1) /
                fadeFrames);
            int offset = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                samples[offset + channel] *= factor;
        }
    }

    void DisposeReaders()
    {
        _introReader?.Dispose();
        _introReader = null;
        _loopReader?.Dispose();
        _loopReader = null;
        _activeReader = null;
    }

    public void Dispose()
    {
        if (_music is not null)
        {
            _music.Stop();
            _music.Dispose();
            _music = null;
        }
        DisposeReaders();
        _sampleBuffer = null;
        _pcmBuffer = null;
    }

    public void Play()
    {
        FillBuffers();
        _music?.Play();
    }

    public void Pause() => _music?.Pause();

    public void Resume()
    {
        FillBuffers();
        _music?.Resume();
    }

    public void Stop() => _music?.Stop();

    public void DisableLoop()
    {
        if (!_loopEnabled)
            return;
        _loopEnabled = false;
        _fadeAtEnd = true;
    }

    public float Volume
    {
        get => _music?.Volume ?? 0;
        set { if (_music is not null) _music.Volume = value; }
    }

    // Dynamic playback remains in Playing state when its queue runs dry.
    // Keep its bounded queue supplied, and finish after the final buffer drains.
    public bool IsPlaying
    {
        get
        {
            FillBuffers();
            return _music?.State == SoundState.Playing &&
                (!_finishedDecoding || _music.PendingBufferCount > 0);
        }
    }
}
