using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PartyGame
{
    /// <summary>
    /// Grey-box game flow: Occasion -> Invite -> Activities -> Party (rounds) -> Results, three times.
    /// The whole game happens in the PartyRoom scene: you walk the host around and press E on things
    /// (phone, activity spots, door, guests, record player). See PartyGameController.World.cs.
    /// Menus (title, rules, friends, results) are built from code and laid over the room.
    /// Pressing Play in any scene works: it switches to PartyRoom if that scene is in Build Settings.
    /// </summary>
    public partial class PartyGameController : MonoBehaviour
    {
        [Tooltip("Leave empty to load Resources/PartyGameContent, or the built-in defaults.")]
        public GameContent content;

        /// <summary>Turn off once the team places PartyGameController in the scene themselves.</summary>
        static readonly bool AutoBootstrap = true;
        public const string RoomScene = "PartyRoom";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!AutoBootstrap || FindObjectOfType<PartyGameController>() != null) return;
            if (FindObjectOfType<PartyStage>() == null && Application.CanStreamedLevelBeLoaded(RoomScene))
            {
                // Started from another scene (e.g. SampleScene): go to the party room first.
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnRoomLoaded;
                UnityEngine.SceneManagement.SceneManager.LoadScene(RoomScene);
                return;
            }
            Spawn();
        }

        static void OnRoomLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnRoomLoaded;
            if (FindObjectOfType<PartyGameController>() == null) Spawn();
        }

        static void Spawn() => new GameObject("PartyGame").AddComponent<PartyGameController>();

        ScoringSettings S => content.scoring;
        int Budget => _plan.occasion.budget;
        int ChatsPerRound => _plan.occasion.chatsPerRound;

        GuestKnowledge _knowledge;
        DialoguePicker _picker;
        int _partyIndex;
        PartyPlan _plan;
        PartyOutcome _outcome;
        int _round;
        int _chatsLeft;
        bool _seenHowTo;
        int _slot = -1; // save slot of the current run

        // Party round state
        readonly Dictionary<GuestData, List<Reason>[]> _schedule = new Dictionary<GuestData, List<Reason>[]>();
        readonly Dictionary<GuestData, string> _bubbles = new Dictionary<GuestData, string>();
        readonly HashSet<GuestData> _chattedThisRound = new HashSet<GuestData>();
        readonly List<Discovery> _partyDiscoveries = new List<Discovery>();

        // Across the whole game
        readonly List<int> _history = new List<int>();
        readonly Dictionary<GuestData, List<(string party, int stars)>> _guestHistory =
            new Dictionary<GuestData, List<(string, int)>>();

        RectTransform _root;
        Image _background;
        PartyStage _room;

        // Menu pages (title, rules, results...) cover the room with this see-through veil.
        static readonly Color Veil = new Color(UIKit.Background.r, UIKit.Background.g, UIKit.Background.b, 0.86f);

        void Start()
        {
            if (content == null) content = GameContent.Load();
            if (content.scoring == null) content.scoring = new ScoringSettings();

            var canvas = UIKit.CreateCanvas(transform);
            _background = UIKit.Panel(canvas.transform, Veil, "Background");
            UIKit.Stretch(_background.rectTransform);
            _root = _background.rectTransform;

            // The room from the PartyRoom scene; built on the fly if this scene has none.
            _room = FindObjectOfType<PartyStage>();
            if (_room == null) _room = RoomBuilder.Build(Camera.main);
            if (_room.cam == null) _room.cam = Camera.main;

            ShowTitle();
        }

        // ================================================================ title / how to play

        void ShowTitle()
        {
            var page = NewPage(TextAnchor.MiddleCenter);
            StageRoom(null, null);
            _background.color = new Color(Veil.r, Veil.g, Veil.b, 0.6f); // lighter veil: the room shows behind the title
            UIKit.Label(page, "Dinner Party", 72, null, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Label(page, "Grey-box prototype  ·  3 parties  ·  get to know your friends", 28, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Spacer(page, 30);
            // Same width, one per row, both centred so they line up.
            Centered(page, p => UIKit.Button(p, "Start", ShowSlots, 320, 80, fontSize: 32));
            Centered(page, p => UIKit.Button(p, "How to play", () => ShowHowTo(ShowTitle), 320, 64, true, UIKit.Muted));
            Centered(page, p => UIKit.Button(p, "Rules", () => ShowRules(0, ShowTitle), 320, 64, true, UIKit.Muted));
        }

        void ShowHowTo(System.Action next)
        {
            _seenHowTo = true;
            var page = NewPage();
            Header(page, "How to play", "Throw parties where every guest has a great time.");

            Step(page, "1", "Walk around your home", "WASD or the arrow keys walk. Press E next to something to use it. Esc pauses the game.");
            Step(page, "2", "Invite friends on the phone", "The phone is on the console by the right wall. Friends' preferences start hidden (???).");
            Step(page, "3", "Set up activities", "Walk to a glowing spot and press E to set up that activity (it costs budget). Each one pushes the party's vibe. Then open the front door.");
            Step(page, "4", "Talk to your guests", "Each round, every guest says something in a speech bubble about how the party is going. Walk up and press E to talk: you see what made them happier (green) or less happy (red).");
            Step(page, "5", "Chat", "While talking, press 1 to ask what they like. That uses one of your chats for this round and tells you one hidden preference.");
            Step(page, "6", "Next round", "Change the record on the record player (by the back wall, next to the door). After the last round, your guests head home and you see how it went.");
            UIKit.Label(page, "Everything you learn is saved under Friends. Details about budget, the Helper, traits and stars are under Rules.",
                22, UIKit.Muted);

            UIKit.Spacer(page);
            Footer(page, "", "Got it", next, true, null);
        }

        static void Step(Transform page, string n, string title, string text)
        {
            var row = UIKit.Row(page, 20, TextAnchor.UpperLeft);
            var badge = UIKit.Panel(row, UIKit.Accent, "Badge");
            UIKit.Size(badge, 52, 52);
            var num = UIKit.Label(badge.transform, n, 28, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(num.rectTransform);
            var col = UIKit.Column(row, 2);
            UIKit.Size(col, 1500);
            UIKit.Label(col, title, 30, null, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Label(col, text, 24, UIKit.Muted);
        }

        // ================================================================ save slots

        void ShowSlots()
        {
            var page = NewPage();
            Header(page, "Choose a save slot",
                "Each player should use their own slot, so everyone gets to know the guests from scratch. " +
                "The game saves after every party.");

            for (int i = 0; i < SaveSystem.SlotCount; i++)
            {
                int slot = i;
                var save = SaveSystem.Load(slot);
                var card = UIKit.CardPanel(page, UIKit.Card, 1200);
                var row = UIKit.Row(card.transform, 20, TextAnchor.MiddleLeft);

                var info = UIKit.Column(row, 4);
                UIKit.Size(info, 640);
                UIKit.Label(info, $"Slot {slot + 1}", 32, null, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Label(info, SlotSummary(save), 22, UIKit.Muted);

                if (save == null)
                {
                    UIKit.Button(row, "New game", () => NewGame(slot), 240, 60);
                }
                else
                {
                    UIKit.Button(row, "Continue", () => LoadGame(slot, save), 240, 60);
                    UIKit.Button(row, "Start over", () => Confirm(
                        $"Erase Slot {slot + 1} and start from scratch?", "Erase and start", () => NewGame(slot)),
                        200, 60, true, UIKit.Muted);
                }
            }

            UIKit.Spacer(page);
            var footer = UIKit.Row(page, 20, TextAnchor.MiddleLeft);
            UIKit.Button(footer, "Back", ShowTitle, 160, 64, true, UIKit.Muted);
        }

        string SlotSummary(SaveData save)
        {
            if (save == null) return "Empty";
            int total = content.occasions.Count;
            string progress = save.finished ? $"Finished all {total} parties (Continue plays them again)"
                : $"Next: party {save.nextParty + 1} of {total}";
            return $"{progress}  ·  {save.FactsLearned()} things learned about friends  ·  saved {save.savedAt}";
        }

        void Confirm(string question, string yes, System.Action onYes)
        {
            var page = NewPage(TextAnchor.MiddleCenter);
            UIKit.Label(page, question, 40, null, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Spacer(page, 20);
            Centered(page, p =>
            {
                UIKit.Button(p, "Cancel", ShowSlots, 220, 70, true, UIKit.Muted);
                UIKit.Spacer(p, 0);
                UIKit.Button(p, yes, onYes, 300, 70, true, UIKit.Bad);
            });
        }

        void ResetRun(int slot)
        {
            _slot = slot;
            _knowledge = new GuestKnowledge();
            _picker = new DialoguePicker(content.dialogue, System.Environment.TickCount);
            _history.Clear();
            _guestHistory.Clear();
            _partyIndex = 0;
        }

        void NewGame(int slot)
        {
            ResetRun(slot);
            _seenHowTo = false; // a new player sees the rules once
            SaveGame(0, false);
            ShowHowTo(StartOccasion);
        }

        void LoadGame(int slot, SaveData save)
        {
            ResetRun(slot);
            _seenHowTo = save.seenHowTo;
            _history.AddRange(save.partyStars);
            foreach (var gs in save.guests)
            {
                var g = content.guests.Find(x => x.id == gs.id);
                if (g == null) continue; // guest removed from content since this save
                _knowledge.ReadFrom(gs, g);
                if (gs.parties.Count > 0)
                    _guestHistory[g] = gs.parties.Select(p => (p.party, p.stars)).ToList();
            }

            int last = content.occasions.Count - 1;
            if (save.finished || save.nextParty > last)
            {
                // Finished slot: play the parties again from the start. Everything learned stays
                // unlocked; only the per-party star ratings of the run start over.
                _history.Clear();
                _partyIndex = 0;
                SaveGame(0, false);
                StartOccasion();
                return;
            }
            _partyIndex = Mathf.Clamp(save.nextParty, 0, last);
            StartOccasion();
        }

        /// <summary>Checkpoint: written at the start of a run and after each party.</summary>
        void SaveGame(int nextParty, bool finished)
        {
            if (_slot < 0) return;
            var data = new SaveData
            {
                nextParty = nextParty,
                finished = finished,
                seenHowTo = _seenHowTo,
                partyStars = new List<int>(_history),
            };
            foreach (var g in content.guests)
            {
                var gs = new GuestSave { id = g.id };
                _knowledge.WriteTo(gs, g);
                if (_guestHistory.TryGetValue(g, out var hist))
                    gs.parties = hist.Select(h => new PartyRecord { party = h.party, stars = h.stars }).ToList();
                data.guests.Add(gs);
            }
            SaveSystem.Save(_slot, data);
        }

        // ================================================================ results

        void FinishParty()
        {
            _history.Add(_outcome.PartyStars);
            foreach (var go in _outcome.guests)
            {
                if (!_guestHistory.TryGetValue(go.guest, out var list))
                    _guestHistory[go.guest] = list = new List<(string, int)>();
                list.Add((_plan.occasion.title, go.Stars));
            }
            bool last = _partyIndex >= content.occasions.Count - 1;
            SaveGame(_partyIndex + 1, last);
            ShowResults();
        }

        void ShowResults()
        {
            var page = NewPage();
            var title = UIKit.Row(page, 20, TextAnchor.MiddleLeft);
            UIKit.Label(title, $"{_plan.occasion.title}: how did it go?", 48, null, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stars(title, _outcome.PartyStars, 5, 36);

            string chem = (_outcome.storyPair ? "Stories and questions flowed (+Vibes). " : "") +
                          (_outcome.gossipFight ? "Too much gossip started a fight (-Vibes)." : "");
            if (chem.Length > 0) UIKit.Label(page, chem, 24, UIKit.Muted);

            var row = UIKit.CardRow(page, 20, TextAnchor.UpperCenter);
            foreach (var go in _outcome.guests)
            {
                var card = UIKit.CardPanel(row, UIKit.Card, 320);
                UIKit.Label(card.transform, go.guest.displayName, 32, null, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Stars(card.transform, go.Stars);
                var best = go.Best;
                var worst = go.Worst;
                if (best != null) UIKit.Label(card.transform, "<b>Best part:</b> “" + _picker.ForReason(best, go.guest) + "”", 22, UIKit.Good);
                if (worst != null) UIKit.Label(card.transform, "<b>Not so great:</b> “" + _picker.ForReason(worst, go.guest) + "”", 22, UIKit.Bad);
                if (best == null && worst == null) UIKit.Label(card.transform, "“" + _picker.Pick("Generic.Good", go.guest, null) + "”", 22);
            }

            if (_partyDiscoveries.Count > 0)
            {
                UIKit.Spacer(page, 10);
                UIKit.Label(page, "You learned (saved in Friends)", 30, null, TextAnchor.UpperLeft, FontStyle.Bold);
                // One line per guest, in the order guests first taught you something
                foreach (var group in _partyDiscoveries.GroupBy(d => d.guest))
                    UIKit.Label(page, $"<b>{group.Key.displayName}</b>:  " +
                                      string.Join("   ·   ", group.Select(d => d.text)), 24);
            }

            UIKit.Spacer(page);
            bool more = _partyIndex < content.occasions.Count - 1;
            Footer(page, "", more ? "Next party" : "Finish", () =>
            {
                if (more) { _partyIndex++; StartOccasion(); }
                else ShowEnd();
            }, true, null, ShowResults);
        }

        void ShowEnd()
        {
            var page = NewPage(TextAnchor.MiddleCenter);
            UIKit.Label(page, "The end. Thanks for hosting!", 64, null, TextAnchor.MiddleCenter, FontStyle.Bold);
            for (int i = 0; i < Mathf.Min(_history.Count, content.occasions.Count); i++)
            {
                var row = UIKit.Row(page, 20, TextAnchor.MiddleCenter);
                var name = UIKit.Label(row, content.occasions[i].title, 30, null, TextAnchor.MiddleRight);
                UIKit.Size(name, 360); // same width on every line so the stars line up
                UIKit.Stars(row, _history[i], 5, 30);
            }
            float avg = _history.Count > 0 ? (float)_history.Average() : 0;
            UIKit.Label(page, $"Average: {avg:0.0} stars", 32, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Spacer(page, 30);
            Centered(page, p =>
            {
                UIKit.Button(p, "Friends", () => ShowFriends(null, ShowEnd), 200, 76, true, UIKit.Muted);
                UIKit.Spacer(p, 0);
                UIKit.Button(p, "Main menu", ShowTitle, 240, 76);
            });
        }

        // ================================================================ rulebook

        /// <summary>
        /// The full rules, one section at a time. Numbers come from ScoringSettings so the rulebook
        /// stays correct when designers tune them.
        /// </summary>
        void ShowRules(int section, System.Action back)
        {
            var sections = RuleSections();
            section = Mathf.Clamp(section, 0, sections.Count - 1);

            var page = NewPage();
            Header(page, "Rules", "Everything the game counts, in plain words.");

            var body = UIKit.Row(page, 30);

            var list = UIKit.Column(body, 8);
            UIKit.Size(list, 320, -1, 0);
            for (int i = 0; i < sections.Count; i++)
            {
                int index = i;
                UIKit.Button(list, sections[i].title, () => ShowRules(index, back), 320, 56,
                    true, i == section ? UIKit.Accent : UIKit.Muted, 22);
            }

            var card = UIKit.CardPanel(body, UIKit.Card, 1400, 32);
            UIKit.Size(card, -1, -1, 1);
            UIKit.Label(card.transform, sections[section].title, 38, null, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Spacer(card.transform, 6);
            foreach (var line in sections[section].lines)
                UIKit.Label(card.transform, line, 24);

            UIKit.Spacer(page);
            bool hasNext = section < sections.Count - 1;
            Footer(page, $"{section + 1} / {sections.Count}", hasNext ? "Next" : "Done",
                hasNext ? () => ShowRules(section + 1, back) : back, true, back);
        }

        List<(string title, string[] lines)> RuleSections()
        {
            string Num(float v) => v.ToString("0.#");
            int partyCount = content.occasions.Count;
            int chats = content.occasions.Count > 0 ? content.occasions[0].chatsPerRound : 2;

            return new List<(string, string[])>
            {
                ("Goal", new[]
                {
                    $"Host {partyCount} parties for your friends. Each guest leaves with 1 to 5 stars; the party's stars are the average.",
                    "You don't know your friends well yet. Every party teaches you more about them, and that knowledge is the key to better parties.",
                }),
                ("One party, step by step", new[]
                {
                    "<b>1. Occasion</b>  sets the starting vibe, how many guests to invite and your budget.",
                    "<b>2. Invite</b>  use the phone (by the right wall) to invite exactly that many friends.",
                    "<b>3. Activities</b>  walk to the glowing spots and set up activities that push the vibe. Then open the front door.",
                    "<b>4. Party</b>  3 rounds. Talk to guests to see how they feel; the record player (by the back wall, next to the door) moves on to the next round. When a round's chats are used up, a note at the top tells you to change the record.",
                    "<b>5. Results</b>  your guests go home; stars for every guest, and a list of what you learned.",
                }),
                ("The party vibe", new[]
                {
                    "Every party sits somewhere on 5 scales:",
                    string.Join("\n", Axes.All.Select(a => $"   ·  {Axes.NegativePole(a)}  /  {Axes.PositivePole(a)}")),
                    "The occasion sets the starting point. Each activity pushes one or more scales (listed in the left panel while you set up).",
                    $"Party size is set by the guest list: {S.neutralGuestCount} guests is neutral, more guests feel like a Big party, fewer feel like a Small one.",
                    "While setting up and during the party, the current vibe is shown in the panel on the left.",
                }),
                ("Guests and preferences", new[]
                {
                    "Every guest has a preference on each of the 5 scales, from strongly one side to strongly the other.",
                    "A guest only cares about a scale if they lean clearly one way. Neutral guests don't mind either way.",
                    "If the party leans the way they like, they get happier. If it leans the other way, they get unhappier. The stronger both lean, the bigger the effect.",
                    "<b>???</b> means you haven't learned that preference yet.",
                }),
                ("Budget and the Helper", new[]
                {
                    "Each activity has a cost. The total must not go over the occasion's budget.",
                    "Adding an activity always adds exactly the cost written on it.",
                    $"<b>Helper:</b> if a Helper is coming, the <b>total</b> cost goes down by {S.helperDiscount}, <b>once per party</b>. It is not taken off every activity.",
                    $"Example: Food (2) + Home Café (3) = 5. With a Helper at the party: 5 - {S.helperDiscount} = {5 - S.helperDiscount}.",
                    "The panel on the left shows the budget used, including what the Helper saved.",
                }),
                ("Food", new[]
                {
                    $"No food at all: every guest is a little unhappy ({Num(S.noFoodPenalty)}).",
                    $"Some guests need vegan / gluten-free food. Without it they are quite unhappy ({Num(S.dietaryUnmetPenalty)}); with it they are pleased (+0.5).",
                }),
                ("Guest traits", new[]
                {
                    "<b>Inquisitive + Storyteller:</b> when both come, stories and questions flow. Everyone at the party gets a bit happier.",
                    $"<b>Gossip:</b> each Gossip makes the other guests feel a bit more like wallflowers. {S.gossipFightCount} or more Gossips start a fight, and everyone is unhappier.",
                    $"<b>Helper:</b> lowers the total activity cost by {S.helperDiscount} (once), and enjoys helping (+0.5 for them).",
                    "Traits start hidden too. You learn a friend's traits when they come to a party. A Helper tells you as soon as you invite them.",
                }),
                ("During the party", new[]
                {
                    "The party has 3 rounds. In each round, every guest reacts to something about the party.",
                    "Their speech bubble says how it's going. Talk to them to see why: <color=#4d9a59><b>green</b></color> made them happier, <color=#cc4d4d><b>red</b></color> made them less happy. Their mood bar moves by exactly that much.",
                    "Small things come up first, the biggest reason last. A guest with nothing new to say just makes small talk.",
                    "Everything a guest reacts to is written into Friends automatically.",
                }),
                ("Chatting", new[]
                {
                    $"You get {chats} chats every round (they refill), at most one per guest per round.",
                    "Walk up to a guest and press E to talk (free). Press 1 in the conversation to ask what they like: that is a chat.",
                    "A chat tells you one hidden preference of that guest. It does not change how the party goes.",
                    "If you already know everything about a guest you can still chat to catch up. You won't learn anything new, and it still uses a chat.",
                }),
                ("Stars", new[]
                {
                    $"Every guest starts at {Num(S.baseHappiness)} stars. Each green or red reason you saw during the party moves that up or down.",
                    "The final value is rounded to 1 to 5 stars. The party's stars are the average of all guests.",
                }),
                ("Saving", new[]
                {
                    "Pick a save slot when you start. Each player should use their own slot.",
                    "Anything you learn about a friend is saved right away and stays unlocked in that slot for good.",
                    "<b>Continue</b> picks up at the next party. After the last party it plays the parties again, keeping what you know.",
                    "<b>Start over</b> erases the slot and starts from scratch.",
                }),
            };
        }

        // ================================================================ friends (character profiles)

        List<GuestData> AvailableGuests() => content.guests.Where(x => x.availableFrom <= _partyIndex).ToList();

        void ShowFriends(GuestData selected, System.Action back)
        {
            var guests = AvailableGuests();
            if (selected == null) selected = guests.FirstOrDefault();

            var page = NewPage();
            Header(page, "Friends", "Everything you've learned about your friends. Host more parties to fill in the ???.");

            var body = UIKit.Row(page, 30);

            // Left: list of friends
            var list = UIKit.Column(body, 8);
            UIKit.Size(list, 260, -1, 0);
            foreach (var g in guests)
            {
                var guest = g;
                int known = Axes.All.Count(a => _knowledge.Knows(g, a));
                UIKit.Button(list, $"{g.displayName}   {known}/{Axes.Count}", () => ShowFriends(guest, back), 260, 56,
                    true, g == selected ? UIKit.Accent : UIKit.Muted, 24);
            }

            if (selected != null) Profile(body, selected);

            UIKit.Spacer(page);
            Footer(page, "", "Back", back, true, null);
        }

        void Profile(Transform parent, GuestData g)
        {
            var card = UIKit.CardPanel(parent, UIKit.Card, 1460, 28);
            UIKit.Size(card, -1, -1, 1); // the profile takes the rest of the row

            // Header: avatar, name, blurb, what we know
            var head = UIKit.Row(card.transform, 20, TextAnchor.MiddleLeft);
            var avatar = UIKit.Panel(head, g.color, "Avatar");
            UIKit.Size(avatar, 96, 96);
            var info = UIKit.Column(head, 4);
            UIKit.Size(info, 1200);
            UIKit.Label(info, g.displayName, 44, null, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Label(info, g.blurb, 24, UIKit.Muted);
            var extras = KnownExtras(g);
            UIKit.Label(info, extras.Count > 0 ? string.Join("  ·  ", extras) : "Traits: ???", 24, UIKit.Ink, TextAnchor.UpperLeft, FontStyle.Bold);

            var row = UIKit.Row(card.transform, 40, TextAnchor.MiddleLeft);

            // Radar chart (the doc's pentagon)
            var chartBox = UIKit.Rect("ChartBox", row);
            UIKit.Size(chartBox, 560, 480);
            var chartRt = UIKit.Rect("Radar", chartBox);
            chartRt.anchorMin = chartRt.anchorMax = new Vector2(0.5f, 0.5f);
            chartRt.sizeDelta = new Vector2(300, 300);
            var chart = chartRt.gameObject.AddComponent<RadarChart>();
            var values = Axes.All.Select(a => _knowledge.Knows(g, a) ? Mathf.InverseLerp(Axes.Min, Axes.Max, g.stats[a]) : 0.5f).ToArray();
            var known = Axes.All.Select(a => _knowledge.Knows(g, a)).ToArray();
            chart.SetData(values, known);
            for (int i = 0; i < Axes.Count; i++)
            {
                var axis = Axes.All[i];
                var lbl = UIKit.Label(chartRt, $"<b>{Axes.PositivePole(axis)}</b>\n<size=18>{(known[i] ? Axes.Describe(axis, g.stats[axis]) : "???")}</size>",
                    22, known[i] ? UIKit.Ink : UIKit.Muted, TextAnchor.MiddleCenter);
                var rt = lbl.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(200, 60);
                // Labels beside the side corners are wide, so push them sideways to clear the chart.
                var pos = chart.LabelPosition(i, 50);
                if (Mathf.Abs(pos.x) > 60f) pos.x += Mathf.Sign(pos.x) * 50f;
                rt.anchoredPosition = pos;
            }

            // Flat sliders (the doc's "flat stats")
            var sliders = UIKit.Column(row, 18);
            UIKit.Size(sliders, 760);
            UIKit.Label(sliders, "Preferences", 28, null, TextAnchor.UpperLeft, FontStyle.Bold);
            foreach (var axis in Axes.All)
                Slider(sliders, axis, _knowledge.Knows(g, axis), g.stats[axis]);

            // Party history
            UIKit.Spacer(card.transform, 10);
            UIKit.Label(card.transform, "Parties", 28, null, TextAnchor.UpperLeft, FontStyle.Bold);
            if (_guestHistory.TryGetValue(g, out var hist) && hist.Count > 0)
            {
                foreach (var (party, stars) in hist)
                {
                    var h = UIKit.Row(card.transform, 16, TextAnchor.MiddleLeft);
                    var name = UIKit.Label(h, party, 24, null, TextAnchor.MiddleLeft);
                    UIKit.Size(name, 300);
                    UIKit.Stars(h, stars, 5, 22);
                }
            }
            else UIKit.Label(card.transform, "Hasn't been to one of your parties yet.", 22, UIKit.Muted);
        }

        static void Slider(Transform parent, PartyAxis axis, bool known, float value)
        {
            var row = UIKit.Row(parent, 14, TextAnchor.MiddleLeft);
            var left = UIKit.Label(row, Axes.NegativePole(axis), 22, null, TextAnchor.MiddleRight);
            UIKit.Size(left, 180);
            var track = UIKit.Panel(row, new Color(0.88f, 0.86f, 0.83f), "Track");
            UIKit.Size(track, 340, 22);
            var mid = UIKit.Panel(track.transform, UIKit.AccentDim, "Middle");
            mid.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            mid.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            mid.rectTransform.sizeDelta = new Vector2(2, 0);
            if (known)
            {
                float t = Mathf.InverseLerp(Axes.Min, Axes.Max, value);
                var marker = UIKit.Panel(track.transform, new Color(0.3f, 0.45f, 0.75f), "Marker");
                marker.rectTransform.anchorMin = new Vector2(t, 0f);
                marker.rectTransform.anchorMax = new Vector2(t, 1f);
                marker.rectTransform.sizeDelta = new Vector2(10, 8);
            }
            else
            {
                var q = UIKit.Label(track.transform, "???", 18, UIKit.Muted, TextAnchor.MiddleCenter);
                UIKit.Stretch(q.rectTransform);
            }
            var right = UIKit.Label(row, Axes.PositivePole(axis), 22, null, TextAnchor.MiddleLeft);
            UIKit.Size(right, 180);
        }

        // ================================================================ helpers

        RectTransform NewPage(TextAnchor align = TextAnchor.UpperLeft)
        {
            for (int i = _root.childCount - 1; i >= 0; i--)
            {
                var old = _root.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }
            // Menu pages show the room behind a see-through veil; the room view (ShowRoom) clears it.
            _background.color = Veil;
            _inRoom = false;
            _room.SetViewport(new Rect(0, 0, 1, 1));
            var page = UIKit.Rect("Page", _root);
            UIKit.Stretch(page);
            UIKit.VLayout(page, 14, 48, align);
            return page;
        }

        /// <summary>Who stands in the room and which furniture is out, for the current screen.</summary>
        void StageRoom(IList<GuestData> guests, IEnumerable<ActivityData> activities)
        {
            _room.SetGuests(guests ?? new List<GuestData>());
            _room.SetActivities(activities ?? Enumerable.Empty<ActivityData>());
        }

        /// <summary>A layer over the page that layout groups ignore, for UI pinned to things in the room.</summary>
        static RectTransform Overlay(Transform page)
        {
            var overlay = UIKit.Rect("Overlay", page);
            overlay.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UIKit.Stretch(overlay);
            return overlay;
        }

        /// <summary>Small tag above a guest's head in the room: mood face plus this round's + and -.</summary>
        void HeadTag(Transform overlay, GuestData g, float mood, List<Reason> now, float raise, System.Action onClick = null)
        {
            var head = _room.HeadOf(g);
            if (head == null) return;

            var tag = UIKit.Panel(overlay, g == _talking ? UIKit.CardSelected : Color.white, "HeadTag_" + g.id);
            if (onClick != null) tag.gameObject.AddComponent<Button>().onClick.AddListener(() => onClick());
            var rt = tag.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; // positioned by WorldFollower
            rt.pivot = new Vector2(0.5f, 0f);
            UIKit.HLayout(tag, 6, 6, TextAnchor.MiddleCenter);
            var fitter = tag.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var face = UIKit.Panel(tag.transform, g.color, "Face");
            UIKit.Size(face, 44, 44);
            var faceText = UIKit.Label(face.transform, UIKit.MoodFace(mood), 22, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(faceText.rectTransform);

            string signs = now == null ? "" :
                string.Concat(now.Select(r => r.IsGood ? "<color=#4d9a59>+</color>" : "<color=#cc4d4d>-</color>"));
            if (signs.Length > 0)
                UIKit.Size(UIKit.Label(tag.transform, signs, 34, null, TextAnchor.MiddleCenter, FontStyle.Bold), -1, 44);

            var follow = tag.gameObject.AddComponent<WorldFollower>();
            follow.target = head;
            follow.cam = _room.Cam;
            follow.offset = new Vector2(0, raise);
        }

        static void Centered(Transform parent, System.Action<Transform> build)
        {
            var row = UIKit.Row(parent, 20, TextAnchor.MiddleCenter);
            build(row);
        }

        static void Header(Transform page, string title, string subtitle)
        {
            UIKit.Label(page, title, 46, null, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Label(page, subtitle, 24, UIKit.Muted);
            UIKit.Spacer(page, 6);
        }

        /// <param name="reopen">If set, shows a Friends button that comes back to this screen.</param>
        void Footer(Transform page, string status, string next, System.Action onNext, bool canNext,
            System.Action onBack, System.Action reopen = null)
        {
            var row = UIKit.Row(page, 20, TextAnchor.MiddleLeft);
            if (onBack != null) UIKit.Button(row, "Back", onBack, 160, 64, true, UIKit.Muted);
            if (reopen != null) UIKit.Button(row, "Friends", () => ShowFriends(null, reopen), 180, 64, true, UIKit.Muted);
            if (reopen != null) UIKit.Button(row, "Rules", () => ShowRules(0, reopen), 160, 64, true, UIKit.Muted);
            var label = UIKit.Label(row, status, 28, null, TextAnchor.MiddleLeft);
            UIKit.Size(label, -1, 64, 1);
            UIKit.Button(row, next, onNext, 300, 64, canNext);
        }


        List<string> KnownExtras(GuestData g)
        {
            var extras = new List<string>();
            extras.AddRange(g.traits.Where(t => _knowledge.Knows(g, t)).Select(t => t.ToString()));
            if (_knowledge.KnowsDislike(g)) extras.Add(DislikeText(g));
            if (_knowledge.KnowsDiet(g)) extras.Add("Vegan / GF");
            return extras;
        }

        string KnownSummary(GuestData g)
        {
            var bits = Axes.All.Where(a => _knowledge.Knows(g, a) && Mathf.Abs(g.stats[a]) >= S.caresThreshold)
                .Select(a => Axes.Describe(a, g.stats[a])).ToList();
            bits.AddRange(g.traits.Where(t => _knowledge.Knows(g, t)).Select(t => t.ToString()));
            return bits.Count > 0 ? string.Join(", ", bits) : "still a mystery";
        }

        static string DislikeText(GuestData g) =>
            $"dislikes {(g.dislikeSign > 0 ? Axes.PositivePole(g.dislikeAxis) : Axes.NegativePole(g.dislikeAxis))} people";

        static string DescribeVibe(AxisValues v)
        {
            var parts = Axes.All.Where(a => Mathf.Abs(v[a]) >= 0.5f).Select(a => Axes.Describe(a, v[a])).ToList();
            return parts.Count > 0 ? string.Join(", ", parts) : "anything goes";
        }

        static string DescribeShift(ActivityData a)
        {
            var parts = Axes.All.Where(x => Mathf.Abs(a.vibeShift[x]) >= 0.25f)
                .Select(x => "+" + (a.vibeShift[x] < 0 ? Axes.NegativePole(x) : Axes.PositivePole(x))).ToList();
            if (a.isFood) parts.Add("food");
            if (a.dietaryFriendly) parts.Add("vegan / GF");
            return parts.Count > 0 ? "<color=#5a8f5f>(" + string.Join(", ", parts) + ")</color>" : "";
        }
    }
}
