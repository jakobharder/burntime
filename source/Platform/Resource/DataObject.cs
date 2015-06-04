
#region The MIT License (MIT) - 2015 Jakob Harder
/*
 * The MIT License (MIT)
 * 
 * Copyright (c) 2015 Jakob Harder
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Platform.Resource
{

    public interface IDataProcessor
    {
        DataObject Process(ResourceID ID, ResourceManager ResourceManager);
        string[] Names { get; }
    }

    internal sealed class NullDataObject : DataObject
    {
        public NullDataObject(ResourceID ID, ResourceManager ResourceManager)
        {
            DataName = ID;
            resourceManager = ResourceManager;
        }
    }

    /////////////////////////////////
    // data
    public class DataObject
    {
        internal ResourceManager resourceManager;
        protected ResourceManager ResourceManager
        {
            get { return resourceManager; }
        }
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
        ResourceManager ResMan { get; set; }
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
        ResourceManager resMan;
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
            resMan = obj.resourceManager;
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

        ResourceManager IDataID.ResMan
        {
            get { return resMan; }
            set { resMan = value; }
        }
    }
}
