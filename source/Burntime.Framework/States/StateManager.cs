using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Burntime.Platform.Resource;

namespace Burntime.Framework.States
{
    public enum SyncCode
    {
        New,
        NewLocal,
        Delete,
        Update,
        RootId,
        End
    }

    public enum StateObjectOptions
    {
        None = 0,
        Temporary = 1,
        PlayerRelative = 2
    }

    public class StateManager
    {
        string debugName;

        StateContainer objects;
        StateContainer syncCopy;
        StateContainer added;

        StateChangeRecord changeRecord = new StateChangeRecord(10);

        IResourceManager resourceManager;
        bool temporaryMode = false;
        WorldState root;
        bool manageGlobalAddressing;

        List<IStateReference> externalReferences = new List<IStateReference>();

        // notifycation objects
        List<ILogicNotifycationHandler> handlerList = new List<ILogicNotifycationHandler>();

        public bool ServerStateManager
        {
            get { return manageGlobalAddressing; }
        }

        public IResourceManager ResourceManager
        {
            get { return resourceManager; }
        }

        public int ObjectCount
        {
            get { return objects.Count; }
        }

        // main state object access
        public WorldState Root
        {
            get { return root; }
            set { root = value; }
        }

        protected void UpdateDebugInfo()
        {
#warning TODO SlimDX/Mono debug info
            //Burntime.Platform.Debug.SetInfo("[" + debugName + "] state objects", objects.Count.ToString());
            //Burntime.Platform.Debug.SetInfo("[" + debugName + "] state objects [add]", added.Count.ToString());
            //Burntime.Platform.Debug.SetInfo("[" + debugName + "] state objects [sync]", syncCopy.Count.ToString());
        }

        public StateManager(IResourceManager resourceManager, bool manageGlobalAddressing)
        {
            this.resourceManager = resourceManager;
            this.debugName = "main";
            this.manageGlobalAddressing = manageGlobalAddressing;

            objects = new StateContainer(false, this);
            syncCopy = new StateContainer(false, this);
            added = new StateContainer(true, this);

            UpdateDebugInfo();
        }

        public StateManager(IResourceManager resourceManager)
        {
            this.resourceManager = resourceManager;
            this.debugName = "main";
            this.manageGlobalAddressing = true;

            objects = new StateContainer(false, this);
            syncCopy = new StateContainer(false, this);
            added = new StateContainer(true, this);

            UpdateDebugInfo();
        }

        public StateManager(IResourceManager resourceManager, string debugName)
        {
            this.resourceManager = resourceManager;
            this.debugName = debugName;
            this.manageGlobalAddressing = false;

            objects = new StateContainer(false, this);
            syncCopy = new StateContainer(false, this);
            added = new StateContainer(true, this);

            UpdateDebugInfo();
        }

        public StateManager(IResourceManager resourceManager, bool manageGlobalAddressing, string debugName)
        {
            this.resourceManager = resourceManager;
            this.debugName = debugName;
            this.manageGlobalAddressing = manageGlobalAddressing;

            objects = new StateContainer(false, this);
            syncCopy = new StateContainer(false, this);
            added = new StateContainer(true, this);

            UpdateDebugInfo();
        }

        public T Create<T>() where T : StateObject
        {
            return Create<T>(StateObjectOptions.None, null);
        }

        public T Create<T>(params object[] parameter) where T : StateObject
        {
            return Create<T>(StateObjectOptions.None, parameter);
        }

        public T Create<T>(StateObjectOptions options) where T : StateObject
        {
            return Create<T>(options, null);
        }

        public T Create<T>(StateObjectOptions options, params object[] parameter) where T : StateObject
        {
            if ((options & StateObjectOptions.Temporary) == StateObjectOptions.Temporary &&
                (options & StateObjectOptions.PlayerRelative) == StateObjectOptions.PlayerRelative)
            {
                throw new BurntimeLogicException();
            }

            if ((options & StateObjectOptions.PlayerRelative) == StateObjectOptions.PlayerRelative)
            {
                PlayerRelativeStateLink<T> state = new PlayerRelativeStateLink<T>();
                added.Add(state);

                for (int i = 0; i < Root.Player.Length; i++)
                {
                    T obj = (T)Activator.CreateInstance(typeof(T));
                    obj.container = this;
                    added.Add(obj);
                    state.Add(obj);
                }

                UpdateDebugInfo();

                return state;
            }
            else
            {
                StateObject obj = (StateObject)Activator.CreateInstance(typeof(T));
                obj.container = this;
                if ((options & StateObjectOptions.Temporary) != StateObjectOptions.Temporary && !temporaryMode)
                {
                    added.Add(obj);
                    UpdateDebugInfo();
                }

                bool old = temporaryMode;
                temporaryMode |= (options & StateObjectOptions.Temporary) == StateObjectOptions.Temporary;
                obj.InitInstance(parameter);
                temporaryMode = old;
                return (T)obj;
            }
        }

        public StateLinkList<T> CreateLinkList<T>() where T : StateObject
        {
            StateLinkList<T> linkList = new StateLinkList<T>();
            linkList.Container = this;
            return linkList;
        }

        public StateReference<T> GetReference<T>(StateObject obj) where T : StateObject
        {
            StateReference<T> reference = new StateReference<T>();
            IStateLink link = reference as IStateLink;
            link.Container = this;
            link.ID = obj.ID;
            link.localID = obj.localID;

            externalReferences.Add(reference);
            return reference;
        }

        public void RemoveReference(IStateReference reference)
        {
            externalReferences.Remove(reference);
        }

        public void Synchronize()
        {
            Synchronize(true);
        }

        public void Synchronize(bool resetChangeRecord)
        {
            if (resetChangeRecord)
            {
                if (added.Count != 0)
                    throw new BurntimeLogicException(); // DEBUG

                // swap change record
                changeRecord.Dequeue();

                // garbage collection
                changeRecord.Add(objects.CollectGarbage(root, externalReferences));

                // save back up
                syncCopy = objects.Clone();
            }

            objects.Add(added, externalReferences);
            added.Clear();

            if (!resetChangeRecord)
                changeRecord.Add(objects.Compare(syncCopy));

            UpdateDebugInfo();
        }

        public Stream GetAllStates()
        {
            Stream s = new MemoryStream();
            StateFormatter bf = new StateFormatter();

            bf.Serialize(s, SyncCode.RootId);
            bf.Serialize(s, root.ID);

            foreach (StateObject obj in objects.Values)
            {
                bf.Serialize(s, SyncCode.New);
                bf.Serialize(s, obj);
            }

            bf.Serialize(s, SyncCode.End);

            s.Seek(0, SeekOrigin.Begin);
            return s;
        }

        public Stream GetChanges(int turns)
        {
            List<SyncObject> changed;

            if (manageGlobalAddressing)
            {
                changed = changeRecord.GetSyncObjects(turns + 1);
            }
            else
            {
                changed = new List<SyncObject>();
                changed.AddRange(objects.Compare(syncCopy));
                foreach (int key in added.Keys)
                    changed.Add(new SyncObject(key, SyncCode.NewLocal));
            }

            Stream s = new MemoryStream();
            StateFormatter bf = new StateFormatter();

            foreach (SyncObject syncObj in changed)
            {
                bf.Serialize(s, syncObj.Code);

                switch (syncObj.Code)
                {
                    case SyncCode.NewLocal:
                        bf.Serialize(s, added[syncObj.Key]);
                        break;
                    case SyncCode.New:
                    case SyncCode.Update:
                        bf.Serialize(s, objects[syncObj.Key]);
                        break;
                    case SyncCode.Delete:
                        bf.Serialize(s, syncObj.Key);
                        break;
                    default:
                        throw new BurntimeLogicException();
                }
            }

            bf.Serialize(s, SyncCode.End);

            s.Seek(0, SeekOrigin.Begin);

            return s;
        }

        public void MonitorChanges()
        {
            syncCopy = objects.Clone();
        }

        void SetContainerLinks()
        {
            List<int> keys = new List<int>();
            foreach (int key in objects.Keys)
                keys.Add(key);

            foreach (int key in keys)
                SetContainerLinks(objects[key]);
        }

        void SetContainerLinks(StateObject obj)
        {
            StateContainer.SetManager(obj, this);
        }

        public void CheckConsistency()
        {
            objects.CheckConsistency(this);
        }

        public void UpdateIds(Stream buffer)
        {
            BinaryReader reader = new BinaryReader(buffer);
            int count = reader.ReadInt32();

            Dictionary<int, int> localToGlobal = new Dictionary<int, int>();

            for (int i = 0; i < count; i++)
            {
                int local = reader.ReadInt32();
                int global = reader.ReadInt32();
                localToGlobal.Add(local, global);

                added[local].ID = global;
            }

            objects.Add(added, externalReferences);
            added.Clear();

            List<int> keys = objects.GetKeyList();

            foreach (int key in keys)
            {
                StateObject obj = objects[key];
                ResolveStateLinks(ref obj, localToGlobal);
                objects[key] = obj;
            }

            UpdateDebugInfo();
        }

        public void Update(Stream sync)
        {
            StateFormatter bf = new StateFormatter();

            List<object> list = new List<object>();

            int rootId = -1;

            Burntime.Platform.Log.Debug("begin sync");

            int countNew = 0;
            int countUpdate = 0;
            int countDelete = 0;

            SyncCode code = (SyncCode)bf.Deserialize(sync);
            while (code != SyncCode.End)
            {
                switch (code)
                {
                    case SyncCode.New:
                        {
                            object obj = bf.Deserialize(sync);
                            list.Add(obj);
                            StateObject state = (StateObject)obj;
                            if (state.ID == -1)
                                throw new Exception("unvalid ID");

                            SetContainerLinks(state);
                            state.AfterDeserialization();

                            if (objects.ContainsKey(state.ID))
                                objects[state.ID] = state;
                            else
                                objects.Add(state);

                            countNew++;
                           // Burntime.Platform.Log.Debug("update new: " + state);
                        }
                        break;
                    case SyncCode.Update:
                        {
                            object obj = bf.Deserialize(sync);
                            list.Add(obj);
                            StateObject state = (StateObject)obj;
                            if (state.ID == -1)
                                throw new Exception("unvalid ID");

                            SetContainerLinks(state);
                            state.AfterDeserialization();
                            if (objects.ContainsKey(state.ID))
                                objects[state.ID] = state;
                            else
                            {
                                added.Remove(state.localID);
                                objects.Add(state);
                            }

                            countUpdate++;
                           // Burntime.Platform.Log.Debug("update update: " + state);
                        }
                        break;
                    case SyncCode.Delete:
                        {
                            int key = (int)bf.Deserialize(sync);
                            if (objects.ContainsKey(key))
                            {
                           //     Burntime.Platform.Log.Debug("update delete: " + objects[key]);
                                objects.Remove(key);
                            }

                            countDelete++;
                        }
                        break;
                    case SyncCode.RootId:
                        rootId = (int)bf.Deserialize(sync);
                        objects.Clear();
                        added.Clear();
                        syncCopy.Clear();
                        break;
                }

                code = (SyncCode)bf.Deserialize(sync);
            }

            if (rootId != -1)
                root = objects[rootId] as WorldState;

           // CheckConsistency(); // DEBUG
            UpdateDebugInfo();
        }

        public Stream UpdateMain(Stream Sync)
        {
            MemoryStream localToGlobalIdResult = new MemoryStream();

            StateFormatter bf = new StateFormatter();

            int rootId = -1;

            SyncCode code = (SyncCode)bf.Deserialize(Sync);
            while (code != SyncCode.End)
            {
                switch (code)
                {
                    case SyncCode.NewLocal:
                        {
                            object obj = bf.Deserialize(Sync);
                            StateObject state = (StateObject)obj;
                            state.ID = -1;
                            SetContainerLinks(state);
                            state.AfterDeserialization();
                            added.Add(state);
                        }
                        break;
                    case SyncCode.Update:
                        {
                            object obj = bf.Deserialize(Sync);
                            StateObject state = (StateObject)obj;
                            SetContainerLinks(state);
                            state.AfterDeserialization();
                            if (objects.ContainsKey(state.ID))
                                objects[state.ID] = state;
                            else
                            {
                                added.Remove(state.localID);
                                objects.Add(state);

                            }
                        }
                        break;
                    case SyncCode.RootId:
                        rootId = (int)bf.Deserialize(Sync);
                        objects.Clear();
                        added.Clear();
                        syncCopy.Clear();
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                code = (SyncCode)bf.Deserialize(Sync);
            }

            if (rootId != -1)
                root = objects[rootId] as WorldState;

            UpdateDebugInfo();

            ApplyIDs(localToGlobalIdResult);
            localToGlobalIdResult.Seek(0, SeekOrigin.Begin);
            return localToGlobalIdResult;
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
                        link.ID = added[link.localID].ID;
                    }
                    info.SetValue(obj, link);
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
                            link.ID = added[linkList[i].localID].ID;
                            linkList[i] = link;
                        }
                    }
                    list.List = linkList;
                }
            }
        }

        public void ApplyIDs(Stream localToGlobalIdResult)
        {
            localToGlobalIdResult.Write(BitConverter.GetBytes(added.Count), 0, 4);

            Dictionary<int, int> localToGlobal = new Dictionary<int, int>();

            foreach (StateObject obj in added.Values)
            {
                objects.Add(obj);

                localToGlobal.Add(obj.localID, obj.ID);

                localToGlobalIdResult.Write(BitConverter.GetBytes(obj.localID), 0, 4);
                localToGlobalIdResult.Write(BitConverter.GetBytes(obj.ID), 0, 4);
            }

            objects.Resolve(localToGlobal);
            added.Clear();

            UpdateDebugInfo();
        }

        public T GetObject<T>(int ID, int LocalID)
        {
            if (ID != -1)
                return (T)(objects[ID] as object);
            return (T)(added[LocalID] as object);
        }

        public StateObject GetObject(int id, int localId)
        {
            if (id != -1)
            {
                if (!objects.ContainsKey(id))
                {
                    Burntime.Platform.Log.Warning("state object not found in container " + debugName);
                    return null;
                }
                return objects[id];
            }

            if (!added.ContainsKey(localId))
            {
                Burntime.Platform.Log.Warning("state object not found in container " + debugName);
                return null;
            }
            return added[localId];
        }

        public void Save(Stream stream)
        {
            StateFormatter bf = new StateFormatter();

            bf.Serialize(stream, root.ID);
            foreach (StateObject state in objects.Values)
                bf.Serialize(stream, state);
            foreach (StateObject state in added.Values)
                bf.Serialize(stream, state);

            stream.Seek(0, SeekOrigin.Begin);
        }

        public void Load(Stream stream)
        {
            objects.Clear();
            Dictionary<int, int> localToGlobal = new Dictionary<int, int>();

            StateFormatter bf = new StateFormatter();
            int rootId = (int)bf.Deserialize(stream);

            while (stream.Position < stream.Length - 1)
            {
                StateObject obj = (StateObject)bf.Deserialize(stream);
                StateContainer.SetManager(obj, this);
                obj.AfterDeserialization();

                if (obj.ID == -1)
                    localToGlobal.Add(obj.localID, objects.Add(obj));
                else
                    objects.Add(obj);
            }

            root = objects[rootId] as WorldState;
            objects.Resolve(localToGlobal);

            UpdateDebugInfo();
        }

        // logic notification handling
        public void AddNotifycationHandler(ILogicNotifycationHandler handler)
        {
            lock (handlerList)
                handlerList.Add(handler);
        }

        public void RemoveNotifycationHandler(ILogicNotifycationHandler handler)
        {
            lock (handlerList)
                handlerList.Remove(handler);
        }

        public void Notify(ILogicNotifycation notify)
        {
            lock (handlerList)
            {
                foreach (ILogicNotifycationHandler handler in handlerList)
                {
                    handler.Handle(notify);
                }
            }
        }
    }
}