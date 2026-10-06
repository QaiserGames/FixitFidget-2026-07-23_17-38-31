using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// BARKS: THE RULES (claude/foundation-pass-build-plan.md §5, agreed 5 Oct 2026)
//
// A bark is one short line near whoever says it: the man at the bins, Ace muttering, a neighbour at a
// window, Grace's "Hm?" (chunk C). Barks.cs draws them; this decides when an ambient line may be said,
// which line of a pool comes next, and how long a line stays up. Pure C#, no Unity types, so the
// console tests (Tests/BarkRules) compile it exactly as the game does.
//
//   * How long: the conversation's reading time (ConversationUI.ReadTime): 0.6 s + 1/18 s a character,
//     between 1.4 and 4.5 s.
//   * Ambient lines (from a pool, said by the world, not by a scene): a speaker waits Speaker Cooldown
//     between their own lines, and a new ambient line from anyone starts at most once every Global Gap.
//     None while a scene plays.
//   * Pools: every line once, in a shuffled order, before any repeats, and never the same line twice in a
//     row across a refill.
// Scenes and one-off lines (Grace's reaction, Ace's line of the night) are not throttled here: the
// caller decides when they happen.
// ---------------------------------------------------------------------------
public sealed class BarkRules
{
    /// <summary>The writing rule (characters): the Bark rules check warns above it. Two lines on screen hold about this much.</summary>
    public const int LengthRule = 60;

    public float readingBase = .6f, readingPerCharacter = 1f / 18f, readingMin = 1.4f, readingMax = 4.5f;
    /// <summary>A speaker's own ambient lines are at least this far apart (seconds).</summary>
    public float speakerCooldown = 20f;
    /// <summary>A new ambient line from anyone starts at most once in this (seconds).</summary>
    public float globalGap = 4f;

    readonly Dictionary<string, float> lastSaid = new Dictionary<string, float>(StringComparer.Ordinal);
    readonly Dictionary<string, Bag> bags = new Dictionary<string, Bag>(StringComparer.Ordinal);
    readonly Random random;
    float lastAmbient = float.NegativeInfinity;

    sealed class Bag
    {
        public int[] order = Array.Empty<int>();
        public int next, count, last = -1;
    }

    public BarkRules(int seed = 0) { random = seed == 0 ? new Random() : new Random(seed); }

    /// <summary>Seconds a line stays up (before its fade out).</summary>
    public float ReadingSeconds(string text)
    {
        float seconds = readingBase + (text == null ? 0 : text.Length) * readingPerCharacter;
        return seconds < readingMin ? readingMin : seconds > readingMax ? readingMax : seconds;
    }

    /// <summary>May <paramref name="speaker"/> start an ambient line at <paramref name="now"/> (seconds)?</summary>
    public bool MayAmbient(string speaker, float now, bool scenePlaying)
    {
        if (scenePlaying || string.IsNullOrEmpty(speaker)) return false;
        if (now - lastAmbient < globalGap) return false;
        return !lastSaid.TryGetValue(speaker, out float said) || now - said >= speakerCooldown;
    }

    /// <summary>An ambient line was said: the gaps start from here.</summary>
    public void SaidAmbient(string speaker, float now)
    {
        if (string.IsNullOrEmpty(speaker)) return;
        lastSaid[speaker] = now;
        lastAmbient = now;
    }

    /// <summary>Seconds until <paramref name="speaker"/> may start an ambient line (0: now).</summary>
    public float Wait(string speaker, float now)
    {
        float wait = globalGap - (now - lastAmbient);
        if (!string.IsNullOrEmpty(speaker) && lastSaid.TryGetValue(speaker, out float said))
            wait = Math.Max(wait, speakerCooldown - (now - said));
        return wait > 0f ? wait : 0f;
    }

    /// <summary>
    /// The index of the next line of a pool of <paramref name="count"/> lines (-1 for an empty pool): every line
    /// once in a shuffled order before any repeats, never the same one twice in a row. A pool whose size
    /// changed starts a new round.
    /// </summary>
    public int Next(string poolKey, int count)
    {
        if (count <= 0 || poolKey == null) return -1;
        if (!bags.TryGetValue(poolKey, out Bag bag)) bags[poolKey] = bag = new Bag();
        if (bag.count != count || bag.order.Length != count)
        {
            bag.order = new int[count];
            bag.count = count;
            bag.next = count;   // forces a shuffle below
        }
        if (bag.next >= count)
        {
            for (int i = 0; i < count; i++) bag.order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int t = bag.order[i]; bag.order[i] = bag.order[j]; bag.order[j] = t;
            }
            // Never the same line twice in a row across a refill.
            if (count > 1 && bag.order[0] == bag.last)
            {
                int t = bag.order[0]; bag.order[0] = bag.order[count - 1]; bag.order[count - 1] = t;
            }
            bag.next = 0;
        }
        bag.last = bag.order[bag.next++];
        return bag.last;
    }

    /// <summary>Forget every gap and every pool (a new night, a new session).</summary>
    public void Reset()
    {
        lastSaid.Clear();
        bags.Clear();
        lastAmbient = float.NegativeInfinity;
    }
}
