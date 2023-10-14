using System.Collections.Generic;

namespace Burntime.Framework.States
{
    class StateChangeRecord
    {
        Dictionary<int, SyncCode>[] changeRecord;

        public StateChangeRecord(int maximumTurns)
        {
            changeRecord = new Dictionary<int, SyncCode>[maximumTurns];
            for (int i = 0; i < changeRecord.Length; i++)
                changeRecord[i] = new Dictionary<int, SyncCode>();
        }

        public void Add(SyncObject[] syncObjects)
        {
            foreach (SyncObject syncObj in syncObjects)
            {
                // update or add in current record
                if (changeRecord[0].ContainsKey(syncObj.Key))
                    changeRecord[0][syncObj.Key] = syncObj.Code == SyncCode.Delete ? SyncCode.Delete : SyncCode.Update;
                else
                    changeRecord[0].Add(syncObj.Key, syncObj.Code);

                // remove from older records to avoid double updates
                for (int i = 1; i < changeRecord.Length; i++)
                {
                    if (changeRecord[i].ContainsKey(syncObj.Key))
                    {
                        changeRecord[i].Remove(syncObj.Key);
                        Burntime.Platform.Log.Debug("StateChangeRecord.Add: [removed from " + i + "] " + syncObj.Key);
                    }
                }

                Burntime.Platform.Log.Debug("StateChangeRecord.Add: " + syncObj.Key);
            }
        }

        public void Dequeue()
        {
            foreach (int key in changeRecord[changeRecord.Length - 1].Keys)
                Burntime.Platform.Log.Debug("StateChangeRecord.Dequeue: " + key);
            for (int i = changeRecord.Length - 2; i >= 0; i--)
                changeRecord[i + 1] = changeRecord[i];
            changeRecord[0] = new Dictionary<int, SyncCode>();
        }

        public List<SyncObject> GetSyncObjects(int turns)
        {
            List<SyncObject> changed = new List<SyncObject>();
            if (turns > changeRecord.Length)
                throw new BurntimeLogicException();

            for (int i = 0; i < turns; i++)
            {
                foreach (int key in changeRecord[i].Keys)
                {
                    changed.Add(new SyncObject(key, changeRecord[i][key]));
                    Burntime.Platform.Log.Debug("StateChangeRecord.GetSyncObjects: " + key);
                }
            }

            for (int i = turns; i < changeRecord.Length; i++)
            {
                foreach (int key in changeRecord[i].Keys)
                    Burntime.Platform.Log.Debug("StateChangeRecord.GetSyncObjects: [old] " + key);
            }

            return changed;
        }
    }
}
