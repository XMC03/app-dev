using System;
using System.Collections.Generic;

namespace Nightfall
{
    // Engine-independent rules also used by the executable C# checks.
    public struct Point
    {
        public double X, Y;
        public Point(double x, double y) { X = x; Y = y; }
        public static double Distance(Point a, Point b)
        { double x = a.X - b.X, y = a.Y - b.Y; return Math.Sqrt(x * x + y * y); }
    }

    public enum RunState { Menu, Playing, Paused, Cleared, Won, Lost }
    public sealed class Spirit
    {
        public Point Position;
        public readonly Queue<Point> Route = new Queue<Point>();
        public double Repath;
        public Spirit(Point position) { Position = position; }
    }
    public sealed class Chapter
    {
        public readonly string Name;
        public readonly int Speed;
        public readonly Point[] Trees, Crystals;
        public Chapter(string name, int speed, int[,] trees, int[,] crystals)
        { Name = name; Speed = speed; Trees = Convert(trees); Crystals = Convert(crystals); }
        private static Point[] Convert(int[,] cells)
        {
            var result = new Point[cells.GetLength(0)];
            for (int i = 0; i < result.Length; i++) result[i] = new Point(cells[i, 0] * 40 + 20, cells[i, 1] * 40 + 20);
            return result;
        }
    }

    public sealed class Campaign
    {
        public const int Width = 960, Height = 600, Tile = 40, Columns = 24, Rows = 15;
        public static readonly Chapter[] Chapters = {
            new Chapter("The quiet grove", 72,
                new int[,] {{5,3},{6,3},{10,2},{15,3},{19,4},{4,8},{5,8},{10,7},{11,7},{16,8},{20,11},{8,12},{13,11}},
                new int[,] {{6,1},{12,2},{21,2},{2,11},{9,10},{16,5},{17,12},{21,8}}),
            new Chapter("The whispering wood", 84,
                new int[,] {{5,2},{5,3},{5,4},{10,4},{11,4},{16,2},{16,3},{19,6},{20,6},{4,9},{9,8},{9,9},{14,9},{15,9},{18,12},{6,12}},
                new int[,] {{3,5},{8,2},{19,2},{2,12},{7,10},{13,6},{15,12},{21,10}}),
            new Chapter("The last light", 96,
                new int[,] {{4,3},{5,3},{8,2},{8,3},{13,3},{14,3},{18,3},{18,4},{3,8},{4,8},{8,7},{9,7},{13,8},{13,9},{18,9},{19,9},{7,12},{15,12}},
                new int[,] {{2,5},{10,2},{20,3},{3,12},{7,9},{15,6},{20,12},{11,12}})
        };
        public RunState State { get; private set; } = RunState.Menu;
        public int Level { get; private set; }
        public int Hearts { get; private set; }
        public int Collected { get; private set; }
        public double Elapsed { get; private set; }
        public double Total { get; private set; }
        public double Stamina { get; private set; }
        public double Protection { get; private set; }
        public bool DashExhausted { get; private set; }
        public bool Dashing { get; private set; }
        public int Facing { get; private set; } = 1;
        public Point Player;
        public readonly Point Gate = new Point(900, 100);
        public readonly List<Point> Crystals = new List<Point>();
        public readonly List<Spirit> Spirits = new List<Spirit>();
        public Point[] Trees { get { return Chapters[Level].Trees; } }
        public bool GateOpen { get { return Collected == 8; } }
        public double CampaignTime { get { return Total + Elapsed; } }
        public event Action<string, Point> Effect;
        private readonly HashSet<int> blocked = new HashSet<int>();

        public Campaign() { Load(); }
        private static double Clamp(double value, double low, double high) { return Math.Max(low, Math.Min(high, value)); }
        private void Emit(string name, Point position) { if (Effect != null) Effect(name, position); }
        private void Load()
        {
            Player = new Point(100, 100); Hearts = 3; Collected = 0; Elapsed = 0;
            Stamina = 1; Protection = 0; Dashing = false; DashExhausted = false; Facing = 1;
            Crystals.Clear(); Crystals.AddRange(Chapters[Level].Crystals); blocked.Clear();
            foreach (var tree in Trees) blocked.Add((int)(tree.Y / Tile) * Columns + (int)(tree.X / Tile));
            var starts = new[] { new Point(860, 540), new Point(100, 540), new Point(900, 300), new Point(500, 540) };
            Spirits.Clear(); for (int i = 0; i < 2 + Level; i++) Spirits.Add(new Spirit(starts[i]));
        }
        public void PrimaryAction()
        {
            if (State == RunState.Cleared) { Total += Elapsed; Level++; Load(); }
            else if (State == RunState.Won) { Level = 0; Total = 0; Load(); }
            else if (State == RunState.Lost) Load();
            State = RunState.Playing;
        }
        public void RestartChapter()
        {
            if (State == RunState.Won) { Level = 0; Total = 0; }
            Load(); State = RunState.Playing;
        }
        public void Pause()
        {
            if (State == RunState.Playing) State = RunState.Paused;
            else if (State == RunState.Paused) State = RunState.Playing;
        }
        public bool Walkable(int x, int y)
        { return x >= 1 && y >= 1 && x < Columns - 1 && y < Rows - 1 && !blocked.Contains(y * Columns + x); }
        public List<Point> FindPath(Point from, Point to)
        {
            int start = (int)(from.Y / Tile) * Columns + (int)(from.X / Tile);
            int goal = (int)(to.Y / Tile) * Columns + (int)(to.X / Tile);
            if (blocked.Contains(goal))
            {
                double nearest = double.MaxValue;
                for (int y = 1; y < Rows - 1; y++) for (int x = 1; x < Columns - 1; x++) if (Walkable(x, y))
                {
                    double d = Point.Distance(to, new Point(x * Tile + 20, y * Tile + 20));
                    if (d < nearest) { nearest = d; goal = y * Columns + x; }
                }
            }
            if (start == goal) return new List<Point> { to };
            var queue = new Queue<int>(); var previous = new Dictionary<int, int>();
            queue.Enqueue(start); previous[start] = -1;
            while (queue.Count > 0 && !previous.ContainsKey(goal))
            {
                int id = queue.Dequeue(), x = id % Columns, y = id / Columns;
                int[,] neighbors = { { x + 1, y }, { x - 1, y }, { x, y + 1 }, { x, y - 1 } };
                for (int i = 0; i < 4; i++)
                {
                    int nx = neighbors[i, 0], ny = neighbors[i, 1], next = ny * Columns + nx;
                    if (Walkable(nx, ny) && !previous.ContainsKey(next)) { previous[next] = id; queue.Enqueue(next); }
                }
            }
            var result = new List<Point>(); if (!previous.ContainsKey(goal)) return result;
            for (int id = goal; id != start; id = previous[id]) result.Add(new Point(id % Columns * Tile + 20, id / Columns * Tile + 20));
            result.Reverse(); var center = new Point(start % Columns * Tile + 20, start / Columns * Tile + 20);
            if (Point.Distance(from, center) > 3) result.Insert(0, center);
            return result;
        }
        public Point Move(Point position, double dx, double dy)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dy * dy) / 6));
            for (int i = 0; i < steps; i++)
            {
                double x = Clamp(position.X + dx / steps, 51, Width - 51);
                bool hit = false; foreach (var tree in Trees) if (Point.Distance(new Point(x, position.Y), tree) < 36) { hit = true; break; }
                if (!hit) position.X = x;
                double y = Clamp(position.Y + dy / steps, 51, Height - 51);
                hit = false; foreach (var tree in Trees) if (Point.Distance(new Point(position.X, y), tree) < 36) { hit = true; break; }
                if (!hit) position.Y = y;
            }
            return position;
        }
        public void Step(double dt, int dx, int dy, bool dash)
        {
            if (State != RunState.Playing) return;
            dt = Clamp(dt, 0, .04); Elapsed += dt; Protection = Math.Max(0, Protection - dt);
            dx = Math.Sign(dx); dy = Math.Sign(dy); double magnitude = Math.Sqrt(dx * dx + dy * dy);
            if (!dash) DashExhausted = false;
            bool activeDash = dash && magnitude > 0 && Stamina > 0 && !DashExhausted;
            if (activeDash && !Dashing) Emit("dash", Player); Dashing = activeDash;
            Stamina = Clamp(Stamina + (Dashing ? -.7 : .28) * dt, 0, 1); if (Stamina == 0) DashExhausted = true;
            if (magnitude > 0) { double speed = Dashing ? 325 : 180; Player = Move(Player, dx / magnitude * speed * dt, dy / magnitude * speed * dt); if (dx != 0) Facing = dx; }
            for (int i = Crystals.Count - 1; i >= 0; i--) if (Point.Distance(Crystals[i], Player) <= 25)
            {
                var position = Crystals[i]; Crystals.RemoveAt(i); Collected++; Emit("crystal", position);
                if (GateOpen) Emit("gate", Gate);
            }
            foreach (var spirit in Spirits)
            {
                spirit.Repath -= dt;
                if (spirit.Repath <= 0 && spirit.Route.Count == 0)
                {
                    var route = FindPath(spirit.Position, Player);
                    for (int i = 0; i < Math.Min(2, route.Count); i++) spirit.Route.Enqueue(route[i]);
                    spirit.Repath = .25;
                }
                if (spirit.Route.Count > 0)
                {
                    var target = spirit.Route.Peek(); double d = Point.Distance(spirit.Position, target), step = Math.Min(d, Chapters[Level].Speed * dt);
                    spirit.Position = Move(spirit.Position, (target.X - spirit.Position.X) / Math.Max(1, d) * step, (target.Y - spirit.Position.Y) / Math.Max(1, d) * step);
                    if (Point.Distance(spirit.Position, target) < 2) spirit.Route.Dequeue();
                }
                if (Point.Distance(spirit.Position, Player) < 23 && Protection == 0)
                {
                    Hearts--; Protection = 1.8; Emit("hurt", Player);
                    if (Hearts == 0) { State = RunState.Lost; Dashing = false; Emit("lost", Player); break; }
                }
            }
            if (State == RunState.Playing && GateOpen && Point.Distance(Player, Gate) < 29)
            { State = Level == 2 ? RunState.Won : RunState.Cleared; Dashing = false; Emit("win", Gate); }
        }
    }
}
