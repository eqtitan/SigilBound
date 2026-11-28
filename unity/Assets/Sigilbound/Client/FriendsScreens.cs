// Friends, friend profile, chat and blocked-player screens.
// Player-written text (names, messages) is drawn with rich text off, so it can't inject markup.
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        public Online Online;
        string _friendId; string _nameInput = "", _codeInput = "", _chatInput = "";
        Vector2 _chatScroll;

        static GUIStyle Plain(int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.MiddleLeft, bool wrap = false) =>
            Ui.Style($"plain{size}{c}{fs}{a}{wrap}", () => new GUIStyle(Ui.Label(size, c, fs, a, wrap)) { richText = false });
        static GUIStyle Field(int size = 16) => Ui.Style($"field{Ui.Sized(size)}", () => new GUIStyle
        {
            fontSize = Ui.Sized(size), // Settings > Accessibility > Text size alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 6, 6), clipping = TextClipping.Clip,
            normal = { background = Ui.RoundTex(Palette.Panel2, Palette.Line, 10), textColor = Palette.Text },
            focused = { background = Ui.RoundTex(Palette.Panel2, Palette.Gold, 10), textColor = Palette.Text },
            border = new RectOffset(12, 12, 12, 12), richText = false,
        });

        public void OpenFriends()
        {
            Go(ScreenId.Friends);
            if (!Online.Probed) Online.Probe(); else if (Online.SignedIn) Online.Refresh();
        }

        void DrawFriends(Rect area)
        {
            var on = Online;
            float y0 = area.y + 8;
            if (!on.Probed || on.Busy && on.Data == null)
            {
                Ui.Text(new Rect(Pad, y0, 300, 32), "Friends", 22, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(Pad, y0 + 44, Ui.W - 32, 30), "Connecting…", 14, Palette.Dim);
                return;
            }
            if (on.Unavailable != null)
            {
                Ui.Text(new Rect(Pad, y0, 300, 32), "Friends", 22, Palette.Text, FontStyle.Bold);
                var r = new Rect(Pad, y0 + 44, Ui.W - 32, 170);
                Ui.Panel(r, new Color(0.12f, 0.1f, 0.17f, 0.95f));
                Ui.Text(new Rect(r.x + 14, r.y + 10, r.width - 28, 24), "Friends and chat aren't available right now", 15, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(r.x + 14, r.y + 40, r.width - 28, 70), on.Unavailable + "\nEverything else in the game works offline.", 13, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
                if (Ui.Button(new Rect(r.x + 14, r.yMax - 56, r.width - 28, Mathf.Max(44, Ui.Tap)), "Try again", Ui.Kind.Secondary)) { on.Probed = false; on.Probe(); }
                return;
            }
            if (!on.SignedIn)
            {
                Ui.Text(new Rect(Pad, y0, 300, 32), "Friends", 22, Palette.Text, FontStyle.Bold);
                var r = new Rect(Pad, y0 + 44, Ui.W - 32, 230);
                Ui.Panel(r, new Color(0.12f, 0.1f, 0.17f, 0.95f));
                Ui.Text(new Rect(r.x + 14, r.y + 10, r.width - 28, 24), "Create your online profile", 16, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + 14, r.y + 38, r.width - 28, 70), "Pick the name friends will see. You'll get a friend code to share. No email or password needed. Your profile is tied to this device.", 13, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
                if (string.IsNullOrEmpty(_nameInput) && !string.IsNullOrEmpty(G.S.playerName)) _nameInput = G.S.playerName;
                _nameInput = GUI.TextField(new Rect(r.x + 14, r.y + 116, r.width - 28, 44), _nameInput, 16, Field());
                if (Ui.Button(new Rect(r.x + 14, r.y + 172, r.width - 28, 44), on.Busy ? "Creating…" : "Create profile", Ui.Kind.Primary, 16, !on.Busy)) on.Register(_nameInput);
                return;
            }
            var d = on.Data;
            if (d == null) { Ui.Text(new Rect(Pad, y0, 300, 32), "Loading friends…", 16, Palette.Dim); return; }

            float content = 330 + d.incoming.Count * 56 + Mathf.Max(1, d.friends.Count) * 64 + d.outgoing.Count * 48 + 160;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            // A tab root: the ribbon banner, like Home, Summon and Trials (design rule 2).
            Ui.Banner(new Rect(Pad + 10, y + 6, 200, 30), "Friends", 19);
            if (Ui.Button(new Rect(Ui.W - Pad - 104, y, 104, Mathf.Max(44, Ui.Tap)), "Refresh", Ui.Kind.Secondary, 13)) on.Refresh();
            y += Mathf.Max(44, Ui.Tap) + 10;

            var c = Row(ref y, 84);
            Ui.Panel(c, new Color(0.12f, 0.1f, 0.17f, 0.95f));
            Ui.PlainText(new Rect(c.x + 14, c.y + 8, c.width - 120, 20), $"Signed in as {d.me.name} · your friend code", 12, Palette.Dim);
            Ui.PlainText(new Rect(c.x + 14, c.y + 30, c.width - 120, 40), d.me.code, 30, Palette.Gold, FontStyle.Bold);
            if (Ui.Button(new Rect(c.xMax - 100, c.y + 20, 86, Mathf.Max(44, Ui.Tap)), "Copy", Ui.Kind.Secondary, 13)) { GUIUtility.systemCopyBuffer = d.me.code; Toast("Friend code copied"); }

            c = Row(ref y, 96);
            Ui.Panel(c, new Color(0.12f, 0.1f, 0.17f, 0.95f));
            Ui.Text(new Rect(c.x + 14, c.y + 8, 200, 22), "Add a friend", 15, Palette.Text, FontStyle.Bold);
            _codeInput = GUI.TextField(new Rect(c.x + 14, c.y + 40, c.width - 120, 42), _codeInput.ToUpperInvariant(), 8, Field(18));
            if (Ui.Button(new Rect(c.xMax - 98, c.y + 39, 84, Mathf.Max(44, Ui.Tap)), "Add", Ui.Kind.Primary, 15)) { on.AddFriend(_codeInput); _codeInput = ""; GUI.FocusControl(null); }

            if (d.incoming.Count > 0)
            {
                c = Row(ref y, 40 + d.incoming.Count * Mathf.Max(56, Ui.Tap + 8));
                Ui.Panel(c, new Color(0.12f, 0.1f, 0.17f, 0.95f), Palette.Gold);
                Ui.Text(new Rect(c.x + 14, c.y + 8, 200, 22), "Friend requests", 15, Palette.Text, FontStyle.Bold);
                for (int i = 0; i < d.incoming.Count; i++)
                {
                    var f = d.incoming[i]; float ry = c.y + 36 + i * Mathf.Max(56, Ui.Tap + 8);
                    Ui.PlainText(new Rect(c.x + 14, ry, c.width - 200, 24), f.name, 15, Palette.Text, FontStyle.Bold);
                    Ui.Text(new Rect(c.x + 14, ry + 22, c.width - 200, 20), "wants to be friends", 12, Palette.Dim);
                    // Decline (Secondary) first, Accept (the Primary) last, both 48 dp (rule 3).
                    if (Ui.Button(new Rect(c.xMax - 182, ry, 84, Mathf.Max(44, Ui.Tap)), "Decline", Ui.Kind.Secondary, 13)) on.Respond(f.id, false);
                    if (Ui.Button(new Rect(c.xMax - 92, ry, 78, Mathf.Max(44, Ui.Tap)), "Accept", Ui.Kind.Primary, 13)) on.Respond(f.id, true);
                }
            }

            c = Row(ref y, 36 + Mathf.Max(1, d.friends.Count) * 64);
            Ui.Panel(c, new Color(0.12f, 0.1f, 0.17f, 0.95f));
            Ui.Text(new Rect(c.x + 14, c.y + 8, 240, 22), $"Your friends ({d.friends.Count})", 15, Palette.Text, FontStyle.Bold);
            if (d.friends.Count == 0) Ui.Text(new Rect(c.x + 14, c.y + 36, c.width - 28, 40), "No friends yet. Share your code above, or add theirs.", 13, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            for (int i = 0; i < d.friends.Count; i++)
            {
                var f = d.friends[i]; var rr = new Rect(c.x + 8, c.y + 36 + i * 64, c.width - 16, 58);
                Ui.Panel(rr, Palette.Panel2, Palette.Line, 10);
                var lead = f.profile?.heroes?.FirstOrDefault(h => Hero(h.id) != null);
                if (lead != null) Ui.Round(new Rect(rr.x + 8, rr.y + 12, 6, 34), Palette.Element(Hero(lead.id).Element), null, 3);
                Ui.PlainText(new Rect(rr.x + 22, rr.y + 6, rr.width - 90, 24), f.name, 15, Palette.Text, FontStyle.Bold);
                string stage = f.profile == null ? "" : f.profile.cleared >= TotalStages ? "Campaign complete" : $"Stage {Game.StageLabel(Mathf.Min(f.profile.cleared, TotalStages - 1))}";
                Ui.Text(new Rect(rr.x + 22, rr.y + 30, rr.width - 90, 20), $"{stage} · Power {Ui.Fmt(f.profile?.power ?? 0)} · {Online.Ago(f.seen)}", 12, Palette.Dim);
                Ui.Badge(new Vector2(rr.xMax - 26, rr.center.y), f.unread); // the one red badge
                if (Ui.Button(rr, "", Ui.Kind.Ghost)) { _friendId = f.id; Go(ScreenId.FriendProfile); }
            }

            if (d.outgoing.Count > 0)
            {
                float step = Mathf.Max(44, Ui.Tap) + 6;
                c = Row(ref y, 40 + d.outgoing.Count * step);
                Ui.Panel(c, new Color(0.12f, 0.1f, 0.17f, 0.95f));
                Ui.Text(new Rect(c.x + 14, c.y + 8, 200, 22), "Sent requests", 15, Palette.Text, FontStyle.Bold);
                for (int i = 0; i < d.outgoing.Count; i++)
                {
                    var f = d.outgoing[i]; float ry = c.y + 36 + i * step;
                    Ui.PlainText(new Rect(c.x + 14, ry + 6, c.width - 130, 24), f.name + "  (waiting)", 14, Palette.Text);
                    if (Ui.Button(new Rect(c.xMax - 98, ry, 84, Mathf.Max(44, Ui.Tap)), "Cancel", Ui.Kind.Secondary, 13)) on.Remove(f.id);
                }
            }

            c = Row(ref y, 44);
            if (Ui.Button(new Rect(c.x, c.y, c.width / 2 - 5, Mathf.Max(44, Ui.Tap)), "Blocked players", Ui.Kind.Secondary, 13)) { on.LoadBlocked(); Go(ScreenId.Blocked); }
            if (Ui.Button(new Rect(c.x + c.width / 2 + 5, c.y, c.width / 2 - 5, Mathf.Max(44, Ui.Tap)), "Delete online profile", Ui.Kind.Secondary, 13))
                Modal("Delete online profile?", 240, b =>
                {
                    Ui.Text(new Rect(b.x, b.y, b.width, 90), "This removes your name, friend code, friends and all chat messages from the server. Your game progress on this device stays.", 14, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width / 2 - 5, 44), "Cancel", Ui.Kind.Secondary)) CloseModal();
                    if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - 46, b.width / 2 - 5, 44), "Delete", Ui.Kind.Danger)) { CloseModal(); on.DeleteAccount(() => Go(ScreenId.Friends)); }
                });
            Scroller.End();
        }

        void DrawFriendProfile(Rect area)
        {
            var f = Online.Friend(_friendId);
            if (f == null) { Go(ScreenId.Friends); return; }
            var p = f.profile ?? new NetProfile();
            float y = area.y + 8;
            float tap = Mathf.Max(44, Ui.Tap);
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), "", 0)) GoBack();
            if (Ui.Button(new Rect(Ui.W - Pad - 100, y, 100, tap), "More…", Ui.Kind.Secondary, 13)) FriendActions(f);
            y += tap + 8;
            Ui.PlainText(new Rect(Pad, y, Ui.W - 32, 30), f.name, 22, Palette.Text, FontStyle.Bold); y += 30;
            Ui.PlainText(new Rect(Pad, y, Ui.W - 32, 20), $"{Online.Ago(f.seen)} · code {f.code}", 13, Palette.Dim); y += 26;
            y = Mathf.Max(y + 200, area.yMax - 260); // their team stands here in 3D
            var r = new Rect(Pad, y, Ui.W - 32, 110);
            Ui.Panel(r, new Color(0.12f, 0.1f, 0.17f, 0.95f));
            string stage = p.cleared >= TotalStages ? "Complete" : Game.StageLabel(Mathf.Min(p.cleared, TotalStages - 1));
            string[] stats = { $"Campaign  {stage}", $"Team power  {Ui.Fmt(p.power)}", $"Titan best  {Ui.Fmt(p.titanBest)}", $"Heroes  {p.heroCount}" };
            for (int i = 0; i < 4; i++) Ui.Text(new Rect(r.x + 14 + (i % 2) * (r.width / 2), r.y + 12 + (i / 2) * 26, r.width / 2 - 20, 24), stats[i], 14, Palette.Text);
            var team = p.heroes.Where(h => Hero(h.id) != null).Select(h => $"{Hero(h.id).ShortName} {Ui.Stars(Mathf.Clamp(h.stars, 1, 6))}");
            Ui.Text(new Rect(r.x + 14, r.y + 66, r.width - 28, 36), "Team: " + string.Join(", ", team), 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            if (Ui.Button(new Rect(Pad, r.yMax + 12, Ui.W - 32, 50), f.unread > 0 ? $"Message {f.name} ({f.unread} new)" : $"Message {f.name}", Ui.Kind.Primary, 16))
            { Online.OpenChat(f.id); _chatInput = ""; Go(ScreenId.Chat); }
        }

        void FriendActions(NetPlayer f) => Modal(f.name, 300, b =>
        {
            if (Ui.Button(new Rect(b.x, b.y, b.width, 44), "Report player", Ui.Kind.Secondary)) { CloseModal(); AskReport(f, 0, null); }
            if (Ui.Button(new Rect(b.x, b.y + 54, b.width, 44), "Block player", Ui.Kind.Secondary))
            { CloseModal(); Online.Block(f.id, () => Go(ScreenId.Friends)); }
            if (Ui.Button(new Rect(b.x, b.y + 108, b.width, 44), "Remove friend", Ui.Kind.Danger))
            { CloseModal(); Online.Remove(f.id, () => Go(ScreenId.Friends)); }
            if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width, 44), "Cancel", Ui.Kind.Chip)) CloseModal();
        });

        void AskReport(NetPlayer f, long messageId, string messageBody)
        {
            string[] reasons = { "Harassment or bullying", "Hate speech", "Spam or scams", "Inappropriate name", "Something else" };
            Modal(messageId > 0 ? "Report message" : $"Report {f.name}", 120 + reasons.Length * 52, b =>
            {
                if (messageBody != null) Ui.PlainText(new Rect(b.x, b.y, b.width, 40), $"\"{messageBody}\"", 12, Palette.Dim, FontStyle.Italic, TextAnchor.UpperLeft, true);
                float ry = b.y + (messageBody != null ? 44 : 0);
                foreach (var reason in reasons)
                {
                    if (Ui.Button(new Rect(b.x, ry, b.width, 44), reason, Ui.Kind.Secondary, 14)) { CloseModal(); Online.Report(f.id, messageId, reason); }
                    ry += 50;
                }
                if (Ui.Button(new Rect(b.x, b.yMax - Mathf.Max(44, Ui.Tap), b.width, Mathf.Max(44, Ui.Tap)), "Cancel", Ui.Kind.Secondary, 13)) CloseModal();
            });
        }

        void DrawChat(Rect area)
        {
            var on = Online; var f = on.Friend(on.ChatWith);
            if (f == null) { Go(ScreenId.Friends); return; }
            float y = area.y + 6;
            float tap = Mathf.Max(44, Ui.Tap);
            if (Ui.BackButton(new Rect(Pad, y, Ui.BackW, tap))) GoBack();
            Ui.PlainText(new Rect(Pad + Ui.BackW + 12, y + 2, Ui.W - Ui.BackW - 44, 20), f.name, 16, Palette.Text, FontStyle.Bold);
            Ui.Text(new Rect(Pad + Ui.BackW + 12, y + 22, Ui.W - Ui.BackW - 44, 18), Online.Ago(f.seen), 11, Palette.Dim);
            y += tap + 6;
            var log = new Rect(Pad, y, Ui.W - 32, area.yMax - y - 60);
            Ui.Panel(log, new Color(0.1f, 0.09f, 0.14f, 0.96f));

            // measure bubbles
            var style = Plain(14, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
            float maxW = log.width * 0.74f, total = 10;
            var heights = new float[on.Chat.Count];
            for (int i = 0; i < on.Chat.Count; i++) { heights[i] = style.CalcHeight(new GUIContent(on.Chat[i].body), maxW - 24) + 30; total += heights[i] + 6; }
            if (on.ChatScrollToEnd && Event.current.type == EventType.Repaint) { _chatScroll.y = Mathf.Max(0, total - log.height); on.ChatScrollToEnd = false; }
            var inner = new Rect(log.x + 4, log.y + 4, log.width - 8, log.height - 8);
            _chatScroll = Scroller.Begin(inner, _chatScroll, total);
            if (on.Chat.Count == 0) Ui.Text(new Rect(0, 20, inner.width, 30), "Say hi!", 14, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            float cy = 6;
            for (int i = 0; i < on.Chat.Count; i++)
            {
                var m = on.Chat[i];
                float w = Mathf.Min(maxW, style.CalcSize(new GUIContent(m.body)).x + 26);
                var b = new Rect(m.mine ? inner.width - w - 6 : 6, cy, w, heights[i]);
                var col = m.mine ? (m.pending ? Palette.Gold * new Color(1, 1, 1, 0.5f) : Palette.Gold) : Palette.Panel2;
                Ui.Round(b, col, null, 12);
                GUI.Label(new Rect(b.x + 12, b.y + 6, b.width - 24, b.height - 26), m.body, Plain(14, m.mine ? Palette.Hex("#1b1408") : Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true));
                var time = System.DateTimeOffset.FromUnixTimeMilliseconds(m.created).ToLocalTime();
                Ui.Text(new Rect(b.x + 12, b.yMax - 20, b.width - 24, 16), m.pending ? "sending…" : time.ToString("h:mm tt"), 11, m.mine ? Palette.Hex("#3a2a10") : Palette.Dim, FontStyle.Normal, TextAnchor.MiddleRight);
                if (!m.mine && m.id > 0 && Ui.Button(b, "", Ui.Kind.Ghost)) AskReport(f, m.id, m.body);
                cy += heights[i] + 6;
            }
            Scroller.End();

            var bar = new Rect(Pad, area.yMax - 52, Ui.W - 32, 46);
            GUI.SetNextControlName("chatinput");
            _chatInput = GUI.TextField(new Rect(bar.x, bar.y, bar.width - 92, 46), _chatInput, 300, Field(15));
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "chatinput";
            if (Ui.Button(new Rect(bar.xMax - 84, bar.y, 84, 46), "Send", Ui.Kind.Primary, 15) || enter)
            {
                var text = _chatInput; _chatInput = "";
                on.SendMessage(text, ok => { if (!ok && string.IsNullOrEmpty(_chatInput)) _chatInput = text; });
                if (enter) Event.current.Use();
            }
            Ui.Text(new Rect(Pad, area.yMax - 8, Ui.W - 32, 16), "Tap a friend's message to report it.", 11, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void DrawBlocked(Rect area)
        {
            float y = area.y + 8;
            float tap = Mathf.Max(44, Ui.Tap);
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), "Blocked players")) GoBack();
            y += tap + 12;
            var list = Online.Blocked?.blocked;
            if (list == null) { Ui.Text(new Rect(Pad, y, 300, 24), "Loading…", 14, Palette.Dim); return; }
            if (list.Count == 0) { Ui.Text(new Rect(Pad, y, Ui.W - 32, 40), "You haven't blocked anyone.", 14, Palette.Dim); return; }
            foreach (var p in list)
            {
                var r = Row(ref y, 52, 8);
                Ui.Panel(r, Palette.Panel2, Palette.Line, 10);
                Ui.PlainText(new Rect(r.x + 14, r.y, r.width - 130, 52), p.name, 15, Palette.Text, FontStyle.Bold);
                if (Ui.Button(new Rect(r.xMax - 104, r.center.y - Mathf.Max(44, Ui.Tap) / 2, 92, Mathf.Max(44, Ui.Tap)), "Unblock", Ui.Kind.Secondary, 13)) Online.Unblock(p.id);
            }
        }
    }
}
