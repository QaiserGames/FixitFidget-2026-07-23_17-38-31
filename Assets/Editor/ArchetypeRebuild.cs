#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ---------------------------------------------------------------------------
// ARCHETYPE REBUILD — five personalities, 2026-08-27
//
// WHY
//
// CustomerArchetype was built as a PERSONALITY type — patience multiplier, tip
// multiplier, where they wait, whether they want a coffee, and a dialogue set.
// Its own default archetypeName is "Cheerful".
//
// The three slots were filled with PhoneOwner, WatchOwner and Radio Owner —
// device labels. The spawner rolls an archetype and a device independently, so
// across three logged days 15 of 19 repair jobs had a "phone person" carrying a
// pocket watch. There was also a Radio Owner archetype for a device that
// doesn't exist.
//
// Nothing in the code is wrong. The data was authored against the wrong model.
// Device affinity already has a correct home: CustomerProfile.preferredDevice,
// on named regulars, where "Alex always brings watches" is a character trait
// rather than a personality type.
//
// WHAT THIS DOES
//
// Replaces the archetype array with five personalities, dials set, and
// PLACEHOLDER dialogue so the system runs immediately. The placeholder lines
// are deliberately flat — they exist so the shape can be heard in play, and so
// the writer has something to overwrite rather than a blank array.
//
// Two of the five stand rather than sit. Six logged days produced 100% Seat and
// zero Loiter, so the 1.15x standing drain and the entire bussing pressure
// model have never once occurred in this game.
//
// This edits the SCENE, so Ctrl+Z works. Save with Ctrl+S when happy.
// ---------------------------------------------------------------------------

public static class ArchetypeRebuild
{
    private struct Spec
    {
        public string name;
        public float patience;
        public float tip;
        public WaitingSpot.SpotKind wait;
        public float drinkWish;
        public Color mood;

        public string[] intake, accepted, completed, declined, reassured, stormed, ordered;
    }

    private static List<Spec> BuildSpecs() => new()
    {
        new Spec
        {
            name = "Cheerful", patience = 1.2f, tip = 1.2f,
            wait = WaitingSpot.SpotKind.Seat, drinkWish = 0.6f,
            mood = new Color(0.98f, 0.84f, 0.42f),
            intake    = new[] { "Hi! My {device} — {fault}. Any chance?",
                                "Morning! {fault} on my {device}. Can you look?" },
            accepted  = new[] { "Amazing, thank you.", "No rush at all." },
            completed = new[] { "Oh, that's great. Thank you!", "Look at that. Cheers." },
            declined  = new[] { "No worries at all.", "That's alright — another time." },
            reassured = new[] { "Of course, take your time.", "No problem, honestly." },
            stormed   = new[] { "Ah — I'll have to go. Sorry.", "I've run out of time. Never mind." },
            ordered   = new[] { "Ooh — could I get a coffee too?", "Since I'm here, a drink?" }
        },

        new Spec
        {
            name = "Impatient", patience = 0.7f, tip = 0.9f,
            wait = WaitingSpot.SpotKind.Loiter, drinkWish = 0.25f,
            mood = new Color(0.90f, 0.45f, 0.35f),
            intake    = new[] { "{device}. {fault}. How long?",
                                "This is {fault}. Can you do it now?" },
            accepted  = new[] { "Fine. I'll wait here.", "Quick as you can." },
            completed = new[] { "Right. Good.", "Finally. Thanks." },
            declined  = new[] { "Seriously?", "Great. Thanks for nothing." },
            reassured = new[] { "Fine. A few more minutes.", "Alright. Alright." },
            stormed   = new[] { "That's it, I'm done waiting.", "Forget it." },
            ordered   = new[] { "Coffee. While I'm standing here.", "Get me a coffee." }
        },

        new Spec
        {
            name = "Chatty", patience = 1.3f, tip = 1.0f,
            wait = WaitingSpot.SpotKind.Seat, drinkWish = 0.8f,
            mood = new Color(0.55f, 0.78f, 0.92f),
            intake    = new[] { "So — my {device}. {fault}. Long story.",
                                "You'll laugh. {device}, {fault}." },
            accepted  = new[] { "Brilliant. I'll be right over there.",
                                "Take your time, I'm in no hurry at all." },
            completed = new[] { "Oh, wonderful. I'll tell everyone about this place.",
                                "You're a lifesaver, honestly." },
            declined  = new[] { "Ah, fair enough. Busy day?", "No, no, I understand completely." },
            reassured = new[] { "Oh, don't worry about me.", "Honestly, it's fine." },
            stormed   = new[] { "I should get going, sadly.", "Another time, maybe." },
            ordered   = new[] { "Actually — a coffee would be lovely.",
                                "Ooh, while you're up. A drink?" }
        },

        new Spec
        {
            name = "Rushed", patience = 0.6f, tip = 1.3f,
            wait = WaitingSpot.SpotKind.Loiter, drinkWish = 0.15f,
            mood = new Color(0.85f, 0.62f, 0.30f),
            intake    = new[] { "Sorry — {device}, {fault}. I'm late.",
                                "I know you're busy. {fault}. Please." },
            accepted  = new[] { "Thank you. Really.", "You're saving me here." },
            completed = new[] { "Thank you — I have to run.", "Perfect. I owe you." },
            declined  = new[] { "No, I get it. Thanks anyway.", "Fair enough. Worth asking." },
            reassured = new[] { "Okay. Okay, I can wait.", "Right — sorry, I'm just up against it." },
            stormed   = new[] { "I can't — I have to go. Sorry.", "I've left it too late. Sorry." },
            ordered   = new[] { "Coffee? To go, if that's alright.", "A quick coffee, if you can." }
        },

        new Spec
        {
            name = "Sentimental", patience = 1.1f, tip = 1.4f,
            wait = WaitingSpot.SpotKind.Seat, drinkWish = 0.5f,
            mood = new Color(0.72f, 0.60f, 0.85f),
            intake    = new[] { "It's {fault}. This {device} means a lot to me.",
                                "My {device} — {fault}. I'd hate to lose it." },
            accepted  = new[] { "Thank you. It's been with me a long time.",
                                "I appreciate it. More than you know." },
            completed = new[] { "Oh — thank you. Really, thank you.",
                                "You've no idea what this means." },
            declined  = new[] { "Oh. That's a shame.", "I understand. I'll try elsewhere." },
            reassured = new[] { "Thank you. I'll wait.", "That's kind of you." },
            stormed   = new[] { "I'll take it somewhere else, then.", "I'd hoped for better. Never mind." },
            ordered   = new[] { "Could I trouble you for a coffee?", "A drink would be nice, thank you." }
        }
    };

    [MenuItem("Fixit Fidget/Content/4 · Rebuild archetypes as personalities")]
    public static void Rebuild()
    {
        CustomerSpawner spawner = Object.FindAnyObjectByType<CustomerSpawner>();
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("Archetype rebuild",
                "No CustomerSpawner in the open scene.\n\n" +
                "Make sure SampleScene is open.", "OK");
            return;
        }

        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty arr = so.FindProperty("archetypes");

        if (arr == null)
        {
            EditorUtility.DisplayDialog("Archetype rebuild",
                "Couldn't find the 'archetypes' field on CustomerSpawner.", "OK");
            return;
        }

        List<string> old = new();
        for (int i = 0; i < arr.arraySize; i++)
        {
            SerializedProperty n = arr.GetArrayElementAtIndex(i).FindPropertyRelative("archetypeName");
            old.Add(n != null ? n.stringValue : "(unnamed)");
        }

        bool go = EditorUtility.DisplayDialog("Rebuild archetypes",
            $"Replacing {arr.arraySize} archetype(s):\n   {string.Join(", ", old)}\n\n" +
            "with five personalities:\n   Cheerful, Impatient, Chatty, Rushed, Sentimental\n\n" +
            "Placeholder dialogue is included so it runs straight away.\n\n" +
            "This edits the SCENE — Ctrl+Z undoes it, and nothing is saved " +
            "until you press Ctrl+S.",
            "Rebuild", "Cancel");

        if (!go) return;

        Undo.RecordObject(spawner, "Rebuild archetypes");

        List<Spec> specs = BuildSpecs();
        arr.ClearArray();
        arr.arraySize = specs.Count;

        for (int i = 0; i < specs.Count; i++)
        {
            Spec s = specs[i];
            SerializedProperty e = arr.GetArrayElementAtIndex(i);

            e.FindPropertyRelative("archetypeName").stringValue = s.name;
            e.FindPropertyRelative("patienceMultiplier").floatValue = s.patience;
            e.FindPropertyRelative("tipMultiplier").floatValue = s.tip;
            e.FindPropertyRelative("moodColor").colorValue = s.mood;
            e.FindPropertyRelative("preferredWaitKind").enumValueIndex = (int)s.wait;
            e.FindPropertyRelative("drinkWishChance").floatValue = s.drinkWish;

            SerializedProperty lines = e.FindPropertyRelative("lines");
            Fill(lines, "intake", s.intake);
            Fill(lines, "accepted", s.accepted);
            Fill(lines, "completed", s.completed);
            Fill(lines, "declined", s.declined);
            Fill(lines, "reassured", s.reassured);
            Fill(lines, "stormedOut", s.stormed);
            Fill(lines, "orderedDrink", s.ordered);
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(spawner);

        EditorUtility.DisplayDialog("Archetype rebuild",
            "Five personalities installed with placeholder dialogue.\n\n" +
            "Cheerful      1.2x patience  1.2x tip  sits    60% coffee\n" +
            "Impatient     0.7x patience  0.9x tip  STANDS  25% coffee\n" +
            "Chatty        1.3x patience  1.0x tip  sits    80% coffee\n" +
            "Rushed        0.6x patience  1.3x tip  STANDS  15% coffee\n" +
            "Sentimental   1.1x patience  1.4x tip  sits    50% coffee\n\n" +
            "PRESS CTRL+S. Nothing is saved until you do.\n\n" +
            "The lines are placeholders — overwrite them in the Inspector as " +
            "the real ones arrive. One personality at a time is fine.", "OK");

        Debug.Log("[Archetype rebuild] Replaced: " + string.Join(", ", old) +
                  "  ->  Cheerful, Impatient, Chatty, Rushed, Sentimental");
    }

    private static void Fill(SerializedProperty lines, string field, string[] values)
    {
        if (lines == null) return;

        SerializedProperty p = lines.FindPropertyRelative(field);
        if (p == null) return;

        p.ClearArray();
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            p.GetArrayElementAtIndex(i).stringValue = values[i];
    }
}
#endif
