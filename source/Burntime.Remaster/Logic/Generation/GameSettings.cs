using Burntime.Platform.IO;
using System;
using System.Collections.Generic;

namespace Burntime.Remaster.Logic.Generation;

class GameSettings
{
    public struct RespawnTimes
    {
        public int NPC;
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

            if (rates.Length > 1)
            {
                if (!int.TryParse(rates[0], out min))
                    min = 1;
                if (rates.Length > 2 && !int.TryParse(rates[1], out max))
                    max = 1;
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
            generation.Maximum = Math.Min(generation.Minimum, upTo);

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
    AI.AiSettings ai;
    ClassStatInfos stats;

    public string[] StartItems => config[difficulty].GetStrings("start_items");

    public int StartRegionCount => config[difficulty].GetInt("start_regions");
    public int[] GetStartLocation(int region) => config[difficulty].GetInts($"start_locations_{region}");

    public ConfigSection GetRegionItem(int entry) => config.GetSection($"region_item_{entry}");

    public int StartExperience => config[difficulty].GetInt("start_experience");
    public string[] RandomItems => config[difficulty].GetStrings("random_items");
    public int RandomItemsMin => config[difficulty].GetInt("random_items_rate_min");
    public int RandomItemsMax => config[difficulty].GetInt("random_items_rate_max");

    public RespawnTimes Respawn => respawn;
    public ClassStatInfos ClassStats => stats;

    public int StartHealth => 100;
    public int StartFood => 9;
    public int StartWater => 5;

    public AI.AiSettings AiSettings => ai;

    public GameSettings(string file)
    {
        config = new ConfigFile();
        config.Open(file);
    }

    public void SetDifficulty(int difficulty)
    {
        this.difficulty = difficulty.ToString();

        respawn.NPC = config[this.difficulty].GetInt("npc_respawn");
        respawn.Trader = config[this.difficulty].GetInt("trader_respawn");
        respawn.Mutant = config[this.difficulty].GetInt("mutant_respawn");
        respawn.Dog = config[this.difficulty].GetInt("dog_respawn");

        stats.TraderHealth = config[this.difficulty].GetInt("trader_health");
        stats.MutantHealth = config[this.difficulty].GetInt("mutant_health");
        stats.DogHealth = config[this.difficulty].GetInt("dog_health");
        stats.TraderAttack = config[this.difficulty].GetInt("trader_attack");
        stats.MutantAttack = config[this.difficulty].GetInt("mutant_attack");
        stats.DogAttack = config[this.difficulty].GetInt("dog_attack");

        ai.MinInterval = config[this.difficulty].GetInts("ai_camp_interval")[0];
        ai.MaxInterval = config[this.difficulty].GetInts("ai_camp_interval")[1];
        ai.MaxAdvance = config[this.difficulty].GetInt("ai_camp_max_advance");
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
