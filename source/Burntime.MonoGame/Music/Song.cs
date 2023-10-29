using Burntime.Platform;
using Burntime.Platform.IO;
using Microsoft.Xna.Framework.Audio;
using NVorbis;
using System;

namespace Burntime.MonoGame.Music;

internal class LoopableSong : IDisposable
{
    public static LoopableSong? FromFileName(string fileName, bool repeat = false)
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

        if (intro is not null && repeat)
            return new LoopableSong(loopFile, FileSystem.GetFile(intro));

        return new LoopableSong(loopFile, null, repeat);
    }

    readonly byte[]? _loopBuffer;
    SoundEffect? _effect;
    SoundEffectInstance? _music;

    public LoopableSong(File loop, File? intro = null, bool repeat = false)
    {
        if (intro is not null)
        {
            using var ogg = new VorbisReader((intro ?? loop).Stream, false);
            var music = new DynamicSoundEffectInstance(ogg.SampleRate, ogg.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
            music.SubmitBuffer(ogg.GetBuffer());

            if (intro is not null)
            {
                using var introOgg = new VorbisReader(loop.Stream, false);

                if (introOgg.SampleRate != ogg.SampleRate || introOgg.Channels != ogg.Channels)
                    return;

                _loopBuffer = introOgg.GetBuffer();
                music.BufferNeeded += BufferNeeded;
            }

            _music = music;
        }
        else
        {
            _effect = OggSoundEffect.FromStream(loop.Stream);
            _music = _effect.CreateInstance();
            _music.IsLooped = repeat;
        }
    }

    private void BufferNeeded(object? sender, EventArgs e)
    {
        if (_loopBuffer is not null && _music is DynamicSoundEffectInstance music)
            music.SubmitBuffer(_loopBuffer);
    }

    public void Dispose()
    {
        if (_music is not null)
        {
            _music.Stop();
            if (_loopBuffer is not null && _music is DynamicSoundEffectInstance music)
                music.BufferNeeded -= BufferNeeded;
            _music.Dispose();
            _music = null;
        }
        _effect?.Dispose();
        _effect = null;
    }

    public void Play() => _music?.Play();
    public void Stop() => _music?.Stop();

    public float Volume
    {
        get => _music?.Volume ?? 0;
        set { if (_music is not null) _music.Volume = value; }
    }

    public bool IsPlaying => _music?.State == SoundState.Playing;
}
