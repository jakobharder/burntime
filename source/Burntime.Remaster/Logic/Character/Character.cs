using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Remaster.Maps;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Burntime.Remaster.Logic
{
    [Serializable]
    [DebuggerDisplay("{Name} at {Location.Title}")]
    public class Character : StateObject, IMapObject, ITurnable, ICharacterCollection
    {
        const int DEFAULT_ATTACK_VALUE = 10;

        protected StateLink<ItemList> items;
        protected StateLink<PathFinding.PathState> path;
        protected StateLink<AI.CharacterMind> mind;
        protected StateLink<Item>? weapon;
        protected StateLink<Item>? protection;

        protected int faceID;
        protected int setBodyId = -1;
        protected float health;
        protected bool dead;

        protected string nameId;

        protected Vector2 position;

        [NonSerialized]
        protected Platform.Graphics.SpriteAnimation ani;
        [NonSerialized]
        protected FadingHelper aniDelay;

        // some helper attributes
        public bool IsWithBoss
        {
            get { if (Player == null) return false; return Player.Group.Contains(this); }
        }

        public bool IsStationed
        {
            get { if (Player == null) return false; return !Player.Group.Contains(this); }
        }

        public bool IsHuman
        {
            get { return Class != CharClass.Dog && Class != CharClass.Mutant; }
        }

        public bool IsTrader
        {
            get { return Class == CharClass.Trader; }
        }

        public bool IsHired
        {
            get { return Player != null; }
        }

        protected int nextAnimation;
        protected int animation;
        public int Animation
        {
            get { return animation; }
            set
            {
                if (nextAnimation != value)
                {
                    aniDelay.State = 0;
                    nextAnimation = value;
                }
            }
        }

        public int FaceID
        {
            get { return faceID; }
            set { faceID = value; }
        }

        public PathFinding.PathState Path
        {
            get { return path; }
            set { path = value; }
        }

        public AI.CharacterMind Mind
        {
            get { return mind; }
            set { mind = value; }
        }

        public Item? Weapon
        {
            get { return (weapon != null && Items.Contains(weapon)) ? weapon : null; }
            set { weapon = value; }
        }

        public Item? Protection
        {
            get { return (protection != null && Items.Contains(protection)) ? protection : null; }
            set { protection = value; }
        }

        public virtual int BaseAttackValue => DEFAULT_ATTACK_VALUE;
        public virtual float AttackValue => (Weapon?.DamageValue ?? BaseAttackValue) * Experience / 100;
        public virtual float DefenseValue => (DEFAULT_ATTACK_VALUE + (Protection?.DefenseValue ?? 0)) * Experience / 100;

        protected DataID<Platform.Graphics.ISprite> body;
        public DataID<Platform.Graphics.ISprite> Body
        {
            get { return body; }
            set { body = value; if (body.Object != null && body.Object.Animation != null) body.Object.Animation.Progressive = false; }
        }

        public int SetBodyId
        {
            get { return setBodyId; }
            set { setBodyId = value; }
        }
        
        public ItemList Items
        {
            get { return items; }
            set { items = value; }
        }

        public CharClass Class;

        public string NameId
        {
            get { return nameId; }
            set { nameId = value; }
        }

        public virtual string Name
        {
            get { return ResourceManager.GetString(nameId); }
            set { throw new NotSupportedException(); }
        }

        public int Health
        {
            get { return (int)health; }
            set { health = value; if (IsDead) Die(); if (health > 100) health = 100; }
        }

        #region character stats
        public int Experience;
        public int Food;
        public int Water;

        public int MaxFood
        {
            get { return 9; }
        }

        public int MaxWater
        {
            get { return 5; }
        }

        public int GetFoodInInventory() => Items.OfType<Item>().Sum(x => x.FoodValue);
        public int GetWaterInInventory() => Items.OfType<Item>().Sum(x => x.WaterValue);
        #endregion

        protected override void InitInstance(object[] parameter)
        {
            items = container.Create<ItemList>();
            dialog = container.Create<Dialog>();
            hireItems = container.CreateLinkList<ItemType>();

            // set health to 1 to avoid IsDead getting true
            health = 1;
            AfterDeserialization();

            base.InitInstance(parameter);
        }

        protected override void AfterDeserialization()
        {
            if (!IsDead)
            {
                ani = new Burntime.Platform.Graphics.SpriteAnimation(2);
                ani.Speed = 10;
            }
            else
            {
                ani = new Burntime.Platform.Graphics.SpriteAnimation(5);
                ani.Endless = false;
            }
            aniDelay = new FadingHelper(20);
            base.AfterDeserialization();

            if (body.Object != null && body.Object.Animation != null)
                body.Object.Animation.Progressive = false;

            // fix character skins, v1.0.2>
            SetBodyId = Helper.GetSetBodyId(Class);
            if (SetBodyId >= 0 && body.Object is not null)
            {
                body = Helper.GetCharacterBody(SetBodyId, Helper.GetColorFromSpriteId(body.Object.ID.Index));
                if (body.Object.Animation is not null)
                    body.Object.Animation.Progressive = false;
            }
        }

        StateLinkList<ItemType> hireItems;
        public StateLinkList<ItemType> HireItems
        {
            get { return hireItems; }
            set { hireItems = value; }
        }

        public Vector2 Position
        {
            get { return position; }
            set
            {
                position = value;
                if (Path != null)
                    Path.MoveTo = position;
            }
        }

        protected StateLink<Location> location;
        public Location Location
        {
            get 
            { 
                if (location != null)
                    return location;
                if (Player != null)
                    return Player.Location;
                return null;
            }
            set
            {
                if (value == null)
                {
                    if (location != null)
                        location.Object.Characters -= this;

                    location = null;
                }
                else
                {
                    if (location != null)
                        location.Object.Characters -= this;

                    value.Characters += this;

                    location = value;
                }
            }
        }

        protected StateLink<Dialog> dialog;
        public Dialog Dialog
        {
            get { return dialog; }
            set { dialog = value; }
        }

        protected StateLink<Player> player;
        public Player Player
        {
            get { return (player != null) ? player : null; }
            set { player = value; }
        }

        public bool IsPlayerCharacter
        {
            get { return player != null && Player.Character == this; }
        }

        // map object implementation
        public virtual String GetTitle(IResourceManager ResourceManager)
        {
            return Name;
        }

        public Vector2 MapPosition
        {
            get { return Position; }
        }

        public Rect MapArea
        {
            get { return new Rect(Position.x - Body.Object.Width / 2, Position.y - Body.Object.Height, Body.Object.Width, Body.Object.Height); }
        }

        // logic
        public bool IsDead
        {
            get { return health <= 0; }
        }

        public bool DeadAnimationFinished
        {
            get { return dead; }
        }

        public bool IsLastInCamp
        {
            get
            {
                if (location == null)
                    return false;
                List<Character> camp = Location.CampNPC.Take(2).ToList();
                return (camp.Count == 1 && camp[0] == this);
            }
        }

        public void JoinCamp()
        {
            Player.Group.Remove(this);
            Location = Player.Location;
            Location.Player = Player;
            Mind = container.Create<AI.PlayerControlledMind>(new object[] { this });
            Path = container.Create<PathFinding.ComplexPath>();
        }

        public void LeaveCamp()
        {
            if (location == null)
                return;

            if (IsLastInCamp)
                Location.Player = null;

            if (Player != null)
                Player.Group.Add(this);

            Location = null;
            Mind = container.Create<AI.FellowerMind>(new object[] { this, Player.Character });
            Path = container.Create<PathFinding.ComplexPath>();
            Path.MoveTo = Position;
        }

        public void Hire(Player boss)
        {
            Location = null;
            boss.Group.Add(this);
            Player = boss;

            if (SetBodyId != -1 && boss.BodyColorSet != -1)
            {
                Body = Helper.GetCharacterBody(SetBodyId, boss.BodyColorSet);
            }

            Item hireItem = null;
            for (int i = 0; hireItem == null && i < HireItems.Count; i++)
            {
                hireItem = boss.Character.Items.Find(HireItems[i]);
            }

            boss.Character.Items.Remove(hireItem);

            Mind = container.Create<AI.FellowerMind>(new object[] { this, boss.Character });
            Path = container.Create<PathFinding.ComplexPath>();
            Path.MoveTo = Position;

            ClassicGame classic = (ClassicGame)container.Root;
            int difficulty = 2 - classic.World.Difficulty; // set inverted difficulty (0 hard, 1 normal, 2 easy)

            // if hire item is food or water then use it
            if (hireItem.FoodValue != 0)
                Food = System.Math.Min(MaxFood, Food + hireItem.FoodValue);
            if (hireItem.WaterValue != 0)
                Water = System.Math.Min(MaxWater, Water + hireItem.WaterValue);

            // prevent 0 food / 0 water situation depending on difficulty setting
            if (Food < difficulty)
                Food = difficulty;
            if (Water < difficulty)
                Water = difficulty;
        }

        public void Dismiss()
        {
            if (location == null)
            {
                Location = Player.Location;
                Player.Group.Remove(this);
            }
            else
            {
                if (IsLastInCamp)
                {
                    if (Player != Root.World.ActivePlayerObj)
                    {
                        // clear some items if we just freed an enemy camp
                        Location.ClearItemsAfterTakeover();
                    }
                    Location.Player = null;
                }
            }

            Player = null;
            Mind = container.Create<AI.SimpleMind>(new object[] { this });
            Path = container.Create<PathFinding.SimplePath>();
            Path.MoveTo = Position;
        }

        public virtual void Die()
        {
            // drop items
            if (Location is not null)
            {
                Location.Items.DropPosition = Position;
                Items.MoveTo(Location.Items);
            }

            // remove from player empire
            if (Player != null && Player.Character != this)
                Dismiss();

            // reset
            health = 0;
            ani = new Burntime.Platform.Graphics.SpriteAnimation(4);
            ani.Endless = false;

            // schedule for respawn
            if (this is not PlayerCharacter)
            {
                ClassicGame classic = (ClassicGame)container.Root;
                classic.World.Respawn.Object.Respawn(this);
            }
        }

        public virtual void Revive()
        {
            // set full heatlh
            health = 100;

            // reset animation
            ani = new Burntime.Platform.Graphics.SpriteAnimation(2);
            ani.Speed = 10;
        }

        public void SelectItem(Item item)
        {
            if (item.Type.IsClass("weapon"))
            {
                if (Weapon == item)
                    Weapon = null;
                else
                    Weapon = item;
            }
            
            if (item.Type.IsClass("protection"))
            {
                if (Protection == item)
                    Protection = null;
                else
                    Protection = item;
            }
        }

        public void CancelAction()
        {
            Path.MoveTo = Position;
            Mind.MoveToObject(null);
        }

        public bool IsInAttackRange(Character target)
        {
            return (Position - target.Position).Length < 30;
        }

        public void Attack(Character defender, bool defendWithAmmo = true)
        {
#warning TODO attacks against camps should involve every camp member

            // Add 25% per difficulty. Reverse if current player is not the attacker
            float difficultyFactor = (1 + Root.World.Difficulty * 0.1f);
            bool isPlayer = (Player == container.Root.CurrentPlayer);

            var attackingGroup = (Player != null && Player.Character == this)
                ? Player.Group.Where(ch => (ch.Position - Position).Length < 25).ToArray()
                : new Character[] { this };

            static void attack(Character attacker, Character defender, bool useAmmo, float factor)
            {
                int attackValue = attacker.UseBestEquipment(useAmmo);
                int damage = (int)System.Math.Max(1, (attackValue - defender.DefenseValue) * factor);
                defender.Health -= damage;
            };

            foreach (var attacker in attackingGroup)
            {
                attack(attacker, defender, useAmmo: true, isPlayer ? 1 : difficultyFactor);
                attack(defender, attacker, defendWithAmmo, isPlayer ? difficultyFactor : 1);

                container.Notify(new AttackEvent(attacker, defender));
                if (defender.IsDead || attacker.IsDead)
                    break;
            }
        }

        public virtual void Turn()
        {
            if (IsDead)
                return;

            if (Player == null)
            {
                TurnNonPlayer();
                return;
            }

            // npc is with boss
            if (IsWithBoss)
            {
                Group group = Player.Group;

                if (Food == 0)
                {
                    IItemCollection owner;
                    Item item = group.FindFood(out owner);
                    if (item != null)
                    {
                        group.Eat(null, item.FoodValue);
                        owner.Remove(item);
                    }
                }

                if (Water == 0)
                {
                    Item item = group.FindWater();
                    if (item != null)
                    {
                        group.Drink(null, item.WaterValue);
                        item.Type = item.Type.Empty;
                    }
                }
            }
            else // npc is stationed
            {
                ICharacterCollection group = GetGroup();

                if (Location.NPCFoodProduction > 0)
                {
                    Location.NPCFoodProduction--;
                    Food++;
                }
                else if (Food == 0)
                {
                    IItemCollection owner;
                    // search for food in rooms
                    Item item = Location.FindFood(out owner);
                    // if not available then try the inventory
                    if (item == null)
                        item = group.FindFood(out owner);
                    if (item != null)
                    {
                        group.Eat(null, item.FoodValue);
                        owner.Remove(item);
                    }
                }

                Location.Source.Reserve = group.Drink(null, Location.Source.Reserve);
                if (Water == 0)
                {
                    // search for stored water in rooms
                    Item item = Location.FindWater();
                    // if not available then try the inventory
                    if (item == null)
                        item = group.FindWater();
                    if (item != null)
                    {
                        group.Drink(null, item.WaterValue);
                        item.Type = item.Type.Empty;
                    }
                }
            }

            // TODO move location healing to location
            bool doctorAvailable = Player?.Group.Any(chr => chr.Class == CharClass.Doctor) == true ||
                Location?.CampNPC.Any(chr => chr.Class == CharClass.Doctor && chr.Player == Player) == true;
            if (doctorAvailable)
            {
                if (health >= 50)
                    health += 4;
            }
            else
            {
                if (health >= 70)
                    health += 2;
            }

            if (health > 100)
                health = 100;
            if (Food == 0)
                health -= 25;
            if (Water == 0)
                health -= 25;

            if (health <= 0)
            {
                Die();
                return;
            }

            Food--;
            if (Food < 0)
                Food = 0;
            Water--;
            if (Water < 0)
                Water = 0;

            //Dialog.Turn();
        }

        protected void TurnNonPlayer()
        {
            if (Food == 0)
                Food = Burntime.Platform.Math.Random.Next() % (MaxFood - 1) + 1;
            if (Water == 0)
                Water = Burntime.Platform.Math.Random.Next() % (MaxWater - 1) + 1;

            Food--;
            if (Food < 0)
                Food = 0;
            Water--;
            if (Water < 0)
                Water = 0;
        }

        public float GetDangerRate()
        {
            if (Location.Danger is null)
                return 0;

            float rate = 1;
            UseBestProtection();

            Interaction.DangerProtection? p = Protection?.Type.GetProtection(Location.Danger.Type);
            if (p is not null)
                rate -= p.Rate;

            return rate;
        }

        public virtual void Update(float elapsed)
        {
            ani.Update(elapsed);

            if (IsDead)
            {
                animation = 12 + ani.Frame;

                if (ani.End)
                {
                    Location = null;
                    dead = true;
                }
                return;
            }

            Path.Speed = 35;
            Vector2 old = new Vector2(position);

            // process dangers (only if hired)
            if (Player != null)
            {
                if (Location.Danger != null)
                {
                    float rate = GetDangerRate();
                    if (rate > 0)
                    {
                        health -= Location.Danger.HealthDecrease * elapsed * rate;
                        if (IsDead)
                        {
                            Die();
                            return;
                        }
                    }
                }
            }

            Mind.Process(elapsed);

            Location loc = Location;
            if (loc == null)
                loc = Player.Location;

            position = Path.Process(loc.Map.Mask, Position, elapsed);

            Vector2 dir = position - old;
            if (dir.x != 0 || dir.y != 0)
            {
                if (dir.y < 0 /*&& System.Math.Abs(dir.y) > System.Math.Abs(dir.x)*/) // up
                {
                    Animation = 8 + ani.Frame;
                }
                else if (dir.y > 0 /*&& System.Math.Abs(dir.y) > System.Math.Abs(dir.x)*/) // down
                {
                    Animation = 6 + ani.Frame;
                }
                else
                {
                    if (dir.x < 0) // left
                    {
                        Animation = 4 + ani.Frame;
                    }
                    else if (dir.x > 0) // right
                    {
                        Animation = 2 + ani.Frame;
                    }
                }
            }
            else
                Animation = 0;

            if (!aniDelay.IsIn && Animation != nextAnimation)
            {
                aniDelay.FadeIn();
                aniDelay.Update(elapsed);

                if (aniDelay.IsIn)
                    animation = nextAnimation;
            }
        }

        public ICharacterCollection GetGroup()
        {
            if (Player != null && Player.Group.Contains(this))
            {
                return Player.Group;
            }

            return this;
        }

        /// <summary>
        /// Use best weapon and protection. Returns attack value.
        /// </summary>
        private int UseBestEquipment(bool allowAmmo = true)
        {
            Protection = Items.FindBestDefense(Protection);

            // make sure empty guns are not used
            if (Weapon?.DamageValue == 0)
                Weapon = null;

            Item? weapon = Items.FindBestWeapon(allowAmmo ? Weapon : null);
            if (weapon is null)
                return BaseAttackValue * Experience / 100;

            int attackValue = weapon.DamageValue;
            if (allowAmmo)
            {
                weapon.Use();
                if (weapon.DamageValue == 0)
                    Weapon = null;
            }

            Weapon = Items.FindBestWeapon(Weapon);
            return attackValue * Experience / 100;
        }

        /// <summary>
        /// Use best danger protection.
        /// </summary>
        private void UseBestProtection()
        {
            Protection = Items.FindBestProtection(Protection, Location.Danger.Type);
        }

        #region ICharacterCollection, IEnumerable implementations
        IEnumerator IEnumerable.GetEnumerator() => new CharacterCollectionEnumerator(this);
        IEnumerator<Character> IEnumerable<Character>.GetEnumerator() => new CharacterCollectionEnumerator(this);

        // ICharacterCollection interface implementation
        void ICharacterCollection.Add(Character character)
        {
            throw new NotSupportedException();
        }

        void ICharacterCollection.Remove(Character character)
        {
            throw new NotSupportedException();
        }

        int ICharacterCollection.Count
        {
            get { return 1; }
        }

        Character ICharacterCollection.this[int index]
        {
            get { return this; }
            set { /* will be ignored, this = value; */  }
        }

        bool ICharacterCollection.Contains(Character character)
        {
            return this == character;
        }

        int ICharacterCollection.Eat(Character leader, int foodValue)
        {
            int eat = System.Math.Min(MaxFood - Food, foodValue);
            Food += eat;

            return foodValue - eat;
        }

        int ICharacterCollection.Drink(Character leader, int waterValue)
        {
            int drink = System.Math.Min(MaxWater - Water, waterValue);
            Water += drink;

            return waterValue - drink;
        }

        Item ICharacterCollection.FindFood(out IItemCollection owner)
        {
            Item item = null;
            owner = null;

            for (int j = 0; j < Items.Count; j++)
            {
                if (Items[j].FoodValue != 0 &&
                    (item == null || Items[j].FoodValue > item.FoodValue))
                {
                    item = Items[j];
                    owner = Items;
                }
            }

            return item;
        }

        Item ICharacterCollection.FindWater()
        {
            Item item = null;

            for (int j = 0; j < Items.Count; j++)
            {
                if (Items[j].WaterValue != 0 &&
                    (item == null || Items[j].WaterValue > item.WaterValue))
                {
                    item = Items[j];
                }
            }

            return item;
        }

        int ICharacterCollection.GetFreeSlotCount()
        {
            return Items.MaxCount - Items.Count;
        }

        void ICharacterCollection.MoveItems(IItemCollection items)
        {
            Items.Move(items);
        }

        bool ICharacterCollection.IsInRange(Character leader, Character character)
        {
            return this == character && leader == character;
        }
        #endregion

        private ClassicGame Root => (ClassicGame)Container.Root;
    }
}
