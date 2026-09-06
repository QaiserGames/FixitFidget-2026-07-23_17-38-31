using System;
using System.Collections.Generic;

// Unity-independent rules: the view never decides connectivity or awards credit.
public sealed class CircuitRun
{
    [Flags]
    public enum Side { None = 0, Up = 1, Right = 2, Down = 4, Left = 8 }
    public readonly struct Cell
    {
        public readonly int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
    }

    private readonly Cell[] cells;
    private readonly Side[] solution;
    private readonly int[] turns;
    private readonly float secondsPerTile;
    private float timer;
    public int Count => cells.Length;
    public int Reached { get; private set; }
    public int BestReached { get; private set; }
    public int Retries { get; private set; }
    public bool Halted { get; private set; }
    public bool Finished => Reached == Count;
    public int Credits => Math.Max(0, BestReached - Retries);
    public int RemainingTasks => Count - Credits;
    public int Ceiling => Math.Max(0, Count - Retries);
    public int NextRetryCeiling => Math.Max(0, Count - Retries - 1);
    public float StepProgress => 1f - timer / secondsPerTile;

    public CircuitRun(int width, int height, int scrambled, float interval, int seed)
    {
        width = Math.Max(2, Math.Min(6, width));
        height = Math.Max(1, Math.Min(6, height));
        secondsPerTile = float.IsNaN(interval) || float.IsInfinity(interval)
            ? 4f : Math.Max(0.25f, interval);
        timer = secondsPerTile;
        var random = new Random(seed); // Never consume Unity's customer/spawner RNG.
        var route = new List<Cell>();
        int row = random.Next(height);
        route.Add(new Cell(0, row));
        for (int col = 0; col < width - 1; col++)
        {
            int target = random.Next(height);
            while (row != target)
            {
                row += target > row ? 1 : -1;
                route.Add(new Cell(col, row));
            }
            route.Add(new Cell(col + 1, row));
        }
        cells = route.ToArray();
        solution = new Side[Count];
        turns = new int[Count];
        var order = new int[Count];
        for (int i = 0; i < Count; i++)
        {
            solution[i] = (i == 0 ? Side.Left : Toward(cells[i], cells[i - 1]))
                        | (i == Count - 1 ? Side.Right : Toward(cells[i], cells[i + 1]));
            order[i] = i;
        }
        for (int i = Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int swap = order[i]; order[i] = order[j]; order[j] = swap;
        }
        for (int n = 0; n < Math.Max(0, Math.Min(scrambled, Count)); n++)
        {
            int i = order[n];
            // A 180-degree straight is already connected, so never call it scrambled.
            do { turns[i] = random.Next(1, 4); } while (IsAligned(i));
        }
    }

    public Cell Position(int i) => cells[i];
    public Side RequiredOpenings(int i) => solution[i];
    public Side Openings(int i) => Rotate(solution[i], turns[i]);
    public int Turns(int i) => turns[i];
    public bool IsAligned(int i) => Openings(i) == solution[i];
    public bool IsLocked(int i) => i < BestReached || Finished;
    public bool Turn(int i)
    {
        if (i < 0 || i >= Count || IsLocked(i)) return false;
        turns[i] = (turns[i] + 1) % 4;
        return true;
    }

    // Only the controller that owns the inspected job supplies active time.
    // Process at most one tile per frame: a hitch must not skip the player's warning.
    public void Tick(float deltaTime, bool watching)
    {
        if (!watching || Halted || Finished || deltaTime <= 0f
            || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        timer -= deltaTime;
        if (timer > 0f) return;
        timer = secondsPerTile;
        if (!IsAligned(Reached)) { Halted = true; return; }
        Reached++;
        BestReached = Math.Max(BestReached, Reached);
        // The final connected tile completes immediately, without another empty wait.
    }

    public bool Retry()
    {
        if (!Halted || Finished) return false;
        Retries++;
        Reached = 0;
        Halted = false;
        timer = secondsPerTile;
        return true;
    }

    public static Side Rotate(Side value, int steps)
    {
        int v = (int)value & 15;
        steps = ((steps % 4) + 4) % 4;
        for (int i = 0; i < steps; i++) v = ((v << 1) | (v >> 3)) & 15;
        return (Side)v;
    }

    private static Side Toward(Cell from, Cell to) =>
        to.X > from.X ? Side.Right : to.X < from.X ? Side.Left
        : to.Y > from.Y ? Side.Up : Side.Down;
}
