using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;

namespace Burntime.Framework;

public class SaveGame
{
    readonly Platform.IO.File? _file;
    readonly long _contentPosition;
    readonly string? _destination;
    string? _temporary;
    bool _closed;
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
            _contentPosition = Stream.Position;
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
        FilePath destination = new(filename) { Package = "user" };
        _destination = destination.Path;
        _temporary = _destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            _file = FileSystem.CreateFile(_temporary);
            if (_file is null)
            {
                Close();
                return;
            }

            BinaryWriter writer = new BinaryWriter(_file);
            writer.Write(game);
            writer.Write(version);
            _contentPosition = Stream.Position;

            IsValid = true;
        }
        catch (Exception exception)
        {
            Burntime.Platform.Log.Warning($"Could not prepare save '{filename}': {exception.Message}");
            Close();
        }
    }

    // Only call after the complete game state has been serialized. Close alone
    // deliberately aborts a write, including when called from a finally block.
    public bool Commit()
    {
        if (!IsValid || _closed || _temporary == null || _destination == null)
            return false;

        try
        {
            if (Stream is FileStream fileStream)
                fileStream.Flush(flushToDisk: true);
            else
                Stream!.Flush();
            _file!.Close();
            _closed = true;
            if (!FileSystem.ReplaceFile(_temporary, _destination))
                return false;
            _temporary = null;
            return true;
        }
        catch (Exception exception)
        {
            Burntime.Platform.Log.Warning($"Could not commit save '{_destination}': {exception.Message}");
            return false;
        }
        finally
        {
            Close();
        }
    }

    public Dictionary<string, string>? PeakInfo(IResourceManager resourceManager, bool includeDetails = false)
    {
        if (!IsValid || Stream is null) return null;

        try
        {
            var container = new States.StateManager(resourceManager);

            Stream.Seek(_contentPosition, SeekOrigin.Begin);
            int player = Stream.ReadByte();
            var ids = new List<int>();
            for (int i = 0; i < player; i++)
                ids.Add(Stream.ReadByte());

            container.Load(Stream, saveHint: !includeDetails);
            Dictionary<string, string> hints = includeDetails
                ? container.Root.GetSaveDetails()
                : container.Root.GetSaveHint();
            if (includeDetails && ids.Count > 0)
            {
                States.PlayerState? firstPlayer = container.Root.Player
                    .FirstOrDefault(candidate => candidate.Index == ids[0]);
                if (firstPlayer is not null)
                    hints["player"] = firstPlayer.Name;
            }
            return hints;
        }
        catch
        {
            return null;
        }
    }

    public void Close()
    {
        try
        {
            if (!_closed)
                _file?.Close();
        }
        catch (Exception exception)
        {
            Burntime.Platform.Log.Warning($"Could not close save: {exception.Message}");
        }
        finally
        {
            _closed = true;
            if (_temporary != null)
            {
                FileSystem.RemoveFile(_temporary);
                _temporary = null;
            }
        }
    }
}
