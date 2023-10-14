using System;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Framework.States
{
    [Serializable]
    public sealed class PlayerRelativeStateLink<TStateObject> : StateLinkList<TStateObject>, IStateLink where TStateObject : StateObject
    {
        internal PlayerRelativeStateLink()
        {
        }

        internal new void Add(TStateObject obj)
        {
            base.Add(obj);
        }

        // disable some statelinklist methods
        internal new void Remove(TStateObject obj) { }
        internal new void Insert(int index, TStateObject state) { }

        int CurrentPlayer
        {
            get { return container.Root.CurrentPlayerIndex; }
        }

        IStateLink CurrentObject
        {
            get { return list[CurrentPlayer]; }
            set { list[CurrentPlayer] = value; }
        }

        // access
        public static implicit operator PlayerRelativeStateLink<TStateObject>(StateObject right)
        {
            throw new NotSupportedException();
        }

        public TStateObject Object
        {
            get { return (TStateObject)CurrentObject.Object; }
        }

        public static implicit operator TStateObject(PlayerRelativeStateLink<TStateObject> right)
        {
            return (right == null) ? null : (TStateObject)right.Object;
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

        int ID
        {
            get { return CurrentObject.ID; }
        }
        int localID
        {
            get { return CurrentObject.localID; }
        }

        StateObject IStateLink.Object
        {
            get { return container.GetObject(ID, localID); }
        }

        int IStateLink.ID
        {
            get { return ID; }
            set { throw new NotSupportedException(); }
        }

        int IStateLink.localID
        {
            get { return localID; }
            set { throw new NotSupportedException(); }
        }
    }
}
