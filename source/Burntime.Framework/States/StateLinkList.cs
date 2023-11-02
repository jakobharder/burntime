using System;
using System.Collections;
using System.Collections.Generic;

namespace Burntime.Framework.States;

interface IStateLinkList
{
    List<IStateLink> List { get; set; }
    StateManager Container { get; internal set; }
}

[Serializable]
public class StateLinkList<TState> : IStateLinkList, IEnumerable, IEnumerable<TState>, ICloneable, IComparable where TState : StateObject
{
    internal List<IStateLink> list = new();
    [NonSerialized]
    protected StateManager container;

    internal sealed class StateLinkListEnumerator : IEnumerator, IEnumerator<TState>
    {
        readonly StateLinkList<TState> list;
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

        void IDisposable.Dispose() { }
        object IEnumerator.Current => list[current];
        TState IEnumerator<TState>.Current => list[current];
    }

    IEnumerator IEnumerable.GetEnumerator() => new StateLinkListEnumerator(this);
    public IEnumerator<TState> GetEnumerator() => new StateLinkListEnumerator(this);

    public StateManager Container
    {
        get => container;
        set => container = value;
    }

    internal protected StateLinkList(StateManager container)
    {
        this.container = container;
    }

    public static StateLinkList<TState> operator -(StateLinkList<TState> list, TState state)
    {
        list.list.Remove(StateLinkBase.MakeLink(state));
        return list;
    }

    public static StateLinkList<TState> operator +(StateLinkList<TState> list, TState state)
    {
        list.list.Add(StateLinkBase.MakeLink(state));
        return list;
    }

    public void Add(TState state) => list.Add(StateLinkBase.MakeLink(state));
    public void Remove(TState state) => list.Remove(StateLinkBase.MakeLink(state));
    public void Insert(int index, TState state) => list.Insert(index, StateLinkBase.MakeLink(state));
    public bool Contains(TState state) => list.Contains(StateLinkBase.MakeLink(state));

    public TState this[int index]
    {
        get => (TState)list[index].Object;
        set => list[index] = (StateLinkBase)value;
    }

    public int Count => list?.Count ?? 0;

    public TState Last
    {
        get => (TState)list[^1].Object;
        set => list[^1] = (StateLinkBase)value;
    }

    public StateLinkList<TState> Clone()
    {
        StateLinkList<TState> list = new StateLinkList<TState>(container);
        list.list = new List<IStateLink>();

        foreach (IStateLink link in this.list)
            list.list.Add(link);

        return list;
    }

    public bool CompareTo(StateLinkList<TState>? obj)
    {
        if (obj is null || list.Count != obj.list.Count)
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

    object ICloneable.Clone() => this.Clone();
    int IComparable.CompareTo(object? obj) => this.CompareTo(obj as StateLinkList<TState>) ? 0 : -1;
}