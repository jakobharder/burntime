namespace Burntime.Platform;

public interface IMusic
{
    bool Enabled { get; set; }
    bool IsMuted { get; set; }
    float Volume { get; set; }
    string? Playing { get; }

    bool CanPlay(string songName);
    string? ResolveSong(string songName);
    void Play(string song, bool loop = true);
    void PlayOnce(string sound);
    void PlaySound(string sound);
    void Stop();

    void LoadSonglist(string filePath);
    void LoadPlaylist(string filePath);
    void RememberPlaylistSong();
    void DiscardRememberedPlaylistSong();
    void SetPlaylistContinuation(bool enabled);
    void PlayPlaylist();
    ICollection<string> Songlist { get; }
    bool IsPlayingFromPlaylist { get; }
}
