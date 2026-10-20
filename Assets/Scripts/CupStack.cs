using System.Collections.Generic;
using UnityEngine;

public class CupStack : Interactable
{
    [SerializeField] private GameObject cupPrefab;

    // Either take one, or put an unwanted one back.
    //
    // The return half exists because carry capacity is 1: pick up a cup with no
    // order waiting and you were stuck holding it, with the only way out being
    // to dump it in a shelf or bench slot that a device needed. The stack is
    // where cups live, so the stack is where they go back.
    public override bool IsAvailable
    {
        get
        {
            PlayerCarry carry = PlayerCarry.Current;
            if (carry == null) return false;

            if (carry.IsCarrying) return HeldCup(carry) != null;

            return ShopInventory.Instance != null && ShopInventory.Instance.Cups > 0;
        }
    }

    // Any cup you're holding, empty or brewed, as long as it isn't mid-brew.
    //
    // This deliberately accepts BREWED cups, reversing an earlier decision. The
    // argument against was that pouring away a latte quietly destroys several
    // dollars of beans. The argument that beat it: the loss isn't quiet if the
    // player chooses it. A prompt reading "Pour it away" is a decision with a
    // visible cost, whereas the alternative — no bin at all — was strictly
    // worse. An orphaned drink had nowhere to go but a shelf slot a device
    // needed, and a full shelf blocks intake entirely.
    //
    // The SinkStation from the architecture spec owns this properly later.
    private static DrinkJob HeldCup(PlayerCarry carry)
    {
        DrinkJob d = carry != null ? carry.Carried as DrinkJob : null;
        return d != null && !d.Locked ? d : null;
    }

    public override string Prompt
    {
        get
        {
            PlayerCarry carry = PlayerCarry.Current;

            if (carry != null && carry.IsCarrying)
            {
                DrinkJob cup = HeldCup(carry);
                if (cup == null) return "Hands full";

                return cup.IsEmpty
                    ? "Put the cup back"
                    : $"Pour away the {cup.Drink.drinkName}";
            }

            if (ShopInventory.Instance != null && ShopInventory.Instance.Cups <= 0)
                return "Out of cups";

            return "Take a cup";
        }
    }

    /// <summary>The stack's glow, on while the step is "take a cup" (BeverageStation asks every frame; it breathes).</summary>
    public void SetGlow(bool on) => Glow.Set(this, on);

    private void OnDisable() => Glow.Set(this, false);

    public override void Interact(PlayerInteractor player)
    {
        PlayerCarry carry = player.GetComponent<PlayerCarry>();
        if (carry == null) return;

        // Putting an unwanted one back, or pouring one away. Either way the cup
        // itself returns to stock — it's the beans that are gone.
        if (HeldCup(carry) != null)
        {
            bool returnStock = HeldCup(carry).IsEmpty;
            carry.Consume();
            Sfx.Play("cup.return", transform.position);
            if (returnStock && ShopInventory.Instance != null) ShopInventory.Instance.ReturnCup();
            return;
        }

        // Taking one.
        if (!carry.HasSpace || cupPrefab == null || cupPrefab.GetComponent<DrinkJob>() == null) return;
        if (ShopInventory.Instance == null || !ShopInventory.Instance.TakeCup()) return;

        GameObject cup = Instantiate(cupPrefab, transform.position, transform.rotation);
        DrinkJob job = cup.GetComponent<DrinkJob>();
        if (job != null) carry.PickUp(job);
    }

    // ------------------------------------------------------------------ the glow (19 Oct 2026, playtest 7)
    //
    // The cup stack is the first thing to use at the drinks machine and the least visible thing in the frame: white
    // cups on a white counter, half behind the machine's frame, while six coloured paddles fill the middle of the screen
    // (the reviewers' day-ui finding; her: "I'll go here and I feel like the 1st thing I'll do is here and I don't know
    // where to go"). While the step is "take a cup" the stack's own materials glow warm amber and breathe (an emissive
    // pulse, like the bench's tool in its stand), and go quiet the frame a cup is in hand. Material instances are made
    // once per stack and put back exactly as they were when the glow ends. The dispenser's take surface
    // (BeverageCupSupply) uses this too.
    public static class Glow
    {
        sealed class State
        {
            public Renderer[] renderers;
            public Material[][] materials;        // the instances (renderer.materials), one array per renderer
            public bool[][] hadEmission;
            public Color[][] emissionBefore;
            public bool on;
        }

        static readonly Dictionary<Component, State> states = new Dictionary<Component, State>();
        static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");
        static readonly Color GlowColour = new Color(UiSkin.Gold.r, UiSkin.Gold.g, UiSkin.Gold.b, 1f);
        const float Low = .35f, High = .95f, BreathSeconds = 1.4f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => states.Clear();

        /// <summary>Glows <paramref name="stack"/>'s renderers (on), breathing, or puts their materials back (off).</summary>
        public static void Set(Component stack, bool on)
        {
            if (stack == null) return;
            states.TryGetValue(stack, out State state);
            if (!on)
            {
                if (state != null && state.on) Restore(state);
                return;
            }
            if (state == null)
            {
                state = Gather(stack);
                if (state == null) return;
                states[stack] = state;
            }
            state.on = true;
            float k = Mathf.Lerp(Low, High, .5f + .5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / BreathSeconds));
            Color glow = GlowColour * k;
            for (int r = 0; r < state.materials.Length; r++)
            {
                if (state.renderers[r] == null) continue;
                foreach (Material m in state.materials[r])
                {
                    if (m == null || !m.HasProperty(EmissionColour)) continue;
                    m.EnableKeyword("_EMISSION");
                    m.SetColor(EmissionColour, glow);
                }
            }
        }

        /// <summary>True while <paramref name="stack"/> glows (labs).</summary>
        public static bool IsOn(Component stack) => stack != null && states.TryGetValue(stack, out State s) && s.on;

        static State Gather(Component stack)
        {
            Renderer[] renderers = stack.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return null;
            var state = new State
            {
                renderers = renderers,
                materials = new Material[renderers.Length][],
                hadEmission = new bool[renderers.Length][],
                emissionBefore = new Color[renderers.Length][],
            };
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] == null || renderers[r] is ParticleSystemRenderer)
                {
                    state.materials[r] = new Material[0];
                    state.hadEmission[r] = new bool[0];
                    state.emissionBefore[r] = new Color[0];
                    continue;
                }
                Material[] instances = renderers[r].materials;   // instances: the shared ones stay as they are
                state.materials[r] = instances;
                state.hadEmission[r] = new bool[instances.Length];
                state.emissionBefore[r] = new Color[instances.Length];
                for (int i = 0; i < instances.Length; i++)
                {
                    Material m = instances[i];
                    if (m == null) continue;
                    state.hadEmission[r][i] = m.IsKeywordEnabled("_EMISSION");
                    state.emissionBefore[r][i] = m.HasProperty(EmissionColour) ? m.GetColor(EmissionColour) : Color.black;
                }
            }
            return state;
        }

        static void Restore(State state)
        {
            state.on = false;
            for (int r = 0; r < state.materials.Length; r++)
                for (int i = 0; i < state.materials[r].Length; i++)
                {
                    Material m = state.materials[r][i];
                    if (m == null) continue;
                    if (m.HasProperty(EmissionColour)) m.SetColor(EmissionColour, state.emissionBefore[r][i]);
                    if (!state.hadEmission[r][i]) m.DisableKeyword("_EMISSION");
                }
        }
    }
}
