// The lair reveal (owner pick C): every chapter's lair has a reveal target, a landmark piece of its
// stage-6 set with a plausible height, and a title for the card.
using System.Linq;
using Sigilbound.Core;

static class LairRevealTests
{
    public static void Run(System.Action<bool, string> check)
    {
        var missing = Enumerable.Range(0, GameData.Chapters.Length).Where(c => StoryScenes.RevealOf(c) == null).Select(c => GameData.Chapters[c]).ToList();
        check(missing.Count == 0, "lair reveal: every chapter's lair has a reveal landmark" + (missing.Count > 0 ? " — missing: " + string.Join(", ", missing) : ""));
        check(Enumerable.Range(0, GameData.Chapters.Length).All(c => StoryScenes.RevealOf(c) is (Piece p, float top) && top >= 3f && top <= 20f && !p.Far),
            "lair reveal: each landmark stands in the lair (not a distant copy) and is 3-20 m tall");
        check(StoryScenes.LairTitle(1) == "The Half-Sunk Mill" && StoryScenes.LairTitle(2) == "The Graveyard in the Pass" && StoryScenes.LairTitle(14) == "The Sea-Cliff Throne",
            "lair reveal: title-cased lair names for the card");
    }
}
