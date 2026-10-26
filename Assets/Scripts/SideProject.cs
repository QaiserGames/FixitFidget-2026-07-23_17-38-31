using UnityEngine;

// ---------------------------------------------------------------------------
// YOUR SIDE PROJECT: A THRIFT FIND WITH NOBODY WAITING (24 Oct 2026; Designer P's spec §2.2 and §6.3, approved by him with his
// answers of 24 Oct: p1 "a cleaning job, so Day 1 brings no new tool", p2 "one at a time: bought at closing, fixed the next day,
// sold at closing", p6 "the close-up closes by itself" when a customer reaches the counter)
//
// His words (22 Oct): "what i mean about dead air is really the things you could be doing when waiting for a customer to
// approach the counter." P's simulation: the side project alone takes Day 1's nothing-to-do from 151 s to 73 s, because a walk
// from the street (25-61 s) is about as long as a bench job. It is the bench he loves, with no patience bar.
//
// This marks a device as a find: no customer owns it (JobBase.Owner stays null), it has the bench's own corner (ItemSlotArea's
// side slot, outside the customers' count), it can be worked on but never carried off, and it sells by itself at closing
// (ThriftShelf). What it says is here: the thing ("watch"), the card's line ("Your thrift watch: grime on the glass.", at most
// 8 words and 46 characters: the bench card's budget), the Shop's words ("a phone with a cracked screen"), and its finished
// line ("Fixed. It sells tonight."). PLACEHOLDER COPY for his sister.
// ---------------------------------------------------------------------------
[DisallowMultipleComponent]
public sealed class SideProject : MonoBehaviour
{
    /// <summary>Day 1's find's fault (Fixit Fidget › Days › Thrift 1 gives the thrift watch this one fault): brush and cloth only.</summary>
    public const string ThriftWatchFault = "Grimy Glass and Case";

    /// <summary>The bench card's finished line for a find (it isn't handed to anyone: it sells at closing).</summary>
    public const string FinishedWords = "Fixed. It sells tonight.";

    /// <summary>The bench card's line for a find: at most this many words and characters (UI/UX's checklist v2 §5.2).</summary>
    public const int CardWords = 8, CardCharacters = 46;

    [Tooltip("Which find this is (the device prefab's name and its fault), for the save.")]
    public string findId = "";
    [Tooltip("What it sells for at closing when Perfect, dollars (Good x0.8, Passable x0.48).")]
    public int salePrice;
    [Tooltip("What Ace calls it: \"watch\", \"phone\".")]
    public string thing = "find";

    /// <summary>Whether <paramref name="job"/> is a find (nobody's job: the side project).</summary>
    public static bool IsSideProject(JobBase job) => job != null && job.GetComponent<SideProject>() != null;

    /// <summary>The find's marker on <paramref name="job"/>, or null.</summary>
    public static SideProject Of(JobBase job) => job != null ? job.GetComponent<SideProject>() : null;

    /// <summary>"Your thrift watch": what the card and the Now line call it.</summary>
    public string YourThing => "Your thrift " + thing;

    /// <summary>"Thrift watch": the pin over it.</summary>
    public string PinWords => "Thrift " + thing;

    /// <summary>The bench card's line: "Your thrift watch: grime on the glass." (plain text).</summary>
    public static string CardLine(JobBase job)
    {
        SideProject find = Of(job);
        string label = job != null && job.Record != null ? job.Record.faultDescription : "";
        string words = WordsFor(label).card;
        return (find != null ? find.YourThing : "Your thrift find") + ": " + words + ".";
    }

    /// <summary>
    /// What a find says, by its fault's label: the card's few words ("grime on the glass"), and the Shop's ("a grimy pocket
    /// watch"). A label not here: its customer's spoken form (FaultSpeech), and "a {thing} to fix". PLACEHOLDER COPY.
    /// </summary>
    public static (string card, string shop) WordsFor(string label)
    {
        switch ((label ?? "").Trim())
        {
            case ThriftWatchFault: return ("grime on the glass", "a grimy pocket watch");
            case "Cracked Screen": return ("the screen's cracked", "a phone with a cracked screen");
            case "Gunked Up, Won't Charge": return ("gunked up, won't charge", "a gunked-up phone");
            case "Frozen Boot Sequence - Reconnect Signal": return ("frozen on start-up", "a phone frozen on start-up");
            case "Debris Inside, Won't Tell Time": return ("grit inside, won't tick", "a pocket watch with grit inside");
            case "Seized Movement": return ("seized up, won't wind", "a seized pocket watch");
        }
        string spoken = string.IsNullOrWhiteSpace(label) ? "needs a look" : FaultSpeech.Spoken(label);
        return (spoken, "something to fix");
    }

    /// <summary>"watch", "phone", "camera" from a device's name ("Pocket Watch" → "watch").</summary>
    public static string ThingOf(string deviceName)
    {
        string n = (deviceName ?? "").Trim().ToLowerInvariant();
        if (n.Contains("watch")) return "watch";
        if (n.Contains("phone")) return "phone";
        if (n.Contains("camera")) return "camera";
        return n.Length > 0 ? n : "find";
    }

    /// <summary>The share of the sale a find of this grade fetches (JobBase's own pay ratios over Perfect's: Good 0.8,
    /// Passable 0.48), 0 when Rejected.</summary>
    public static float SaleShare(JobGrade grade) =>
        JobBase.PayMultiplier(grade) / JobBase.PayMultiplier(JobGrade.Perfect);
}
