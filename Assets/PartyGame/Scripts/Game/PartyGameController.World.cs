using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PartyGame
{
    /// <summary>
    /// The game inside the 3D room. You play the host and walk around (WASD), pressing E on things:
    ///   1. Invite   phone (right wall)         -> pick friends on the phone
    ///   2. Prep     glowing activity spots     -> set up furniture within the budget
    ///               front door                 -> guests walk in, the party starts
    ///   3. Party    guests                     -> talk; ask what they like (uses a chat)
    ///               record player              -> next round; after the last one guests leave
    ///   4. Results  (menu page), then the next day starts in the same room.
    /// The screen only shows a small goal card, head tags, the "E" prompt and small pop-ups.
    /// </summary>
    public partial class PartyGameController
    {
        enum Phase { Invite, Prep, Arriving, Party, Leaving }
        enum Popup { None, Occasion, Phone, Talk }

        Phase _phase;
        Popup _popup;
        GuestData _talking;          // guest in the open conversation
        int _popupFrame;             // ignore keys on the frame a pop-up opened
        bool _inRoom;                // the room HUD is showing (not a menu page)
        bool _paused;                // Esc pause menu is open (time is stopped)
        bool _offerUsed;             // this party's one intervention is spent
        bool _offering;              // the talk panel is showing the list of things to offer
        bool _worldReady;
        HostController _host;
        readonly Dictionary<ActivityProp, GameObject> _markers = new Dictionary<ActivityProp, GameObject>();

        // HUD prompt ("E  Talk to Guest 1"), updated every frame
        RectTransform _promptBox;
        Image _promptBg;
        Graphic _promptText;
        WorldFollower _promptFollow;
        string _promptShown;

        // ================================================================ setup

        /// <summary>Creates the host and makes the phone, door, record player and activity spots usable.</summary>
        void SetupWorld()
        {
            if (_worldReady) return;
            _worldReady = true;

            var start = _room.Point(RoomPoint.Kind.HostStart);
            _host = HostController.Create(_room, start != null ? start.transform.position : Vector3.zero);

            Use(_room.Point(RoomPoint.Kind.Phone),
                () => _phase == Phase.Invite || _phase == Phase.Prep ? "Use the phone (invite friends)" : null,
                () => true, OpenPhone);

            Use(_room.Point(RoomPoint.Kind.Door),
                () => _phase == Phase.Invite ? "Front door (invite your guests first)"
                    : _phase != Phase.Prep ? null
                    : WithinBudget ? "Open the door: start the party!"
                    : "Front door (over budget: put something away first)",
                () => _phase == Phase.Prep && WithinBudget, OpenDoor);

            Use(_room.Point(RoomPoint.Kind.Music),
                () => _phase != Phase.Party ? null
                    : _round >= _plan.occasion.rounds - 1 ? "Change the record: wrap up the party"
                    : $"Change the record: on to round {_round + 2} of {_plan.occasion.rounds}",
                () => true, UseRecordPlayer);

            foreach (var prop in _room.Props)
            {
                var activity = content.activities.Find(a => a.id == prop.activityId);
                if (activity == null) continue;

                // Glowing disc on the floor where the activity can go.
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(disc.GetComponent<Collider>());
                disc.name = "EmptySpot";
                disc.transform.SetParent(prop.transform, false);
                disc.transform.position = prop.SpotPosition + Vector3.up * 0.03f;
                disc.transform.localScale = new Vector3(0.9f, 0.005f, 0.9f);
                disc.GetComponent<Renderer>().sharedMaterial = RoomBuilder.GlowMat(new Color(1f, 0.8f, 0.35f, 0.55f));
                _markers[prop] = disc;

                var spot = new GameObject("Use_" + activity.id);
                spot.transform.SetParent(prop.transform, false);
                spot.transform.position = prop.SpotPosition;
                var a = activity;
                var it = AddInteractable(spot, 1.1f, 1.2f,
                    () => _phase != Phase.Prep ? null
                        : _plan.activities.Contains(a) ? $"Put away {a.displayName}"
                        : CanAfford(a) ? $"Set up {a.displayName}  (cost {a.cost})  {DescribeShift(a)}"
                        : $"{a.displayName} costs {a.cost}: over budget",
                    () => _plan.activities.Contains(a) || CanAfford(a),
                    () => ToggleActivity(a));
                it.name = "Use_" + a.id;
            }
        }

        void Use(RoomPoint point, System.Func<string> prompt, System.Func<bool> canUse, System.Action use)
        {
            if (point == null) return;
            AddInteractable(point.gameObject, point.radius, 1.4f, prompt, canUse, use);
        }

        static Interactable AddInteractable(GameObject go, float radius, float promptHeight,
            System.Func<string> prompt, System.Func<bool> canUse, System.Action use)
        {
            var it = go.AddComponent<Interactable>();
            it.radius = radius;
            it.prompt = prompt;
            it.canUse = canUse;
            it.use = use;
            var anchor = new GameObject("PromptAnchor").transform;
            anchor.SetParent(go.transform, false);
            anchor.position = go.transform.position + Vector3.up * promptHeight;
            it.promptAnchor = anchor;
            return it;
        }

        // ================================================================ a new day

        void StartOccasion()
        {
            _plan = new PartyPlan { occasion = content.occasions[_partyIndex] };
            _outcome = null;
            _round = 0;
            _talking = null;
            SetupWorld();
            _room.ClearGuests();
            _room.SetActivities(_plan.activities);
            _room.SetDoorOpen(false);
            var start = _room.Point(RoomPoint.Kind.HostStart);
            _host.Teleport(start != null ? start.transform.position : Vector3.zero);
            _phase = Phase.Invite;
            OpenPopup(Popup.Occasion);
        }

        void OpenPopup(Popup popup)
        {
            _popup = popup;
            _popupFrame = Time.frameCount;
            ShowRoom();
        }

        void ClosePopup()
        {
            if (_popup == Popup.Phone)
            {
                // Invites done: on to setting up. Re-opening the phone can still change the guest list.
                bool full = _plan.guests.Count == _plan.occasion.guestCount;
                if (_phase == Phase.Invite && full) _phase = Phase.Prep;
                else if (_phase == Phase.Prep && !full) _phase = Phase.Invite;

                // A Helper says so when you invite them ("I'll come early and help set up!"),
                // so the budget saving never shows up before you know who caused it.
                if (_phase == Phase.Prep)
                    foreach (var helper in _plan.guests.Where(g => g.HasTrait(GuestTrait.Helper)))
                        if (_knowledge.Reveal(helper, GuestTrait.Helper)) SaveLearned();
            }
            _popup = Popup.None;
            _talking = null;
            _offering = false;
            ShowRoom();
        }

        // ================================================================ invite (phone)

        string DeclineLine(GuestData g)
        {
            // Same line every time the phone panel redraws.
            if (!content.dialogue.TryGet("Invite.Decline", out var lines) || lines.Count == 0) return "Maybe next time.";
            return lines[Mathf.Abs(g.id.GetHashCode()) % lines.Count];
        }

        void OpenPhone() => OpenPopup(Popup.Phone);

        void ToggleGuest(GuestData g)
        {
            if (!_plan.guests.Remove(g) && _plan.guests.Count < _plan.occasion.guestCount && WillCome(g)) _plan.guests.Add(g);
            ShowRoom();
        }

        // ================================================================ prep (activities)

        /// <summary>"is a Gossip" / "is an Inquisitive".</summary>
        static string IsA(GuestTrait t) => ("AEIOU".IndexOf(t.ToString()[0]) >= 0 ? "is an " : "is a ") + t;

        bool WithinBudget => PartyScorer.ActivityCost(_plan, S) <= Budget;

        bool CanAfford(ActivityData a)
        {
            _plan.activities.Add(a);
            int cost = PartyScorer.ActivityCost(_plan, S);
            _plan.activities.Remove(a);
            return cost <= Budget;
        }

        void ToggleActivity(ActivityData a)
        {
            if (!_plan.activities.Remove(a))
            {
                if (!CanAfford(a)) return;
                _plan.activities.Add(a);
            }
            _room.SetActivities(_plan.activities);
            ShowRoom();
        }

        void OpenDoor() => StartCoroutine(GuestsArrive());

        IEnumerator GuestsArrive()
        {
            _phase = Phase.Arriving;
            _room.SetDoorOpen(true);
            ShowRoom();
            yield return _room.Arrive(_plan.guests);
            _room.SetDoorOpen(false);

            foreach (var g in _plan.guests)
            {
                var root = _room.RootOf(g);
                if (root == null) continue;
                var guest = g;
                AddInteractable(root.gameObject, 1.3f, _room.headHeight + 0.75f,
                    () => _phase != Phase.Party ? null
                        : _chatsLeft <= 0 ? $"Talk to {guest.displayName}  (no chats left this round)"
                        : $"Talk to {guest.displayName}",
                    () => true, () => OpenTalk(guest));
            }
            StartParty();
        }

        // ================================================================ the party

        void StartParty()
        {
            _outcome = PartyScorer.Score(_plan, S);
            _round = 0;
            _offerUsed = false;
            _offering = false;
            _partyDiscoveries.Clear();
            _schedule.Clear();
            foreach (var go in _outcome.guests)
                _schedule[go.guest] = go.Schedule(_plan.occasion.rounds);

            // Rules: "Traits start hidden. You learn them when that friend comes to a party."
            // (A Helper is already known from the invite.)
            foreach (var g in _plan.guests)
                foreach (var t in g.traits)
                    if (_knowledge.Reveal(g, t))
                        _partyDiscoveries.Add(new Discovery { guest = g, text = IsA(t) });

            _phase = Phase.Party;
            PlayRound();
            ShowRoom();
        }

        /// <summary>Reveal this round's reasons: they set what guests say and teach the player about them.</summary>
        void PlayRound()
        {
            _bubbles.Clear();
            _chattedThisRound.Clear();
            _chatsLeft = ChatsPerRound; // chats refill every round
            foreach (var go in _outcome.guests)
            {
                var now = _schedule[go.guest][_round];
                _bubbles[go.guest] = _picker.ForRound(go.guest, now);
                foreach (var r in now) LearnFrom(go.guest, r);
            }
            SaveLearned();
        }

        /// <summary>
        /// Anything learned is unlocked for good in this slot, even if the player quits mid-party.
        /// Only Start over on the slot clears it. Party progress itself is saved per party.
        /// </summary>
        void SaveLearned() => SaveGame(_partyIndex, false);

        float MoodAt(GuestData g, int round)
        {
            float mood = S.baseHappiness;
            var s = _schedule[g];
            for (int i = 0; i <= round && i < s.Length; i++)
                mood += s[i].Sum(r => r.value);
            return mood;
        }

        void UseRecordPlayer()
        {
            if (_round >= _plan.occasion.rounds - 1) StartCoroutine(GuestsLeave());
            else
            {
                _round++;
                PlayRound();
                ShowRoom();
            }
        }

        IEnumerator GuestsLeave()
        {
            _phase = Phase.Leaving;
            _talking = null;
            _popup = Popup.None;
            ShowRoom();
            _room.SetDoorOpen(true);
            yield return _room.Leave();
            _room.SetDoorOpen(false);
            FinishParty(); // records stars, saves, shows the results page
        }

        void OpenTalk(GuestData g)
        {
            _talking = g;
            _offering = false;
            OpenPopup(Popup.Talk);
        }

        // ================================================================ intervention

        bool CanOffer => _phase == Phase.Party && !_offerUsed && content.interventions.Count > 0;

        /// <summary>Which scales an item nudges, e.g. "Small party, Wallflower".</summary>
        static string ItemPush(InterventionItem item) => string.Join(", ", Axes.All
            .Where(a => Mathf.Abs(item.push[a]) > 0.01f)
            .Select(a => item.push[a] < 0 ? Axes.NegativePole(a) : Axes.PositivePole(a)));

        /// <summary>
        /// Design doc "Intervention": once per party, offer one guest something. It only affects that guest:
        /// they like it if they lean the way it pushes, dislike it if they lean the other way, and say so.
        /// Either way the player learns where they stand on the item's scales.
        /// </summary>
        void Offer(GuestData g, InterventionItem item)
        {
            if (!CanOffer || g == null) return;
            _offerUsed = true;
            _offering = false;

            float value = 0f;
            var axes = Axes.All.Where(a => Mathf.Abs(item.push[a]) > 0.01f).ToList();
            foreach (var a in axes)
            {
                float stat = g.stats[a];
                if (Mathf.Abs(stat) >= S.caresThreshold) value += item.push[a] * stat * S.interventionScale;
            }
            value = Mathf.Clamp(value, -S.interventionMax, S.interventionMax);
            string key = value > 0.05f ? "Offer.Good" : value < -0.05f ? "Offer.Bad" : "Offer.Meh";

            // It changes their mood from this round on, and counts toward their stars.
            if (key != "Offer.Meh")
            {
                var reason = new Reason { key = key, value = value, axis = axes.OrderByDescending(a => Mathf.Abs(g.stats[a])).First() };
                var outcome = _outcome.For(g);
                outcome.happiness += value;
                outcome.reasons.Add(reason);
                _schedule[g][_round].Add(reason);
            }

            var learned = new List<string>();
            foreach (var a in axes)
                if (_knowledge.Reveal(g, a))
                {
                    string text = $"{Axes.Describe(a, g.stats[a])} ({a})";
                    learned.Add(text);
                    _partyDiscoveries.Add(new Discovery { guest = g, text = text + "  (offer)", axis = a });
                }
            SaveLearned();

            string note = learned.Count > 0 ? "Learned: " + string.Join(", ", learned) : "Nothing new, but now you're sure.";
            _bubbles[g] = $"({item.displayName}) " + _picker.Pick(key, g, null) +
                          $"\n<color=#8a7f76><size=20>{note}</size></color>";
            _talking = g;
            ShowRoom();
        }

        bool CanChat(GuestData g) => _chatsLeft > 0 && !_chattedThisRound.Contains(g);

        void Chat(GuestData g)
        {
            if (!CanChat(g)) return;
            _chatsLeft--;
            _chattedThisRound.Add(g);

            // Prefer something this party won't show anyway, so chatting always adds information.
            var unknown = _knowledge.UnknownAxes(g).ToList();
            if (unknown.Count == 0)
            {
                // Nothing new to learn: small talk, or they talk about something you already know they love.
                var loves = Axes.All.Where(a => Mathf.Abs(g.stats[a]) >= S.caresThreshold).ToList();
                string line = loves.Count > 0 && Random.value < 0.5f
                    ? _picker.ForReveal(g, loves[Random.Range(0, loves.Count)], S.caresThreshold)
                    : _picker.ForRound(g, null);
                _bubbles[g] = line + "\n<color=#8a7f76><size=20>Nothing new, but it was a nice chat.</size></color>";
            }
            else
            {
                var shownLater = new HashSet<PartyAxis>(_schedule[g].SelectMany(x => x)
                    .Where(r => r.axis.HasValue).Select(r => r.axis.Value));
                PartyAxis axis = unknown
                    .OrderBy(a => shownLater.Contains(a) ? 1 : 0)
                    .ThenByDescending(a => Mathf.Abs(g.stats[a]))
                    .First();
                _knowledge.Reveal(g, axis);
                SaveLearned();
                string learned = $"{Axes.Describe(axis, g.stats[axis])} ({axis})";
                _partyDiscoveries.Add(new Discovery { guest = g, text = learned + "  (chat)", axis = axis });
                _bubbles[g] = _picker.ForReveal(g, axis, S.caresThreshold) +
                              $"\n<color=#8a7f76><size=20>Learned: {learned}</size></color>";
            }
            _talking = g;
            ShowRoom();
        }

        /// <summary>Records what a revealed reason tells the player about a guest.</summary>
        void LearnFrom(GuestData g, Reason r)
        {
            if (r.axis.HasValue && _knowledge.Reveal(g, r.axis.Value))
                _partyDiscoveries.Add(new Discovery { guest = g, text = $"{Axes.Describe(r.axis.Value, g.stats[r.axis.Value])} ({r.axis.Value})", axis = r.axis.Value });
            if (r.trait.HasValue && _knowledge.Reveal(g, r.trait.Value))
                _partyDiscoveries.Add(new Discovery { guest = g, text = IsA(r.trait.Value) });
            if (r.key == "Dislike.Bad" && _knowledge.RevealDislike(g))
                _partyDiscoveries.Add(new Discovery { guest = g, text = DislikeText(g) });
            if (r.key.StartsWith("Diet.") && _knowledge.RevealDiet(g))
                _partyDiscoveries.Add(new Discovery { guest = g, text = "needs vegan / gluten-free food" });
        }

        // ================================================================ input

        void Update()
        {
            if (_host == null) return;
            bool busy = _phase == Phase.Arriving || _phase == Phase.Leaving;
            _host.frozen = !_inRoom || _paused || _popup != Popup.None || busy;
            if (_inRoom) UpdatePrompt();
            if (!_inRoom) return;

            // Esc always pauses (or resumes); panels close with E or their buttons.
            if (Input.GetKeyDown(KeyCode.Escape)) { SetPaused(!_paused); return; }
            if (_paused || _popup == Popup.None || Time.frameCount == _popupFrame) return;

            bool close = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
            if (_popup == Popup.Talk && _offering)
            {
                // Number keys pick an item; E goes back to the conversation.
                var items = content.interventions;
                for (int i = 0; i < items.Count && i < 9; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { Offer(_talking, items[i]); return; }
                if (close) { _offering = false; ShowRoom(); }
            }
            else if (_popup == Popup.Talk)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) && _talking != null) Chat(_talking);
                else if (Input.GetKeyDown(KeyCode.Alpha2) && CanOffer) { _offering = true; _popupFrame = Time.frameCount; ShowRoom(); }
                else if (close) ClosePopup();
            }
            else if (close) ClosePopup();
        }

        // ================================================================ pause

        void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f; // also stops guests walking in or out
            ShowRoom();
        }

        void QuitToMenu()
        {
            _paused = false;
            Time.timeScale = 1f;
            // Stop guests arriving/leaving, or the coroutine would pop the room back over the menu.
            StopAllCoroutines();
            _room.StopAllCoroutines();
            _popup = Popup.None;
            _talking = null;
            ShowTitle();
        }

        void PauseMenu(Transform page)
        {
            var dim = Overlay(page);
            var veil = dim.gameObject.AddComponent<Image>();
            veil.color = new Color(0f, 0f, 0f, 0.45f); // blocks clicks on the HUD underneath

            var card = UIKit.FloatingCard(dim, UIKit.Background, 520, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 36);
            card.GetComponent<VerticalLayoutGroup>().spacing = 14;
            UIKit.Label(card.transform, "Paused", 52, null, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Spacer(card.transform, 6);
            UIKit.Button(card.transform, "Resume  (Esc)", () => SetPaused(false), 448, 64);
            UIKit.Button(card.transform, "Rules", () => ShowRules(0, ShowRoom), 448, 60, true, UIKit.Muted, 24);
            UIKit.Button(card.transform, "Friends", () => ShowFriends(null, ShowRoom), 448, 60, true, UIKit.Muted, 24);
            UIKit.Button(card.transform, "Main menu", QuitToMenu, 448, 60, true, UIKit.Muted, 24);
            UIKit.Label(card.transform, "Leaving now ends this party. Everything you learned about your friends stays saved.",
                18, UIKit.Muted, TextAnchor.MiddleCenter);
        }

        /// <summary>Banner at the top of the room once this round's chats are used up, pointing at the record player.</summary>
        void OutOfChatsBanner(Transform layer, float canvasWidth)
        {
            bool last = _round >= _plan.occasion.rounds - 1;
            string next = last ? "to wrap up the party" : $"to start round {_round + 2}";
            RoomBanner(layer, canvasWidth, "No chats left this round.",
                $"Walk to the <b>record player</b> (next to the door) and press E {next}.", UIKit.Accent);
        }

        /// <summary>A note centred at the top of the room view.</summary>
        void RoomBanner(Transform layer, float canvasWidth, string title, string body, Color titleColor)
        {
            float roomCentre = -(canvasWidth - PanelWidth) / 2f;
            var card = UIKit.FloatingCard(layer, new Color(1f, 0.93f, 0.82f), 760, new Vector2(1, 1), new Vector2(0.5f, 1f),
                new Vector2(roomCentre, -24), 18);
            UIKit.Label(card.transform, $"<b>{title}</b>", 26, titleColor, TextAnchor.MiddleCenter);
            UIKit.Label(card.transform, body, 22, null, TextAnchor.MiddleCenter);
        }

        enum BudgetState { Nothing, Over, Exact, RoomLeft, NothingFits }

        /// <summary>Where the setup stands against the budget, with a title, a line of advice and a colour.</summary>
        BudgetState PrepState(out string title, out string body, out Color color)
        {
            int cost = PartyScorer.ActivityCost(_plan, S);
            int left = Budget - cost;
            bool food = _plan.activities.Any(x => x.isFood);
            string foodNote = food ? "" : " <color=#cc4d4d>No food yet: guests will be hungry!</color>";
            var fits = content.activities.Where(a => !_plan.activities.Contains(a) && CanAfford(a)).ToList();

            if (cost > Budget)
            {
                title = $"Over budget by {cost - Budget}!";
                body = "Walk to a set-up activity and press E to put it away. The door stays shut until you're within budget.";
                color = UIKit.Bad;
                return BudgetState.Over;
            }
            if (_plan.activities.Count == 0)
            {
                title = $"Nothing set up yet  ·  {left} to spend";
                body = "Walk to a glowing spot and press E to set up an activity." + foodNote;
                color = UIKit.Accent;
                return BudgetState.Nothing;
            }
            if (left == 0)
            {
                title = "Budget used exactly. Ready to go!";
                body = "Open the <b>front door</b> to start the party." + foodNote;
                color = UIKit.Good;
                return BudgetState.Exact;
            }
            if (fits.Count == 0)
            {
                title = $"{left} left, but nothing else fits";
                body = "You're all set: open the <b>front door</b> to start the party." + foodNote;
                color = UIKit.Good;
                return BudgetState.NothingFits;
            }
            string names = string.Join(", ", fits.Select(a => $"{a.displayName} ({a.cost})"));
            title = $"{left} left to spend";
            body = $"Still fits: {names}. Or open the <b>front door</b> to start now." + foodNote;
            color = UIKit.Accent;
            return BudgetState.RoomLeft;
        }

        void UpdatePrompt()
        {
            if (_promptBox == null) return;
            var target = _host.frozen ? null : _host.Target;
            string text = target != null ? target.Prompt : null;
            bool show = !string.IsNullOrEmpty(text);
            if (_promptBox.gameObject.activeSelf != show) _promptBox.gameObject.SetActive(show);
            if (!show) { _promptShown = null; return; }

            bool usable = target.CanUse;
            string full = usable ? $"<b>E</b>   {text}" : text;
            if (full != _promptShown)
            {
                _promptShown = full;
                UIKit.SetText(_promptText, full);
                _promptBg.color = usable ? Color.white : new Color(0.9f, 0.88f, 0.85f);
            }
            // Float above the host's own head, so it never covers the face or the thing being used.
            _promptFollow.target = _host.head != null ? _host.head : target.promptAnchor;
        }

        // ================================================================ HUD (left panel + the room on the right)

        /// <summary>Width of the UI column on the left, in canvas units (the canvas is 1920 wide).</summary>
        const float PanelWidth = 640f;

        /// <summary>Rebuilds the screen: the UI column on the left, the room filling the rest.</summary>
        void ShowRoom()
        {
            var page = NewPage();
            _background.color = Color.clear;
            _inRoom = true;
            float canvasWidth = _root.rect.width > 1f ? _root.rect.width : 1920f;
            float split = Mathf.Clamp01(PanelWidth / canvasWidth);
            _room.SetViewport(new Rect(split, 0, 1f - split, 1));

            // Glowing spots only while setting up, and only where nothing stands yet.
            foreach (var pair in _markers)
                pair.Value.SetActive(_phase == Phase.Prep && !_plan.activities.Any(a => a.id == pair.Key.activityId));

            // Things floating in the room: head tags, spot labels, the "E" prompt.
            var tags = Overlay(page);
            if (_phase == Phase.Party)
            {
                var guests = _plan.guests;
                for (int i = 0; i < guests.Count; i++)
                {
                    // Guests in the front row stand lower on screen, right next to the back row; lift their
                    // bubbles above the back row's so the two rows of bubbles don't overlap.
                    var root = _room.RootOf(guests[i]);
                    bool frontRow = root != null && root.position.z < -1.4f;
                    SpeechBubble(tags, guests[i], MoodAt(guests[i], _round), frontRow ? 135f : 0f);
                }
            }
            if (_phase == Phase.Prep)
                foreach (var pair in _markers)
                {
                    if (!pair.Value.activeSelf) continue;
                    var a = content.activities.Find(x => x.id == pair.Key.activityId);
                    SpotLabel(tags, pair.Value.transform, $"{a.displayName}  ·  {a.cost}", CanAfford(a));
                }
            if (_phase == Phase.Party && _chatsLeft <= 0) OutOfChatsBanner(tags, canvasWidth);
            if (_phase == Phase.Prep && _popup == Popup.None)
            {
                PrepState(out var title, out var body, out var color);
                RoomBanner(tags, canvasWidth, title, body, color);
            }
            PromptBox(tags);

            // The UI column.
            var panel = LeftPanel(page);
            switch (_popup)
            {
                case Popup.Occasion: OccasionPanel(panel); break;
                case Popup.Phone: PhonePanel(panel); break;
                case Popup.Talk: TalkPanel(panel); break;
                default: StatusPanel(panel); break;
            }
            UIKit.Spacer(panel);
            PanelFooter(panel);

            if (_paused) PauseMenu(page); // last, so it sits on top of everything
        }

        RectTransform LeftPanel(Transform page)
        {
            var bg = UIKit.Panel(page, UIKit.Background, "LeftPanel");
            bg.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(PanelWidth, 0);
            rt.anchoredPosition = Vector2.zero;
            var layout = UIKit.VLayout(bg, 12, 32);
            layout.childForceExpandHeight = false;

            // Thin divider between the UI and the room.
            var line = UIKit.Panel(bg.transform, UIKit.AccentDim, "Divider");
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            line.rectTransform.anchorMin = new Vector2(1, 0);
            line.rectTransform.anchorMax = new Vector2(1, 1);
            line.rectTransform.pivot = new Vector2(1, 0.5f);
            line.rectTransform.sizeDelta = new Vector2(4, 0);
            return rt;
        }

        void PanelTitle(Transform panel)
        {
            UIKit.Label(panel, $"Party {_partyIndex + 1} of {content.occasions.Count}", 22, UIKit.Muted);
            UIKit.Label(panel, _plan.occasion.title, 44, null, TextAnchor.UpperLeft, FontStyle.Bold);
        }

        /// <summary>Default panel: what to do now, numbers for this phase, the vibe and the guests.</summary>
        void StatusPanel(Transform panel)
        {
            var occ = _plan.occasion;
            PanelTitle(panel);

            // What to do now
            var goal = UIKit.Panel(panel, new Color(1f, 0.93f, 0.82f), "Goal");
            UIKit.VLayout(goal, 4, 16);
            UIKit.Label(goal.transform, "WHAT TO DO", 16, UIKit.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            int cost = PartyScorer.ActivityCost(_plan, S);
            switch (_phase)
            {
                case Phase.Invite:
                    UIKit.Label(goal.transform, $"Walk to the <b>phone</b> by the right wall and press E to invite {occ.guestCount} friends.", 22);
                    UIKit.Label(goal.transform, $"Invited {_plan.guests.Count} / {occ.guestCount}", 20, UIKit.Muted);
                    break;
                case Phase.Prep:
                    UIKit.Label(goal.transform, "Walk to a <b>glowing spot</b> and press E to set up that activity. When you're ready, open the <b>front door</b>.", 22);
                    string helper = PartyScorer.HelperSaving(_plan, S) > 0 ? $"  (Helper saved {PartyScorer.HelperSaving(_plan, S)})" : "";
                    UIKit.Label(goal.transform, $"Budget used <b>{cost} / {Budget}</b>{helper}", 22, cost > Budget ? UIKit.Bad : UIKit.Ink);
                    PrepState(out var stateTitle, out _, out var stateColor);
                    UIKit.Label(goal.transform, $"<b>{stateTitle}</b>", 22, stateColor);
                    break;
                case Phase.Arriving:
                    UIKit.Label(goal.transform, "Your guests are arriving...", 22);
                    break;
                case Phase.Party:
                    float avg = _plan.guests.Average(g => MoodAt(g, _round));
                    UIKit.Label(goal.transform, "Walk up to a guest and press E to talk. Change the record on the <b>record player</b> (next to the door) for the next round.", 22);
                    UIKit.Label(goal.transform, $"Round <b>{_round + 1} / {occ.rounds}</b>   ·   Chats left <b>{_chatsLeft}</b>   ·   Offer {(_offerUsed ? "used" : "<b>1</b> left")}   ·   Mood {UIKit.MoodFace(avg)}", 22);
                    if (_chatsLeft <= 0)
                        UIKit.Label(goal.transform, _round >= occ.rounds - 1
                            ? "<b>No chats left.</b> Use the record player to wrap up the party."
                            : $"<b>No chats left this round.</b> Use the record player to start round {_round + 2}.", 22, UIKit.Accent);
                    break;
                case Phase.Leaving:
                    UIKit.Label(goal.transform, "Saying goodbye...", 22);
                    break;
            }

            // Party vibe
            if (_phase != Phase.Invite)
            {
                // Compact: only the scales the party leans on, in one line (the full list is in Rules).
                var vibe = PartyScorer.ComputeVibe(_plan, S);
                string food = _plan.activities.Any(x => x.isFood) ? "" : "   <color=#cc4d4d>No food yet!</color>";
                UIKit.Label(panel, $"<b>Party vibe</b>   {DescribeVibe(vibe)}{food}", 20);
            }

            // What every activity costs and does (Rules: "Each activity pushes one or more scales").
            if (_phase == Phase.Prep)
            {
                UIKit.Spacer(panel, 4);
                UIKit.Label(panel, "Activities", 24, null, TextAnchor.UpperLeft, FontStyle.Bold);
                foreach (var a in content.activities)
                {
                    bool placed = _plan.activities.Contains(a);
                    string mark = placed ? "<color=#4d9a59><b>Set up</b></color>  " : "";
                    UIKit.Label(panel, $"{mark}<b>{a.displayName}</b>  <color=#8a7f76>cost {a.cost}</color>  <color=#4d9a59>{DescribeShift(a)}</color>", 17);
                }
            }

            // Guests
            if (_plan.guests.Count > 0)
            {
                UIKit.Spacer(panel, 4);
                UIKit.Label(panel, "Guests", 24, null, TextAnchor.UpperLeft, FontStyle.Bold);
                foreach (var g in _plan.guests)
                {
                    var line = UIKit.Panel(panel, Color.white, "Guest");
                    UIKit.HLayout(line, 12, 8, TextAnchor.MiddleLeft);
                    var face = UIKit.Panel(line.transform, g.color, "Face");
                    UIKit.Size(face, 36, 36);
                    if (_phase == Phase.Party)
                    {
                        UIKit.Stretch(UIKit.Label(face.transform, UIKit.MoodFace(MoodAt(g, _round)), 18, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform);
                        // Name, then in plain words the biggest thing they reacted to this round.
                        var info = UIKit.Column(line.transform, 0);
                        UIKit.Size(info, 480, -1, 1);
                        UIKit.Label(info, $"<b>{g.displayName}</b>", 22);
                        UIKit.Label(info, ReactionSummary(g), 17);
                    }
                    else
                    {
                        // One line, so five guests plus the activity list still fit the panel.
                        var info = UIKit.Label(line.transform, $"<b>{g.displayName}</b>   <color=#8a7f76><size=16>{KnownSummary(g)}</size></color>", 20);
                        UIKit.Size(info, 480, -1, 1);
                    }
                }
            }
        }

        void PanelFooter(Transform panel)
        {
            UIKit.Label(panel, "<color=#8a7f76>WASD / arrows: walk   ·   E: use / close   ·   Esc: pause</color>", 18);
            var row = UIKit.Row(panel, 12, TextAnchor.MiddleLeft);
            UIKit.Button(row, "Friends", () => ShowFriends(null, ShowRoom), 180, 54, true, UIKit.Muted, 22);
            UIKit.Button(row, "Rules", () => ShowRules(0, ShowRoom), 170, 54, true, UIKit.Muted, 22);
            UIKit.Button(row, "Pause", () => SetPaused(true), 170, 54, true, UIKit.Muted, 22);
        }

        /// <summary>
        /// "Liked the playful mood" / "Didn't like: Too crowded" for this round, coloured green or red,
        /// so the guest list reads in words instead of + and - signs.
        /// </summary>
        string ReactionSummary(GuestData g)
        {
            var now = _schedule[g][_round];
            if (now.Count == 0) return "<color=#8a7f76>Just chatting, nothing new</color>";
            var parts = now.OrderByDescending(r => Mathf.Abs(r.value)).Select(r =>
                r.IsGood ? $"<color=#4d9a59>{_picker.Caption(r)}</color>" : $"<color=#cc4d4d>{_picker.Caption(r)}</color>");
            return string.Join("<color=#8a7f76>  ·  </color>", parts);
        }

        /// <summary>
        /// Speech bubble above a guest's head with what they're saying this round (and their mood face),
        /// like a comic: players read how the party is going straight from the guests.
        /// </summary>
        void SpeechBubble(Transform layer, GuestData g, float mood, float raise)
        {
            var head = _room.HeadOf(g);
            if (head == null) return;
            bool talking = g == _talking;

            var bubble = UIKit.Panel(layer, talking ? UIKit.CardSelected : Color.white, "Bubble_" + g.id);
            var outline = bubble.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.17f, 0.15f, 0.6f);
            outline.effectDistance = new Vector2(2, -2);
            var rt = bubble.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero; // positioned by WorldFollower
            rt.pivot = new Vector2(0.5f, 0f);
            UIKit.HLayout(bubble, 8, 10, TextAnchor.MiddleLeft);
            var fit = bubble.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var face = UIKit.Panel(bubble.transform, g.color, "Face");
            UIKit.Size(face, 30, 30);
            UIKit.Stretch(UIKit.Label(face.transform, UIKit.MoodFace(mood), 15, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform);

            // First line only (chat results add a second "Learned: ..." line, shown in the left panel).
            string line = _bubbles.TryGetValue(g, out var said) ? said.Split('\n')[0] : "...";
            var text = UIKit.Label(bubble.transform, line, 17);
            UIKit.Size(text, Mathf.Min(210f, 24f + line.Length * 8.5f));

            // Little tail pointing down at the speaker.
            var tail = UIKit.Panel(bubble.transform, talking ? UIKit.CardSelected : Color.white, "Tail");
            tail.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            tail.rectTransform.sizeDelta = new Vector2(14, 14);
            tail.rectTransform.anchoredPosition = new Vector2(0, 0);
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            tail.transform.SetAsFirstSibling(); // behind the bubble, so only the point sticks out

            var follow = bubble.gameObject.AddComponent<WorldFollower>();
            follow.target = head;
            follow.cam = _room.Cam;
            follow.offset = new Vector2(0, 12 + raise);
        }

        void SpotLabel(Transform layer, Transform spot, string text, bool affordable)
        {
            var bg = UIKit.Panel(layer, new Color(1f, 0.96f, 0.86f, affordable ? 0.92f : 0.6f), "SpotLabel");
            var rt = bg.rectTransform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            UIKit.HLayout(bg, 0, 6, TextAnchor.MiddleCenter);
            var fit = bg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIKit.Label(rt, text, 18, affordable ? UIKit.Ink : UIKit.Muted, TextAnchor.MiddleCenter);
            var follow = bg.gameObject.AddComponent<WorldFollower>();
            follow.cam = _room.Cam;
            follow.target = spot;
            follow.offset = new Vector2(0, 14);
        }

        void PromptBox(Transform layer)
        {
            _promptBg = UIKit.Panel(layer, Color.white, "Prompt");
            _promptBox = _promptBg.rectTransform;
            _promptBox.anchorMin = _promptBox.anchorMax = Vector2.zero;
            _promptBox.pivot = new Vector2(0.5f, 0f);
            UIKit.HLayout(_promptBg, 0, 12, TextAnchor.MiddleCenter);
            var fit = _promptBg.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _promptText = UIKit.Label(_promptBox, "", 24, null, TextAnchor.MiddleCenter);
            _promptFollow = _promptBg.gameObject.AddComponent<WorldFollower>();
            _promptFollow.cam = _room.Cam;
            _promptFollow.offset = new Vector2(0, 10);
            _promptShown = null;
            _promptBox.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- panel modes

        void OccasionPanel(Transform panel)
        {
            var occ = _plan.occasion;
            PanelTitle(panel);
            UIKit.Label(panel, occ.brief, 26);
            UIKit.Spacer(panel, 6);
            UIKit.Label(panel, $"<b>Starting vibe</b>\n{DescribeVibe(occ.baseVibe)}", 22, UIKit.Muted);
            UIKit.Label(panel, $"<b>Guests</b> {occ.guestCount}   ·   <b>Budget</b> {Budget}   ·   <b>Chats</b> {ChatsPerRound} each round", 22, UIKit.Muted);
            UIKit.Spacer(panel, 6);
            UIKit.Label(panel, "First, invite your friends: walk to the phone by the right wall and press E.", 22);
            UIKit.Button(panel, "Let's get ready  (E)", ClosePopup, 400, 64);
        }

        void PhonePanel(Transform panel)
        {
            var occ = _plan.occasion;
            UIKit.Label(panel, "Phone", 22, UIKit.Muted);
            UIKit.Label(panel, "Contacts", 44, null, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Label(panel, $"Invite {occ.guestCount} for {occ.title}.   Invited <b>{_plan.guests.Count} / {occ.guestCount}</b>", 22, UIKit.Muted);
            var legend = UIKit.Row(panel, 8, TextAnchor.MiddleLeft);
            UIKit.Icon(legend, UIKit.HeartSprite, UIKit.HeartColor, 18);
            UIKit.Size(UIKit.Label(legend, "<b>Willingness</b>: how keen they are to come. A good party adds a heart, a bad one costs one; at 0 they skip a party.", 17, UIKit.Muted), 540);

            foreach (var g in AvailableGuests())
            {
                bool selected = _plan.guests.Contains(g);
                bool isNew = _partyIndex > 0 && g.availableFrom == _partyIndex;
                bool willCome = WillCome(g);
                var contact = UIKit.Panel(panel, selected ? UIKit.CardSelected : willCome ? Color.white : new Color(0.93f, 0.91f, 0.89f), "Contact");
                UIKit.HLayout(contact, 12, 8, TextAnchor.MiddleLeft);
                var avatar = UIKit.Panel(contact.transform, g.color, "Avatar");
                UIKit.Size(avatar, 40, 40);
                var info = UIKit.Column(contact.transform, 0);
                UIKit.Size(info, 340, -1, 1);
                string badge = isNew ? "   <color=#db6b4d><size=15>NEW</size></color>" : "";
                var nameRow = UIKit.Row(info, 10, TextAnchor.MiddleLeft);
                UIKit.Label(nameRow, $"<b>{g.displayName}</b>{badge}", 22);
                WillingnessPips(nameRow, g, 13);
                UIKit.Label(info, willCome ? KnownSummary(g) : $"<i>“{DeclineLine(g)}”</i>", 16, UIKit.Muted);
                var guest = g;
                UIKit.Button(contact.transform, selected ? "Cancel" : willCome ? "Invite" : "Busy", () => ToggleGuest(guest), 120, 44,
                    selected || (willCome && _plan.guests.Count < occ.guestCount), selected ? UIKit.Muted : UIKit.Accent, 20);
            }

            bool full = _plan.guests.Count == occ.guestCount;
            UIKit.Button(panel, full ? "Done  (E)" : "Hang up  (E)", ClosePopup, 260, 56, true, full ? UIKit.Accent : UIKit.Muted, 22);
        }

        void TalkPanel(Transform panel)
        {
            var g = _talking;
            if (g == null || _outcome == null) { StatusPanel(panel); return; }
            float mood = MoodAt(g, _round);
            UIKit.Label(panel, $"Round {_round + 1} of {_plan.occasion.rounds}   ·   Chats left {_chatsLeft}", 22, UIKit.Muted);

            var head = UIKit.Row(panel, 14, TextAnchor.MiddleLeft);
            var avatar = UIKit.Panel(head, g.color, "Avatar");
            UIKit.Size(avatar, 60, 60);
            UIKit.Stretch(UIKit.Label(avatar.transform, UIKit.MoodFace(mood), 26, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold).rectTransform);
            UIKit.Label(head, g.displayName, 40, null, TextAnchor.MiddleLeft, FontStyle.Bold);
            MoodBar(panel, mood);

            if (_offering)
            {
                OfferList(panel, g);
                return;
            }

            var bubble = UIKit.Panel(panel, Color.white, "Bubble");
            UIKit.VLayout(bubble, 0, 18);
            UIKit.Label(bubble.transform, "“" + _bubbles[g] + "”", 26);
            string notes = GuessNotes(g);
            if (notes.Length > 0) UIKit.Label(panel, $"<i>Your guestbook notes: {notes}</i>", 18, UIKit.Muted);
            else if (_knowledge.UnknownAxes(g).Any())
                UIKit.Label(panel, "<i>Think you know what they like? Write a guess in your guestbook.</i>", 18, UIKit.Muted);

            // Why their mood changed this round, in words (green = made them happier, red = less happy).
            var now = _schedule[g][_round];
            if (now.Count == 0)
                UIKit.Label(panel, "Nothing new this round, just chatting.", 22, UIKit.Muted);
            else
                UIKit.Label(panel, "How they feel this round:", 20, UIKit.Muted);
            foreach (var r in now)
                UIKit.Label(panel, (r.IsGood ? "Happier: " : "Less happy: ") + _picker.Caption(r), 24,
                    r.IsGood ? UIKit.Good : UIKit.Bad, TextAnchor.UpperLeft, FontStyle.Bold);

            UIKit.Spacer(panel, 6);
            bool knowsAll = !_knowledge.UnknownAxes(g).Any();
            string ask = _chattedThisRound.Contains(g) ? "You already chatted this round"
                : _chatsLeft <= 0 ? "No chats left this round"
                : knowsAll ? "[1]  Catch up (uses 1 chat)"
                : $"[1]  Ask what they like  ({_chatsLeft} left)";
            var guest = g;
            UIKit.Button(panel, ask, () => Chat(guest), 576, 58, CanChat(g), null, 22);
            if (knowsAll) UIKit.Label(panel, "You already know all their preferences.", 18, UIKit.Muted);
            if (_chatsLeft <= 0)
                UIKit.Label(panel, _round >= _plan.occasion.rounds - 1
                    ? "That was your last chat. Go to the record player to wrap up the party."
                    : $"That was your last chat this round. Go to the record player to start round {_round + 2}.",
                    20, UIKit.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Button(panel, _offerUsed ? "Already offered something this party" : "[2]  Offer something  (once per party)",
                () => { _offering = true; ShowRoom(); }, 576, 52, CanOffer, new Color(0.45f, 0.6f, 0.75f), 22);
            if (_knowledge.UnknownAxes(g).Any())
                UIKit.Button(panel, "Guestbook: write a guess", () => { ClosePopup(); ShowFriends(guest, ShowRoom); }, 576, 46, true,
                    new Color(0.72f, 0.68f, 0.64f), 20);
            UIKit.Button(panel, "[E]  Bye", ClosePopup, 576, 52, true, UIKit.Muted, 22);
        }

        /// <summary>The talk panel while choosing what to offer: one button per item, with the way it pushes.</summary>
        void OfferList(Transform panel, GuestData g)
        {
            UIKit.Label(panel, $"Offer {g.displayName} something", 28, null, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Label(panel, "Only once per party, and only for this guest. If it suits them they get happier, if not, less happy. Either way you learn where they stand.",
                18, UIKit.Muted);
            var items = content.interventions;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string key = i < 9 ? $"[{i + 1}]  " : "";
                UIKit.Button(panel, $"{key}<b>{item.displayName}</b>   <size=17>({ItemPush(item)})</size>",
                    () => Offer(g, item), 576, 46, true, new Color(0.45f, 0.6f, 0.75f), 20);
            }
            UIKit.Button(panel, "[E]  Back", () => { _offering = false; ShowRoom(); }, 576, 48, true, UIKit.Muted, 22);
        }

        /// <summary>"Mood tonight" bar: how much fun this guest is having right now. Resets every party.</summary>
        static void MoodBar(Transform parent, float mood)
        {
            var row = UIKit.Row(parent, 12, TextAnchor.MiddleLeft);
            UIKit.Label(row, "<b>Mood tonight</b>", 20, UIKit.Muted);
            var bar = UIKit.Panel(row, UIKit.AccentDim, "MoodBar");
            UIKit.Size(bar, -1, 16, 1);
            string word = mood >= 4.2f ? "loving it" : mood >= 3.4f ? "having fun" : mood >= 2.6f ? "it's okay" : mood >= 1.8f ? "a bit bored" : "not enjoying it";
            UIKit.Label(row, word, 20, UIKit.MoodColor(mood), TextAnchor.MiddleLeft, FontStyle.Bold);
            var fill = UIKit.Panel(bar.transform, UIKit.MoodColor(mood), "Fill");
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(Mathf.InverseLerp(1f, 5f, mood), 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
