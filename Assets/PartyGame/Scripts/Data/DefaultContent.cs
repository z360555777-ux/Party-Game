using System.Collections.Generic;
using UnityEngine;

namespace PartyGame
{
    /// <summary>
    /// Built-in grey-box content: 3 occasions, 6 guests, 6 activities and the dialogue pool.
    /// Used when no content asset exists, and by the editor menu to create editable assets.
    /// All names, numbers and lines are placeholders for the team to replace.
    /// </summary>
    public static class DefaultContent
    {
        public static GameContent Build()
        {
            var c = ScriptableObject.CreateInstance<GameContent>();
            c.name = "PartyGameContent (defaults)";
            c.occasions = BuildOccasions();
            c.guests = BuildGuests();
            c.activities = BuildActivities();
            c.dialogue = BuildDialogue();
            c.interventions = BuildInterventions();
            return c;
        }

        // ---------------------------------------------------------------- interventions
        // The doc lists one so far ("A book - boosts introvert/small party"); the rest are placeholders,
        // one for each side of the five scales.

        static List<InterventionItem> BuildInterventions()
        {
            return new List<InterventionItem>
            {
                Item("book", "A good book", "Somewhere quiet to read.", new AxisValues(crowd: -1f, social: -1f, tone: 0, structure: 0, formality: 0)),
                Item("introduce", "Introduce them around", "Bring them over to meet everyone.", new AxisValues(tone: 0, structure: 0, crowd: 1f, formality: 0, social: 1f)),
                Item("game", "A silly party game", "Get them laughing.", new AxisValues(tone: 1f, structure: 0, crowd: 0, formality: 0, social: 0)),
                Item("talk", "A heart-to-heart", "Sit down for a real conversation.", new AxisValues(tone: -1f, structure: 0, crowd: 0, formality: 0, social: 0)),
                Item("task", "A little project", "Ask them to help with something hands-on.", new AxisValues(tone: 0, structure: 1f, crowd: 0, formality: 0, social: 0)),
                Item("sofa", "A comfy spot to hang out", "No plans, just relax.", new AxisValues(tone: 0, structure: -1f, crowd: 0, formality: 0, social: 0)),
                Item("fancy", "A fancy drink", "Something classy, in the good glasses.", new AxisValues(tone: 0, structure: 0, crowd: 0, formality: -1f, social: 0)),
                Item("slippers", "Cozy slippers", "Shoes off, feet up.", new AxisValues(tone: 0, structure: 0, crowd: 0, formality: 1f, social: 0)),
            };
        }

        static InterventionItem Item(string id, string name, string desc, AxisValues push) =>
            new InterventionItem { id = id, displayName = name, description = desc, push = push };

        // ---------------------------------------------------------------- occasions

        static List<OccasionData> BuildOccasions()
        {
            return new List<OccasionData>
            {
                // Design doc game loop: Level 1 Tea Party, Level 2 Watch Party, Level 3 "could be: Dinner Party,
                // Birthday, Personal Celebration, Holiday specific" (Dinner Party until the team picks).
                // Descriptions, vibes, guest counts and budgets are placeholders.
                Occasion("teaparty", "Tea Party",
                    "A cozy afternoon tea. A little bit proper, a lot of chatting. Invite 3 friends.",
                    new AxisValues(tone: 0, structure: 0, crowd: 0, formality: -1f, social: 1f),
                    guests: 3, budget: 4),
                Occasion("watchparty", "Watch Party",
                    "Movie night on the sofa: snacks, laughs and a screen everyone can see. Invite 4 friends.",
                    new AxisValues(tone: 1f, structure: 0.5f, crowd: 0, formality: 1f, social: 0),
                    guests: 4, budget: 5),
                Occasion("dinnerparty", "Dinner Party",
                    "A proper sit-down dinner. Good food and good conversation. Invite 5 friends.",
                    new AxisValues(tone: -0.5f, structure: 0.5f, crowd: 0, formality: -1f, social: 0.5f),
                    guests: 5, budget: 6),
            };
        }

        static OccasionData Occasion(string id, string title, string brief, AxisValues vibe, int guests, int budget)
        {
            var o = ScriptableObject.CreateInstance<OccasionData>();
            o.name = o.id = id;
            o.title = title;
            o.brief = brief;
            o.baseVibe = vibe;
            o.guestCount = guests;
            o.budget = budget;
            o.chatsPerRound = 2;
            o.rounds = 3;
            return o;
        }

        // ---------------------------------------------------------------- guests
        // AxisValues order (same as the Game Data spreadsheet columns):
        // tone (Serious- / Playful+), structure (Flexible- / Activity+), crowd (Small- / Big+),
        // formality (Formal- / Informal+), social (Wallflower- / Butterfly+)

        // Copied from the team's "Game Data.xlsx", Guest sheet (rows 3-11), in the same order.
        // Names are empty in the spreadsheet, so these are placeholders until the team fills them in.
        // Row 3 has no "Available" level; it is assumed to be Level 0.
        // Columns: Playful, Activity-focused, Big gathering, Informal, Social Butterfly, Available level, Tag
        static readonly (string name, float playful, float activity, float big, float informal, float butterfly, int level, GuestTrait? tag)[] SheetGuests =
        {
            ("Guest 1",  2, -1,  1,  0,  1, 0, GuestTrait.Inquisitive),
            ("Guest 2",  1,  2,  0, -1,  1, 0, GuestTrait.Storyteller),
            ("Guest 3",  0,  1, -1,  1,  2, 0, null),
            ("Guest 4",  1,  2,  1,  0, -1, 0, null),
            ("Guest 5",  0,  0,  0,  0,  0, 0, null),
            ("Guest 6",  0,  0,  0,  0,  2, 1, GuestTrait.Gossip),
            ("Guest 7",  0,  0,  0,  0,  0, 1, null),
            ("Guest 8",  0,  2,  2,  0,  0, 2, GuestTrait.Helper),
            ("Guest 9", -1,  0,  0,  2,  2, 2, null),
        };

        static readonly Color[] Palette =
        {
            new Color(1f, 0.55f, 0.55f), new Color(1f, 0.8f, 0.4f), new Color(0.55f, 0.75f, 1f),
            new Color(0.7f, 0.55f, 0.9f), new Color(0.5f, 0.85f, 0.6f), new Color(1f, 0.6f, 0.85f),
            new Color(0.6f, 0.85f, 0.9f), new Color(0.95f, 0.7f, 0.5f), new Color(0.75f, 0.8f, 0.5f),
        };

        static List<GuestData> BuildGuests()
        {
            var list = new List<GuestData>();
            for (int i = 0; i < SheetGuests.Length; i++)
            {
                var r = SheetGuests[i];
                var g = r.tag.HasValue
                    ? Guest($"guest{i + 1}", r.name, "A friend from around town.", Palette[i % Palette.Length],
                        new AxisValues(r.playful, r.activity, r.big, r.informal, r.butterfly), r.tag.Value)
                    : Guest($"guest{i + 1}", r.name, "A friend from around town.", Palette[i % Palette.Length],
                        new AxisValues(r.playful, r.activity, r.big, r.informal, r.butterfly));
                g.availableFrom = r.level;
                list.Add(g);
            }
            return list;
        }

        static GuestData Guest(string id, string name, string blurb, Color color, AxisValues stats, params GuestTrait[] traits)
        {
            var g = ScriptableObject.CreateInstance<GuestData>();
            g.name = g.id = id;
            g.displayName = name;
            g.blurb = blurb;
            g.color = color;
            g.stats = stats;
            g.traits = new List<GuestTrait>(traits);
            return g;
        }

        // ---------------------------------------------------------------- activities
        // Costs are the doc's resource + prep, merged.

        static List<ActivityData> BuildActivities()
        {
            return new List<ActivityData>
            {
                Activity("craft", "Craft Station", "Something to do with your hands.", 4,
                    new AxisValues(tone: 0.5f, structure: 1.5f, crowd: 0, formality: 0, social: -0.5f)),
                Activity("food", "Food", "Snacks and a main dish.", 2, new AxisValues(), everyone: 0.25f, food: true),
                Activity("dietfood", "Vegan / Gluten-free Food", "Food everyone can eat.", 3, new AxisValues(),
                    everyone: 0.25f, food: true, diet: true),
                Activity("cafe", "Home Café", "Drinks and comfy seats.", 3,
                    new AxisValues(tone: 0, structure: -0.5f, crowd: 0, formality: 1f, social: 0)),
                Activity("bookclub", "Book Club", "A book to talk about.", 2,
                    new AxisValues(tone: -1f, structure: 0.5f, crowd: 0, formality: -1.5f, social: 0)),
                // Was "Watch Party"; renamed now that Watch Party is the Level 2 occasion.
                Activity("watch", "Movie Corner", "Put a movie on.", 0,
                    new AxisValues(tone: 1f, structure: 0, crowd: 0, formality: 0.5f, social: 0.5f)),
            };
        }

        static ActivityData Activity(string id, string name, string desc, int cost, AxisValues shift,
            float everyone = 0, bool food = false, bool diet = false)
        {
            var a = ScriptableObject.CreateInstance<ActivityData>();
            a.name = a.id = id;
            a.displayName = name;
            a.description = desc;
            a.cost = cost;
            a.vibeShift = shift;
            a.everyoneBonus = everyone;
            a.isFood = food;
            a.dietaryFriendly = diet;
            return a;
        }

        // ---------------------------------------------------------------- dialogue

        static DialogueData BuildDialogue()
        {
            var d = ScriptableObject.CreateInstance<DialogueData>();
            d.name = "Dialogue";

            // Axis reactions: key = "<Axis>.<guest's pole>.<Good|Bad>"; sign picks the pole (-1 / +1)
            AxisLines(d, PartyAxis.Tone, 1, // Playful guest
                good: new[] { "I'm having a blast! I'd love to see more events like this!", "I haven't laughed this hard in ages." },
                bad: new[] { "Everyone's so... serious tonight.", "I kept trying to start a game. Nobody bit." });
            AxisLines(d, PartyAxis.Tone, -1, // Serious guest
                good: new[] { "I had some really meaningful conversations tonight.", "It's nice when people actually listen." },
                bad: new[] { "There were a lot of jokes I didn't get.", "It got a bit too silly for me." });

            AxisLines(d, PartyAxis.Structure, -1,
                good: new[] { "I love that we could just hang out.", "No schedule, no pressure. Perfect." },
                bad: new[] { "It felt like a to-do list, not a party.", "I just wanted to chill, not do a project." });
            AxisLines(d, PartyAxis.Structure, 1,
                good: new[] { "Having something to do made it so much easier!", "I made a thing! Look!" },
                bad: new[] { "I wasn't sure what we were supposed to be doing.", "I kind of just stood around." });

            AxisLines(d, PartyAxis.Crowd, -1,
                good: new[] { "Just the right number of people.", "It felt cozy, like a real catch-up." },
                bad: new[] { "I'd like to find somewhere quiet to sit like... my own home.", "So many people. I need a minute." });
            AxisLines(d, PartyAxis.Crowd, 1,
                good: new[] { "I made new friends! We'll probably meet up somewhere this weekend.", "The more the merrier!" },
                bad: new[] { "Where is everyone? It's kind of empty.", "I expected a bigger crowd." });

            AxisLines(d, PartyAxis.Formality, -1,
                good: new[] { "Such a classy evening.", "I'm glad I dressed up." },
                bad: new[] { "I felt overdressed.", "It was a bit... sloppy?" });
            AxisLines(d, PartyAxis.Formality, 1,
                good: new[] { "I could just kick off my shoes and relax.", "Super laid-back. Loved it." },
                bad: new[] { "I felt like I had to sit up straight all night.", "Bit stiff, wasn't it?" });

            AxisLines(d, PartyAxis.Social, 1, // Social butterfly guest
                good: new[] { "I talked to literally everyone!", "Such good energy tonight!" },
                bad: new[] { "Nobody really wanted to chat.", "It was hard to get a conversation going." });
            AxisLines(d, PartyAxis.Social, -1, // Wallflower guest
                good: new[] { "I could just sit back and enjoy it.", "Nobody put me on the spot. Thank you." },
                bad: new[] { "Everyone kept pulling me into things.", "It was a lot of... talking." });

            // Guest-to-guest chemistry
            d.Add("Pair.Story.Good", "{other} told the best stories. I had so many questions!",
                "I could listen to {other} all night.");
            d.Add("Pair.Ask.Good", "{other} actually wanted to hear my stories!",
                "{other} asked the best questions.");
            d.Add("Gossip.Fight.Bad", "Ugh, the gossip drama ruined it.", "I did NOT come here to pick sides.");
            d.Add("Dislike.Bad", "Did you have to invite {other}?", "{other} and I just don't click.");
            d.Add("Helper.Good", "It felt good to help out tonight.", "Glad I could lend a hand!");

            // Food
            d.Add("Food.None.Bad", "Was there... no food?", "I'm starving.");
            d.Add("Food.Some.Good", "The snacks were great.", "Good food, good people.");
            d.Add("Diet.Unmet.Bad", "There was nothing I could actually eat.", "I'll grab something on the way home.");
            d.Add("Diet.Met.Good", "You remembered I'm gluten-free! That means a lot.");

            // Short +/- captions shown under the bubble during the party: "Label.<reason key>"
            Caption(d, PartyAxis.Tone, 1, "Loves the playful mood", "Wanted more fun and games");
            Caption(d, PartyAxis.Tone, -1, "Enjoys the serious talk", "Too silly for them");
            Caption(d, PartyAxis.Structure, 1, "Glad there's stuff to do", "Wanted something to do");
            Caption(d, PartyAxis.Structure, -1, "Likes just hanging out", "Feels too scheduled");
            Caption(d, PartyAxis.Crowd, 1, "Loves the big crowd", "Too few people");
            Caption(d, PartyAxis.Crowd, -1, "Likes the small group", "Too crowded");
            Caption(d, PartyAxis.Formality, 1, "Loves how relaxed it is", "Too stiff and proper");
            Caption(d, PartyAxis.Formality, -1, "Enjoys the classy feel", "Too sloppy for them");
            Caption(d, PartyAxis.Social, 1, "Lots of people to talk to", "Not enough mingling");
            Caption(d, PartyAxis.Social, -1, "Can relax in a quiet corner", "Too much socializing");
            d.Add("Label.Pair.Story.Good", "Loves {other}'s stories");
            d.Add("Label.Pair.Ask.Good", "{other} asks great questions");
            d.Add("Label.Vibes.Good", "Good energy in the room");
            d.Add("Label.Gossip.Fight.Bad", "Caught in the gossip fight");
            d.Add("Label.Dislike.Bad", "Doesn't get along with {other}");
            d.Add("Label.Food.None.Bad", "No food");
            d.Add("Label.Food.Some.Good", "Enjoying the food");
            d.Add("Label.Diet.Met.Good", "Food they can eat");
            d.Add("Label.Diet.Unmet.Bad", "Nothing they can eat");
            d.Add("Label.Helper.Good", "Happy to help out");
            d.Add("Vibes.Good", "Such good energy in here!", "Everyone's having a great time.");

            // Said in the first round by guests without their own small talk
            d.Add("SmallTalk", "Oh, I love what you've done with the place!", "Who else is coming?",
                "Have you tried the snacks?", "It's been ages since we all hung out.", "Nice playlist.");

            // Fallbacks
            d.Add("Generic.Good", "It was a nice night.", "Thanks for having me!");
            d.Add("Generic.Bad", "It was... fine.", "I don't know, something felt off.");

            // What guests say when the player chats with them: "Reveal.<Axis>.<pole>"
            Reveal(d, PartyAxis.Tone, 1, "Honestly? I just want to have fun. Life's too short.");
            Reveal(d, PartyAxis.Tone, -1, "I prefer a proper conversation over party games.");
            Reveal(d, PartyAxis.Structure, -1, "I don't need a plan. Just let things happen.");
            Reveal(d, PartyAxis.Structure, 1, "I'm happiest when there's something to do.");
            Reveal(d, PartyAxis.Crowd, -1, "Big crowds wear me out. A few close friends is best.");
            Reveal(d, PartyAxis.Crowd, 1, "The bigger the party, the better!");
            Reveal(d, PartyAxis.Formality, -1, "I like an occasion to dress up for.");
            Reveal(d, PartyAxis.Formality, 1, "Sweatpants party? I'm in.");
            Reveal(d, PartyAxis.Social, 1, "I love meeting new people!");
            Reveal(d, PartyAxis.Social, -1, "I'm more of a corner-of-the-room person.");
            d.Add("Reveal.Nothing", "I'm pretty easy-going about that, honestly.");
            d.Add("Reveal.Done", "Ha, you already know me too well!");

            // Interventions (design doc: once per party, one guest, they say how well it worked)
            d.Add("Offer.Good", "Oh, this is exactly what I needed!", "You know me so well. Thank you!");
            d.Add("Offer.Bad", "Um... thanks, I guess?", "That's... not really my thing.");
            d.Add("Offer.Meh", "Oh, that's nice. Thanks!", "Sure, why not.");
            d.Add("Label.Offer.Good", "Loved what you offered");
            d.Add("Label.Offer.Bad", "Didn't like what you offered");

            // Willingness: said on the phone when they don't want to come
            d.Add("Invite.Decline", "Sorry, I think I'll sit this one out.", "Ah, I'm busy that day. Maybe next time?");

            return d;
        }

        static void AxisLines(DialogueData d, PartyAxis axis, int sign, string[] good, string[] bad)
        {
            string pole = Axes.PoleKey(axis, sign);
            d.Add($"{axis}.{pole}.Good", good);
            d.Add($"{axis}.{pole}.Bad", bad);
        }

        static void Caption(DialogueData d, PartyAxis axis, int sign, string good, string bad)
        {
            string pole = Axes.PoleKey(axis, sign);
            d.Add($"Label.{axis}.{pole}.Good", good);
            d.Add($"Label.{axis}.{pole}.Bad", bad);
        }

        static void Reveal(DialogueData d, PartyAxis axis, int sign, string line) =>
            d.Add($"Reveal.{axis}.{Axes.PoleKey(axis, sign)}", line);
    }
}
