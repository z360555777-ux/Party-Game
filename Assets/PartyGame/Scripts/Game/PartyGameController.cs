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
        int _savedNext;       // progress written by the last save, so notes can be saved without changing it
        bool _savedFinished;
        System.Action _friendsBack; // where the Friends page returns to

        // Party round state
        readonly Dictionary<GuestData, List<Reason>[]> _schedule = new Dictionary<GuestData, List<Reason>[]>();
        readonly Dictionary<GuestData, string> _bubbles = new Dictionary<GuestData, string>();
        readonly HashSet<GuestData> _chattedThisRound = new HashSet<GuestData>();
        readonly List<Discovery> _partyDiscoveries = new List<Discovery>();

        // Across the whole game
        readonly List<int> _history = new List<int>();
        readonly Dictionary<GuestData, List<(string party, int stars)>> _guestHistory =
            new Dictionary<GuestData, List<(string, int)>>();

        // Willingness (design doc: visible to the player; incompatible parties lower it)
        readonly Dictionary<GuestData, int> _willing = new Dictionary<GuestData, int>();
        readonly List<(GuestData guest, int from, int to)> _willingChanges = new List<(GuestData, int, int)>();

        RectTransform _root;
        Image _background;
        PartyStage _room;

        // Menu pages (title, rules, results...) cover the room with this see-through veil.
        static readonly Color Veil = new Color(UIKit.Background.r, UIKit.Background.g, UIKit.Background.b, 0.86f);

        void Start()
        {
            if (content == null) content = GameContent.Load();
            if (content.scoring == null) content.scoring = new ScoringSettings();
            if (content.interventions == null || content.interventions.Count == 0)
                content.interventions = DefaultContent.Build().interventions; // content assets made before interventions existed

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
            Step(page, "2", "Invite friends on the phone", "The phone is on the console by the right wall. Friends' preferences start hidden (???). Friends you keep inviting to parties they don't enjoy lose Willingness, and may say no.");
            Step(page, "3", "Set up activities", "Walk to a glowing spot and press E to set up that activity (it costs budget). Each one pushes the party's vibe. Then open the front door.");
            Step(page, "4", "Talk to your guests", "Each round, every guest says something in a speech bubble about how the party is going. Walk up and press E to talk: you see what made them happier (green) or less happy (red).");
            Step(page, "5", "Chat, or offer something", "Press 1 to ask what they like (uses a chat). Press 2 to offer them something, like a good book: once per party, it changes how that guest feels and shows you what they prefer.");
            Step(page, "6", "Next round", "Change the record on the record player (by the back wall, next to the door). After the last round, your guests head home and you see how it went.");
            Step(page, "7", "Keep a guestbook", "Open Friends and guess what each friend likes: for every scale you haven't found out yet, click a side or Doesn't mind. When the real answer shows up, you'll see if you were right.");
            UIKit.Label(page, "Details about budget, the Helper, traits, Willingness and stars are under Rules.",
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
            _willing.Clear();
            _partyIndex = 0;
        }

        int Willing(GuestData g) => _willing.TryGetValue(g, out var w) ? w : Mathf.Clamp(S.willingnessStart, 0, S.willingnessMax);

        /// <summary>
        /// A friend with no Willingness left turns the invitation down, unless there aren't enough willing
        /// friends to fill the guest list (then they come anyway, so a run can never get stuck).
        /// </summary>
        bool WillCome(GuestData g)
        {
            if (Willing(g) > 0) return true;
            int willing = AvailableGuests().Count(x => Willing(x) > 0);
            return willing < _plan.occasion.guestCount;
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
                if (gs.willingness >= 0) _willing[g] = Mathf.Clamp(gs.willingness, 0, S.willingnessMax);
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
            _savedNext = nextParty;
            _savedFinished = finished;
            var data = new SaveData
            {
                nextParty = nextParty,
                finished = finished,
                seenHowTo = _seenHowTo,
                partyStars = new List<int>(_history),
            };
            foreach (var g in content.guests)
            {
                var gs = new GuestSave { id = g.id, willingness = Willing(g) };
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
            UpdateWillingness();
            bool last = _partyIndex >= content.occasions.Count - 1;
            SaveGame(_partyIndex + 1, last);
            ShowResults();
        }

        /// <summary>
        /// Unhappy guests (incompatible party) lose Willingness, very happy ones gain it back.
        /// A friend who sat a party out with none left misses you a little and gets 1 back.
        /// </summary>
        void UpdateWillingness()
        {
            _willingChanges.Clear();
            foreach (var g in AvailableGuests())
            {
                int from = Willing(g), to = from;
                var go = _outcome.For(g);
                if (go != null)
                {
                    if (go.Stars <= S.willingnessLossStars) to = from - 1;
                    else if (go.Stars >= S.willingnessGainStars) to = from + 1;
                }
                else if (from == 0) to = 1;
                to = Mathf.Clamp(to, 0, S.willingnessMax);
                if (to == from) continue;
                _willing[g] = to;
                _willingChanges.Add((g, from, to));
            }
        }

        /// <summary>Row of hearts: filled = Willingness left (how keen they are to come to your parties).</summary>
        void WillingnessPips(Transform parent, GuestData g, float size = 18)
        {
            UIKit.Hearts(parent, Willing(g), S.willingnessMax, size);
        }

        string WillingnessText(GuestData g)
        {
            int w = Willing(g);
            return w >= S.willingnessMax ? $"{w}/{S.willingnessMax}, can't wait to come"
                : w > 0 ? $"{w}/{S.willingnessMax}"
                : $"0/{S.willingnessMax}, skipping the next party";
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
                var funRow = UIKit.Row(card.transform, 10, TextAnchor.MiddleLeft);
                UIKit.Size(UIKit.Label(funRow, "Fun tonight", 18, UIKit.Muted), 120);
                UIKit.Stars(funRow, go.Stars, 5, 24);
                var best = go.Best;
                var worst = go.Worst;
                if (best != null) UIKit.Label(card.transform, "<b>Best part:</b> “" + _picker.ForReason(best, go.guest) + "”", 22, UIKit.Good);
                if (worst != null) UIKit.Label(card.transform, "<b>Not so great:</b> “" + _picker.ForReason(worst, go.guest) + "”", 22, UIKit.Bad);
                if (best == null && worst == null) UIKit.Label(card.transform, "“" + _picker.Pick("Generic.Good", go.guest, null) + "”", 22);

                // Willingness after this party
                var change = _willingChanges.Find(c => c.guest == go.guest);
                string wText = change.guest == null ? ""
                    : change.to < change.from ? "   <color=#cc4d4d>-1, less keen to come</color>"
                    : "   <color=#4d9a59>+1, keen to come again</color>";
                UIKit.Label(card.transform, $"Willingness {Willing(go.guest)}/{S.willingnessMax}{wText}", 18, UIKit.Muted);
                WillingnessPips(card.transform, go.guest, 22);
            }

            // Friends who sat this one out with no Willingness left
            foreach (var c in _willingChanges.Where(c => _outcome.For(c.guest) == null))
                UIKit.Label(page, $"<b>{c.guest.displayName}</b> missed you and would come again now.", 22, UIKit.Muted);

            if (_partyDiscoveries.Count > 0)
            {
                UIKit.Spacer(page, 10);
                UIKit.Label(page, "You learned (saved in Friends)", 30, null, TextAnchor.UpperLeft, FontStyle.Bold);
                // One line per guest, in the order guests first taught you something
                foreach (var group in _partyDiscoveries.GroupBy(d => d.guest))
                    UIKit.Label(page, $"<b>{group.Key.displayName}</b>:  " +
                                      string.Join("   ·   ", group.Select(d => d.text + GuessCheck(d))), 24);
            }

            // Nudge toward the guestbook while any guest from tonight still has a ??? left
            var stillUnknown = _outcome.guests.Select(x => x.guest).FirstOrDefault(x => _knowledge.UnknownAxes(x).Any());
            if (stillUnknown != null)
            {
                UIKit.Spacer(page, 6);
                var nudge = UIKit.Row(page, 16, TextAnchor.MiddleLeft);
                UIKit.Button(nudge, "Update your guestbook", () => ShowFriends(stillUnknown, ShowResults), 340, 56);
                UIKit.Label(nudge, "Write down what you think your friends like, based on what they said tonight.", 22, UIKit.Muted, TextAnchor.MiddleLeft);
            }

            UIKit.Spacer(page);
            bool more = _partyIndex < content.occasions.Count - 1;
            Footer(page, "", more ? "Next party" : "Finish", () =>
            {
                if (more) { _partyIndex++; StartOccasion(); }
                else ShowEnd();
            }, true, null, ShowResults);
        }

        /// <summary>" (you guessed right!)" when a revealed preference matches the player's note.</summary>
        string GuessCheck(Discovery d)
        {
            if (!d.axis.HasValue) return "";
            var guess = _knowledge.GuessFor(d.guest, d.axis.Value);
            if (guess == Guess.None) return "";
            bool right = guess == GuestKnowledge.Truth(d.guest.stats[d.axis.Value], S.caresThreshold);
            return right ? " <color=#4d9a59>(you guessed right!)</color>" : " <color=#cc4d4d>(not what you guessed)</color>";
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
                    "<b>2. Invite</b>  use the phone (by the right wall) to invite exactly that many friends. Friends with no Willingness left say no.",
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
                    "<b>Your guestbook:</b> in Friends, each scale you haven't found out yet has three buttons: one side, <b>Doesn't mind</b>, or the other side. Click the one you think is right to write down your guess (click again to clear). Guesses are only notes, they never change the game. When the real answer is revealed, Friends tells you if you guessed right.",
                }),
                ("Willingness", new[]
                {
                    $"Every friend starts with a Willingness of {S.willingnessStart} out of {S.willingnessMax} (the pink hearts next to their name). You can always see it. Hearts are about coming to your <i>next</i> parties; the \"Mood tonight\" bar and the stars are about <i>this</i> party.",
                    $"A guest who leaves with {S.willingnessGainStars} stars or more had a good party: they gain 1 Willingness (up to {S.willingnessMax}).",
                    $"A guest who leaves with {S.willingnessLossStars} stars or fewer had a party that didn't suit them: they lose 1 Willingness.",
                    "At 0 they skip the next party (they turn down your invitation). After sitting it out they get 1 back, so you can invite them again.",
                    "If too few friends are willing to fill the guest list, the unwilling ones come anyway.",
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
                    "Their speech bubble says how it's going. Talk to them to see why: <color=#4d9a59><b>green</b></color> made them happier, <color=#cc4d4d><b>red</b></color> made them less happy. Their \"Mood tonight\" bar moves by exactly that much, and where it ends gives their stars.",
                    "Small things come up first, the biggest reason last. A guest with nothing new to say just makes small talk.",
                    "Everything a guest reacts to is written into Friends automatically.",
                }),
                ("Chatting and offering", new[]
                {
                    $"You get {chats} chats every round (they refill), at most one per guest per round.",
                    "Walk up to a guest and press E to talk (free). Press 1 in the conversation to ask what they like: that is a chat.",
                    "A chat tells you one hidden preference of that guest. It does not change how the party goes.",
                    "If you already know everything about a guest you can still chat to catch up. You won't learn anything new, and it still uses a chat.",
                    "<b>Offer something (press 2):</b> once per party, give one guest something, like a good book. Each item pushes one or two scales (shown on its button).",
                    $"If the guest leans that way they get happier, if they lean the other way they get less happy (up to {Num(S.interventionMax)} either way). It only affects that guest, it counts toward their stars, and you learn where they stand on those scales.",
                }),
                ("Stars", new[]
                {
                    $"Every guest starts at {Num(S.baseHappiness)} stars. Each green or red reason you saw during the party moves that up or down.",
                    "The final value is rounded to 1 to 5 stars. The party's stars are the average of all guests.",
                }),
                ("Saving", new[]
                {
                    "Pick a save slot when you start. Each player should use their own slot.",
                    "Anything you learn about a friend is saved right away and stays unlocked in that slot for good. So are your guestbook notes and every friend's Willingness.",
                    "<b>Continue</b> picks up at the next party. After the last party it plays the parties again, keeping what you know.",
                    "<b>Start over</b> erases the slot and starts from scratch.",
                }),
            };
        }

        // ================================================================ friends (character profiles)

        List<GuestData> AvailableGuests() => content.guests.Where(x => x.availableFrom <= _partyIndex).ToList();

        void ShowFriends(GuestData selected, System.Action back)
        {
            _friendsBack = back;
            var guests = AvailableGuests();
            if (selected == null) selected = guests.FirstOrDefault();

            var page = NewPage();
            Header(page, "Friends", "Your guestbook: everything you've learned about your friends, plus your own guesses. Host more parties to fill in the ???.");

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
            var willing = UIKit.Row(info, 10, TextAnchor.MiddleLeft);
            UIKit.Label(willing, "Willingness", 22, UIKit.Muted);
            WillingnessPips(willing, g, 18);
            UIKit.Label(willing, WillingnessText(g), 20, UIKit.Muted);

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
            if (_knowledge.UnknownAxes(g).Any())
            {
                // Call-out so players notice they can write guesses in
                var tip = UIKit.Panel(sliders, new Color(1f, 0.93f, 0.8f), "GuessTip");
                UIKit.VLayout(tip, 4, 14);
                UIKit.Label(tip.transform, "Guess what they like!", 22, UIKit.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Label(tip.transform, "For each scale you haven't found out yet, click the side you think they lean to, or <b>Doesn't mind</b>. " +
                                           "Your guess turns orange; click it again to clear. When you find out the real answer, you'll see if you were right.", 18);
            }
            foreach (var axis in Axes.All)
                Slider(sliders, g, axis);

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

        /// <summary>
        /// One scale in the profile. Known: a blue marker at the real value. Unknown: "???", and the player
        /// can click a side (or the bar for "neither") to write their own guess in, like the doc's guestbook.
        /// A guess stays after the real value is revealed, coloured green if it was right, red if not.
        /// </summary>
        void Slider(Transform parent, GuestData g, PartyAxis axis)
        {
            bool known = _knowledge.Knows(g, axis);
            float value = g.stats[axis];
            var guess = _knowledge.GuessFor(g, axis);

            void Pick(Guess choice)
            {
                _knowledge.SetGuess(g, axis, guess == choice ? Guess.None : choice);
                SaveGame(_savedNext, _savedFinished); // notes are saved right away, without touching progress
                ShowFriends(g, _friendsBack);
            }

            var row = UIKit.Row(parent, 12, TextAnchor.MiddleLeft);
            UIKit.Size(row, -1, 44);
            var name = UIKit.Label(row, $"{Axes.NegativePole(axis)} / {Axes.PositivePole(axis)}", 17, UIKit.Muted, TextAnchor.MiddleLeft);
            UIKit.Size(name, 230);

            if (!known)
            {
                // Unknown: three clear buttons to write a guess in (click the chosen one again to clear it)
                void GuessButton(string text, Guess choice)
                {
                    bool chosen = guess == choice;
                    UIKit.Button(row, text, () => Pick(choice), choice == Guess.Neither ? 150 : 165, 40,
                        true, chosen ? UIKit.Accent : new Color(0.72f, 0.68f, 0.64f), 17);
                }
                GuessButton(Axes.NegativePole(axis), Guess.Negative);
                GuessButton("Doesn't mind", Guess.Neither);
                GuessButton(Axes.PositivePole(axis), Guess.Positive);
                return;
            }

            // Known: the real value on a track, plus how the player's guess did
            var track = UIKit.Panel(row, new Color(0.88f, 0.86f, 0.83f), "Track");
            UIKit.Size(track, 230, 22);
            var mid = UIKit.Panel(track.transform, UIKit.AccentDim, "Middle");
            mid.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            mid.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            mid.rectTransform.sizeDelta = new Vector2(2, 0);
            float t = Mathf.InverseLerp(Axes.Min, Axes.Max, value);
            var marker = UIKit.Panel(track.transform, new Color(0.3f, 0.45f, 0.75f), "Marker");
            marker.rectTransform.anchorMin = new Vector2(t, 0f);
            marker.rectTransform.anchorMax = new Vector2(t, 1f);
            marker.rectTransform.sizeDelta = new Vector2(10, 8);

            string answer = $"<b>{Axes.Describe(axis, value)}</b>";
            if (guess != Guess.None)
            {
                bool right = guess == GuestKnowledge.Truth(value, S.caresThreshold);
                answer += right ? "  <color=#4d9a59>you guessed right!</color>"
                                : $"  <color=#cc4d4d>you guessed {GuessName(axis, guess)}</color>";
            }
            UIKit.Size(UIKit.Label(row, answer, 18, null, TextAnchor.MiddleLeft), 240);
        }

        static string GuessName(PartyAxis axis, Guess g) =>
            g == Guess.Negative ? Axes.NegativePole(axis) : g == Guess.Positive ? Axes.PositivePole(axis) : "doesn't mind";

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
            var notes = GuessNotes(g);
            if (notes.Length > 0) bits.Add($"<i>your guess: {notes}</i>");
            return bits.Count > 0 ? string.Join(", ", bits) : "still a mystery";
        }

        /// <summary>The player's guesses for scales they haven't confirmed yet, e.g. "Playful?, Small party?".</summary>
        string GuessNotes(GuestData g) => string.Join(", ", Axes.All
            .Where(a => !_knowledge.Knows(g, a) && _knowledge.GuessFor(g, a) != Guess.None)
            .Select(a =>
            {
                var guess = _knowledge.GuessFor(g, a);
                return (guess == Guess.Negative ? Axes.NegativePole(a) : guess == Guess.Positive ? Axes.PositivePole(a) : $"not fussed about {Axes.NegativePole(a)}/{Axes.PositivePole(a)}") + "?";
            }));

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
