using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Remaster.Maps;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Burntime.Remaster.Logic.Rules;

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
        [NonSerialized]
        float proximityPauseRemaining;
        [NonSerialized]
        bool wasInTalkingDistance;
        [NonSerialized]
        float movementElapsed;
        [NonSerialized]
        float fleeTimeRemaining;
        [NonSerialized]
        Vector2 fleeDestination;

        const float WALK_SPEED = 35;
        const float CONTROLLED_WALK_SPEED = 50;
        const float FLEE_SPEED = 70;
        const float FLEE_DURATION = 2.5f;
        const float FLEE_DISTANCE = 70;
        const float TALKING_DISTANCE = 30;
        const float PROXIMITY_PAUSE_TIME = 10;
        // Vertical facing covers 65 degrees to either side of the vertical axis,
        // leaving a 25-degree cone around each horizontal direction.
        const float VERTICAL_FACING_MIN_SLOPE = 0.46630767f; // tan(25 degrees)

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
        internal int CombatExperience => RuleFormulas.CombatExperience(Class, Experience);
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

        public virtual int MaxFood => 9;

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
            Position = Location.GetResidentPosition(this);
            Mind = container.Create<AI.SimpleMind>(new object[] { this });
            Path = container.Create<PathFinding.SimplePath>();
            Path.MoveTo = Position;
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

        public void Hire(Player boss, bool waivePayment = false) =>
            Hire(boss, waivePayment, initializeRecruit: true);

        internal void HireWithoutInitialization(Player boss) =>
            Hire(boss, waivePayment: true, initializeRecruit: false);

        void Hire(Player boss, bool waivePayment, bool initializeRecruit)
        {
            Location = null;
            boss.Group.Add(this);
            Player = boss;

            if (SetBodyId != -1 && boss.BodyColorSet != -1)
            {
                Body = Helper.GetCharacterBody(SetBodyId, boss.BodyColorSet);
            }

            Item hireItem = null;
            for (int i = 0; !waivePayment && hireItem == null && i < HireItems.Count; i++)
            {
                hireItem = boss.Character.Items.Find(HireItems[i]);
            }

            if (!waivePayment && hireItem != null)
                boss.Character.Items.Remove(hireItem);

            Mind = container.Create<AI.FellowerMind>(new object[] { this, boss.Character });
            Path = container.Create<PathFinding.ComplexPath>();
            Path.MoveTo = Position;

            if (initializeRecruit)
                ((ClassicGame)container.Root).RuleBook.InitializeRecruit(this, boss, hireItem);
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
            Path.Stop(Position);
            Mind.MoveToObject(null);
        }

        public bool IsInAttackRange(Character target)
        {
            return (Position - target.Position).Length < 30;
        }

        public void Attack(Character defender, bool defendWithAmmo = true)
        {
#warning TODO attacks against camps should involve every camp member

            if (IsDead || defender.IsDead)
                return;

            var attackingGroup = (Player != null && Player.Character == this && !Player.SingleMode)
                ? Player.Group.ToArray()
                : new Character[] { this };

            foreach (var attacker in attackingGroup)
            {
                if (attacker.IsDead)
                    continue;
                Root.RuleBook.DealAttackDamage(attacker, defender, useAmmo: true);
                if (!defender.IsDead)
                    Root.RuleBook.DealAttackDamage(defender, attacker, defendWithAmmo);

                if (attacker.IsHuman && !defender.IsDead)
                    defender.FleeFrom(attacker);

                container.Notify(new AttackEvent(attacker, defender));
                if (defender.Player?.AiState is AI.ClassicAiState strategicAi)
                    strategicAi.RecordAttack(attacker, defender);
                if (defender.IsDead || attacker.IsDead)
                    break;
            }
        }

        internal void AttackWithoutRetaliation(Character defender)
        {
            Root.RuleBook.DealAttackDamage(this, defender, useAmmo: true);
            FleeFrom(defender);

            container.Notify(new AttackEvent(this, defender));
            if (defender.Player?.AiState is AI.ClassicAiState strategicAi)
                strategicAi.RecordAttack(this, defender);
        }

        internal Item? FindOriginalWeapon(bool allowUnloadedRifle = false)
        {
            Item? selected = Weapon;
            if (selected?.DamageValue == 0 &&
                !(allowUnloadedRifle && selected.ID == "item_unloaded_rifle"))
                selected = null;
            selected = Items.FindBestWeapon(selected);
            if (allowUnloadedRifle && selected == null)
                selected = Items.FirstOrDefault(item => item.ID == "item_unloaded_rifle");
            return selected;
        }

        internal Item? SelectOriginalWeapon(bool allowUnloadedRifle = false)
        {
            Weapon = FindOriginalWeapon(allowUnloadedRifle);
            return Weapon;
        }

        internal void UseOriginalWeapon(Item weapon)
        {
            ItemType loadedType = weapon.Type;
            weapon.Use();
            if (weapon.DamageValue != 0)
                return;

            Item? ammunition = Items.FirstOrDefault(item => item.ID == "item_ammunition");
            if (ammunition != null)
            {
                Items.Remove(ammunition);
                weapon.Reload(loadedType);
            }
            else
            {
                Weapon = null;
            }
        }

        void FleeFrom(Character attacker)
        {
            Vector2f direction = Position - attacker.Position;
            if (direction.Length < 0.1f)
            {
                direction = new Vector2f(
                    Burntime.Platform.Math.Random.Next(0, 2) == 0 ? -1 : 1,
                    Burntime.Platform.Math.Random.Next(-1, 2));
            }
            direction.Normalize();

            Vector2 destination = Position + (Vector2)(direction * FLEE_DISTANCE);
            Location? location = Location ?? Player?.Location;
            if (location is not null && !location.Map.Mask.IsWalkableMapPosition(destination))
            {
                // Try nearby escape angles when the direct route ends outside the
                // walkable map. Prefer continuing generally away from the attacker.
                float[] angles = { 45, -45, 90, -90 };
                foreach (float angle in angles)
                {
                    float radians = angle * (float)System.Math.PI / 180;
                    float cos = (float)System.Math.Cos(radians);
                    float sin = (float)System.Math.Sin(radians);
                    Vector2f alternative = new(
                        direction.x * cos - direction.y * sin,
                        direction.x * sin + direction.y * cos);
                    Vector2 candidate = Position + (Vector2)(alternative * FLEE_DISTANCE);
                    if (location.Map.Mask.IsWalkableMapPosition(candidate))
                    {
                        destination = candidate;
                        break;
                    }
                }
            }

            if (destination == Position)
                return;

            Mind.MoveToObject(null);
            if (Path is PathFinding.ManualPath)
                Path = container.Create<PathFinding.ComplexPath>();
            Path.MoveTo = destination;
            fleeDestination = destination;
            fleeTimeRemaining = FLEE_DURATION;
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

            Root.RuleBook.TurnEmployedCharacter(this);

            if (IsDead)
                return;

            ResetStationedMind();

            //Dialog.Turn();
        }

        internal bool HasLocalDoctor => IsWithBoss
            ? Player.Group.Any(member => !member.IsDead && member.Class == CharClass.Doctor)
            : Location?.CampNPC.Any(member => !member.IsDead &&
                member.Class == CharClass.Doctor && member.Player == Player) == true;

        internal void TurnExtendedEmployed(bool amigaSurvivalBehavior)
        {
            bool amigaActiveParty = amigaSurvivalBehavior && IsWithBoss;
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

                if (!amigaActiveParty && Water == 0)
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
            bool doctorAvailable = HasLocalDoctor;
            bool aiAutoHealing = Player?.Type == PlayerType.Ai && !amigaSurvivalBehavior;
            if (doctorAvailable)
            {
                if (health >= 50 || aiAutoHealing)
                    health += 4;
            }
            else
            {
                if (health >= (amigaSurvivalBehavior ? 50 : 70) || aiAutoHealing)
                    health += 2;
            }

            if (health > 100)
                health = 100;
            if (Food == 0)
                health -= 25;
            if (!amigaActiveParty && Water == 0)
                health -= 25;

            if (health <= 0)
            {
                Die();
                return;
            }

            Food--;
            if (Food < 0)
                Food = 0;
            if (!amigaActiveParty)
            {
                Water--;
                if (Water < 0)
                    Water = 0;
            }
        }

        void ResetStationedMind()
        {
            // Selecting a stationed NPC gives it a player-controlled mind. At the
            // start of each new day, camp NPCs return to roaming independently of
            // whether their camp belongs to a human or AI player.
            if (IsStationed && Mind is not AI.SimpleMind)
            {
                Mind = container.Create<AI.SimpleMind>(new object[] { this });
                Path = container.Create<PathFinding.SimplePath>();
                Path.MoveTo = Position;
            }
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

        public float GetHazardProtectionRate(string hazardType)
        {
            if (((ClassicGame)Container.Root).RuleBook.Settings.IsHazardImmune(hazardType, FaceID))
                return 1;
            return System.Math.Clamp(Protection?.Type.GetProtection(hazardType)?.Rate ?? 0, 0, 1);
        }

        public float GetDangerRate()
        {
            if (Location.Danger is null)
                return 0;

            UseBestProtection();
            return 1 - GetHazardProtectionRate(Location.Danger.Type);
        }

        internal bool ApplyContinuousHazard(float elapsed)
        {
            if (Location?.Danger == null)
                return false;

            var settings = ((ClassicGame)Container.Root).RuleBook.Settings;
            if (settings.IsHazardImmune(Location.Danger.Type, FaceID))
                return false;
            float rate = GetDangerRate();
            if (rate <= 0)
                return false;

            health -= settings.HazardDamage(Location.Danger.Type) * elapsed * rate;
            if (!IsDead)
                return false;
            Die();
            return true;
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

            Player activePlayer = container.Root.CurrentPlayer as Player;
            bool isActiveGroup = activePlayer?.Group.Contains(this) == true;
            bool isPlayerControlled = activePlayer?.SelectedCharacter == this || Mind is AI.PlayerControlledMind;
            Path.Speed = isActiveGroup || isPlayerControlled
                ? CONTROLLED_WALK_SPEED
                : Mind is not AI.SimpleMind
                    ? WALK_SPEED
                    : Class == CharClass.Dog
                        ? WALK_SPEED
                        : Class == CharClass.Mutant
                            ? WALK_SPEED / 2
                            : WALK_SPEED * 0.66f;
            if (fleeTimeRemaining > 0)
                Path.Speed = FLEE_SPEED;

            bool isHovered = Location?.HoverCharacter == this;
            bool isInTalkingDistance = IsHuman && !isActiveGroup && !isPlayerControlled &&
                activePlayer?.SelectedCharacter != null &&
                activePlayer.Location == Location &&
                (activePlayer.SelectedCharacter.Position - Position).Length < TALKING_DISTANCE;

            if (isInTalkingDistance && !wasInTalkingDistance)
                proximityPauseRemaining = PROXIMITY_PAUSE_TIME;
            else if (!isInTalkingDistance)
                proximityPauseRemaining = 0;
            wasInTalkingDistance = isInTalkingDistance;

            bool isFleeing = fleeTimeRemaining > 0;
            if (proximityPauseRemaining > 0)
            {
                proximityPauseRemaining = System.Math.Max(0, proximityPauseRemaining - elapsed);
                if (!isFleeing)
                    Path.Speed = 0;
            }
            if (isHovered && !isFleeing && !isActiveGroup && !isPlayerControlled)
                Path.Speed = 0;

            Vector2 old = new Vector2(position);

            // process dangers (only if hired)
            if (Player != null && Root.RuleBook.ApplyContinuousHazard(this, elapsed))
            {
                return;
            }

            Mind.Process(elapsed);

            if (fleeTimeRemaining > 0)
            {
                fleeTimeRemaining = System.Math.Max(0, fleeTimeRemaining - elapsed);
                if ((fleeDestination - Position).Length <= 2)
                    fleeTimeRemaining = 0;
                else
                    Path.MoveTo = fleeDestination;
            }

            Location loc = Location;
            if (loc == null)
                loc = Player.Location;

            float pathElapsed = elapsed;
            if (Path.Speed <= 0)
            {
                movementElapsed = 0;
                pathElapsed = 0;
            }
            else if (Path.Speed < WALK_SPEED)
            {
                movementElapsed += elapsed;
                if (Path.Speed * movementElapsed < 1)
                    pathElapsed = 0;
                else
                {
                    pathElapsed = movementElapsed;
                    movementElapsed = 0;
                }
            }
            else
                movementElapsed = 0;

            position = pathElapsed > 0
                ? Path.Process(loc.Map.Mask, Position, pathElapsed)
                : Position;

            Vector2f dir = position - old;
            if (dir == Vector2f.Zero && pathElapsed == 0 && movementElapsed > 0 && Path.MoveTo != Position)
                dir = Path.MoveTo - Position;
            if (Path.Speed > 0 && Path.MovementDirection != Vector2f.Zero)
                dir = Path.MovementDirection;
            if (System.Math.Abs(dir.x) > 0.01f || System.Math.Abs(dir.y) > 0.01f)
            {
                bool faceVertical = System.Math.Abs(dir.y) >=
                    System.Math.Abs(dir.x) * VERTICAL_FACING_MIN_SLOPE;
                if (faceVertical && dir.y < 0) // up
                {
                    Animation = 8 + ani.Frame;
                }
                else if (faceVertical && dir.y > 0) // down
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
        /// Select the best protection against the current environmental hazard.
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
