using Burntime.Framework.States;
using Burntime.Platform;
using Burntime.Platform.Resource;
using Burntime.Remaster.Maps;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Burntime.Remaster.Logic.Generation;
using Burntime.Remaster.Logic.Rules;

namespace Burntime.Remaster
{
    public enum CharClass
    {
        Mercenary,
        Technician,
        Doctor,
        Boss,
        Mutant,
        Trader,
        Dog,

        Count
    }
}

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
        [NonSerialized]
        Character fleeFromCharacter;
        [NonSerialized]
        Character combatApproachTarget;
        [NonSerialized]
        float combatApproachRange;
        [NonSerialized]
        bool combatHold;

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

        internal bool IsFleeing => fleeTimeRemaining > 0;
        internal bool IsHeldForCombat => combatHold;
        internal bool IsCommittedToCombat => combatHold || combatApproachTarget != null;

        // some helper attributes
        public bool IsWithBoss
        {
            get { if (Player == null) return false; return Player.Party.Contains(this); }
        }

        public bool IsStationed
        {
            get { if (Player == null) return false; return !Player.Party.Contains(this); }
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
        internal int CombatExperience(GameSettings settings) =>
            RuleFormulas.CombatExperience(settings.IsFightClass(Class), Experience);
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
        internal bool HasItemFunction(ItemFunction function) =>
            Items?.Any(item => item.Type.HasFunction(function)) == true;
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
                        location.Object.Characters.Remove(this);

                    location = null;
                }
                else
                {
                    if (location != null)
                        location.Object.Characters.Remove(this);

                    value.Characters.Add(this);

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
            Player.Party.Remove(this);
            Location = Player.Location;
            Location.Player = Player;
            Position = Location.EntryPoint;
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
                Player.Party.Add(this);

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
            boss.Party.Add(this);
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
                Player.Party.Remove(this);
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
            health = 95;
            Food = 8;
            Water = 4;
            Items.Clear();

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

        internal float AttackRange
        {
            get
            {
                // Amiga uses a short innate range for mutants and a wider
                // autonomous range for dogs/traders. Inventory-using characters
                // take the range from the weapon selected by normal combat.
                if (Class == CharClass.Mutant)
                    return 16;
                if (Class is CharClass.Dog or CharClass.Trader)
                    return 24;

                Item? selected = Items == null ? null : FindOriginalWeapon();
                return selected?.Type.AttackRange > 0
                    ? selected.Type.AttackRange
                    : 16;
            }
        }

        public bool IsInAttackRange(Character target) =>
            IsInAttackRange(target, AttackRange);

        internal bool IsInAttackRange(Character target, float range)
        {
            Vector2f difference = Position - target.Position;
            return difference.x * difference.x + difference.y * difference.y <= range * range;
        }

        internal void BeginCombatApproach(Character target, float range)
        {
            // A new attack starts a new exchange. Do not let the retreat from
            // the previous exchange override this approach for several seconds;
            // the encounter will start a fresh flee after all new responses.
            fleeTimeRemaining = 0;
            fleeFromCharacter = null;
            fleeDestination = Position;
            combatHold = false;
            combatApproachTarget = target;
            combatApproachRange = range;
            Mind?.MoveToObject(null);
            if (Path == null || Path is PathFinding.ManualPath)
            {
                Path = container.Create<PathFinding.ComplexPath>();
                Path.MoveTo = Position;
            }

            // Replace an existing formation or autonomous route immediately.
            // Subsequent updates can track a moving target without replanning
            // every frame.
            if (Path is PathFinding.ComplexPath complexPath)
                complexPath.UpdateMovingTarget(target.Position, forceRepath: true);
            else
                Path.MoveTo = target.Position;
        }

        internal void ClearCombatApproach(Character target)
        {
            if (combatApproachTarget != target)
                return;

            combatApproachTarget = null;
            combatApproachRange = 0;
            Path?.Stop(Position);
        }

        internal void HoldForCombat()
        {
            fleeTimeRemaining = 0;
            fleeFromCharacter = null;
            fleeDestination = Position;
            combatApproachTarget = null;
            combatApproachRange = 0;
            combatHold = true;
            Mind?.MoveToObject(null);
            Path?.Stop(Position);
        }

        internal void ReleaseCombatHold()
        {
            combatHold = false;
        }

        void UpdateCombatApproach()
        {
            Character target = combatApproachTarget;
            if (target == null)
                return;
            if (target.IsDead || target.Location != Location)
            {
                ClearCombatApproach(target);
                return;
            }

            if (IsInAttackRange(target, combatApproachRange))
            {
                Path?.Stop(Position);
                return;
            }

            if (Path is PathFinding.ComplexPath complexPath)
                complexPath.UpdateMovingTarget(target.Position, forceRepath: false);
            else if (Path != null)
                Path.MoveTo = target.Position;
        }

        internal bool ResolveSingleAttack(Character defender, bool useAmmo = true)
        {
            if (IsDead || defender.IsDead)
                return false;

            Root.RuleBook.DealAttackDamage(this, defender, useAmmo);
            container.Notify(new AttackEvent(this, defender));
            if (defender.Player?.AiState is AI.ClassicAiState strategicAi)
                strategicAi.RecordAttack(this, defender);
            return true;
        }

        public void Attack(Character defender, bool defendWithAmmo = true)
        {
#warning TODO attacks against camps should involve every camp member

            if (IsDead || defender.IsDead)
                return;

            var attackingGroup = (Player != null && Player.Character == this && !Player.SingleMode)
                ? Player.Party.ToArray()
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
            Item? selected = Weapon != null && Items.Contains(Weapon)
                ? Weapon
                : null;
            if (selected?.DamageValue == 0 &&
                !(allowUnloadedRifle && selected.ID == "item_unloaded_rifle"))
                selected = null;
            if (selected == null)
            {
                // Firearms are an explicit ammunition-use choice for human
                // players. AI weapon policy continues to manage its own ammo.
                selected = Player?.Type == PlayerType.Human
                    ? Items.Where(item => item.DamageValue > 0 &&
                        !item.ConsumesAmmo)
                        .OrderBy(item => item.Type.WeaponPriority)
                        .LastOrDefault()
                    : Items.FindBestWeapon();
            }
            else
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

        internal void BeginFleeFrom(Character attacker) => FleeFrom(attacker);

        Vector2 FindFleeDestination(Character attacker)
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
            if (location is null || location.Map.Mask.IsWalkableMapPosition(destination))
                return destination;

            // Try nearby escape angles when the direct route ends outside the
            // walkable map. Prefer continuing generally away from the attacker.
            float[] angles = { 45, -45, 90, -90, 135, -135, 180 };
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
                    return candidate;
                }
            }

            return Position;
        }

        void FleeFrom(Character attacker)
        {
            Vector2 destination = FindFleeDestination(attacker);
            if (destination == Position)
                return;

            Mind?.MoveToObject(null);
            combatHold = false;
            combatApproachTarget = null;
            combatApproachRange = 0;
            if (Path == null || Path is PathFinding.ManualPath)
                Path = container.Create<PathFinding.ComplexPath>();
            Path.MoveTo = destination;
            fleeFromCharacter = attacker;
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

            Root.RuleBook.TurnEmployedCharacter(
                this, AI.AiStateOperations.GetNaturalHealingThreshold(Player.AiState));

            if (IsDead)
                return;

            ResetStationedMind();

            //Dialog.Turn();
        }

        internal bool HasLocalDoctor => IsWithBoss
            ? Player.Party.Any(member => member.Class == CharClass.Doctor)
            : Location?.CampNPC.Any(member => !member.IsDead &&
                member.Class == CharClass.Doctor && member.Player == Player) == true;

        internal void TurnExtendedEmployed(int? naturalHealingThreshold, bool skipSupplies)
        {
            ICharacterCollection group = GetGroup();

            if (!skipSupplies && Food == 0)
            {
                Item? item = FindAccessibleFood(out IItemCollection? owner);
                if (item != null && owner != null)
                {
                    group.Eat(null, item.FoodValue);
                    owner.Remove(item);
                }
            }

            // npc is with boss
            if (!skipSupplies && IsWithBoss)
            {
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
            else if (!skipSupplies) // npc is stationed
            {
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
            int healingThreshold = RuleFormulas.NaturalHealingThreshold(
                doctorAvailable, naturalHealingThreshold);
            int stabilization = RuleFormulas.DoctorStabilizationHealing(
                Health, doctorAvailable, Food > 0 && Water > 0,
                Root.RuleBook.Settings.DoctorStabilization);
            if (stabilization > 0)
                health += stabilization;
            else if (health >= healingThreshold)
                health += doctorAvailable ? 4 : 2;

            if (health > 100)
                health = 100;
            if (!skipSupplies)
            {
                if (Food == 0)
                    health -= 25;
                if (Water == 0)
                    health -= 25;
            }

            if (health <= 0)
            {
                Die();
                return;
            }

            if (!skipSupplies)
            {
                Food--;
                if (Food < 0)
                    Food = 0;
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
            bool isActiveGroup = activePlayer?.Party.Contains(this) == true;
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
            bool isInTalkingDistance = !combatHold && combatApproachTarget == null &&
                fleeTimeRemaining <= 0 && IsHuman && !isActiveGroup && !isPlayerControlled &&
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
                if (!isFleeing && combatApproachTarget == null)
                    Path.Speed = 0;
            }
            if (isHovered && !isFleeing && combatApproachTarget == null &&
                !isActiveGroup && !isPlayerControlled)
                Path.Speed = 0;

            Vector2 old = new Vector2(position);

            // process dangers (only if hired)
            if (Player != null && Root.RuleBook.ApplyContinuousHazard(this, elapsed))
            {
                return;
            }

            // Combat movement owns the character until its strike is resolved.
            // In particular, FellowerMind must not restore formation movement,
            // and CreatureMind must not deliver an additional autonomous hit.
            if (combatApproachTarget == null && fleeTimeRemaining <= 0 && !combatHold)
                Mind.Process(elapsed);

            // A local combat order temporarily takes precedence over formation
            // following and ordinary autonomous movement. Fleeing is applied
            // afterwards and therefore remains the final movement authority.
            UpdateCombatApproach();

            if (fleeTimeRemaining > 0)
            {
                fleeTimeRemaining = System.Math.Max(0, fleeTimeRemaining - elapsed);
                if (fleeTimeRemaining <= 0)
                {
                    fleeFromCharacter = null;
                    Path.Stop(Position);
                }
                else
                {
                    // Reaching one segment does not end the retreat. Continue
                    // choosing walkable destinations until the flee timer ends.
                    if ((fleeDestination - Position).Length <= 4 &&
                        fleeFromCharacter != null)
                    {
                        Vector2 nextDestination = FindFleeDestination(fleeFromCharacter);
                        if (nextDestination != Position)
                            fleeDestination = nextDestination;
                    }
                    Path.MoveTo = fleeDestination;
                }
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
            if (Player != null && Player.Party.Contains(this))
            {
                return Player.Party;
            }

            return this;
        }

        internal Item? FindAccessibleFood(out IItemCollection? owner)
        {
            ICharacterCollection inventory = GetGroup();
            Item? item = inventory.FindFood(out IItemCollection inventoryOwner);
            if (item != null)
            {
                owner = inventoryOwner;
                return item;
            }

            if (!IsWithBoss && Location != null)
                return Location.FindFood(out owner);

            owner = null;
            return null;
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
                if (Items[j].FoodValue > 0 &&
                    (item == null || Items[j].FoodValue < item.FoodValue))
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
