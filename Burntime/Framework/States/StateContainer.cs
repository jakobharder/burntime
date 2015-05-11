/*
 *  Burntime Framework
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  contact: 
 *    juern - burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
 *  authors: 
 *    Juernjakob Harder - 原田ゆあん (yn.harada@gmail.com)
 * 
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Burntime.Platform.Resource;

namespace Burntime.Framework.States
{
    class SyncObjectComparer : IComparer<SyncObject>
    {
        public int Compare(SyncObject left, SyncObject right)
        {
            return left.Key.CompareTo(right.Key);
        }
    }

    struct SyncObject
    {
        public int Key;
        public SyncCode Code;

        public SyncObject(int key, SyncCode code)
        {
            Key = key;
            Code = code;
        }
    }

    public interface ITestIF
    {
    }

    class StateContainer
    {
        bool localAddressing;
        int nextKey;
        Dictionary<int, StateObject> objects = new Dictionary<int, StateObject>();

        StateManager stateManager;

        public int Count
        {
            get { return objects.Count; }
        }

        public Dictionary<int, StateObject>.KeyCollection Keys
        {
            get { return objects.Keys; }
        }

        public Dictionary<int, StateObject>.ValueCollection Values
        {
            get { return objects.Values; }
        }

        public List<int> GetKeyList()
        {
            List<int> keys = new List<int>();
            foreach (int key in objects.Keys)
                keys.Add(key);

            return keys;
        }

        public StateContainer(bool localAddressing, StateManager stateMan)
        {
            this.localAddressing = localAddressing;
            stateManager = stateMan;
            nextKey = 0;
        }

        public StateContainer Clone()
        {
            StateContainer container = new StateContainer(localAddressing, stateManager);
            
            container.nextKey = nextKey;
            foreach (StateObject obj in objects.Values)
                container.objects.Add(localAddressing ? obj.localID : obj.ID, obj.Clone());

            return container;
        }

        public int Add(StateObject obj)
        {
            if (localAddressing)
            {
                if (obj.localID == -1)
                {
                    obj.ID = -1;
                    obj.localID = nextKey;
                    objects.Add(nextKey, obj);
                    nextKey++;

                    return nextKey - 1;
                }

                objects.Add(obj.localID, obj);
                return obj.localID;
            }
            else if (obj.ID == -1)
            {
                obj.ID = nextKey;
                objects.Add(nextKey, obj);
                nextKey++;

                return nextKey - 1;
            }
            else
            {
                // update next key when loading from file
                if (obj.ID >= nextKey)
                    nextKey = obj.ID + 1;
            }

            objects.Add(obj.ID, obj);
            return obj.ID;
        }

        public void Add(StateContainer container, List<IStateReference> externalReferences)
        {
            if (localAddressing || !container.localAddressing)
                throw new InvalidCastException();

            Dictionary<int, int> localToGlobal = new Dictionary<int, int>();

            foreach (StateObject obj in container.objects.Values)
                localToGlobal.Add(obj.localID, Add(obj));

            Resolve(localToGlobal);

            for (int i = 0; i < externalReferences.Count; i++)
            {
                if (externalReferences[i].ID == -1)
                    externalReferences[i].ID = localToGlobal[externalReferences[i].localID];
            }
        }

        public void Remove(int key)
        {
            objects.Remove(key);
        }

        public void Remove(StateObject obj)
        {
        }

        public bool ContainsKey(int key)
        {
            return objects.ContainsKey(key);
        }

        public void Clear()
        {
            objects.Clear();
        }

        public StateObject this[int key]
        {
            get { return objects[key]; }
            set { objects[key] = value; }
        }

        public SyncObject[] Compare(StateContainer right)
        {
            List<SyncObject> changed = new List<SyncObject>();

            foreach (int key in right.objects.Keys)
            {
                if (!objects.ContainsKey(key))
                {
                    changed.Add(new SyncObject(key, SyncCode.Delete));
                    Burntime.Platform.Log.Debug("compare delete: " + key);
                }
                else
                {
                    if (!objects[key].CompareTo(right.objects[key]))
                    {
                        if (objects[key].ID != -1)
                            changed.Add(new SyncObject(objects[key].ID, SyncCode.Update));
                        else
                            throw new Exception("invalid ID");
                    }
                }
            }

            foreach (int key in objects.Keys)
            {
                if (!right.objects.ContainsKey(key))
                {
                    changed.Add(new SyncObject(key, SyncCode.New));
                    Burntime.Platform.Log.Debug("compare new: " + key);
                }
            }

            return changed.ToArray();
        }

        public void Resolve(Dictionary<int, int> localToGlobal)
        {
            List<int> keys = GetKeyList();
            foreach (int key in keys)
            {
                StateObject obj = objects[key];
                ResolveStateLinks(ref obj, localToGlobal);
                objects[key] = obj;
            }
        }

        public SyncObject[] CollectGarbage(StateObject root, List<IStateReference> externalReferences)
        {
            Dictionary<int, bool> mark = new Dictionary<int, bool>();
            foreach (int key in Keys)
                mark.Add(key, true);

            // mark
            Mark(root, mark);

            for (int i = 0; i < externalReferences.Count; i++)
                Mark(externalReferences[i].Object, mark);

            List<SyncObject> removeList = new List<SyncObject>();
            foreach (int key in Keys)
            {
                if (mark[key])
                    removeList.Add(new SyncObject(key, SyncCode.Delete));
            }

            // and sweep
            foreach (SyncObject sync in removeList)
            {
                objects.Remove(sync.Key);
                Burntime.Platform.Log.Debug("gc object " + sync.Key);
            }

            return removeList.ToArray();
        }

        void Mark(StateObject obj, Dictionary<int, bool> mark)
        {
            if (obj == null)
                throw new BurntimeLogicException();

            if (obj.ID == -1 || !mark[obj.ID])
                return;

            mark[obj.ID] = false;

            Type type = obj.GetType();

            FieldInfo[] infos = type.GetFields(BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo info in infos)
            {
                if (typeof(IStateLink).IsAssignableFrom(info.FieldType))
                {
                    IStateLink link = (IStateLink)info.GetValue(obj);
                    if (link == null)
                        continue;
                    Mark(link.Object, mark);
                }
                else if (typeof(IStateLinkList).IsAssignableFrom(info.FieldType))
                {
                    IStateLinkList list = (IStateLinkList)info.GetValue(obj);
                    if (list == null)
                        continue;
                    List<IStateLink> linkList = list.List;
                    for (int i = 0; i < linkList.Count; i++)
                        Mark(linkList[i].Object, mark);
                    list.List = linkList;
                }
                else if (!info.FieldType.IsValueType && info.FieldType != typeof(StateManager) &&
                    !info.FieldType.IsArray && !info.IsNotSerialized && info.FieldType != typeof(String))
                {
                    throw new Exception("StateObject members must be ValueType, StateLinks or NonSerialized.");
                }
            }
        }

        void ResolveStateLinks(ref StateObject obj, Dictionary<int, int> localToGlobal)
        {
            Type type = obj.GetType();

            FieldInfo[] infos = type.GetFields(BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo info in infos)
            {
                if (typeof(IStateLink).IsAssignableFrom(info.FieldType))
                {
                    IStateLink link = (IStateLink)info.GetValue(obj);
                    if (link == null)
                        continue;
                    if (link.ID == -1)
                    {
                        link.ID = localToGlobal[link.localID];
                        info.SetValue(obj, link);
                    }
                }
                else if (typeof(IStateLinkList).IsAssignableFrom(info.FieldType))
                {
                    IStateLinkList list = (IStateLinkList)info.GetValue(obj);
                    if (list == null)
                        continue;
                    List<IStateLink> linkList = list.List;
                    for (int i = 0; i < linkList.Count; i++)
                    {
                        if (linkList[i].ID == -1)
                        {
                            IStateLink link = linkList[i];
                            link.ID = localToGlobal[linkList[i].localID];
                            linkList[i] = link;
                        }
                    }
                    list.List = linkList;
                }
                else if (!info.FieldType.IsValueType && info.FieldType != typeof(StateManager) && 
                    !info.FieldType.IsArray && !info.IsNotSerialized && info.FieldType != typeof(String) &&
                    info.FieldType.IsSubclassOf(typeof(EventHandler)))
                {
                     throw new Exception("StateObject members must be ValueType, StateLinks or NonSerialized.");
                }
            }
        }

        StateObject GetClone(StateObject obj)
        {
            //StateFormatter bf = new StateFormatter();

            //Stream stream = new MemoryStream();
            //bf.Serialize(stream, obj);

            //stream.Seek(0, SeekOrigin.Begin);

            //StateObject clone = (StateObject)bf.Deserialize(stream);
            //clone.AfterDeserialization();

            //return clone;

            StateObject clone = obj.Clone();
            return clone;
        }

        public void CheckConsistency(StateManager container)
        {
            foreach (StateObject obj in objects.Values)
            {
                CheckConsistency(obj, container);
            }
        }

        internal static void CheckConsistency(StateObject obj, StateManager container)
        {
            if (obj == null)
                return;

            obj.container = container;

            Type type = obj.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                Type fieldtype = fields[i].FieldType;
                if (typeof(IDataID).IsAssignableFrom(fieldtype))
                {
                }
                else if (typeof(IStateLink).IsAssignableFrom(fieldtype))
                {
                    IStateLink t = (IStateLink)fields[i].GetValue(obj);
                    if (t != null)
                    {
                        if (t.Object == null)
                            throw new BurntimeLogicException();
                    }
                }
                else if (typeof(IStateLinkList).IsAssignableFrom(fieldtype))
                {
                    IStateLinkList t = (IStateLinkList)fields[i].GetValue(obj);
                    if (t != null)
                    {
                        for (int j = 0; j < t.List.Count; j++)
                        {
                            t.List[j].Container = container;
                            if (t.List[j].Object == null)
                                throw new BurntimeLogicException();
                        }
                    }
                }
                else if (!fieldtype.IsValueType && fieldtype != typeof(StateManager) &&
                    !fieldtype.IsArray && !fields[i].IsNotSerialized && fieldtype != typeof(String))
                {
                    throw new Exception("StateObject members must be ValueType, StateLinks or NonSerialized.");
                }
            }
        }

        internal static void SetManager(StateObject obj, StateManager container)
        {
            if (obj == null)
                return;

            obj.container = container;

            Type type = obj.GetType();
            FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                Type fieldtype = fields[i].FieldType;
                if (typeof(IDataID).IsAssignableFrom(fieldtype))
                {
                    IDataID data = (IDataID)fields[i].GetValue(obj);
                    data.ResMan = container.ResourceManager;
                    fields[i].SetValue(obj, data);
                }
                else if (fieldtype.IsArray)
                {
                    Array array = (Array)fields[i].GetValue(obj);
                    for (int j = 0; j < array.Length; j++)
                    {
                        IDataID item = array.GetValue(j) as IDataID;
                        if (item != null)
                        {
                            ((IDataID)item).ResMan = container.ResourceManager;
                            array.SetValue(item, j);
                        }
                    }
                    fields[i].SetValue(obj, array);
                }
                //else if (fieldtype.IsSubclassOf(typeof(StateObject)))
                //{
                //    StateObject t = (StateObject)fields[i].GetValue(obj);
                //    SetManager(t, container);
                //    fields[i].SetValue(obj, t);
                //}
                else if (typeof(IStateLink).IsAssignableFrom(fieldtype))
                {
                    IStateLink t = (IStateLink)fields[i].GetValue(obj);
                    if (t != null)
                    {

                        t.Container = container;
                        fields[i].SetValue(obj, t);
                    }
                }
                else if (typeof(IStateLinkList).IsAssignableFrom(fieldtype))
                {
                    IStateLinkList t = (IStateLinkList)fields[i].GetValue(obj);
                    if (t != null)
                    {
                        t.Container = container;
                        for (int j = 0; j < t.List.Count; j++)
                            t.List[j].Container = container;
                        fields[i].SetValue(obj, t);
                    }
                }
                else if (!fieldtype.IsValueType && fieldtype != typeof(StateManager) &&
                    !fieldtype.IsArray && !fields[i].IsNotSerialized && fieldtype != typeof(String))
                {
                    throw new Exception("StateObject members must be ValueType, StateLinks or NonSerialized.");
                }
            }
        }
    }
}