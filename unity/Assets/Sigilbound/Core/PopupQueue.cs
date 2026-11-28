// One pop-up at a time (QA review #8, P2-24): the first-time tutorial, What's New, the update banner,
// first-time tips and the Act II intro used to draw on their own and could pile up on a first launch.
// The client fills a PopupContext a few times a second and shows only what Next returns. Unity-free so
// Tests~ can check the order.
//
// Order, highest first:
//   1. A save-problem notice (shown at start-up; nothing else until it's closed).
//   2. The tutorial's coach panel: while it is up, nothing else opens.
//   3. What's New, on Home only, and never on the very first launch (a new player has nothing to compare).
//   4. The Act II intro, on the campaign map.
//   5. First-time tips (one at a time, in Tips.All order).
//   6. The update banner, last, outside battle, when nothing else is showing.
// Between two pop-ups there is a short gap so a tap meant for the closing one never lands on the next.
namespace Sigilbound.Core
{
    public enum Popup { None, LoadProblem, WhatsNew, Act2Intro, Tip, UpdateBanner }

    public struct PopupContext
    {
        public bool ModalOpen;        // any modal is open (the queue's own or a screen's)
        public bool TutorialBlocking; // the tutorial's coach panel is on screen
        public bool FirstLaunch;      // this run started with no save (or after "Reset save")
        public bool InBattle, OnHome, OnMap;
        public bool LoadProblem, WhatsNewPending, Act2Pending, TipPending, UpdateAvailable;
        public double SinceClose;     // seconds since the last modal closed
    }

    public static class PopupQueue
    {
        public const double Gap = 0.8;
        public static readonly Popup[] Order = { Popup.LoadProblem, Popup.WhatsNew, Popup.Act2Intro, Popup.Tip, Popup.UpdateBanner };

        public static Popup Next(PopupContext c)
        {
            if (c.ModalOpen) return Popup.None;
            if (c.LoadProblem) return Popup.LoadProblem;
            if (c.SinceClose < Gap || c.TutorialBlocking) return Popup.None;
            if (c.WhatsNewPending && !c.FirstLaunch && c.OnHome && !c.InBattle) return Popup.WhatsNew;
            if (c.Act2Pending && c.OnMap && !c.InBattle) return Popup.Act2Intro;
            if (c.TipPending) return Popup.Tip;
            if (c.UpdateAvailable && !c.InBattle) return Popup.UpdateBanner;
            return Popup.None;
        }

        // What's New is owed on this launch: there are unseen notes and this isn't the first launch (a first
        // launch marks every note seen instead, so the next update shows only what's new since).
        public static bool WhatsNewOwed(bool firstLaunch, int unseen) => !firstLaunch && unseen > 0;
    }
}
