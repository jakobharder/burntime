using Burntime.Platform;
using Burntime.Platform.IO;
using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System;

namespace Burntime.MonoGame.Music;

internal class LoopableSong : IDisposable
{
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

    readonly byte[]? _loopBuffer;
    readonly byte[]? _fadeOutBuffer;
    SoundEffect? _effect;
    SoundEffectInstance? _music;
    bool _loopEnabled;
    bool _bufferNeededSubscribed;

    public LoopableSong(Burntime.Platform.IO.File loop,
        Burntime.Platform.IO.File? intro = null, bool repeat = false,
        bool fade = false)
    {
        if (intro is null && !repeat && !fade)
        {
            _effect = OggSoundEffect.FromStream(loop.Stream);
            _music = _effect.CreateInstance();
            return;
        }

        using var loopOgg = new VorbisReader(loop.Stream, false);
        byte[] loopBuffer = loopOgg.GetBuffer();
        byte[]? introBuffer = null;
        if (intro is not null)
        {
            using var introOgg = new VorbisReader(intro.Stream, false);
            if (introOgg.SampleRate != loopOgg.SampleRate ||
                introOgg.Channels != loopOgg.Channels)
                return;
            introBuffer = introOgg.GetBuffer();
        }

        _loopBuffer = loopBuffer;
        _loopEnabled = repeat;
        if (repeat || fade)
        {
            _fadeOutBuffer = (byte[])loopBuffer.Clone();
            OggSoundEffect.ApplyFadeOut(_fadeOutBuffer, loopOgg.SampleRate,
                loopOgg.Channels);
        }

        var music = new DynamicSoundEffectInstance(loopOgg.SampleRate,
            loopOgg.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
        _music = music;
        if (repeat)
        {
            music.BufferNeeded += BufferNeeded;
            _bufferNeededSubscribed = true;
        }
        byte[] firstLoopBuffer = fade && !repeat ? _fadeOutBuffer! : loopBuffer;
        music.SubmitBuffer(introBuffer ?? firstLoopBuffer);
        if (!repeat && introBuffer is not null)
            music.SubmitBuffer(firstLoopBuffer);
    }

    private void BufferNeeded(object? sender, EventArgs e)
    {
        if (_loopEnabled && _loopBuffer is not null &&
            _music is DynamicSoundEffectInstance music)
            music.SubmitBuffer(_loopBuffer);
    }

    public void Dispose()
    {
        if (_music is not null)
        {
            _music.Stop();
            if (_bufferNeededSubscribed && _music is DynamicSoundEffectInstance music)
                music.BufferNeeded -= BufferNeeded;
            _music.Dispose();
            _music = null;
        }
        _effect?.Dispose();
        _effect = null;
    }

    public void Play() => _music?.Play();
    public void Pause() => _music?.Pause();
    public void Resume() => _music?.Resume();
    public void Stop() => _music?.Stop();

    public void DisableLoop()
    {
        if (!_loopEnabled)
            return;
        _loopEnabled = false;
        if (_bufferNeededSubscribed && _music is DynamicSoundEffectInstance music)
        {
            music.BufferNeeded -= BufferNeeded;
            _bufferNeededSubscribed = false;
        }
        if (_fadeOutBuffer is not null &&
            _music is DynamicSoundEffectInstance fadingMusic)
            fadingMusic.SubmitBuffer(_fadeOutBuffer);
    }

    public void EnableLoop()
    {
        if (_loopEnabled || _loopBuffer is null ||
            _music is not DynamicSoundEffectInstance music)
            return;
        _loopEnabled = true;
        music.BufferNeeded += BufferNeeded;
        _bufferNeededSubscribed = true;
    }

    public float Volume
    {
        get => _music?.Volume ?? 0;
        set { if (_music is not null) _music.Volume = value; }
    }

    public bool IsPlaying => _music?.State == SoundState.Playing;
}
