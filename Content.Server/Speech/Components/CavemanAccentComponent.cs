using Content.Server.Speech.EntitySystems;

namespace Content.Server.Speech.Components;

[RegisterComponent]
[Access(typeof(CavemanAccentSystem))]
public sealed partial class CavemanAccentComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("MaxWordLength")]
    public static int MaxWordLength = 6; // so caveman not be verbose

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("MinWordLengthToRemoveForbiddenSuffix")]
    public static int MinWordLengthToRemoveForbiddenSuffix = 5; // 5 means going = go, but ring != ri

    [ViewVariables]
    public static readonly List<string> ForbiddenWords = new()
    {
        "accent-caveman-forbidden-words-0",
        "accent-caveman-forbidden-words-1",
        "accent-caveman-forbidden-words-2",
        "accent-caveman-forbidden-words-3",
        "accent-caveman-forbidden-words-4",
        "accent-caveman-forbidden-words-5",
        "accent-caveman-forbidden-words-6",
        "accent-caveman-forbidden-words-7",
    };

    [ViewVariables]
    public static readonly List<string> Grunts = new()
    {
        "accent-caveman-grunts-0",
        "accent-caveman-grunts-1",
        "accent-caveman-grunts-2",
        "accent-caveman-grunts-3",
        "accent-caveman-grunts-4",
        "accent-caveman-grunts-5",
        "accent-caveman-grunts-6",
        "accent-caveman-grunts-7",
        "accent-caveman-grunts-8",
        "accent-caveman-grunts-9",
        "accent-caveman-grunts-10",
        "accent-caveman-grunts-11",
        "accent-caveman-grunts-12",
        "accent-caveman-grunts-13",
        "accent-caveman-grunts-14",
        "accent-caveman-grunts-15",
        "accent-caveman-grunts-16",
        "accent-caveman-grunts-17",
        "accent-caveman-grunts-18",
    };

    [ViewVariables]
    public static readonly List<string> Numbers = new()
    {
        "accent-caveman-numbers-0",
        "accent-caveman-numbers-1",
        "accent-caveman-numbers-2",
        "accent-caveman-numbers-3",
        "accent-caveman-numbers-4",
        "accent-caveman-numbers-5",
        "accent-caveman-numbers-6",
        "accent-caveman-numbers-7",
        "accent-caveman-numbers-8",
        "accent-caveman-numbers-9",
        "accent-caveman-numbers-10",
        "accent-caveman-numbers-11",
    };

    [ViewVariables]
    public static readonly List<string> ForbiddenSuffixes = new()
    {
        "accent-caveman-forbidden-suffixes-0",
        "accent-caveman-forbidden-suffixes-1",
        "accent-caveman-forbidden-suffixes-2"
    };

    [ViewVariables]
    public static readonly List<string> Endings = new()
    {
        ".",
        "!",
        "!!"
    };

}
