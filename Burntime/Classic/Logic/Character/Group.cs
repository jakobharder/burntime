/*
 *  Burntime Classic
 *  Copyright (C) 2009
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * 
 *  contact: 
 *    juern - burntimedeluxe@gmail.com or yn.harada@gmail.com
 * 
 *  authors: 
 *    Juernjakob Harder - 原田ゆあん (yn.harada@gmail.com)
 * 
*/

using System;
using System.Collections;

using Burntime.Framework.States;
using Burntime.Platform;

namespace Burntime.Classic.Logic
{
    [Serializable]
    public class Group : StateObject, ICharacterCollection
    {
        StateLinkList<Character> characterList;
        float rangeFilterValue;

        public int Count
        {
            get { return characterList.Count; }
        }

        public Character this[int index]
        {
            get { return characterList[index]; }
            set { characterList[index] = value; }
        }

        // hide/show npcs out of range
        public bool IsRangeFiltered
        {
            get { return rangeFilterValue > 0; }
        }

        public float RangeFilterValue
        {
            get { return rangeFilterValue; }
            set { rangeFilterValue = value; }
        }

        protected override void InitInstance(object[] parameter)
        {
            characterList = container.CreateLinkList<Character>();
        }

        public void Add(Character character)
        {
            characterList.Add(character);
        }

        public void Insert(int index, Character character)
        {
            characterList.Insert(index, character);
        }

        public void Remove(Character character)
        {
            characterList.Remove(character);
        }

        public bool Contains(Character character)
        {
            return characterList.Contains(character);
        }

        public int Eat(Character leader, int eatValue)
        {
            if (characterList.Count == 0)
                return eatValue;

            for (int step = 0; step < 9 && eatValue > 0; step++)
            {
                foreach (Character ch in characterList)
                {
                    // skip if out of range
                    if (IsRangeFiltered && leader != null && 
                        (leader.Position - ch.Position).Length >= RangeFilterValue)
                        continue;

                    if (ch.Food == step)
                    {
                        eatValue--;
                        ch.Food++;

                        if (eatValue == 0)
                            break;
                    }
                }
            }

            return eatValue;
        }

        public int Drink(Character leader, int drinkValue)
        {
            if (characterList.Count == 0)
                return drinkValue;

            for (int step = 0; step < 5 && drinkValue > 0; step++)
            {
                foreach (Character ch in characterList)
                {
                    // skip if out of range
                    if (IsRangeFiltered && leader != null && 
                        (leader.Position - ch.Position).Length >= RangeFilterValue)
                        continue;

                    if (ch.Water == step)
                    {
                        drinkValue--;
                        ch.Water++;

                        if (drinkValue == 0)
                            break;
                    }
                }
            }

            return drinkValue;
        }

        public int GetFreeSlotCount()
        {
            int count = 0;

            foreach (Character ch in characterList)
            {
                count += ch.Items.MaxCount - ch.Items.Count;
            }

            return count;
        }

        public void MoveItems(IItemCollection items)
        {
            foreach (Character ch in characterList)
                ch.Items.Move(items);
        }

        public void Heal(Character leader, int healValue)
        {
            if (characterList.Count == 0)
                return;

            for (int step = 0; step < 100 && healValue > 0; step++)
            {
                foreach (Character ch in characterList)
                {
                    // skip if out of range
                    if (IsRangeFiltered && leader != null && 
                        (leader.Position - ch.Position).Length >= RangeFilterValue)
                        continue;

                    if (ch.Health == step)
                    {
                        healValue--;
                        ch.Health++;

                        if (healValue == 0)
                            break;
                    }
                }
            }
        }

        public Item FindFood(out IItemCollection owner)
        {
            Item item = null;
            owner = null;

            for (int i = 0; i < characterList.Count; i++)
            {
                for (int j = 0; j < characterList[i].Items.Count; j++)
                {
                    if (characterList[i].Items[j].FoodValue != 0 &&
                        (item == null || characterList[i].Items[j].FoodValue > item.FoodValue))
                    {
                        item = characterList[i].Items[j];
                        owner = characterList[i].Items;
                    }
                }
            }

            return item;
        }

        public Item FindWater()
        {
            Item item = null;

            for (int i = 0; i < characterList.Count; i++)
            {
                for (int j = 0; j < characterList[i].Items.Count; j++)
                {
                    if (characterList[i].Items[j].WaterValue != 0 &&
                        (item == null || characterList[i].Items[j].WaterValue > item.WaterValue))
                    {
                        item = characterList[i].Items[j];
                    }
                }
            }

            return item;
        }

        // IEnumerable interface implementation
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new CharacterCollectionEnumerator(this);
        }

        // ICharacterCollection interface implementation
        void ICharacterCollection.Add(Character character)
        {
            this.Add(character);
        }

        void ICharacterCollection.Remove(Character character)
        {
            this.Remove(character);
        }

        int ICharacterCollection.Count
        {
            get { return characterList.Count; }
        }

        Character ICharacterCollection.this[int index]
        {
            get { return characterList[index]; }
            set { characterList[index] = value; }
        }

        bool ICharacterCollection.Contains(Character character)
        {
            return this.Contains(character);
        }

        int ICharacterCollection.Eat(Character leader, int foodValue)
        {
            return this.Eat(leader, foodValue);
        }

        int ICharacterCollection.Drink(Character leader, int waterValue)
        {
            return this.Drink(leader, waterValue);
        }

        bool ICharacterCollection.IsInRange(Character leader, Character character)
        {
            if (!Contains(character))
                return false;

            if (!IsRangeFiltered)
                return true;

            return RangeFilterValue > (leader.Position - character.Position).Length;
        }
    }
}