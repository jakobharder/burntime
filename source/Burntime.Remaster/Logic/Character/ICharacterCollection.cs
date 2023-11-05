using System;
using System.Collections;
using System.Collections.Generic;

namespace Burntime.Remaster.Logic;

public interface ICharacterCollection : IEnumerable, IEnumerable<Character>
{
    void Add(Character character);
    void Remove(Character item);
    int Count { get; }
    Character this[int index] { get; set; }
    bool Contains(Character character);

    int Eat(Character leader, int foodValue);
    int Drink(Character leader, int waterValue);
    Item FindFood(out IItemCollection owner);
    Item FindWater();

    // item methods
    int GetFreeSlotCount();
    void MoveItems(IItemCollection items);

    // range methods
    bool IsInRange(Character leader, Character character);
}

internal sealed class CharacterCollectionEnumerator : IEnumerator, IEnumerator<Character>
{
    ICharacterCollection collection;
    int current;

    public CharacterCollectionEnumerator(ICharacterCollection collection)
    {
        this.collection = collection;
        Reset();
    }

    public void Reset() => current = -1;

    public bool MoveNext()
    {
        current++;
        return (current < collection.Count);
    }

    void IDisposable.Dispose() { }
    object IEnumerator.Current => collection[current];
    public Character Current => collection[current];
}