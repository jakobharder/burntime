namespace Burntime.Platform.Graphics;

/// <summary>Session-only opt-in for the new character artwork and animations.</summary>
public static class CharacterGraphicsOptions
{
    public const string CommandLineFlag = "--experimental-characters";
    public static bool Enabled { get; set; }

    public static void Configure(string[] arguments) =>
        Enabled = arguments.Any(argument =>
            argument.Equals(CommandLineFlag, StringComparison.OrdinalIgnoreCase));
}
