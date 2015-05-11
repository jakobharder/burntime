using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Framework.States
{
    public interface IStateReference
    {
        StateObject Object { get; }
        int ID { get; set; }
        int localID { get; }
    }

    [Serializable]
    public class StateReference<T> : IStateLink, IStateReference where T : StateObject
    {
        internal StateReference()
        {
        }

        public T Object
        {
            get { return (T)container.GetObject(ID, localID); }
        }

        public static implicit operator T(StateReference<T> right)
        {
            return (right == null) ? null : (T)right.Object;
        }

        public override bool Equals(object obj)
        {
            if (obj == null && this == null)
                return true;
            if (obj == null || this == null)
                return false;
            if (!(obj is IStateLink))
                return false;

            return ID == (obj as IStateLink).ID && localID == (obj as IStateLink).localID;
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode() + localID.GetHashCode();
        }

        int ID;
        int localID;

        [NonSerialized]
        protected StateManager container;

        // IStateLink interface implementation
        StateManager IStateLink.Container
        {
            get { return container; }
            set { container = value; }
        }

        StateObject IStateLink.Object
        {
            get { return container.GetObject(ID, localID); }
        }

        int IStateLink.ID
        {
            get { return ID; }
            set { ID = value; }
        }

        int IStateLink.localID
        {
            get { return localID; }
            set { localID = value; }
        }

        // IStateReference interface implementation
        StateObject IStateReference.Object
        {
            get { return this.Object; }
        }

        int IStateReference.ID
        {
            get { return this.ID; }
            set { this.ID = value; }
        }

        int IStateReference.localID
        {
            get { return this.localID; }
        }
    }
}
