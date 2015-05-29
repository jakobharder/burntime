
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