using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Burntime.Framework.States
{
    interface IStateLinkList
    {
        List<IStateLink> List { get; set; }
        StateManager Container { get; set; }
    }

    [Serializable]
    public class StateLinkList<TState> : IStateLinkList, IEnumerable, ICloneable, IComparable where TState : StateObject
    {
        internal List<IStateLink> list = new List<IStateLink>();
        [NonSerialized] 
        protected StateManager container;

        internal sealed class StateLinkListEnumerator : IEnumerator
        {
            StateLinkList<TState> list;
            int current;

            public StateLinkListEnumerator(StateLinkList<TState> List)
            {
                list = List;
                Reset();
            }

            public void Reset()
            {
                current = -1;
            }

            public bool MoveNext()
            {
                current++;
                return (current < list.Count);
            }

            public object Current
            {
                get { return list[current]; }
            }
        }

        public IEnumerator GetEnumerator()
        {
            return new StateLinkListEnumerator(this);
        }

        public StateManager Container
        {
            get { return container; }
            set { container = value; }
        }

        internal protected StateLinkList()
        {
        }

        public static StateLinkList<TState> operator -(StateLinkList<TState> list, TState state)
        {
            StateLinkBase link = state;
            list.list.Remove(link);
            return list;
        }

        public void Remove(TState state)
        {
            StateLinkBase link = state;
            list.Remove(link);
        }

        public static StateLinkList<TState> operator +(StateLinkList<TState> list, TState state)
        {
            StateLinkBase link = state;
            list.list.Add(link);
            return list;
        }

        public void Add(TState state)
        {
            StateLinkBase link = state;
            list.Add(link);
        }

        public void Insert(int index, TState state)
        {
            StateLinkBase link = state;
            list.Insert(index, link);
        }

        public bool Contains(TState state)
        {
            StateLinkBase link = state;
            return list.Contains(link);
        }

        public TState this[int index]
        {
            get { return list[index].Object as TState; }
            set { list[index] = (StateLinkBase)value; }
        }

        public int Count
        {
            get
            {
                if (list == null)
                    return 0;
                return list.Count;
            }
        }

        public TState Last
        {
            get { return list[list.Count - 1].Object as TState; }
            set { list[list.Count - 1] = (StateLinkBase)value; }
        }

        public StateLinkList<TState> Clone()
        {
            StateLinkList<TState> list = new StateLinkList<TState>();
            list.list = new List<IStateLink>();

            foreach (IStateLink link in this.list)
                list.list.Add(link);
            list.container = this.container;

            return list;
        }

        public bool CompareTo(StateLinkList<TState> obj)
        {
            if (obj == null || list.Count != obj.list.Count)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Equals(obj.list[i]))
                    return false;
            }

            return true;
        }

        // IStateLinkList interface implementation
        List<IStateLink> IStateLinkList.List
        {
            get { return list; }
            set { list = value; }
        }

        StateManager IStateLinkList.Container
        {
            get { return container; }
            set { container = value; }
        }

        // ICloneable interface implementation
        object ICloneable.Clone()
        {
            return this.Clone();
        }

        // IComparable interface implementation
        int IComparable.CompareTo(object obj)
        {
            return this.CompareTo(obj as StateLinkList<TState>) ? 0 : -1;
        }
    }
}