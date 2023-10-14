using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Burntime.Remaster
{
    public class OwnedItem
    {
        private Item item;
        public Item Item
        {
            get { return item; }
        }

        private IItemCollection owner;
        public IItemCollection Owner
        {
            get { return owner; }
        }

        public OwnedItem(Item item, IItemCollection owner)
        {
            this.item = item;
            this.owner = owner;
        }

        public static implicit operator Item(OwnedItem right)
        {
            return (right == null) ? null : right.item;
        }
    }
}
