using System;
using System.Reflection;
using Burntime.Platform.Resource;

namespace Burntime.Framework.States
{
    public class NonSynchronizedAttribute : Attribute
    {
    }
    
    [Serializable]
    public class StateObject : ICloneable, IComparable
    {
        [NonSerialized]
        internal protected StateManager container;
        public StateManager Container
        {
            get { return container; }
        }

        public IResourceManager ResourceManager
        {
            get { return container.ResourceManager; }
        }

        internal int ID = -1;
        internal int localID = -1;

        internal protected StateObject()
        {
        }

        internal protected virtual void InitInstance(object[] parameter)
        {
        }

        internal protected virtual void AfterDeserialization() { }

        internal protected virtual void AfterResolving() { }

        public StateObject Clone()
        {
            // simple clone value type members
            StateObject obj = this.MemberwiseClone() as StateObject;

            Type type = obj.GetType();

            // state link lists need to be cloned manually
            FieldInfo[] infos = type.GetFields(BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo info in infos)
            {
                if (typeof(IStateLinkList).IsAssignableFrom(info.FieldType) && 
                    typeof(ICloneable).IsAssignableFrom(info.FieldType))
                {
                    ICloneable list = (ICloneable)info.GetValue(obj);
                    // clone if non-null
                    if (list != null)
                        info.SetValue(obj, list.Clone());
                }
            }

            StateContainer.SetManager(obj, container);
            // run custom after deserialization/clone initializing
            obj.AfterDeserialization();
            
            return obj;
        }

        public override bool Equals(object obj)
        {
            if (obj == null && this == null)
                return true;
            if (obj == null || this == null)
                return false;
            if (!(obj is StateObject))
                return false;

            return ID == (obj as StateObject).ID && localID == (obj as StateObject).localID;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode() + localID.GetHashCode();
        }

        public bool CompareTo(StateObject obj)
        {
            if (obj == null)
                return false;

            Type type = obj.GetType();
            if (type != this.GetType())
                return false;

            FieldInfo[] infos = type.GetFields(BindingFlags.Default | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo info in infos)
            {
                // skip not serialized members
                if (info.IsNotSerialized)
                    continue;

                if (typeof(IComparable).IsAssignableFrom(info.FieldType))
                {
                    IComparable left = (IComparable)info.GetValue(this);
                    IComparable right = (IComparable)info.GetValue(obj);
                    if (!CompareItems(left, right))
                        return false;
                }
                else if (info.FieldType.IsArray)
                {
                    Array left = info.GetValue(this) as Array;
                    Array right = info.GetValue(obj) as Array;

                    if (left == null && right == null)
                        return true;
                    if (left == null || right == null || left.Length != right.Length)
                        return false;
                    
                    for (int i = 0; i < left.Length; i++)
                    {
                        if (!CompareOrEqualItems(left.GetValue(i), right.GetValue(i)))
                            return false;
                    }
                }
                else
                {
                    object left = info.GetValue(this);
                    object right = info.GetValue(obj);

                    if (!EqualItems(left, right))
                        return false;
                }
            }

            return true;
        }

        bool CompareOrEqualItems(object left, object right)
        {
            if (left is IComparable && right is IComparable)
                return CompareItems(left as IComparable, right as IComparable);
            return EqualItems(left, right);
        }

        bool CompareItems(IComparable left, IComparable right)
        {
            if (left == null && right == null)
                return true;
            if (left == null || right == null || 0 != left.CompareTo(right))
                return false;
            return true;
        }

        bool EqualItems(object left, object right)
        {
            if (left == null && right == null)
                return true;
            if (left == null || right == null || !left.Equals(right))
                return false;
            return true;
        }

        public override string ToString()
        {
            if (ID != -1)
                return "[" + ID + "] " + GetType().Name;
            return "[@" + localID + "] " + GetType().Name;
        }

        // ICloneable interface implementation
        object ICloneable.Clone()
        {
            return this.Clone();
        }

        // IComparable interface implementation
        int IComparable.CompareTo(object obj)
        {
            // note: this implementation can not be used to order items, only for equality check
            return this.CompareTo(obj as StateObject) ? 0 : -1;
        }
    }
}
