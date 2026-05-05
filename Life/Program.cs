using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Life
{
    // ─────────────────────────────────────────────────────────────────────────
    //  CELL
    // ─────────────────────────────────────────────────────────────────────────
    public class Cell
    {
        public bool IsAlive { get; set; }

        public Cell(bool isAlive = false) => IsAlive = isAlive;

        public Cell Clone() => new Cell(IsAlive);

        public override string ToString() => IsAlive ? "■" : "□";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SETTINGS
    // ─────────────────────────────────────────────────────────────────────────
    public class Settings
    {
        public int Rows { get; set; } = 20;
        public int Cols { get; set; } = 40;
        public int DelayMs { get; set; } = 100;
        public double InitialDensity { get; set; } = 0.3;
        public int MaxGenerations { get; set; } = 1000;
        public int StableThreshold { get; set; } = 10;

        public static Settings Load(string path)
        {
            if (!File.Exists(path)) return new Settings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        }

        public void Save(string path)
        {
            var opts = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(path, JsonSerializer.Serialize(this, opts));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ELEMENT — combination (cluster) of alive cells
    // ─────────────────────────────────────────────────────────────────────────
    public class Element
    {
        public List<(int Row, int Col)> Cells { get; } = new();

        public int Count => Cells.Count;

        public string Classify()
        {
            // Bounding box of the cluster
            int minR = Cells.Min(c => c.Row), maxR = Cells.Max(c => c.Row);
            int minC = Cells.Min(c => c.Col), maxC = Cells.Max(c => c.Col);
            int h = maxR - minR + 1, w = maxC - minC + 1;

            // Normalise positions
            var norm = Cells.Select(c => (c.Row - minR, c.Col - minC))
                            .OrderBy(c => c.Item1).ThenBy(c => c.Item2)
                            .ToList();

            // Known stable figures
            if (MatchesBlock(norm, h, w)) return "Block";
            if (MatchesBeehive(norm, h, w)) return "Beehive";
            if (MatchesLoaf(norm, h, w)) return "Loaf";
            if (MatchesBoat(norm, h, w)) return "Boat";
            if (MatchesTub(norm, h, w)) return "Tub";
            if (Count == 1) return "Singleton";
            if (Count == 2) return "Pair";
            if (Count >= 3 && h == 1) return "Row";
            if (Count >= 3 && w == 1) return "Column";
            return $"Unknown-{Count}";
        }

        private static bool MatchesCells(List<(int, int)> norm, IEnumerable<(int, int)> pattern)
        {
            var pat = pattern.OrderBy(c => c.Item1).ThenBy(c => c.Item2).ToList();
            if (norm.Count != pat.Count) return false;
            return norm.SequenceEqual(pat);
        }

        private static bool MatchesBlock(List<(int, int)> n, int h, int w) =>
            h == 2 && w == 2 && n.Count == 4 &&
            MatchesCells(n, new[] { (0,0),(0,1),(1,0),(1,1) });

        private static bool MatchesBeehive(List<(int, int)> n, int h, int w) =>
            h == 3 && w == 4 && n.Count == 6 &&
            MatchesCells(n, new[] { (0,1),(0,2),(1,0),(1,3),(2,1),(2,2) });

        private static bool MatchesLoaf(List<(int, int)> n, int h, int w) =>
            h == 4 && w == 4 && n.Count == 7 &&
            MatchesCells(n, new[] { (0,1),(0,2),(1,0),(1,3),(2,1),(2,3),(3,2) });

        private static bool MatchesBoat(List<(int, int)> n, int h, int w) =>
            h == 3 && w == 3 && n.Count == 5 &&
            MatchesCells(n, new[] { (0,0),(0,1),(1,0),(1,2),(2,1) });

        private static bool MatchesTub(List<(int, int)> n, int h, int w) =>
            h == 3 && w == 3 && n.Count == 4 &&
            MatchesCells(n, new[] { (0,1),(1,0),(1,2),(2,1) });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BOARD
    // ─────────────────────────────────────────────────────────────────────────
    public class Board
    {
        public int Rows { get; }
        public int Cols { get; }
        public int Generation { get; private set; }

        private Cell[,] _grid;
        private readonly Random _rng = new();

        public Board(int rows, int cols)
        {
            Rows = rows; Cols = cols;
            _grid = new Cell[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    _grid[r, c] = new Cell();
        }

        // ── Accessors ─────────────────────────────────────────────────────
        public Cell GetCell(int row, int col) => _grid[row, col];

        public bool IsAlive(int row, int col) => _grid[row, col].IsAlive;

        public void SetAlive(int row, int col, bool alive) => _grid[row, col].IsAlive = alive;

        public int CountAlive() =>
            Enumerable.Range(0, Rows).Sum(r => Enumerable.Range(0, Cols).Count(c => _grid[r, c].IsAlive));

        // ── Randomise ─────────────────────────────────────────────────────
        public void Randomize(double density)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    _grid[r, c].IsAlive = _rng.NextDouble() < density;
            Generation = 0;
        }

        // ── Step ──────────────────────────────────────────────────────────
        public void Step()
        {
            var next = new Cell[Rows, Cols];
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                {
                    int n = CountNeighbors(r, c);
                    bool alive = _grid[r, c].IsAlive;
                    next[r, c] = new Cell(alive ? (n == 2 || n == 3) : n == 3);
                }
            _grid = next;
            Generation++;
        }

        private int CountNeighbors(int row, int col)
        {
            int count = 0;
            for (int dr = -1; dr <= 1; dr++)
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0) continue;
                    int nr = (row + dr + Rows) % Rows;
                    int nc = (col + dc + Cols) % Cols;
                    if (_grid[nr, nc].IsAlive) count++;
                }
            return count;
        }

        // ── Element detection ────────────────────────────────────────────
        public List<Element> FindElements()
        {
            bool[,] visited = new bool[Rows, Cols];
            var result = new List<Element>();

            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (_grid[r, c].IsAlive && !visited[r, c])
                    {
                        var el = new Element();
                        BFS(r, c, visited, el);
                        result.Add(el);
                    }
            return result;
        }

        private void BFS(int startR, int startC, bool[,] visited, Element el)
        {
            var q = new Queue<(int, int)>();
            q.Enqueue((startR, startC));
            visited[startR, startC] = true;

            while (q.Count > 0)
            {
                var (r, c) = q.Dequeue();
                el.Cells.Add((r, c));
                for (int dr = -1; dr <= 1; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = r + dr, nc = c + dc;
                        if (nr >= 0 && nr < Rows && nc >= 0 && nc < Cols &&
                            _grid[nr, nc].IsAlive && !visited[nr, nc])
                        {
                            visited[nr, nc] = true;
                            q.Enqueue((nr, nc));
                        }
                    }
            }
        }

        // ── Stable detection ─────────────────────────────────────────────
        public bool IsStable(int prevAlive, int threshold, ref int stableCount)
        {
            int cur = CountAlive();
            if (cur == prevAlive) stableCount++;
            else stableCount = 0;
            return stableCount >= threshold;
        }

        // ── Save / Load ───────────────────────────────────────────────────
        public void SaveToFile(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Generation={Generation}");
            sb.AppendLine($"Rows={Rows}");
            sb.AppendLine($"Cols={Cols}");
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                    sb.Append(_grid[r, c].IsAlive ? '1' : '0');
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString());
        }

        public static Board LoadFromFile(string path)
        {
            var lines = File.ReadAllLines(path);
            int gen = int.Parse(lines[0].Split('=')[1]);
            int rows = int.Parse(lines[1].Split('=')[1]);
            int cols = int.Parse(lines[2].Split('=')[1]);

            var board = new Board(rows, cols);
            board.Generation = gen;

            for (int r = 0; r < rows; r++)
            {
                string line = lines[3 + r];
                for (int c = 0; c < cols && c < line.Length; c++)
                    board.SetAlive(r, c, line[c] == '1');
            }
            return board;
        }

        // ── Render ────────────────────────────────────────────────────────
        public string Render()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Generation: {Generation}  Alive: {CountAlive()}");
            sb.AppendLine(new string('─', Cols + 2));
            for (int r = 0; r < Rows; r++)
            {
                sb.Append('│');
                for (int c = 0; c < Cols; c++)
                    sb.Append(_grid[r, c].IsAlive ? '■' : ' ');
                sb.AppendLine("│");
            }
            sb.AppendLine(new string('─', Cols + 2));
            return sb.ToString();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RESEARCH — Task 2
    // ─────────────────────────────────────────────────────────────────────────
    public static class Research
    {
        private static readonly Random _rng = new();

        /// <summary>Run one trial: returns generation count when stable (or maxGen).</summary>
        public static int RunTrial(int rows, int cols, double density, int maxGen, int stableThreshold)
        {
            var board = new Board(rows, cols);
            board.Randomize(density);
            int prevAlive = board.CountAlive(), stableCount = 0;

            for (int g = 0; g < maxGen; g++)
            {
                board.Step();
                if (board.IsStable(prevAlive, stableThreshold, ref stableCount))
                    return board.Generation;
                prevAlive = board.CountAlive();
            }
            return maxGen;
        }

        /// <summary>Runs many trials across densities and returns (density, avgGen) pairs.</summary>
        public static List<(double Density, double AvgGen)> RunExperiment(
            int rows, int cols, int trialsPerDensity, int maxGen, int stableThreshold,
            Action<double>? progress = null)
        {
            var densities = Enumerable.Range(1, 19).Select(i => i * 0.05).ToList(); // 5%..95%
            var results = new List<(double, double)>();

            foreach (var d in densities)
            {
                double total = 0;
                for (int t = 0; t < trialsPerDensity; t++)
                    total += RunTrial(rows, cols, d, maxGen, stableThreshold);
                results.Add((d, total / trialsPerDensity));
                progress?.Invoke(d);
            }
            return results;
        }

        /// <summary>Analyse board: count cells and clusters, classify clusters.</summary>
        public static string AnalyseBoard(Board board)
        {
            int alive = board.CountAlive();
            var elements = board.FindElements();
            var classified = elements.GroupBy(e => e.Classify())
                                     .OrderByDescending(g => g.Count());

            var sb = new StringBuilder();
            sb.AppendLine($"Generation  : {board.Generation}");
            sb.AppendLine($"Alive cells : {alive}");
            sb.AppendLine($"Clusters    : {elements.Count}");
            sb.AppendLine("─── Classification ───");
            foreach (var g in classified)
                sb.AppendLine($"  {g.Key,-18}: {g.Count(),4}");
            return sb.ToString();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PRESET COLONIES
    // ─────────────────────────────────────────────────────────────────────────
    public static class Presets
    {
        private static void Apply(Board b, int baseR, int baseC, (int, int)[] cells)
        {
            foreach (var (dr, dc) in cells)
            {
                int r = baseR + dr, c = baseC + dc;
                if (r >= 0 && r < b.Rows && c >= 0 && c < b.Cols)
                    b.SetAlive(r, c, true);
            }
        }

        public static Board Glider(int rows = 20, int cols = 40)
        {
            var b = new Board(rows, cols);
            Apply(b, 1, 1, new[] { (0,1),(1,2),(2,0),(2,1),(2,2) });
            return b;
        }

        public static Board Blinker(int rows = 20, int cols = 40)
        {
            var b = new Board(rows, cols);
            Apply(b, 9, 19, new[] { (0,0),(0,1),(0,2) });
            return b;
        }

        public static Board Block(int rows = 20, int cols = 40)
        {
            var b = new Board(rows, cols);
            Apply(b, 9, 19, new[] { (0,0),(0,1),(1,0),(1,1) });
            return b;
        }

        public static Board Beehive(int rows = 20, int cols = 40)
        {
            var b = new Board(rows, cols);
            Apply(b, 9, 18, new[] { (0,1),(0,2),(1,0),(1,3),(2,1),(2,2) });
            return b;
        }

        public static Board Pulsar(int rows = 20, int cols = 40)
        {
            var b = new Board(rows, cols);
            (int, int)[] cells = {
                (2,4),(2,5),(2,6),(2,10),(2,11),(2,12),
                (4,2),(4,7),(4,9),(4,14),(5,2),(5,7),(5,9),(5,14),
                (6,2),(6,7),(6,9),(6,14),(7,4),(7,5),(7,6),(7,10),(7,11),(7,12),
                (9,4),(9,5),(9,6),(9,10),(9,11),(9,12),
                (10,2),(10,7),(10,9),(10,14),(11,2),(11,7),(11,9),(11,14),
                (12,2),(12,7),(12,9),(12,14),(14,4),(14,5),(14,6),(14,10),(14,11),(14,12)
            };
            Apply(b, 2, 12, cells);
            return b;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PROGRAM (console application)
    // ─────────────────────────────────────────────────────────────────────────
    public class Program
    {
        private static Settings _settings = new();
        private static Board _board = new Board(20, 40);
        private static string _dataDir = Path.Combine("..", "Data");

        public static void Main(string[] args)
        {
            Directory.CreateDirectory(_dataDir);
            var settingsPath = Path.Combine(_dataDir, "settings.json");

            // Create default settings file if missing
            if (!File.Exists(settingsPath)) _settings.Save(settingsPath);
            else _settings = Settings.Load(settingsPath);

            _board = new Board(_settings.Rows, _settings.Cols);
            _board.Randomize(_settings.InitialDensity);

            Console.OutputEncoding = Encoding.UTF8;
            RunMenu();
        }

        // ── MENU ─────────────────────────────────────────────────────────
        private static void RunMenu()
        {
            while (true)
            {
                try { Console.Clear(); } catch { }
                Console.WriteLine("╔══════════════════════════════╗");
                Console.WriteLine("║   Game of Life — Lab 05      ║");
                Console.WriteLine("╠══════════════════════════════╣");
                Console.WriteLine("║  1. Run simulation           ║");
                Console.WriteLine("║  2. Save state               ║");
                Console.WriteLine("║  3. Load state               ║");
                Console.WriteLine("║  4. Load preset colony       ║");
                Console.WriteLine("║  5. Analyse board            ║");
                Console.WriteLine("║  6. Run research experiment  ║");
                Console.WriteLine("║  7. Show settings            ║");
                Console.WriteLine("║  8. Edit settings            ║");
                Console.WriteLine("║  0. Exit                     ║");
                Console.WriteLine("╚══════════════════════════════╝");
                Console.Write("Choice: ");
                switch (Console.ReadLine()?.Trim())
                {
                    case "1": RunSimulation(); break;
                    case "2": SaveState(); break;
                    case "3": LoadState(); break;
                    case "4": LoadPreset(); break;
                    case "5": AnalyseBoard(); break;
                    case "6": RunResearch(); break;
                    case "7": ShowSettings(); break;
                    case "8": EditSettings(); break;
                    case "0": return;
                }
            }
        }

        // ── 1. Simulation ──────────────────────────────────────────────
        private static void RunSimulation()
        {
            Console.WriteLine($"Running up to {_settings.MaxGenerations} generations...\n");
            int prevAlive = _board.CountAlive(), stableCount = 0;

            for (int i = 0; i < _settings.MaxGenerations; i++)
            {
                try { Console.Clear(); } catch { }
                Console.WriteLine(_board.Render());
                Console.WriteLine($"Generation {_board.Generation} / {_settings.MaxGenerations}");
                _board.Step();

                if (_board.IsStable(prevAlive, _settings.StableThreshold, ref stableCount))
                {
                    try { Console.Clear(); } catch { }
                    Console.WriteLine(_board.Render());
                    Console.WriteLine($"Stable state reached at generation {_board.Generation}.");
                    Console.WriteLine("[Enter] to continue...");
                    Console.ReadLine();
                    return;
                }
                prevAlive = _board.CountAlive();

                bool keyPressed = false;
                try { keyPressed = Console.KeyAvailable; } catch { }
                if (keyPressed) { try { Console.ReadKey(true); } catch { } break; }

                Thread.Sleep(_settings.DelayMs);
            }

            Console.WriteLine($"\nStopped at generation {_board.Generation}. [Enter]");
            Console.ReadLine();
        }

        // ── 2. Save ────────────────────────────────────────────────────
        private static void SaveState()
        {
            Console.Write("Filename (in ../Data/): ");
            var name = Console.ReadLine()?.Trim() ?? "save";
            if (!name.EndsWith(".txt")) name += ".txt";
            var path = Path.Combine(_dataDir, name);
            _board.SaveToFile(path);
            Console.WriteLine($"Saved → {path}  [Enter]");
            Console.ReadLine();
        }

        // ── 3. Load ────────────────────────────────────────────────────
        private static void LoadState()
        {
            var files = Directory.GetFiles(_dataDir, "*.txt").Where(f => !f.EndsWith("data.txt")).ToArray();
            if (files.Length == 0) { Console.WriteLine("No save files found.  [Enter]"); Console.ReadLine(); return; }
            Console.WriteLine("Files:");
            for (int i = 0; i < files.Length; i++) Console.WriteLine($"  {i + 1}. {Path.GetFileName(files[i])}");
            Console.Write("Choose: ");
            if (int.TryParse(Console.ReadLine(), out int idx) && idx >= 1 && idx <= files.Length)
            {
                _board = Board.LoadFromFile(files[idx - 1]);
                Console.WriteLine("Loaded. [Enter]");
            }
            Console.ReadLine();
        }

        // ── 4. Presets ─────────────────────────────────────────────────
        private static void LoadPreset()
        {
            Console.WriteLine("1. Glider  2. Blinker  3. Block  4. Beehive  5. Pulsar");
            Console.Write("Choose: ");
            _board = Console.ReadLine()?.Trim() switch
            {
                "1" => Presets.Glider(_settings.Rows, _settings.Cols),
                "2" => Presets.Blinker(_settings.Rows, _settings.Cols),
                "3" => Presets.Block(_settings.Rows, _settings.Cols),
                "4" => Presets.Beehive(_settings.Rows, _settings.Cols),
                "5" => Presets.Pulsar(_settings.Rows, _settings.Cols),
                _   => _board
            };
            Console.WriteLine("Preset loaded. [Enter]");
            Console.ReadLine();
        }

        // ── 5. Analyse ─────────────────────────────────────────────────
        private static void AnalyseBoard()
        {
            try { Console.Clear(); } catch { }
            Console.WriteLine(Research.AnalyseBoard(_board));
            Console.ReadLine();
        }

        // ── 6. Research experiment ─────────────────────────────────────
        private static void RunResearch()
{
    Console.WriteLine("\nRunning stability experiment (this may take a minute)...\n");
    const int trials = 15;
    var results = Research.RunExperiment(
        _settings.Rows, _settings.Cols, trials,
        _settings.MaxGenerations, _settings.StableThreshold,
        d => Console.Write($"\r  Density {d:P0}...   "));

    Console.WriteLine("\n\nResults:");
    Console.WriteLine($"{"Density",10}  {"Avg Gen",10}");
    Console.WriteLine(new string('─', 24));
    foreach (var (d, avg) in results)
        Console.WriteLine($"{d,10:P0}  {avg,10:F1}");

    // Save data
    var dataPath = Path.Combine(_dataDir, "data.txt");
    var sb = new StringBuilder();
    sb.AppendLine("# Density vs Average Stable Generation");
    sb.AppendLine($"# Trials per density: {trials}");
    sb.AppendLine($"# Grid: {_settings.Rows}x{_settings.Cols}");
    sb.AppendLine("Density\tAvgGen");
    foreach (var (d, avg) in results) sb.AppendLine($"{d:F2}\t{avg:F1}");
    File.WriteAllText(dataPath, sb.ToString());
    Console.WriteLine($"\nData saved → {dataPath}");

    // Plot PNG через ScottPlot
    var plotPath = Path.Combine(_dataDir, "plot.png");
    try
    {
        var plt = new ScottPlot.Plot(800, 400);
        double[] xs = results.Select(r => r.Density).ToArray();
        double[] ys = results.Select(r => r.AvgGen).ToArray();
        plt.AddScatterLines(xs, ys, System.Drawing.Color.SteelBlue, 2f);
        plt.AddScatterPoints(xs, ys, System.Drawing.Color.SteelBlue, 8f);
        plt.Title("Game of Life — Transition to Stable Phase");
        plt.XLabel("Density");
        plt.YLabel("Average generation");
        plt.SaveFig(plotPath);
        Console.WriteLine($"Plot saved → {plotPath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Plot error: {ex.Message}");
    }

    // ASCII chart
    Console.WriteLine("\nASCII chart (Density → Avg Gen):");
    double maxGen = results.Max(r => r.AvgGen);
    foreach (var (d, avg) in results)
    {
        int bars = (int)(avg / maxGen * 40);
        Console.WriteLine($" {d:P0} │{new string('█', bars)}{new string('░', 40 - bars)} {avg:F0}");
    }

    Console.WriteLine("\n[Enter] to continue...");
    Console.ReadLine();
}

        // ── 7–8. Settings ─────────────────────────────────────────────
        private static void ShowSettings()
        {
            try { Console.Clear(); } catch { }
            Console.WriteLine($"Rows={_settings.Rows}  Cols={_settings.Cols}");
            Console.WriteLine($"Delay={_settings.DelayMs}ms  Density={_settings.InitialDensity:P0}");
            Console.WriteLine($"MaxGenerations={_settings.MaxGenerations}  StableThreshold={_settings.StableThreshold}");
            Console.ReadLine();
        }

        private static void EditSettings()
        {
            var path = Path.Combine(_dataDir, "settings.json");
            Console.WriteLine("Open settings.json in editor? (y/n): ");
            if (Console.ReadLine()?.Trim().ToLower() == "y")
            {
                _settings.Save(path);
                Console.WriteLine($"File: {Path.GetFullPath(path)}");
                Console.WriteLine("Edit and save, then press [Enter] to reload.");
                Console.ReadLine();
                _settings = Settings.Load(path);
                _board = new Board(_settings.Rows, _settings.Cols);
                _board.Randomize(_settings.InitialDensity);
                Console.WriteLine("Settings reloaded. [Enter]");
            }
            Console.ReadLine();
        }
    }
}
