using System.Collections.Generic;
using Burntime.Framework;
using Burntime.Platform;
using Burntime.Platform.Graphics;
using Burntime.Platform.Resource;

namespace Burntime.Remaster.Tests;

using static Program;

static class TooltipTextTests
{
    public static IEnumerable<Case<string>> SubstitutionCases()
    {
        TestResourceManager resources = new();

        TextHelper damage = new(resources, "tooltip");
        damage.AddArgument("{damage}", "9-20");
        yield return new("knife damage", "Damage 9-20",
            () => damage.Get(16));

        TextHelper values = new(resources, "tooltip");
        values.AddArgument("{food}", 7);
        values.AddArgument("{water}", 5);
        yield return new("food value", "Food 7",
            () => values.Get(32));
        yield return new("water value", "Water 5",
            () => values.Get(33));

        TextHelper recipe = new(resources, "tooltip");
        recipe.AddArgument("{recipe}", "Rat Trap");
        yield return new("known recipe", "Can build Rat Trap",
            () => recipe.Get(42));

        TextHelper unknown = new(resources, "tooltip");
        unknown.AddArgument("{count}", 2);
        yield return new("unknown recipe count", "Can build ??? x2",
            () => unknown.Get(44));

        TextHelper trap = new(resources, "tooltip");
        trap.AddArgument("{food}", 3);
        yield return new("trap production", "Yields 3 food daily",
            () => trap.Get(39));

        TextHelper capacity = new(resources, "tooltip");
        capacity.AddArgument("{water}", 5);
        yield return new("container capacity", "Capacity 5",
            () => capacity.Get(40));

        TextHelper production = new(resources, "tooltip");
        production.AddArgument("{product}", "Maggots");
        production.AddArgument("{tools}", "Knife/Axe/Pitchfork");
        yield return new("production overview", "Maggots: Knife/Axe/Pitchfork",
            () => production.Get(66));
        yield return new("production heading", "Food Production",
            () => production.Get(65));
        yield return new("group supplied", "Group is full",
            () => production.Get(69));
        yield return new("refill blocked", "Not enough water",
            () => production.Get(70));
        yield return new("construction blocked", "Needs materials",
            () => production.Get(71));
        yield return new("reload blocked", "Needs ammunition",
            () => production.Get(72));
        yield return new("previous production", "Previous",
            () => production.Get(73));
        yield return new("next production", "Next",
            () => production.Get(74));
    }

    sealed class TestResourceManager : ResourceManagerBase
    {
        public TestResourceManager() : base(new TestLoadingCounter()) { }
        public override Font? GetFont(string file, PixelColor color) => null;
        public override Font? GetFont(string file, PixelColor color,
            PixelColor backColor) => null;
        public override ISprite GetImage(ResourceID id,
            ResourceLoadType loadType = ResourceLoadType.Delayed) => null!;
        public override void Reload(ISprite sprite,
            ResourceLoadType loadType = ResourceLoadType.Delayed) { }
    }

    sealed class TestLoadingCounter : ILoadingCounter
    {
        public void IncreaseLoadingCount() { }
        public void DecreaseLoadingCount() { }
    }
}
