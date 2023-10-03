using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform.Resource
{

    public interface IDataProcessor
    {
        DataObject Process(ResourceID ID, IResourceManager ResourceManager);
        string[] Names { get; }
    }

    public sealed class NullDataObject : DataObject
    {
        public NullDataObject(ResourceID ID, IResourceManager ResourceManager)
        {
            DataName = ID;
            this.ResourceManager = ResourceManager;
        }
    }

    /////////////////////////////////
    // data
    public class DataObject
    {
        public IResourceManager ResourceManager { get; set; }
        public String DataName;

        public virtual void PostProcess()
        {
        }

        //public void LoadData(String name)
        //{
        //    DataName = name;
        //    Load(name);
        //}

        //public abstract void Load(String name);
    }

    public interface IDataID
    {
        string Name { get; }
        IResourceManager ResMan { get; set; }
    }

    //[Serializable]
    //internal sealed class DataIDBase : IDataID
    //{
    //    string name;
    //    [NonSerialized]
    //    ResourceManager resMan;
    //    [NonSerialized]
    //    DataObject obj;

    //    // IDataID interface implementation
    //    String IDataID.Name
    //    {
    //        get { return name; }
    //    }

    //    ResourceManager IDataID.ResMan
    //    {
    //        get { return resMan; }
    //        set { resMan = value; }
    //    }
    //}

    [Serializable]
    public struct DataID<T> : IDataID where T : class
    {
        String name;
        [NonSerialized]
        IResourceManager resMan;
        [NonSerialized]
        DataObject obj;

        public T Object
        {
            get
            {
                if (name == null)
                    return null;
                if (obj == null)
                    obj = resMan.GetData(name);
                return (T)(obj as object);
            }
        }

        public DataID(DataObject obj)
        {
            if (!(obj is NullDataObject))
                this.obj = obj;
            else
                this.obj = null;
            name = obj.DataName;
            resMan = obj.ResourceManager;
        }

        // force to load object
        public void Touch()
        {
            // do nothing more than access the object
            T obj = Object;
        }

        public override string ToString()
        {
            return name;
        }

        public static implicit operator T(DataID<T> DataID)
        {
            return DataID.Object;
        }

        public static implicit operator DataID<T>(DataObject Data)
        {
            return new DataID<T>(Data);
        }

        public string Name
        {
            get { return name; }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (!(obj is DataID<T>))
                return false;
            DataID<T> right = (DataID<T>)obj;

            if (name == null && right.name == null)
                return true;
            else if (name == null || right.name == null)
                return false;

            return name.Equals(right.name, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return name.GetHashCode();
        }

        // IDataID interface implementation
        string IDataID.Name
        {
            get { return name; }
        }

        IResourceManager IDataID.ResMan
        {
            get { return resMan; }
            set { resMan = value; }
        }
    }
}
