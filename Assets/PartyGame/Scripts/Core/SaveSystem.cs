using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PartyGame
{
    /// <summary>One save slot: which party is next and everything the player has learned so far.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public string savedAt;
        public int nextParty;          // index into GameContent.occasions
        public bool finished;          // all parties done
        public bool seenHowTo;
        public List<int> partyStars = new List<int>();
        public List<GuestSave> guests = new List<GuestSave>();

        public int FactsLearned()
        {
            int n = 0;
            foreach (var g in guests) n += g.axes.Count + g.traits.Count + (g.dislike ? 1 : 0) + (g.diet ? 1 : 0);
            return n;
        }
    }

    [Serializable]
    public class GuestSave
    {
        public string id;
        public List<int> axes = new List<int>();      // PartyAxis values the player knows
        public List<int> traits = new List<int>();    // GuestTrait values the player knows
        public bool dislike;
        public bool diet;
        public List<PartyRecord> parties = new List<PartyRecord>();
    }

    [Serializable]
    public class PartyRecord
    {
        public string party;
        public int stars;
    }

    /// <summary>
    /// Save slots as JSON files in Application.persistentDataPath, so each player (or playtester)
    /// can have their own run and start from scratch.
    /// </summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;

        public static string PathFor(int slot) =>
            Path.Combine(Application.persistentDataPath, $"party_save_{slot + 1}.json");

        public static bool Exists(int slot) => File.Exists(PathFor(slot));

        public static SaveData Load(int slot)
        {
            try
            {
                if (!Exists(slot)) return null;
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(PathFor(slot)));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartyGame] Could not read save slot {slot + 1}: {e.Message}");
                return null;
            }
        }

        public static void Save(int slot, SaveData data)
        {
            try
            {
                data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartyGame] Could not write save slot {slot + 1}: {e.Message}");
            }
        }

        public static void Delete(int slot)
        {
            try
            {
                if (Exists(slot)) File.Delete(PathFor(slot));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PartyGame] Could not delete save slot {slot + 1}: {e.Message}");
            }
        }
    }
}
