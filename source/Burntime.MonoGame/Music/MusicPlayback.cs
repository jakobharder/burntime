using Burntime.Platform;
using Burntime.Platform.IO;
using System.Collections.Generic;
using System.Threading;

namespace Burntime.MonoGame;

public sealed class MusicPlayback : IMusic
{
    readonly List<string> _playlist = new();
    readonly List<string> _mapPlaylist = new();
    readonly HashSet<string> _playlistSongs = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _songMapping = new();
    readonly List<Music.LoopableSong> _sounds = new();
    public ICollection<string> Songlist => _songMapping.Keys;
    public bool IsPlayingFromPlaylist => _isMapPlaylistPlayback;

    Music.LoopableSong? _music;
    Music.LoopableSong? _rememberedPlaylistMusic;
    bool _repeat = false;
    string? _rememberedPlaylistSong;
    string? _lastPlaylistSong;
    bool _isMapPlaylistPlayback;
    bool _continueWithMapPlaylist;
    Thread? _musicThread;
    bool _requestStop;

    public bool Enabled { get; set; }
    public string? Playing { get; private set; }

    bool isMuted = true;
    public bool IsMuted
    {
        get => isMuted;
        set { isMuted = value; Volume = _volume; }
    }

    float _volume = 0;
    public float Volume
    {
        get { if (_music == null) return 0; return _music.Volume; }
        set { _volume = value; if (_music != null) _music.Volume = isMuted ? 0 : value; }
    }

    public void ClearPlayList()
    {
        _playlist.Clear();
        Stop();
    }

    public void AddPlayList(string fileName)
    {
        if (!Enabled)
            return;

        _playlist.Add(fileName);
    }

    public void LoadSonglist(string fileName)
    {
        _songMapping.Clear();

        var songlist = new ConfigFile();
        if (!songlist.Open(fileName))
            return;

        var section = songlist.GetSection("");
        if (section is null)
            return;

        foreach (var value in section.Values)
            _songMapping[value.Key] = value.Value;

        if (_music is not null && Playing is not null)
        {
            string? nextSong = CanPlay(Playing)
                ? Playing
                : CanPlay("radio") ? "radio" : null;
            if (nextSong is not null)
                _playlist.Insert(0, nextSong);
            _music.Stop();
        }
    }

    public void LoadPlaylist(string fileName)
    {
        var loadedSongs = new List<string>();
        var loadedSongNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var playlist = new ConfigFile();
        if (playlist.Open(fileName))
        {
            foreach (string song in playlist[""].GetStrings("songs"))
            {
                if (loadedSongNames.Add(song))
                    loadedSongs.Add(song);
            }
        }

        lock (this)
        {
            _mapPlaylist.Clear();
            _mapPlaylist.AddRange(loadedSongs);
            _playlistSongs.Clear();
            _playlistSongs.UnionWith(loadedSongNames);

            // A profile switch (including F9) must not pick a different map song.
            // Retain its semantic name, but discard the paused audio object because
            // it belongs to the previous soundtrack profile.
            _rememberedPlaylistMusic?.Dispose();
            _rememberedPlaylistMusic = null;
        }
    }

    public void RememberPlaylistSong()
    {
        lock (this)
        {
            if (_isMapPlaylistPlayback && _music is not null && Playing is not null)
            {
                _rememberedPlaylistMusic?.Dispose();
                _music.Pause();
                _rememberedPlaylistMusic = _music;
                _rememberedPlaylistSong = Playing;
                _music = null;
                Playing = null;
                _isMapPlaylistPlayback = false;
                _playlist.Clear();
                return;
            }

            // The music thread may not have started a freshly selected track yet.
            // Preserve that queued choice instead of selecting another at random.
            if (_isMapPlaylistPlayback && _playlist.Count > 0)
            {
                _rememberedPlaylistSong = _playlist[0];
                _playlist.Clear();
                _isMapPlaylistPlayback = false;
                return;
            }

            // Automatic advancement has a brief handoff where the completed
            // instance is gone and its replacement is not attached yet. Keep
            // the durable selection so returning from a scene cannot randomize.
            if (_rememberedPlaylistMusic is null &&
                _rememberedPlaylistSong is null &&
                _lastPlaylistSong is not null)
            {
                _rememberedPlaylistSong = _lastPlaylistSong;
            }
        }
    }

    public void DiscardRememberedPlaylistSong()
    {
        lock (this)
        {
            _rememberedPlaylistSong = null;
            _rememberedPlaylistMusic?.Dispose();
            _rememberedPlaylistMusic = null;
        }
    }

    public void SetPlaylistContinuation(bool enabled)
    {
        lock (this)
        {
            _continueWithMapPlaylist = enabled;
            if (enabled)
            {
                _repeat = false;
                _music?.DisableLoop();
            }
            if (enabled && _music is null && _playlist.Count == 0 &&
                _rememberedPlaylistSong is null)
            {
                QueueRandomPlaylistSong();
            }
        }
    }

    public void PlayPlaylist()
    {
        if (!Enabled || _mapPlaylist.Count == 0)
        {
            DiscardRememberedPlaylistSong();
            Stop();
            return;
        }

        string? rememberedSong = null;
        lock (this)
        {
            _continueWithMapPlaylist = true;
            if (_rememberedPlaylistMusic is not null &&
                _rememberedPlaylistSong is not null &&
                CanPlay(_rememberedPlaylistSong))
            {
                _playlist.Clear();
                if (_music is not null)
                {
                    _music.Stop();
                    _music.Dispose();
                }

                _music = _rememberedPlaylistMusic;
                Playing = _rememberedPlaylistSong;
                _isMapPlaylistPlayback = true;
                _rememberedPlaylistMusic = null;
                _rememberedPlaylistSong = null;
                _music.Volume = isMuted ? 0 : _volume;
                _music.Resume();
                return;
            }

            _rememberedPlaylistMusic?.Dispose();
            _rememberedPlaylistMusic = null;
            if (_rememberedPlaylistSong is not null &&
                CanPlay(_rememberedPlaylistSong))
            {
                rememberedSong = _rememberedPlaylistSong;
            }
            _rememberedPlaylistSong = null;
        }

        lock (this)
        {
            if (rememberedSong is not null)
            {
                _playlist.Clear();
                _repeat = false;
                _playlist.Add(rememberedSong);
                _isMapPlaylistPlayback = true;
            }
            else
            {
                QueueRandomPlaylistSong();
            }
        }
    }

    void QueueRandomPlaylistSong()
    {
        if (!Enabled || _mapPlaylist.Count == 0)
            return;

        string song;
        do
        {
            song = _mapPlaylist[Random.Shared.Next(_mapPlaylist.Count)];
        }
        while (_mapPlaylist.Count > 1 && song == _lastPlaylistSong);

        _lastPlaylistSong = song;
        _playlist.Clear();
        _repeat = false;
        _playlist.Add(song);
        _isMapPlaylistPlayback = true;
    }

    public bool CanPlay(string songName)
    {
        if (FileSystem.ExistsFile(songName))
            return true;

        if (_songMapping.TryGetValue(songName, out string? songFilePath))
            return FileSystem.ExistsFile(songFilePath);

        return false;
    }

    public string? ResolveSong(string songName)
    {
        if (FileSystem.ExistsFile(songName))
            return songName;

        return _songMapping.TryGetValue(songName, out string? songFilePath)
            ? songFilePath
            : null;
    }

    public void Play(string fileName, bool loop = true) =>
        Play(fileName, loop, isMapPlaylist: false);

    void Play(string fileName, bool loop, bool isMapPlaylist)
    {
        if (!Enabled)
            return;

        _playlist.Clear();
        Stop();
        _repeat = loop;
        _playlist.Add(fileName);
        _isMapPlaylistPlayback = isMapPlaylist;
    }

    public void PlayOnce(string fileName) => Play(fileName, false);

    public void PlaySound(string fileName)
    {
        if (!Enabled)
            return;

        Music.LoopableSong? sound = Music.LoopableSong.FromFileName(fileName);
        if (sound is null)
            return;

        sound.Volume = 1;
        sound.Play();
        lock (this)
            _sounds.Add(sound);
    }

    public void Stop()
    {
        _playlist.Clear();

        lock (this)
        {
            if (_music != null)
            {
                _music.Stop();
                _music.Dispose();
                _music = null;
            }

            Playing = null;
            _isMapPlaylistPlayback = false;
        }
    }

    public void RunThread()
    {
        _requestStop = false;
        _musicThread = new Thread(new ThreadStart(MusicThread));
        _musicThread.Start();
    }

    public void StopThread()
    {
        Stop();

        if (_musicThread != null)
        {
            _requestStop = true;
            _musicThread.Join();
            _musicThread = null;
        }
    }

    private string? GetNextTitle()
    {
        if (_playlist.Count == 0)
            return null;

        var next = _playlist[0];
        _playlist.RemoveAt(0);
        if (_repeat)
            _playlist.Add(next);

        Playing = next;

        if (FileSystem.ExistsFile(next))
            return next;

        if (_songMapping.TryGetValue(next, out string? songFilePath))
            return songFilePath;

        return null;
    }

    private void MusicThread()
    {
        int sleep = 0;

        while (!_requestStop)
        {
            if (sleep > 0)
                Thread.Sleep(sleep);

#warning THREADING lock playlist
            lock (this)
            {
                for (int i = _sounds.Count - 1; i >= 0; i--)
                {
                    if (_sounds[i].IsPlaying)
                        continue;

                    _sounds[i].Dispose();
                    _sounds.RemoveAt(i);
                }

                if (_music == null)
                {
                    sleep = 200;

                    string? next = GetNextTitle();
                    if (next == null)
                        continue;

                    _music = Music.LoopableSong.FromFileName(next, _repeat);
                    if (_music is null)
                        continue;

                    _music.Volume = _volume;
                    _music.Play();
                }
                else
                {
                    if (!_music.IsPlaying)
                    {
                        Playing = null;
                        _isMapPlaylistPlayback = false;
                        _music.Dispose();
                        _music = null;
                        if (_continueWithMapPlaylist)
                            QueueRandomPlaylistSong();
                        sleep = 0;
                    }
                    else
                        sleep = 50;
                }
            }
        }

        lock (this)
        {
            _music?.Dispose();
            _rememberedPlaylistMusic?.Dispose();
            _rememberedPlaylistMusic = null;
            _rememberedPlaylistSong = null;
            foreach (Music.LoopableSong sound in _sounds)
                sound.Dispose();
            _sounds.Clear();
        }
    }
}
