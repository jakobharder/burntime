using System;
using System.Collections;

namespace Burntime.Classic.Logic
{
    public interface ICharacterCollection : IEnumerable
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

    internal sealed class CharacterCollectionEnumerator : IEnumerator
    {
        ICharacterCollection collection;
        int current;

        public CharacterCollectionEnumerator(ICharacterCollection collection)
        {
            this.collection = collection;
            Reset();
        }

        public void Reset()
        {
            current = -1;
        }

        public bool MoveNext()
        {
            current++;
            return (current < collection.Count);
        }

        public object Current
        {
            get { return collection[current]; }
        }
    }
}