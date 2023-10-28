namespace Burntime.Platform;

public interface IMusic
{
    bool Enabled { get; set; }
    bool IsMuted { get; set; }
    float Volume { get; set; }

    void Play(string song, bool loop = true);
    void PlayOnce(string sound);
    void Stop();

    void LoadSonglist(string filePath);
    ICollection<string> Songlist { get; }
}
