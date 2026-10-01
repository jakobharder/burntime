using Burntime.Platform;
using Burntime.Platform.IO;
using System.Collections.Generic;
using System.Threading;

namespace Burntime.MonoGame;

public sealed class MusicPlayback : IMusic
{
    enum RequestKind { Play, Stop, ResumePlaylist }
    readonly record struct PlaybackRequest(RequestKind Kind, string? Song = null,
        bool Repeat = false, bool IsMapPlaylist = false);

    const float TransitionStep = 0.1f;
    const int TransitionInterval = 20;
    static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    readonly List<string> _playlist = new();
    readonly List<string> _mapPlaylist = new();
    readonly HashSet<string> _playlistSongs = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _songMapping = new();
    readonly List<Music.LoopableSong> _sounds = new();
    public ICollection<string> Songlist => _songMapping.Keys;
    public bool IsPlayingFromPlaylist => _isMapPlaylistPlayback ||
        (_pendingRequest?.IsMapPlaylist ?? false);

    Music.LoopableSong? _music;
    Music.LoopableSong? _rememberedMusic;
    Music.LoopableSong? _rememberedPlaylistMusic;
    bool _repeat = false;
    bool _rememberedMusicRepeat;
    bool _rememberedMusicWasMapPlaylist;
    string? _rememberedSong;
    string? _rememberedPlaylistSong;
    string? _lastPlaylistSong;
    bool _isMapPlaylistPlayback;
    bool _continueWithMapPlaylist;
    bool _rememberPlaylistOnTransition;
    PlaybackRequest? _pendingRequest;
    float _transitionVolume = 1;
    Thread? _musicThread;
    bool _suspended;

    public void SetSuspended(bool suspended)
    {
        lock (this)
        {
            if (_suspended == suspended) return;
            _suspended = suspended;
            if (suspended)
            {
                _music?.Pause();
                foreach (var sound in _sounds) sound.Pause();
            }
            else
            {
                _music?.Resume();
                foreach (var sound in _sounds) sound.Resume();
            }
        }
    }
    volatile bool _requestStop;

    public bool Enabled { get; set; }
    public string? Playing { get; private set; }

    bool isMuted = true;
    public bool IsMuted
    {
        get => isMuted;
        set { isMuted = value; ApplyVolume(); }
    }

    float _volume = 0;
    public float Volume
    {
        get => _volume;
        set { _volume = value; ApplyVolume(); }
    }

    float _sceneVolume = 1;
    public float SceneVolume
    {
        get => _sceneVolume;
        set { _sceneVolume = value; ApplyVolume(); }
    }

    void ApplyVolume()
    {
        if (_music is not null)
            _music.Volume = isMuted ? 0 :
                _volume * _sceneVolume * _transitionVolume;
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
            _rememberedMusic?.Dispose();
            _rememberedMusic = null;
            _rememberPlaylistOnTransition = false;
        }
    }

    public void RememberCurrentSong()
    {
        lock (this)
        {
            _rememberedMusic?.Dispose();
            _rememberedMusic = null;
            _rememberedSong = null;

            _rememberedMusicRepeat = _repeat;
            _rememberedMusicWasMapPlaylist = _isMapPlaylistPlayback;
            if (_music is not null && Playing is not null)
            {
                _music.Pause();
                _rememberedMusic = _music;
                _rememberedSong = Playing;
                _music = null;
                Playing = null;
            }
            else if (_playlist.Count > 0)
            {
                // Preserve a song that has been queued but not started yet.
                _rememberedSong = _playlist[0];
            }

            _playlist.Clear();
            _isMapPlaylistPlayback = false;
        }
    }

    public void ResumeRememberedSong()
    {
        lock (this)
        {
            if (!Enabled)
            {
                _rememberedMusic?.Dispose();
                _rememberedMusic = null;
                _rememberedSong = null;
                return;
            }

            if (_rememberedMusic is not null && _rememberedSong is not null &&
                CanPlay(_rememberedSong))
            {
                _playlist.Clear();
                _music?.Dispose();
                _music = _rememberedMusic;
                Playing = _rememberedSong;
                _repeat = _rememberedMusicRepeat;
                _isMapPlaylistPlayback = _rememberedMusicWasMapPlaylist;
                _rememberedMusic = null;
                _rememberedSong = null;
                ApplyVolume();
                _music.Resume();
                return;
            }

            _rememberedMusic?.Dispose();
            _rememberedMusic = null;
            if (_rememberedSong is not null && CanPlay(_rememberedSong))
            {
                _playlist.Clear();
                _playlist.Add(_rememberedSong);
                _repeat = _rememberedMusicRepeat;
                _isMapPlaylistPlayback = _rememberedMusicWasMapPlaylist;
            }
            _rememberedSong = null;
        }
    }

    public void DiscardRememberedSong()
    {
        lock (this)
        {
            _rememberedSong = null;
            _rememberedMusic?.Dispose();
            _rememberedMusic = null;
        }
    }

    public void RememberPlaylistSong()
    {
        lock (this)
        {
            if (_isMapPlaylistPlayback && _music is not null && Playing is not null)
            {
                _rememberedPlaylistMusic?.Dispose();
                _rememberedPlaylistMusic = null;
                _rememberedPlaylistSong = Playing;
                _playlist.Clear();
                _rememberPlaylistOnTransition = true;
                return;
            }

            if (_pendingRequest is { IsMapPlaylist: true, Song: not null } pending)
            {
                _rememberedPlaylistSong = pending.Song;
                _pendingRequest = null;
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
            _rememberPlaylistOnTransition = false;
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
                if (_pendingRequest is { Kind: RequestKind.Play } pending)
                    _pendingRequest = pending with
                    {
                        Repeat = false,
                        IsMapPlaylist = true
                    };
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

        lock (this)
        {
            _continueWithMapPlaylist = true;
            if (_rememberPlaylistOnTransition && _music is not null &&
                _isMapPlaylistPlayback)
            {
                _rememberPlaylistOnTransition = false;
                _pendingRequest = null;
                return;
            }
            if (_rememberedPlaylistMusic is not null &&
                _rememberedPlaylistSong is not null &&
                CanPlay(_rememberedPlaylistSong))
            {
                _playlist.Clear();
                _pendingRequest = new(RequestKind.ResumePlaylist,
                    IsMapPlaylist: true);
                return;
            }

            _rememberedPlaylistMusic?.Dispose();
            _rememberedPlaylistMusic = null;
            if (_rememberedPlaylistSong is not null &&
                CanPlay(_rememberedPlaylistSong))
            {
                RequestPlayback(new(RequestKind.Play, _rememberedPlaylistSong,
                    IsMapPlaylist: true));
                _rememberedPlaylistSong = null;
                return;
            }
            _rememberedPlaylistSong = null;
            RequestPlayback(new(RequestKind.Play, PickPlaylistSong(),
                IsMapPlaylist: true));
        }
    }

    string PickPlaylistSong()
    {
        string song;
        do
        {
            song = _mapPlaylist[Random.Shared.Next(_mapPlaylist.Count)];
        }
        while (_mapPlaylist.Count > 1 && song == _lastPlaylistSong);
        _lastPlaylistSong = song;
        return song;
    }

    void QueueRandomPlaylistSong()
    {
        if (!Enabled || _mapPlaylist.Count == 0)
            return;

        string song = PickPlaylistSong();
        _playlist.Clear();
        _repeat = false;
        _playlist.Add(song);
        _isMapPlaylistPlayback = true;
    }

    public bool CanPlay(string songName)
    {
        if (SongFilesExist(songName))
            return true;

        if (_songMapping.TryGetValue(songName, out string? songFilePath))
            return SongFilesExist(songFilePath);

        return false;
    }

    static bool SongFilesExist(string fileName)
    {
        string[] files = fileName.Split(':');
        if (files.Length > 2)
            return false;

        foreach (string file in files)
        {
            if (string.IsNullOrWhiteSpace(file) || !FileSystem.ExistsFile(file))
                return false;
        }

        return true;
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

        lock (this)
            RequestPlayback(new(RequestKind.Play, fileName, loop, isMapPlaylist));
    }

    void RequestPlayback(PlaybackRequest request)
    {
        _playlist.Clear();
        if (request.Kind == RequestKind.Play && _music is not null &&
            Playing == request.Song && _repeat == request.Repeat &&
            _isMapPlaylistPlayback == request.IsMapPlaylist)
        {
            // Only reuse identical playback. A scene's looping song must not
            // replace the finite map instance that will be resumed afterwards.
            _rememberPlaylistOnTransition = false;
            _pendingRequest = null;
            return;
        }
        _pendingRequest = request;
    }

    public void PlayOnce(string fileName) => Play(fileName, false);

    public void PlaySound(string fileName)
    {
        lock (this)
        {
            if (!Enabled || _suspended)
                return;

            Music.LoopableSong? sound = Music.LoopableSong.FromFileName(fileName);
            if (sound is null)
                return;

            sound.Volume = 1;
            sound.Play();
            _sounds.Add(sound);
        }
    }

    public void Stop()
    {
        lock (this)
        {
            _playlist.Clear();
            _pendingRequest = new(RequestKind.Stop);
        }
    }

    void StopImmediate()
    {
        _playlist.Clear();
        _pendingRequest = null;
        _music?.Dispose();
        _music = null;
        Playing = null;
        _isMapPlaylistPlayback = false;
        _transitionVolume = 1;
    }

    public void RunThread()
    {
        _requestStop = false;
        _musicThread = new Thread(new ThreadStart(MusicThread))
        {
            IsBackground = true,
            Name = "MusicPlayback"
        };
        _musicThread.Start();
    }

    public void StopThread()
    {
        Thread? thread = _musicThread;
        if (thread != null)
        {
            _requestStop = true;
            if (thread == Thread.CurrentThread || thread.Join(StopTimeout))
                _musicThread = null;
            else
            {
                Log.Warning("Timed out stopping the music thread.");
                return;
            }
        }

        lock (this)
            StopImmediate();
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

    bool UpdateTransition()
    {
        if (_pendingRequest.HasValue)
        {
            if (_music is not null && _transitionVolume > 0)
            {
                _transitionVolume = System.Math.Max(0,
                    _transitionVolume - TransitionStep);
                ApplyVolume();
                return true;
            }

            FinishCurrentTrack();
            PlaybackRequest request = _pendingRequest.Value;
            _pendingRequest = null;
            StartRequest(request);
            return true;
        }

        if (_music is not null && _transitionVolume < 1)
        {
            _transitionVolume = System.Math.Min(1,
                _transitionVolume + TransitionStep);
            ApplyVolume();
        }
        return false;
    }

    void FinishCurrentTrack()
    {
        if (_music is not null && _rememberPlaylistOnTransition &&
            _isMapPlaylistPlayback && Playing is not null)
        {
            _rememberedPlaylistMusic?.Dispose();
            _music.Pause();
            _rememberedPlaylistMusic = _music;
            _rememberedPlaylistSong = Playing;
        }
        else
        {
            _music?.Dispose();
        }

        _music = null;
        Playing = null;
        _isMapPlaylistPlayback = false;
        _rememberPlaylistOnTransition = false;
        _transitionVolume = 1;
    }

    void StartRequest(PlaybackRequest request)
    {
        if (request.Kind == RequestKind.Stop)
            return;

        if (request.Kind == RequestKind.ResumePlaylist)
        {
            if (_rememberedPlaylistMusic is not null &&
                _rememberedPlaylistSong is not null)
            {
                _music = _rememberedPlaylistMusic;
                Playing = _rememberedPlaylistSong;
                _rememberedPlaylistMusic = null;
                _rememberedPlaylistSong = null;
                _repeat = false;
                _isMapPlaylistPlayback = true;
                _transitionVolume = 0;
                ApplyVolume();
                _music.Resume();
            }
            return;
        }

        string? fileName = request.Song is null ? null : ResolveSong(request.Song);
        if (fileName is null)
            return;

        _repeat = request.Repeat;
        _isMapPlaylistPlayback = request.IsMapPlaylist;
        Playing = request.Song;
        _music = Music.LoopableSong.FromFileName(fileName, request.Repeat,
            fade: request.IsMapPlaylist && _continueWithMapPlaylist);
        if (_music is null)
        {
            Playing = null;
            _isMapPlaylistPlayback = false;
            return;
        }

        _transitionVolume = 0;
        ApplyVolume();
        _music.Play();
    }

    private void MusicThread()
    {
        while (!_requestStop)
        {
            Thread.Sleep(TransitionInterval);

#warning THREADING lock playlist
            lock (this)
            {
                if (_suspended) continue;
                for (int i = _sounds.Count - 1; i >= 0; i--)
                {
                    if (_sounds[i].IsPlaying)
                        continue;

                    _sounds[i].Dispose();
                    _sounds.RemoveAt(i);
                }

                if (UpdateTransition())
                    continue;

                if (_music == null)
                {
                    string? next = GetNextTitle();
                    if (next == null)
                        continue;

                    _music = Music.LoopableSong.FromFileName(next, _repeat,
                        fade: _isMapPlaylistPlayback && _continueWithMapPlaylist);
                    if (_music is null)
                        continue;

                    ApplyVolume();
                    _music.Play();
                }
                else if (!_music.IsPlaying)
                {
                    Playing = null;
                    _isMapPlaylistPlayback = false;
                    _music.Dispose();
                    _music = null;
                    if (_continueWithMapPlaylist)
                        QueueRandomPlaylistSong();
                }
            }
        }

        lock (this)
        {
            _music?.Dispose();
            _rememberedMusic?.Dispose();
            _rememberedMusic = null;
            _rememberedSong = null;
            _rememberedPlaylistMusic?.Dispose();
            _rememberedPlaylistMusic = null;
            _rememberedPlaylistSong = null;
            foreach (Music.LoopableSong sound in _sounds)
                sound.Dispose();
            _sounds.Clear();
        }
    }
}
