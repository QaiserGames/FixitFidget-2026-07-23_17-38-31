using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// ---------------------------------------------------------------------------
// SOUND, THE PLUMBING: THE BANK (claude/sound-plan.md §4.3)
//
// Every sound the game plays is a named cue here ("cup.set", "door.bell"). Game code only names
// cues (Sfx.Play); the files, loudness, variation and reach live in this one asset, so choosing or
// swapping a sound never touches code. The asset is Assets/Data/Resources/Sound bank.asset, made and
// kept up to date by Fixit Fidget > Sound > Create or update the sound bank.
//
// The files are Sonniss GDC bundle sounds under Assets/Audio/Licensed/, which is git-ignored: the
// licence lets the game ship them but forbids sharing the files. A fresh clone of the public
// repository therefore has cues without files, and a cue without files is simply silent.
//
// The direction (Mansoor, 28 Sept): "cozy and nice and unique". Soft starts, warm, small and near,
// calm levels, no alarms; every repeating cue has several files and a small wobble.
// ---------------------------------------------------------------------------
public enum SoundBus { World, Ace, UI, Ambience, Outside, Music }

[CreateAssetMenu(fileName = "Sound bank", menuName = "Fixit Fidget/Sound bank")]
public sealed class SoundBank : ScriptableObject
{
    public const string ResourceName = "Sound bank";

    [Serializable]
    public sealed class Cue
    {
        [Tooltip("The name game code plays it by, e.g. cup.set.")]
        public string name = "";
        [Tooltip("What it is and when it plays (for choosing the files).")]
        [TextArea(1, 4)] public string when = "";
        [Tooltip("The files to vary between (never the same one twice in a row). Empty: silent.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float volume = .45f;
        [Tooltip("Each play's volume varies by up to this share, up or down.")]
        [Range(0f, .5f)] public float volumeWobble = .08f;
        [Range(.25f, 2f)] public float pitch = 1f;
        [Tooltip("Each play's pitch varies by up to this share, up or down (.06 is about a semitone either way).")]
        [Range(0f, .25f)] public float pitchWobble = .04f;
        [Tooltip("Placed in the world (louder near Ace, panned), or flat (the UI, the beds).")]
        public bool threeD = true;
        [Tooltip("Metres: full volume within this distance of the ears.")]
        [Min(.1f)] public float near = 1.5f;
        [Tooltip("Metres: silent beyond this distance.")]
        [Min(.5f)] public float far = 12f;
        [Tooltip("Seconds: the shortest gap between two plays (a scrub asked for every frame sounds once in this time).")]
        [Min(0f)] public float minGap = .05f;
        [Tooltip("How many of this cue can sound at once; the oldest makes way.")]
        [Range(1, 16)] public int maxAtOnce = 4;
        public SoundBus bus = SoundBus.World;

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                foreach (AudioClip clip in clips) if (clip != null) return true;
                return false;
            }
        }
    }

    [Tooltip("The game's mixer (Assets/Audio/Game.mixer). Its groups are found by name: World, Ace, UI, Ambience, Outside, Music. " +
             "Without it everything still plays, straight to the speakers.")]
    public AudioMixer mixer;
    [Tooltip("In the overhead view the ears sit this high above Ace's feet, turned with the camera (§4.1).")]
    [Range(.5f, 2.5f)] public float earHeight = 1.6f;
    [Tooltip("Ace's steps: one every this many metres walked (§4.5).")]
    [Range(.4f, 1.2f)] public float stride = .72f;
    public List<Cue> cues = new List<Cue>();

    Dictionary<string, Cue> byName;

    public Cue Find(string cueName)
    {
        if (string.IsNullOrEmpty(cueName)) return null;
        if (byName == null)
        {
            byName = new Dictionary<string, Cue>(StringComparer.Ordinal);
            foreach (Cue cue in cues)
                if (cue != null && !string.IsNullOrEmpty(cue.name) && !byName.ContainsKey(cue.name)) byName.Add(cue.name, cue);
        }
        return byName.TryGetValue(cueName, out Cue found) ? found : null;
    }

    void OnEnable() => byName = null;
    void OnValidate() => byName = null;

    static Cue C(string name, SoundBus bus, bool threeD, float volume, string when,
                 float near = 1.5f, float far = 12f, float minGap = .05f, int maxAtOnce = 4, float pitchWobble = .04f)
        => new Cue { name = name, bus = bus, threeD = threeD, volume = volume, when = when, near = near, far = far,
                     minGap = minGap, maxAtOnce = maxAtOnce, pitchWobble = pitchWobble };

    /// <summary>
    /// Every cue the game names, with a note on what it's for and a starting loudness and reach
    /// (the plan's §5 for the café, §6 for the night's beds). The files are chosen later (§7).
    /// </summary>
    public static List<Cue> Defaults() => new List<Cue>
    {
        // Today's sounds: their components keep their own files until the bank has better ones.
        C("drink.pour", SoundBus.World, true, .3f, "The drinks dispenser pouring (a loop). Replaces SoftPour.wav, which the roadmap review heard as \"a buzz\": soft liquid, modest splashes.", 1f, 9f, 0f, 6),
        C("drink.ready", SoundBus.World, true, .35f, "A cup has finished filling: a gentle chime (the café's signature family).", 1.5f, 12f),
        C("drink.machine", SoundBus.World, true, .22f, "The dispenser working under the pour: a soft hum or a little steam (a loop).", 1f, 8f, 0f, 6),
        C("phone.confirm", SoundBus.UI, false, .3f, "The phone at the counter shows it works again (seen close up)."),
        C("phone.ring", SoundBus.World, true, .55f, "The support line ringing, waiting to be answered (a loop).", 2f, 20f),
        C("phone.hold", SoundBus.World, true, .22f, "Hold music from the support line's handset while Ace waits (a loop, quiet and a bit tinny).", 1f, 10f),

        // Coming and going, and the room.
        C("door.bell", SoundBus.World, true, .5f, "Someone comes in or goes out of the café's door: the café's own little bell (signature). A group rings once.", 2.5f, 24f, 1.5f, 2),
        C("cafe.room", SoundBus.Ambience, false, .3f, "The café's own quiet: fridge, machine idle, air (a flat loop, 1-3 minutes)."),
        C("cafe.murmur", SoundBus.Ambience, false, .35f, "People chatting with no words you can follow (a flat loop); louder as the café fills."),
        C("street.day", SoundBus.Outside, false, .3f, "The street by day: traffic and the city, muffled while Ace is inside (a flat loop)."),

        // Ace.
        C("ace.step.cafe", SoundBus.Ace, true, .3f, "Ace's steps on the café floor.", 1f, 10f, .12f, 2, .06f),
        C("ace.step.pavement", SoundBus.Ace, true, .3f, "Ace's steps on the pavement (at night).", 1f, 10f, .12f, 2, .06f),
        C("ace.step.road", SoundBus.Ace, true, .3f, "Ace's steps on the road (at night).", 1f, 10f, .12f, 2, .06f),

        // Handling things.
        C("item.pickup", SoundBus.World, true, .4f, "Ace picks up a device or an item.", 1f, 10f, .08f),
        C("item.putdown", SoundBus.World, true, .4f, "Ace puts a device or an item down: shelf, bench, counter.", 1f, 10f, .08f),
        C("cup.pickup", SoundBus.World, true, .4f, "Ace picks up a cup: from the stack, the dispenser or a table.", 1f, 10f, .08f),
        C("cup.set", SoundBus.World, true, .4f, "A cup set down: ceramic on wood (dispenser, counter, table).", 1f, 10f, .08f),
        C("cup.return", SoundBus.World, true, .4f, "An unwanted cup back on the stack, or a drink poured away.", 1f, 10f, .1f),

        // The bench.
        C("tool.pick", SoundBus.World, true, .35f, "Ace switches tool at the bench: hand, brush, screwdriver, pry, tweezers.", 1f, 8f, .1f),
        C("screw.turn", SoundBus.World, true, .4f, "A screw turning out or in (half a second to a second).", .8f, 6f, .1f, 3),
        C("screw.drop", SoundBus.World, true, .4f, "A screw landing in the bin: a small tink.", .8f, 6f, .05f, 4, .1f),
        C("screw.seat", SoundBus.World, true, .35f, "A screw tightened home: a small final click.", .8f, 6f),
        C("part.off", SoundBus.World, true, .4f, "A cover or panel lifted off with the pry tool.", 1f, 8f),
        C("part.on", SoundBus.World, true, .4f, "A cover put back on: a snap.", 1f, 8f),
        C("part.replace", SoundBus.World, true, .45f, "A new part fitted with the tweezers: \"pull-out click, then magnetic snap\".", 1f, 8f),
        C("camera.shutter", SoundBus.World, true, .55f, "Grace's camera fires once its new shutter is in: an old film camera's shutter, not a phone's.", 1f, 10f),
        C("grime.scrub", SoundBus.World, true, .3f, "Scrubbing grime with the brush (asked for every frame, heard every so often).", .8f, 6f, .22f, 2),
        C("grime.clean", SoundBus.World, true, .4f, "The last of the grime gone: a small bright chime.", 1f, 8f),
        C("circuit.turn", SoundBus.World, true, .35f, "A circuit tile turned: a small click.", .8f, 6f),
        C("circuit.step", SoundBus.World, true, .25f, "The charge reaches the next tile.", .8f, 6f),
        C("circuit.fizzle", SoundBus.World, true, .3f, "The charge stops at a bad joint: a soft fizzle, never an alarm.", .8f, 6f),
        C("circuit.done", SoundBus.World, true, .4f, "The circuit is complete: the signal is back.", 1f, 8f),

        // Serving.
        C("handover.drink", SoundBus.World, true, .35f, "A drink handed over to a customer.", 1f, 10f, .1f),
        C("handover.repair", SoundBus.World, true, .4f, "A repaired device handed back.", 1f, 10f, .1f),
        C("repair.returned", SoundBus.UI, false, .3f, "The café's little \"job done\" when a repair goes back (signature family).", 1.5f, 12f, .5f, 1),
        C("money.paid", SoundBus.World, true, .35f, "Paid: coins into the till drawer, soft (no \"cha-ching\").", 1.5f, 12f, .15f, 2),
        C("money.tip", SoundBus.World, true, .3f, "A tip: a coin into the tip jar.", 1.5f, 12f, .15f, 2),
        C("ticket.new", SoundBus.UI, false, .25f, "A new repair ticket slides onto the rail.", 1.5f, 12f, .25f, 2),
        C("chair.sit", SoundBus.World, true, .25f, "Someone sits down: a chair scrape and settle.", 1f, 10f, .1f, 3),
        C("chair.stand", SoundBus.World, true, .25f, "Someone stands up: a chair scrape.", 1f, 10f, .1f, 3),

        // The day's rhythm and the menus.
        C("day.open", SoundBus.UI, false, .4f, "The café opens for the day (signature: the café's bell).", 1.5f, 12f, 2f, 1),
        C("day.lastorders", SoundBus.UI, false, .4f, "Last orders: no new customers from now (signature, a softer bell).", 1.5f, 12f, 2f, 1),
        C("day.closed", SoundBus.UI, false, .4f, "The last customer has gone and the day is over (signature).", 1.5f, 12f, 2f, 1),
        C("recap.open", SoundBus.UI, false, .3f, "The day's recap appears: paper.", 1.5f, 12f, 1f, 1),
        C("ui.confirm", SoundBus.UI, false, .3f, "A confirming click in the menus (Open Tomorrow).", 1.5f, 12f, .1f, 2),
        C("phone.tap", SoundBus.UI, false, .22f, "A tap on Ace's phone at closing (the recap): switching apps, Details, buying. Soft, quieter than ui.confirm.", 1.5f, 12f, .05f, 2),

        // The night's beds (filled in the night pass, §6).
        C("night.city", SoundBus.Outside, false, .3f, "The city at night, earlier and busier (a flat loop); cross-fades into night.city.late by 2 am."),
        C("night.city.late", SoundBus.Outside, false, .25f, "The city late at night: sparse and distant (a flat loop)."),
        C("night.far", SoundBus.Outside, false, .25f, "Now and then at night: a siren streets away, a dog, a car on the ring road (one-offs, panned).", 1.5f, 12f, 8f, 1),
        C("cafe.night", SoundBus.Ambience, false, .25f, "Inside the closed café at night: a fridge hum, a ticking clock (a flat loop)."),

        // The Night 1 slice: a trophy, going home, and the straight face the morning after.
        C("night.take", SoundBus.Ace, true, .4f, "Ace pockets something at night (Grace's gnome): a soft scrape and a rustle, quiet on purpose.", 1f, 10f, .5f, 1),
        C("night.home", SoundBus.UI, false, .3f, "Ace calls it a night inside the café's door: the door closing softly behind.", 1.5f, 12f, 1f, 1),
        C("night.dawn", SoundBus.UI, false, .3f, "Dawn ends the night: the first bird, Ace hurrying home.", 1.5f, 12f, 1f, 1),
        C("face.held", SoundBus.UI, false, .3f, "Ace keeps a straight face: a small relieved breath or a soft tick of the needle (never a fanfare).", 1.5f, 12f, .5f, 1),
        C("face.cracked", SoundBus.UI, false, .3f, "Ace cracks: a stifled snort, comic but small.", 1.5f, 12f, .5f, 1),
    };
}
