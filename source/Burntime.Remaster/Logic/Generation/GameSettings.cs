using Burntime.Platform.IO;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster.Logic.Generation;

class GameSettings
{
    public struct RespawnTimes
    {
        public RespawnMethod Method;
        public int NPC;
        public int CitySpawnThreshold;
        public int Trader;
        public int Dog;
        public int Mutant;
    }

    public struct ClassStatInfos
    {
        public int TraderHealth;
        public int MutantHealth;
        public int DogHealth;

        public int TraderAttack;
        public int MutantAttack;
        public int DogAttack;
    }

    public struct ItemGeneration
    {
        public string[] Include;
        public string[] Exclude;
        public int Minimum;
        public int Maximum;

        public int RandomCount
        {
            get
            {
                if (Minimum == Maximum)
                    return Minimum;
                return Burntime.Platform.Math.Random.Next() % (Maximum - Minimum) + Minimum;
            }
        }

        public static ItemGeneration FromString(string itemsConfig, string rateConfig = "1")
        {
            int min = 1;
            int max = 1;
            var rates = rateConfig.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (rates.Length > 0)
            {
                if (!int.TryParse(rates[0], out min))
                    min = 1;
                max = min;
                if (rates.Length > 1 && !int.TryParse(rates[1], out max))
                    max = min;
            }

            return FromStrings(
                itemsConfig.Split(' ', StringSplitOptions.RemoveEmptyEntries),
                min, max);
        }

        public static ItemGeneration FromStrings(IEnumerable<string> itemsConfig, int atLeast = 1, int upTo = 1)
        {
            var include = new List<string>();
            var exclude = new List<string>();
            var generation = new ItemGeneration();

            foreach (string item in itemsConfig)
            {
                if (item.StartsWith("-"))
                    exclude.Add(item[1..]);
                else
                    include.Add(item);
            }

            generation.Include = include.ToArray();
            generation.Exclude = exclude.ToArray();

            generation.Minimum = Math.Max(0, atLeast);
            generation.Maximum = Math.Max(generation.Minimum, upTo);

            return generation;
        }

        public List<Item> GenerateAll(ItemTypes types)
        {
            string[] itemTypes = types.GetTypesWithClass(Include, Exclude);
            int count = RandomCount;
            var items = new List<Item>();

            for (int i = 0; i < count; i++)
            {
                string insert = itemTypes[Platform.Math.Random.Next(0, itemTypes.Length)];
                items.Add(types[insert].Generate());
            }

            return items;
        }
    }

    ConfigFile config;
    string difficulty;
    RespawnTimes respawn;
    ClassStatInfos stats;

    public string[] StartItems => config[difficulty].GetStrings("start_items");
    public string[] GetStartItems(int difficultyLevel) =>
        config[difficultyLevel.ToString()].GetStrings("start_items");

    public int StartRegionCount => config[difficulty].GetInt("start_regions");
    public int[] GetStartLocation(int region) => config[difficulty].GetInts($"start_locations_{region}");

    public int OriginalStartGroupCount => config["original_start_locations"].GetInt("groups");
    public int[] GetOriginalStartLocations(int group) =>
        config["original_start_locations"].GetInts($"group_{group}");

    public ConfigSection GetRegionItem(int entry) => config.GetSection($"region_item_{entry}");

    public int StartExperience => config["rules"].GetInt("start_experience");
    public string StartLocationRules => config["rules"].GetString("start_locations");
    public string BossExperienceRules => config["rules"].GetString("boss_experience");
    public string RecruitmentRules => config["rules"].GetString("recruitment");
    public string TraderRefreshRules => config["rules"].GetString("trader_refresh");
    public string WaterOutputRules => config["rules"].GetString("water_output");
    public string PlayerSetupRules => config["rules"].GetString("player_setup");
    public string InitialItemRules => config["rules"].GetString("initial_items");
    public string RecruitSupplyRules => config["rules"].GetString("recruit_supplies");
    public string TraderInventoryRules => config["rules"].GetString("trader_inventory");
    public string FoodProductionRules => config["rules"].GetString("food_production");
    public string SurvivalRules => config["rules"].GetString("survival");
    public string ServiceValueRules => config["rules"].GetString("service_value");
    public string CombatRules => config["rules"].GetString("combat");
    public string[] RandomItems => config[difficulty].GetStrings("random_items");
    public int RandomItemsMin => config[difficulty].GetInt("random_items_rate_min");
    public int RandomItemsMax => config[difficulty].GetInt("random_items_rate_max");
    public float MutantDropChance => Math.Clamp(config[difficulty].GetFloat("mutant_drop_chance"), 0.0f, 1.0f);
    public string[] MutantDropType => config[difficulty].GetStrings("mutant_drop_type");

    public RespawnTimes Respawn => respawn;
    public ClassStatInfos ClassStats => stats;

    public int GetBarterFactor(int level) => config[Math.Clamp(level, 0, 2).ToString()].GetInt("barter_factor");
    public float DoctorHealingFactor => config["rules"].GetFloat("healing_factor");
    public int DoctorHealthCap => config["rules"].GetInt("health_cap");
    public int CombatTierWidth => config["rules"].GetInt("experience_tier_width");
    public string[] FightClasses => config["rules"].GetStrings("fight_class");
    public int[] GetTraderAttack(int difficultyLevel) =>
        config[Math.Clamp(difficultyLevel, 0, 2).ToString()].GetInts("trader_attack");
    public int[] GetMutantAttack(int difficultyLevel) =>
        config[Math.Clamp(difficultyLevel, 0, 2).ToString()].GetInts("mutant_attack");
    public int[] GetDogAttack(int difficultyLevel) =>
        config[Math.Clamp(difficultyLevel, 0, 2).ToString()].GetInts("dog_attack");
    public bool IsFightClass(CharClass characterClass)
    {
        string name = characterClass switch
        {
            CharClass.Mercenary => "fighter",
            CharClass.Technician => "technician",
            CharClass.Doctor => "doctor",
            CharClass.Boss => "boss",
            CharClass.Mutant => "mutant",
            CharClass.Trader => "trader",
            CharClass.Dog => "dog",
            _ => "",
        };
        return Array.Exists(FightClasses,
            configured => configured.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    public float HazardDamage(string type) => config["rules"].GetFloat(type + "_damage_per_second");
    public bool IsHazardImmune(string type, int face) =>
        Array.IndexOf(config["rules"].GetInts(type + "_immune_faces"), face) >= 0;

    public int StartHealth => 100;
    public int StartFood => 9;
    public int StartWater => 5;

    public GameSettings(string file)
    {
        config = new ConfigFile();
        config.Open(file);
    }

    public void SetDifficulty(int difficulty)
    {
        this.difficulty = difficulty.ToString();

        string method = config["rules"].GetString("spawn_method");
        respawn.Method = method.ToLowerInvariant() switch
        {
            "dos_player_cycle" => RespawnMethod.PlayerCycle,
            "amiga_location_cycle" => RespawnMethod.LocationCycle,
            _ => RespawnMethod.Timer,
        };

        // npc_respawn is retained as a fallback for custom and older rulesets.
        respawn.NPC = string.IsNullOrWhiteSpace(config[this.difficulty].GetString("npc_spawn"))
            ? config[this.difficulty].GetInt("npc_respawn")
            : config[this.difficulty].GetInt("npc_spawn");
        respawn.CitySpawnThreshold = config[this.difficulty].GetInt("city_spawn_threshold");
        respawn.Trader = config[this.difficulty].GetInt("trader_respawn");
        respawn.Mutant = config[this.difficulty].GetInt("mutant_respawn");
        respawn.Dog = config[this.difficulty].GetInt("dog_respawn");

        stats.TraderHealth = config[this.difficulty].GetInt("trader_health");
        stats.MutantHealth = config[this.difficulty].GetInt("mutant_health");
        stats.DogHealth = config[this.difficulty].GetInt("dog_health");
        stats.TraderAttack = config[this.difficulty].GetInt("trader_attack");
        stats.MutantAttack = config[this.difficulty].GetInt("mutant_attack");
        stats.DogAttack = config[this.difficulty].GetInt("dog_attack");

    }

    public ItemGeneration GetItemGeneration(string name)
    {
        string[] itemclass = config[difficulty].GetStrings(name);
        List<string> include = new List<string>();
        List<string> exclude = new List<string>();

        ItemGeneration generation;

        foreach (string item in itemclass)
        {
            if (item.StartsWith("-"))
                exclude.Add(item.Substring(1));
            else
                include.Add(item);
        }

        generation.Include = include.ToArray();
        generation.Exclude = exclude.ToArray();

        int[] rate = config[difficulty].GetInts(name + "_rate");

        generation.Minimum = rate.Length > 0 ? rate[0] : 0;
        generation.Maximum = rate.Length > 1 ? rate[1] : generation.Minimum;

        return generation;
    }
}
