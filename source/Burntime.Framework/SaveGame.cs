using System.Collections.Generic;
using System.IO;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Framework;

public class SaveGame
{
    readonly Platform.IO.File? _file;
    public Stream? Stream => _file?.Stream;

    public string? Version { get; init; }
    public string? Game { get; init; }
    public bool IsValid { get; init; }

    public SaveGame(string filename)
    {
        _file = FileSystem.GetFile(filename);
        if (_file is null)
        {
            IsValid = false;
            return;
        }

        BinaryReader reader = new BinaryReader(_file);

        try
        {
            Game = reader.ReadString();
            Version = reader.ReadString();
        }
        catch
        {
            IsValid = false;
            return;
        }

        IsValid = true;
    }

    public SaveGame(string filename, string game, string version)
    {
        if (FileSystem.ExistsFile(filename))
            FileSystem.RemoveFile(filename);

        _file = FileSystem.CreateFile(filename);
        if (_file is null)
        {
            IsValid = false;
            return;
        }

        BinaryWriter writer = new BinaryWriter(_file);

        writer.Write(game);
        writer.Write(version);

        IsValid = true;
    }

    public Dictionary<string, string>? PeakInfo(IResourceManager resourceManager)
    {
        if (!IsValid || Stream is null) return null;

        try
        {
            var container = new States.StateManager(resourceManager);

            int player = Stream.ReadByte();
            var ids = new List<int>();
            for (int i = 0; i < player; i++)
                ids.Add(Stream.ReadByte());

            container.Load(Stream, saveHint: true);
            return container.Root.GetSaveHint();
        }
        catch
        {
            return null;
        }
    }

    public void Close()
    {
        if (_file == null)
            return;

        _file.Close();
    }
}
