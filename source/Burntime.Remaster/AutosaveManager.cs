using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Remaster.Logic.Generation;
using System;

namespace Burntime.Remaster;

internal sealed class AutosaveManager
{
    public const int SlotCount = 2;

    const string SaveFolder = "saves/";
    const string FilePrefix = "autosave-";

    readonly BurntimeClassic _app;
    bool _suppressNextTravelAutosave;

    public AutosaveManager(BurntimeClassic app)
    {
        _app = app;
    }

    public static bool IsAutosave(string fileName)
    {
        string name = System.IO.Path.GetFileName(fileName);
        for (int slot = 0; slot < SlotCount; slot++)
        {
            if (name.Equals(GetFileName(slot), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public void OnNewGameCreated()
    {
        _suppressNextTravelAutosave = false;
    }

    public void OnGameLoaded(string fileName)
    {
        _suppressNextTravelAutosave = IsAutosave(fileName);
    }

    public void SaveBeforeTravel()
    {
        if (_suppressNextTravelAutosave)
        {
            _suppressNextTravelAutosave = false;
            return;
        }

        string fileName = GetOldestSlotFileName();
        try
        {
            var creation = new GameCreation(_app);
            creation.SaveGame(SaveFolder + fileName);
        }
        catch (Exception exception)
        {
            // An autosave failure must not prevent the confirmed travel.
            Log.Warning($"Could not create autosave '{fileName}': {exception.Message}");
        }
    }

    static string GetOldestSlotFileName()
    {
        string oldestFile = GetFileName(0);
        DateTime oldestTime = GetLastWriteTimeUtc(oldestFile);

        for (int slot = 1; slot < SlotCount; slot++)
        {
            string candidate = GetFileName(slot);
            DateTime candidateTime = GetLastWriteTimeUtc(candidate);
            if (candidateTime < oldestTime)
            {
                oldestFile = candidate;
                oldestTime = candidateTime;
            }
        }

        return oldestFile;
    }

    static DateTime GetLastWriteTimeUtc(string fileName) =>
        FileSystem.GetLastWriteTimeUtc(SaveFolder + fileName) ?? DateTime.MinValue;

    static string GetFileName(int slot) => $"{FilePrefix}{slot + 1}.sav";
}
