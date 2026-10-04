using System;
using System.Linq;
using Burntime.Platform;
using Burntime.Platform.IO;
using Burntime.Platform.Resource;
using Burntime.Remaster.PathFinding;

namespace Burntime.Remaster.Logic.Generation;

/// <summary>Map-defined residents and guaranteed loot, applied only during new-game creation.</summary>
internal sealed class LocationPopulationCreator
{
    public void Create(ClassicGame game, Burntime.Data.BurnGfx.Save.SaveGame source, IResourceManager resources)
    {
        foreach (Location location in game.World.Locations)
        {
            string configPath = $"maps/mat_{location.Id + 1:D3}.txt";
            if (!FileSystem.ExistsFile(configPath))
                continue;

            var config = new ConfigFile();
            if (!config.Open(configPath))
                continue;

            foreach (var section in config.GetAllSections().Where(s => s.Name.StartsWith("resident")))
            {
                if (!Enum.TryParse(section.GetString("class"), out CharClass kind) ||
                    kind is not (CharClass.Dog or CharClass.Mercenary or CharClass.Technician or CharClass.Doctor))
                    continue;

                // Borrow original visual/identity defaults, without moving an existing resident.
                var template = source.Characters.FirstOrDefault(c => kind == CharClass.Dog
                    ? c.Info.NameId == 178
                    : (CharClass)c.Type == kind && c.Info.LocationId > 0);
                if (template == null)
                {
                    Log.Warning($"No character template for {section.Name} at location {location.Id}.");
                    continue;
                }
                var info = template.Info;
                var container = game.Container;
                Character character = kind == CharClass.Dog ? container.Create<Dog>() : container.Create<Character>();
                character.Class = kind;
                character.Health = kind == CharClass.Dog ? game.World.Respawn.Object.DogHealth : info.Health;
                character.FaceID = info.FaceId;
                string name = section.GetString("name");
                character.NameId = string.IsNullOrEmpty(name) ? $"burn?{info.NameId - 1}" : name;
                character.SetBodyId = Helper.GetSetBodyId(kind);
                int sprite = kind == CharClass.Dog ? info.SpriteId : Helper.GetBodyId(character.SetBodyId, 0);
                character.Body = resources.GetData($"burngfxani@syssze.raw?{sprite}-{sprite + 15}", ResourceLoadType.LinkOnly);
                character.Dialog.MenFile = section.GetInt("dialog");
                character.Dialog.Parent = character;
                character.Path = container.Create<SimplePath>();
                character.Mind = kind == CharClass.Dog
                    ? container.Create<AI.CreatureMind>(new object[] { character })
                    : container.Create<AI.SimpleMind>(new object[] { character });
                character.Items.MaxCount = 6;
                character.Experience = info.Experience;
                character.Food = info.Food;
                character.Water = info.Water;
                foreach (string item in new[] { "item_snake", "item_meat", "item_water_bottle" })
                    if (game.ItemTypes.Contains(item))
                        character.HireItems += game.ItemTypes[item];
                character.Location = location;
                character.Position = PathState.GetNearestWalkablePosition(location.Map.Mask, section.GetVector2("position"));
                character.Path.Stop(character.Position);
                game.World.AllCharacters += character;
            }

            foreach (string item in config[""].GetStrings("initial_ground_items"))
                if (game.ItemTypes.Contains(item))
                    location.DropItemRandom(game.ItemTypes.Generate(item));
            int roomIndex = 0;
            var rooms = location.Rooms.Where(room => !room.Items.IsFull).ToArray();
            foreach (string item in config[""].GetStrings("initial_room_items"))
                if (game.ItemTypes.Contains(item))
                    location.StoreItem(game.ItemTypes.Generate(item), preferredRoom:
                        rooms.Length > 0 ? rooms[roomIndex++ % rooms.Length] : null);
        }
    }
}
