using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Reflection;

using Burntime.Platform;
using Burntime.Platform.Resource;

namespace Burntime.Framework
{
    //public class Factory
    //{
    //    public static String DefaultNamespace = "Burntime.";
    //    static Dictionary<int, StateObject> list = new Dictionary<int, StateObject>();
    //    static int nextID = 0;
    //    static Dictionary<String, Type> loader = new Dictionary<string, Type>();

    //    public static ResourceManager ResourceManager;

    //    public static void AddLoader(String extension, Type loader)
    //    {
    //        Factory.loader[extension] = loader;
    //    }

    //    public static void Save(StateObject state, Stream stream)
    //    {
    //        BinaryFormatter bf = new BinaryFormatter();
    //        bf.Serialize(stream, state);
    //        bf.Serialize(stream, nextID);
    //    }

    //    public static StateObject Load(Stream stream)
    //    {
    //        nextID = 0;
    //        list.Clear();

    //        BinaryFormatter bf = new BinaryFormatter();
    //        bf.Binder = new GenericSerializationBinder();
    //        StateObject obj = (StateObject)bf.Deserialize(stream);
    //        DeserializeRecursive(obj);
    //        nextID = (int)bf.Deserialize(stream);

    //        SetResourceManager(obj, ResourceManager);

    //        return obj;
    //    }

    //    static void SetResourceManager(StateObject Object, ResourceManager ResourceManager)
    //    {
    //        if (Object == null)
    //            return;

    //        Type type = Object.GetType();
    //        FieldInfo[] fields = type.GetFields();
    //        for (int i = 0; i < fields.Length; i++)
    //        {
    //            Type fieldtype = fields[i].FieldType;
    //            if (fieldtype.IsSubclassOf(typeof(DataIDBase)))
    //            {
    //                DataIDBase data = (DataIDBase)fields[i].GetValue(Object);
    //                data.resMan = ResourceManager;
    //                fields[i].SetValue(Object, data);
    //            }
    //            else if (fieldtype.IsSubclassOf(typeof(StateObject)))
    //            {
    //                StateObject obj = (StateObject)fields[i].GetValue(Object);
    //                SetResourceManager(obj, ResourceManager);
    //                fields[i].SetValue(Object, obj);
    //            }
    //        }
    //    }

    //    internal sealed class GenericSerializationBinder : System.Runtime.Serialization.SerializationBinder
    //    {
    //        public override Type BindToType(string assemblyName, string typeName)
    //        {
    //            Type typeToDeserialize = null;
               
    //            List<Type> tmpTypes = new List<Type>();
    //            Type genType = null;
 
    //            try
    //            {
    //                if (typeName.Contains("System.Collections.Generic") && typeName.Contains("[["))
    //                {
    //                    string[] splitTyps = typeName.Split(new char[] { '[' });
 
    //                    foreach (string typ in splitTyps)
    //                    {
    //                        if (typ.Contains("Version"))
    //                        {
    //                            string asmTmp = typ.Substring(typ.IndexOf(',') + 1);
    //                            string asmName = asmTmp.Remove(asmTmp.IndexOf(']')).Trim();
    //                            string typName = typ.Remove(typ.IndexOf(','));
    //                            tmpTypes.Add(BindToType(asmName, typName));
    //                        }
    //                        else if (typ.Contains("Generic"))
    //                        {
    //                            genType = BindToType(assemblyName, typ);
    //                        }
    //                    }
    //                    if( genType != null && tmpTypes.Count > 0)
    //                    {
    //                        return genType.MakeGenericType(tmpTypes.ToArray());
    //                    }
    //                }
 
    //                string ToAssemblyName = assemblyName.Split(',')[0];
    //                Assembly[] Assemblies = AppDomain.CurrentDomain.GetAssemblies();
    //                foreach (Assembly asm in Assemblies)
    //                {
    //                    if (asm.FullName.Split(',')[0] == ToAssemblyName)
    //                    {
    //                        typeToDeserialize = asm.GetType(typeName);
    //                        break;
    //                    }
    //                }
 
    //            }
    //            catch (System.Exception exception)
    //            {
    //                throw exception;
    //            }
    //            return typeToDeserialize;
    //        }
    //    }

    //    static StateObject DeserializeRecursive(StateObject obj)
    //    {
    //        //Type type = obj.GetType();
    //        //FieldInfo[] fields = type.GetFields();
    //        //foreach (FieldInfo f in fields)
    //        //{
    //        //    if (f.FieldType.IsSubclassOf(typeof(StateObject)))
    //        //    {
    //        //        if (f.GetValue(obj) != null)
    //        //            DeserializeRecursive((StateObject)f.GetValue(obj));
    //        //    }
    //        //}

    //        //list[obj.ID] = obj;
    //        return obj;
    //    }


    //    public static LogicObject GetLogic(Type type)
    //    {
    //        return (LogicObject)System.Activator.CreateInstance(type);
    //    }

    //    public static LogicObject GetLogic(String name)
    //    {
    //        Type type;
    //        int pos = name.IndexOf('?');
    //        if (pos >= 0)
    //        {
    //            type = Type.GetType(DefaultNamespace + name.Substring(0, pos));
    //            LogicObject obj = (LogicObject)System.Activator.CreateInstance(type);
    //            obj.MethodInfo = type.GetMethod(name.Substring(pos + 1));
    //            return obj;
    //        }

    //        type = Type.GetType(DefaultNamespace + name);
    //        return GetLogic(type);
    //    }

    //    public static StateObject GetState(Type type)
    //    {
    //        StateObject state = (StateObject)System.Activator.CreateInstance(type);
    //        //state.ID = nextID;
    //        //nextID++;
    //        //list[state.ID] = state;
    //        return state;
    //    }

    //    public static StateObject GetState(String name)
    //    {
    //        Type type = Type.GetType(DefaultNamespace + name);
    //        return GetState(type);
    //    }

    //    public static StateObject GetState(int ID)
    //    {
    //        return list[ID];
    //    }

    //    //public static DataObject GetData(Type type)
    //    //{
    //    //    return (DataObject)System.Activator.CreateInstance(type);
    //    //}

    //    //public static DataObject GetData(String name)
    //    //{
    //    //    string[] fmtsplit = name.Split(new char[] { '@' });

    //    //    string format = "";

    //    //    if (fmtsplit.Length == 2)
    //    //    {
    //    //        format = fmtsplit[0];
    //    //        name = fmtsplit[1];
    //    //    }

    //    //    if (format == "")
    //    //    {
    //    //        string[] split = name.Split(new char[] { '?' });
    //    //        int pos = split[0].LastIndexOf('.');
    //    //        format = split[0].Substring(pos + 1);
    //    //    }

    //    //    DataObject obj = GetData(loader[format], );
    //    //    obj.Load(name);
    //    //    return obj;
    //    //}

    //    //public static DataObject GetData(String typename, String name)
    //    //{
    //    //    Type type = Type.GetType(DefaultNamespace + typename);
    //    //    DataObject obj = GetData(type);
    //    //    obj.Load(name);
    //    //    return obj;
    //    //}
    //}

    //public class ObjectContainer
    //{
    //    List<BurnObject> list = new List<BurnObject>();
    //    List<LogicObject> logic = new List<LogicObject>();
    //    public static StateObject GlobalState = null;

    //    public void Serialize(StateObject state, Stream stream)
    //    {
    //        BinaryFormatter bf = new BinaryFormatter();
    //        bf.Serialize(stream, state);
    //    }

    //    public void Deserialize(Stream stream)
    //    {
    //        list.Clear();
    //        logic.Clear();

    //        BinaryFormatter bf = new BinaryFormatter();
    //        StateObject obj = (StateObject)bf.Deserialize(stream);
    //        DeserializeRelink(obj);
    //    }

    //    StateObject DeserializeRelink(StateObject obj)
    //    {
    //        list.Add(obj);

    //        Type type = obj.GetType();
    //        FieldInfo[] fields = type.GetFields();
    //        foreach (FieldInfo f in fields)
    //        {
    //            if (f.FieldType.IsSubclassOf(Type.GetType("Framework.StateObject")))
    //            {
    //                if (f.GetValue(obj) != null)
    //                    DeserializeRelink((StateObject)f.GetValue(obj));
    //            }
    //        }

    //        return obj;
    //    }

    //    //public StateObject AddState(String name)
    //    //{
    //    //    Type t = Type.GetType(Factory.DefaultNamespace + name);
    //    //    StateObject obj = (StateObject)System.Activator.CreateInstance(t);
    //    //    obj.ID = 0;
    //    //    return AddState(obj);
    //    //}

    //    //public StateObject AddState(StateObject obj)
    //    //{
    //    //    list.Add(obj);
    //    //    obj.Container = this;
    //    //    return obj;
    //    //}

    //    //public DataID AddData(String type, String name)
    //    //{
    //    //    Type t = Type.GetType(Factory.DefaultNamespace + type);
    //    //    DataObject obj = (DataObject)System.Activator.CreateInstance(t);
    //    //    obj.LoadData(name);
    //    //    return new DataID(obj);
    //    //}

    //    public void RunLogic(String name, StateObject state)
    //    {
    //        Factory.GetLogic(name).Run(state);
    //    }
    //}

    [Serializable]
    public class BurnObject
    {
        //[NonSerialized]
        //public ObjectContainer Container;
    }

    ///////////////////////////////////
    //// states
    //[Serializable]
    //public class StateObject : BurnObject
    //{
    //    protected StateObject()
    //    {
    //    }

    //    public static implicit operator StateObject(String name)
    //    {
    //        return Factory.GetState(name);
    //    }
    //}

    //[Serializable]
    //public class StateLink<TState> : BurnObject
    //{
    //    [NonSerialized]
    //    TState link;
    //    public TState Link
    //    {
    //        get
    //        {
    //            if (link == null)
    //                link = (TState)(object)Factory.GetState(LinkID);
    //            return link;
    //        }
    //    }

    //    public StateLink(TState state)
    //    {
    //        link = state;
    //        //LinkID = ((StateObject)(object)state).ID;
    //    }

    //    public static implicit operator TState(StateLink<TState> link)
    //    {
    //        return link.Link;
    //    }

    //    public static implicit operator StateLink<TState>(TState state)
    //    {
    //        return new StateLink<TState>(state);
    //    }

    //    public int LinkID;
    //}

    //[Serializable]
    //public class StateList<TState> : StateObject
    //{
    //    List<TState> list = new List<TState>();
    //    //StateObject Parent;

    //    //public StateList<TState>(StateObject )

    //    public static StateList<TState> operator -(StateList<TState> list, TState state)
    //    {
    //        //((StateObject)state).Parent = null;
    //        list.list.Remove(state);
    //        return list;
    //    }

    //    public static StateList<TState> operator +(StateList<TState> list, TState state)
    //    {
    //        if (list == null)
    //            list = new StateList<TState>();
    //        list.list.Add(state);
    //        //((StateObject)state).Parent = Parent.Parent;
    //        return list;
    //    }

    //    public TState this[int index]
    //    {
    //        get { return list[index]; }
    //        set { list[index] = value; }
    //    }

    //    public int Count
    //    {
    //        get
    //        {
    //            if (list == null)
    //                return 0;
    //            return list.Count;
    //        }
    //    }

    //    public TState Last
    //    {
    //        get { return list[list.Count - 1]; }
    //        set { list[list.Count - 1] = value; }
    //    }
    //}

    /////////////////////////
    //// logic
    //public delegate void LogicMethod();
    //public delegate void LogicMethodParam(StateObject state);

    //public class LogicObject : BurnObject
    //{
    //    public LogicMethodParam RunMethod;
    //    public LogicMethod RunMethod2;
    //    public MethodInfo MethodInfo;

    //    public virtual void Run(StateObject state)
    //    {
    //        if (RunMethod != null)
    //            RunMethod(state);
    //        if (RunMethod2 != null)
    //            RunMethod2();
    //        if (MethodInfo != null)
    //            MethodInfo.Invoke(this, new object[] { (StateObject)state });
    //    }

    //    public void Run()
    //    {
    //        //Run(ObjectContainer.GlobalState);
    //    }

    //    public static implicit operator LogicID(LogicObject obj)
    //    {
    //        return new LogicID(obj);
    //    }

    //    public static implicit operator LogicObject(String name)
    //    {
    //        return Factory.GetLogic(name);
    //    }

    //    public static implicit operator LogicObject(LogicMethod method)
    //    {
    //        LogicObject obj = new LogicObject();
    //        obj.RunMethod2 = method;
    //        return obj;
    //    }
    //}

    //[Serializable]
    //public class LogicID
    //{
    //    public Type Type;

    //    [NonSerialized]
    //    protected LogicObject obj;

    //    public LogicObject Object
    //    {
    //        get
    //        {
    //            if (obj == null)
    //                obj = (LogicObject)System.Activator.CreateInstance(Type);
    //            return obj;
    //        }
    //    }

    //    public LogicID(BurnObject obj)
    //    {
    //        this.obj = (LogicObject)obj;
    //        this.Type = obj.GetType();
    //    }

    //    public override string ToString()
    //    {
    //        return Type.FullName;
    //    }

    //    public static implicit operator LogicID(Type type)
    //    {
    //        return new LogicID(Factory.GetLogic(type));
    //    }

    //    public static implicit operator LogicID(String name)
    //    {
    //        return new LogicID(Factory.GetLogic(name));
    //    }
    //}

    //public class LogicChain
    //{
    //    List<LogicObject> list = new List<LogicObject>();

    //    public static LogicChain operator +(LogicChain chain, LogicObject obj)
    //    {
    //        if (chain == null)
    //            chain = new LogicChain();
    //        chain.list.Add(obj);
    //        return chain;
    //    }

    //    public void Run(StateObject state)
    //    {
    //        foreach (LogicObject obj in list)
    //        {
    //            obj.Run(state);
    //        }
    //    }

    //    public void Run()
    //    {
    //        foreach (LogicObject obj in list)
    //        {
    //            obj.Run();
    //        }
    //    }
    //}

    //[Serializable]
    //public class LogicIDChain
    //{
    //    List<LogicID> list = new List<LogicID>();

    //    public static LogicIDChain operator +(LogicIDChain chain, LogicID obj)
    //    {
    //        if (chain == null)
    //            chain = new LogicIDChain();
    //        chain.list.Add(obj);
    //        return chain;
    //    }

    //    public void Run(StateObject state)
    //    {
    //        foreach (LogicID id in list)
    //        {
    //            id.Object.Run(state);
    //        }
    //    }

    //    public void Run()
    //    {
    //        foreach (LogicID id in list)
    //        {
    //            id.Object.Run();
    //        }
    //    }
    //}

    ///////////////////////////
    //// default logic
    //public class Hotkey : LogicObject
    //{
    //    public LogicObject Logic;
    //    public char Key;

    //    public static char Escape = (char)27;

    //    public Hotkey(char key, LogicObject logic)
    //    {
    //        Key = key;
    //        Logic = logic;
    //    }

    //    public override void Run(StateObject state)
    //    {
    //        // if key
    //        Logic.Run(state);
    //    }
    //}


    //public class ConfigSet : Dictionary<String, String>
    //{
    //    public int GetInt(String name)
    //    {
    //        int res = 0;
    //        int.TryParse(this[name], out res);
    //        return res;
    //    }
    //}

}
