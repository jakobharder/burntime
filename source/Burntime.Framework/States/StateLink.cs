using System;

namespace Burntime.Framework.States
{
    internal interface IStateLink
    {
        StateObject Object { get; }
        StateManager Container { get; internal set; }

        int ID { get; set; }
        int localID { get; set; }
    }
    
    [Serializable]
    public class StateLinkBase : IStateLink
    {
        public static implicit operator StateLinkBase(StateObject right)
        {
            StateLinkBase state = new()
            {
                ID = right.ID,
                localID = right.localID,
                container = right.Container
            };
            if (right.container == null)
                throw new CustomException("null container");
            return state;
        }

        public static StateLinkBase MakeLink(StateObject right)
        {
            StateLinkBase link;
            link = right;
            return link;
        }

        protected StateLinkBase()
        {
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
            get => container;
            set => container = value;
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
    }

    [Serializable]
    public class StateLink<T> : IStateLink where T : StateObject
    {
        public static implicit operator StateLink<T>(StateObject right)
        {
            if (right == null)
                return null;

            StateLink<T> state = new StateLink<T>();
            state.ID = right.ID;
            state.localID = right.localID;
            state.container = right.container;
            if (right.container == null)
                throw new CustomException("null container");
            return state;
        }

        protected StateLink()
        {
        }

        public T Object
        {
            get { return (T)container.GetObject(ID, localID); }
        }

        public static implicit operator T(StateLink<T> right)
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

        StateManager IStateLink.Container
        {
            get => container;
            set => container = value;
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
    }
}
