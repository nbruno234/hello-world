using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Conway
{
    public class GridUtils
    {
        public static void FillRandom(bool[,] grid, double ratio, int seed = 1234)
        {
            Random random = new Random(seed);
            for (int i = 0; i < grid.GetLength(0); ++i)
            {
                for (int j = 0; j < grid.GetLength(1); ++j)
                {
                    grid[i, j] = random.NextDouble() <= ratio;
                }
            }
        }

        public static void AddGlider(bool[,] grid, int i, int j)
        {
            Debug.Assert(i + 2 < grid.GetLength(0) && j + 2 < grid.GetLength(1));
            grid[i + 1, j] = grid[i + 2, j + 1] = grid[i, j + 2] = grid[i + 1, j + 2] = grid[i + 2, j + 2] = true;
        }

        public static void AddPentoR(bool[,] grid, int i, int j)
        {
            Debug.Assert(i + 2 < grid.GetLength(0) && j + 2 < grid.GetLength(1));
            grid[i + 1, j] = grid[i + 2, j] = grid[i, j + 1] = grid[i + 1, j + 1] = grid[i + 1, j + 2] = true;
        }
    }

    public abstract class Life
    {
        // Name.
        public abstract string Name();

        // Gets underlying grid even if the internal implementation is different.
        public abstract bool[,] ToGrid();

        // Next generation.
        public abstract Life Next();

        // Size of grid (overload if implementation is not direct).
        public virtual long Size()
        {
            Debug.Assert(ToGrid().GetLongLength(0) == ToGrid().GetLongLength(1));
            return ToGrid().GetLongLength(0);
        }

        // How many generations are advanced per invocation to Next? (Normal algorithms use 1).
        public virtual long GenerationStep()
        {
            return 1;
        }

        // Coordinates of alive cells (overload if implementation is not direct).
        public virtual IEnumerable<Tuple<long, long>> AliveCoordinates()
        {
            bool[,] grid = ToGrid();
            for (long i = 0; i < Size(); i++)
            {
                for (long j = 0; j < Size(); j++)
                {
                    if (grid[i, j])
                    {
                        yield return Tuple.Create(i, j);
                    }
                }
            }

        }

        // Cheap print to screen for debugging.
        public void Print(ConsoleColor color = ConsoleColor.Green) {
            Console.ForegroundColor = color;
            Console.CursorVisible = false;

            bool[,] grid = ToGrid();
            for (int i = 0; i < grid.GetLength(0); ++i)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    Console.Write(grid[j, i] ? 'X' : ' ');
                }
                Console.WriteLine();
            }
        }

        public static void Animate(Life life, bool pause = false)
        {
            while (true)
            {
                Console.SetCursorPosition(0, 0);
                life.Print();
                life = life.Next();
                if (pause)
                {
                    Console.ReadLine();
                }
            }
        }

        // Signature independent of grid size for validation.
        public string Signature()
        {
            var coords = AliveCoordinates().ToList();
            if (coords.Count == 0)
            {
                return string.Empty;
            }

            // Signature is sorted, normalized list of alive coordinates, separated by ','.
            long minX = coords.Min(coord => coord.Item1);
            long minY = coords.Min(coord => coord.Item2);
            var normCoords =
                coords.
                Select(coord => string.Format("{0}:{1}", coord.Item1 - minX, coord.Item2 - minY)).
                OrderBy(str => str).
                ToList();

            return string.Join(",", normCoords);
        }

        // Number of alive cells.
        public long AliveCount()
        {
            return AliveCoordinates().Count();
        }

        // Validate two instances for a while.
        public static bool Validate(Life life1, Life life2, int seconds = 10, long minGenerations = 1000)
        {
            if (life1.Signature() != life2.Signature())
            {
                Console.WriteLine("Invalid initial state!");
                return false;
            }

            Stopwatch sw = new Stopwatch();
            sw.Start();
            int nextShow = 1000;
            long gen1 = 0;
            long gen2 = 0;
            long lastGoodGen = 0;
            while(lastGoodGen < minGenerations || sw.ElapsedMilliseconds < seconds * 1000)
            {
                if (sw.ElapsedMilliseconds > nextShow)
                {
                    nextShow += 1000;
                    Console.Write("Validating ok up to {0} generations, {1}[{2}] vs {3}[{4}]\t\r", 
                        lastGoodGen, life1.Name(), gen1, life2.Name(), gen2);
                }

                if (gen1 < gen2)
                {
                    life1 = life1.Next();
                    gen1 += life1.GenerationStep();
                }
                else if (gen1 > gen2)
                {
                    life2 = life2.Next();
                    gen2 += life2.GenerationStep();
                }
                else
                {
                    // Same, compare and advance
                    string sig1 = life1.Signature();
                    string sig2 = life2.Signature();
                    if (sig1 != sig2)
                    {
                        Console.WriteLine("Validation failed at generation {0}\n{1}: {2}\n{3}: {4}",
                            gen1, life1.Name(), sig1, life2.Name(), sig2);
                        return false;
                    }

                    lastGoodGen = gen1;
                    life1 = life1.Next();
                    life2 = life2.Next();
                    gen1 += life1.GenerationStep();
                    gen2 += life2.GenerationStep();
                }
            }

            Console.WriteLine("Validation passed up to {0} generations, {1} vs {2}\t\t\t",
                lastGoodGen, life1.Name(), life2.Name());
            return true;
        }

        // Any extra information to print while measuring?
        public virtual string ExtraInfo()
        {
            return string.Empty;
        }

        // Print large or small numbers with units.
        // https://en.wikipedia.org/wiki/Metric_prefix#List_of_SI_prefixes
        public static string numberUnits(double n, string unit)
        {
            string[] prefix = new string[] { "Y", "Z", "E", "P", "T", "G", "M", "K", "", "m", "u", "n", "p", "f", "a", "z", "y"};
            int idx = 0;
            n *= 1E-24;
            while (n < 1 && ++idx < prefix.Count() - 1) n *= 1E3;
            return string.Format("{0:0.00}\t{1}{2}", n, prefix[idx], unit);
        }

        // Measure a given algorithm.
        public static void Measure(ref Life life, int seconds)
        {
            // Clear the global cache to measure all algorithms in a fair way.
            NodeCache.Clear();

            var sw = new Stopwatch();
            long iter = 0;
            int nextShow = 1000;
            bool done = false;
            while (!done)
            {
                iter++;

                sw.Start();
                life = life.Next();
                sw.Stop();

                long elapsed = sw.ElapsedMilliseconds;
                done = elapsed >= 1000 * seconds;
                if (elapsed > nextShow || done)
                {
                    double generation = (double)iter * life.GenerationStep();
                    Console.Write("[{0}]\t{1},\t{2}\t{3}\t\r",
                        string.Format("{0} | {1}^2", life.Name(), life.Size()).PadRight(32, ' '),
                        numberUnits(elapsed / 1000.0 / generation, "Secs"),
                        numberUnits(generation * 1000.0 / Math.Max(1, elapsed), "LOPs"),
                        life.ExtraInfo());
                    nextShow += 1000;
                }
            }
            Console.WriteLine();
        }
    }

    // Memoization class for nodes.
    public class NodeCache
    {
        // Cache of nodes, and special case for empty nodes.
        private static Dictionary<Node, Node> cache = new Dictionary<Node,Node>();
        private static Dictionary<int, Node> zeroCache = new Dictionary<int, Node>();

        public static string Size { get { return String.Format("{0}+z{1}", cache.Count, zeroCache.Count); } }

        public static void Clear()
        {
            cache.Clear();
            zeroCache.Clear();
        }

        public static Node Get(Node input)
        {
            if (cache.TryGetValue(input, out Node output))
            {
                return output;
            }
            cache.Add(input, input);
            return input;
        }

        public static Node GetZero(int level)
        {
            if (zeroCache.TryGetValue(level, out Node output))
            {
                return output;
            }

            Node result;
            if (level == 0)
            {
                result = Node.Create(false /*alive*/);
            }
            else
            {
                Node subZeroNode = GetZero(level - 1);
                result = Node.Create(subZeroNode, subZeroNode, subZeroNode, subZeroNode);
            }
            zeroCache[level] = result;
            return result;
        }
    }

    public class Node
    {
        // Quadrants.
        private Node nw, ne, sw, se;
        // Memoized optional result.
        private Node result;
        // Base case, single bit (reused as WarpMode for non-base nodes).
        private bool alive;

        // Empty/level are computed columns.
        private bool isEmpty;
        public int Level { get; }

        // Private constructor.
        private Node(Node nw, Node ne, Node sw, Node se, Node result, bool alive)
        {
            Debug.Assert((nw == null) == (ne == null));
            Debug.Assert((nw == null) == (se == null));
            Debug.Assert((nw == null) == (se == null));
            Debug.Assert(nw == null || (nw.Level == ne.Level && nw.Level == sw.Level && nw.Level == se.Level));
            this.nw = nw;
            this.ne = ne;
            this.sw = sw;
            this.se = se;
            this.result = result;
            this.Level = nw == null ? 0 : nw.Level + 1;
            this.alive = alive;
            this.isEmpty = nw == null ? !alive : (nw.isEmpty && ne.isEmpty && sw.isEmpty && se.isEmpty);
        }

        // Create base case (memoized).
        public static Node Create(bool alive)
        {
            Node result = new Node(null, null, null, null, null, alive);
            return NodeCache.Get(result);
        }

        // Create quadrant (memoized).
        public static Node Create(Node nw, Node ne, Node sw, Node se, bool warpMode)
        {
            // We distinguish warp internal nodes by being 'alive' for the purpose of memoization.
            Node result = new Node(nw, ne, sw, se, null, warpMode /*alive*/);
            return NodeCache.Get(result);
        }

        // Create quadrant without warp mode (memoized).
        public static Node Create(Node nw, Node ne, Node sw, Node se)
        {
            Node result = new Node(nw, ne, sw, se, null, false /*alive*/);
            return NodeCache.Get(result);
        }

        // Create quadrant for warp mode (memoized).
        public static Node CreateWarp(Node nw, Node ne, Node sw, Node se)
        {
            Node result = new Node(nw, ne, sw, se, null, true /*alive*/);
            return NodeCache.Get(result);
        }

        // Simple helpers (is this base cell alive? what is the numeric value? when is the next generation alive?)
        public bool Alive { get { Debug.Assert(Level == 0); return alive; } }
        public bool IsWarp {  get { Debug.Assert(Level > 0); return alive; } }
        public int AV { get { return Alive ? 1 : 0; }  }
        public bool NextAlive(int sum) { return sum == 3 || (alive && sum==2); }

        // Main logic for the base case (shared in warp/nonWarp mode).
        public Node NextBase()
        {
            // base 4x4 grid, resulting in center 2x2.
            Debug.Assert(Level == 2);
            int sumnw = nw.nw.AV + nw.ne.AV + ne.nw.AV + nw.sw.AV + ne.sw.AV + sw.nw.AV + sw.ne.AV + se.nw.AV;
            int sumne = nw.ne.AV + ne.nw.AV + ne.ne.AV + nw.se.AV + ne.se.AV + sw.ne.AV + se.nw.AV + se.ne.AV;
            int sumsw = nw.sw.AV + nw.se.AV + ne.sw.AV + sw.nw.AV + se.nw.AV + sw.sw.AV + sw.se.AV + se.sw.AV;
            int sumse = nw.se.AV + ne.sw.AV + ne.se.AV + sw.ne.AV + se.ne.AV + sw.se.AV + se.sw.AV + se.se.AV;
            return Node.Create(
                Node.Create(nw.se.NextAlive(sumnw)),
                Node.Create(ne.sw.NextAlive(sumne)),
                Node.Create(sw.ne.NextAlive(sumsw)),
                Node.Create(se.nw.NextAlive(sumse)));
        }

        // Shredding and assembling sub-fragments for non-warp mode.
        public Node centeredSubnode() { return Node.Create(nw.se, ne.sw, sw.ne, se.nw); }
        public Node centeredHorizontal(Node w, Node e) { return Node.Create(w.ne.se, e.nw.sw, w.se.ne, e.sw.nw); }
        public Node centeredVertical(Node n, Node s) { return Node.Create(n.sw.se, n.se.sw, s.nw.ne, s.ne.nw); }
        public Node centeredSubSubnode() { return Node.Create(nw.se.se, ne.sw.sw, sw.ne.ne, se.nw.nw); }
        public Node NextSimple()
        {
            Debug.Assert(Level >= 2);
            if (result != null)
            {
                return result; // cached already.
            }
            else if (Level == 2)
            {
                result = NextBase();
            }
            else
            {
                // recursive case, exploding and reassembling.
                Node n00 = nw.centeredSubnode(),
                     n01 = centeredHorizontal(nw, ne),
                     n02 = ne.centeredSubnode(),
                     n10 = centeredVertical(nw, sw),
                     n11 = centeredSubSubnode(),
                     n12 = centeredVertical(ne, se),
                     n20 = sw.centeredSubnode(),
                     n21 = centeredHorizontal(sw, se),
                     n22 = se.centeredSubnode();
                result = Node.Create(
                   Node.Create(n00, n01, n10, n11).NextSimple(),
                   Node.Create(n01, n02, n11, n12).NextSimple(),
                   Node.Create(n10, n11, n20, n21).NextSimple(),
                   Node.Create(n11, n12, n21, n22).NextSimple());
            }

            return result;
        }

        // Shredding and assembling sub-fragments for warp mode.
        public Node horizontalWarp(Node w, Node e) { return Node.CreateWarp(w.ne, e.nw, w.se, e.sw).NextWarp(); }
        public Node verticalWarp(Node n, Node s) { return Node.CreateWarp(n.sw, n.se, s.nw, s.ne).NextWarp(); }
        public Node NextWarp()
        {
            Debug.Assert(Level >= 2);
            if (result != null)
            {
                return result; // cached already.
            }
            else if (Level == 2)
            {
                result = NextBase();
            }
            else
            {
                Node n00 = nw.NextWarp(),
                     n01 = horizontalWarp(nw, ne),
                     n02 = ne.NextWarp(),
                     n10 = verticalWarp(nw, sw),
                     n11 = Node.CreateWarp(nw.se, ne.sw, sw.ne, se.nw).NextWarp(),
                     n12 = verticalWarp(ne, se),
                     n20 = sw.NextWarp(),
                     n21 = horizontalWarp(sw, se),
                     n22 = se.NextWarp();
                result = Node.CreateWarp(
                    Node.CreateWarp(n00, n01, n10, n11).NextWarp(),
                    Node.CreateWarp(n01, n02, n11, n12).NextWarp(),
                    Node.CreateWarp(n10, n11, n20, n21).NextWarp(),
                    Node.CreateWarp(n11, n12, n21, n22).NextWarp());
            }

            return result;
        }

        // Fills up grid starting on position (i,j).
        public void ToGridHelper(bool[,] grid, long i, long j)
        {
            if (Level == 0)
            {
                grid[i, j] = this.Alive;
            }
            else
            {
                long step = (long)Math.Pow(2, Level - 1);
                nw.ToGridHelper(grid, i, j);
                ne.ToGridHelper(grid, i + step, j);
                sw.ToGridHelper(grid, i, j + step);
                se.ToGridHelper(grid, i + step, j + step);
            }
        }

        // Fills up coordinates starting on position (i,j).
        public void AliveCoordinatesHelper(long i, long j, List<Tuple<long, long>> result)
        {
            if (isEmpty)
            {
                return;
            }

            if (Level == 0)
            {
                if (Alive)
                {
                    result.Add(Tuple.Create(i, j));
                }
            }
            else
            {
                long step = (long)Math.Pow(2, Level - 1);
                nw.AliveCoordinatesHelper(i, j, result);
                ne.AliveCoordinatesHelper(i + step, j, result);
                sw.AliveCoordinatesHelper(i, j + step, result);
                se.AliveCoordinatesHelper(i + step, j + step, result);
            }
        }

        // Helper to create from given grid.
        public static Node FromGrid(bool[,] grid, int x, int y, int size, bool warpMode)
        {
            if (size == 1)
            {
                // crop from max to power of two.
                bool alive = x < grid.GetLength(0) && y < grid.GetLength(1) ? grid[x, y] : false;
                return Node.Create(alive);
            }

            int newSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(size) / Math.Log(2)) - 1);
            return Node.Create(
                FromGrid(grid, x, y, newSize, warpMode),
                FromGrid(grid, x + newSize, y, newSize, warpMode),
                FromGrid(grid, x, y + newSize, newSize, warpMode),
                FromGrid(grid, x + newSize, y + newSize, newSize, warpMode),
                warpMode);
        }

        // Pads a grid with an empty dead layer growing to twice the grid size.
        public Node ZeroPad()
        {
            Debug.Assert(Level > 0);
            Node zero = NodeCache.GetZero(Level - 1);
            return Node.Create(
                Node.Create(zero, zero, zero, nw, IsWarp),
                Node.Create(zero, zero, ne, zero, IsWarp),
                Node.Create(zero, sw, zero, zero, IsWarp),
                Node.Create(se, zero, zero, zero, IsWarp),
                IsWarp);
        }

        // Pads a grid with a torus representation growing to twice the grid size.
        public Node TorusPad()
        {
            Debug.Assert(Level > 0);
            return Node.Create(
                Node.Create(se, sw, ne, nw, IsWarp),
                Node.Create(se, sw, ne, nw, IsWarp),
                Node.Create(se, sw, ne, nw, IsWarp),
                Node.Create(se, sw, ne, nw, IsWarp),
                IsWarp
                );
        }

        // Prunes a grid to the smallest one without dead layers.
        public Node ZeroPrune()
        {
            if (Level <= 2) return this;
            if (nw.isEmpty && ne.isEmpty && sw.isEmpty) return se.ZeroPrune();
            if (nw.isEmpty && ne.isEmpty && se.isEmpty) return sw.ZeroPrune();
            if (nw.isEmpty && se.isEmpty && sw.isEmpty) return ne.ZeroPrune();
            if (se.isEmpty && ne.isEmpty && sw.isEmpty) return nw.ZeroPrune();
            if (!nw.nw.isEmpty || !nw.ne.isEmpty || !nw.sw.isEmpty ||
                !ne.nw.isEmpty || !ne.ne.isEmpty || !ne.se.isEmpty ||
                !sw.nw.isEmpty || !sw.sw.isEmpty || !sw.se.isEmpty ||
                !se.ne.isEmpty || !se.sw.isEmpty || !se.se.isEmpty)
            {
                return this;
            }
            return Node.Create(nw.se, ne.sw, sw.ne, se.nw, IsWarp).ZeroPrune();
        }

        // Equals/Hash for memoization
        public override bool Equals(object obj)
        {
            var node = obj as Node;
            return node != null &&
                   object.ReferenceEquals(nw, node.nw) &&
                   object.ReferenceEquals(sw, node.sw) &&
                   object.ReferenceEquals(ne, node.ne) &&
                   object.ReferenceEquals(se, node.se) &&
                   alive == node.alive;
        }

        public override int GetHashCode()
        {
            int h = -271263922;
            h = h * -1521134295 + RuntimeHelpers.GetHashCode(nw);
            h = h * -1521134295 + RuntimeHelpers.GetHashCode(ne);
            h = h * -1521134295 + RuntimeHelpers.GetHashCode(sw);
            h = h * -1521134295 + RuntimeHelpers.GetHashCode(se);
            h = h * -1521134295 + alive.GetHashCode();
            return h;
        }
    }

    public class NodeLife : Life
    {
        public enum Mode { Torus, Cropped, Open, Warp };
        public Mode mode;

        Node root;

        public NodeLife(Node root, Mode mode)
        {
            this.root = root;
            this.mode = mode;
        }

        public override string Name()
        {
            switch (mode)
            {
                case Mode.Open:
                    return "Open";
                case Mode.Torus:
                    return "Torus";
                case Mode.Cropped:
                    return "Cropped";
                case Mode.Warp:
                    return String.Format("Warp{0}", root.Level - 1);
                default:
                    Debug.Assert(false);
                    return "Invalid";
            }
        }

        // Create from given grid.
        public static NodeLife Create(bool[,] grid, Mode mode, int warpModeLevel = 0)
        {
            Debug.Assert(grid.GetLength(0) == grid.GetLength(1));
            Node root = Node.FromGrid(grid, 0, 0, grid.GetLength(0), mode == Mode.Warp);
            while (mode == Mode.Warp && root.Level < warpModeLevel)
            {
                root = root.ZeroPad();
            }

            return new NodeLife(root, mode);
        }

        // Size (2^level)
        public override long Size()
        {
            return (long)Math.Pow(2, root.Level);
        }

        public override long GenerationStep()
        {
            return mode == Mode.Warp ? Size() / 2 : 1;
        }

        // Extrainfo on cache state
        public override string ExtraInfo()
        {
            return String.Format("Cache: {0}", NodeCache.Size);
        }

        // Fills up grid.
        public override bool[,] ToGrid()
        {
            bool[,] grid = new bool[Size(), Size()];
            root.ToGridHelper(grid, 0, 0);
            return grid;
        }

        // Returns alive coordinates.
        public override IEnumerable<Tuple<long, long>> AliveCoordinates()
        {
            var result = new List<Tuple<long, long>>();
            root.AliveCoordinatesHelper(0, 0, result);
            return result;
        }

        public override Life Next()
        {
            Node nextRoot;
            switch (mode)
            {
                case Mode.Torus:
                    nextRoot = root.TorusPad().NextSimple();
                    break;
                case Mode.Cropped:
                    nextRoot = root.ZeroPad().NextSimple();
                    break;
                case Mode.Open:
                    nextRoot = root.ZeroPad().ZeroPad().NextSimple().ZeroPrune();
                    break;
                case Mode.Warp:
                    nextRoot = root.ZeroPad().NextWarp();
                    break;
                default:
                    Debug.Assert(false);
                    return null;
            }

            return new NodeLife(nextRoot, mode);
        }
    }

    // Naive implementation with grids.
    public class GridLife : Life
    {
        private bool[,] grid = null;

        public override string Name() { return "Grid"; }

        // Creates board.
        public static Life Create(bool[,] grid)
        {
            return new GridLife { grid = grid };
        }

        // Returns board.
        public override bool[,] ToGrid()
        {
            return grid;
        }

        // Advances board.
        public override Life Next()
        {
            long size = Size();
            bool[,] newgrid = new bool[size, size];
            for (int i = 0; i < size; i++)
            {
                long im = (i - 1 + size) % size;
                long ip = (i + 1) % size;
                for (int j = 0; j < size; j++)
                {
                    long jm = (j - 1 + size) % size;
                    long jp = (j + 1) % size;
                    int sum = (grid[im, jm] ? 1 : 0) +
                              (grid[im, j] ? 1 : 0) +
                              (grid[im, jp] ? 1 : 0) +
                              (grid[i, jm] ? 1 : 0) +
                              (grid[i, jp] ? 1 : 0) +
                              (grid[ip, jm] ? 1 : 0) +
                              (grid[ip, j] ? 1 : 0) +
                              (grid[ip, jp] ? 1 : 0);
                    newgrid[i, j] = sum == 3 || (grid[i, j] && sum == 2);
                }
            }

            return GridLife.Create(newgrid);
        }
    }

    public class Program
    {
        static void Main(string[] args)
        {
            // Create grid.
            const int N = 32;
            bool[,] grid = new bool[N, N];

            // Fill it up with something.
            //GridUtils.AddGlider(grid, N / 2, N / 2);
            //GridUtils.AddPentoR(grid, N / 2, N / 2);
            GridUtils.FillRandom(grid, 0.2, 11235);

            // Create candidates.
            Life gridLife = GridLife.Create(grid);
            Life nodeCropped = NodeLife.Create(grid, NodeLife.Mode.Cropped);
            Life nodeTorus = NodeLife.Create(grid, NodeLife.Mode.Torus);
            Life nodeOpen = NodeLife.Create(grid, NodeLife.Mode.Open);
            Life nodeWarp = NodeLife.Create(grid, NodeLife.Mode.Warp);
            Life nodeWarp16 = NodeLife.Create(grid, NodeLife.Mode.Warp, 16);
            Life nodeWarp32 = NodeLife.Create(grid, NodeLife.Mode.Warp, 32);
            Life nodeWarp60 = NodeLife.Create(grid, NodeLife.Mode.Warp, 60);
            
            // Animate one?
            //Life.Animate(gridLife, false /*pause*/);

            // Some validation.
            Life.Validate(gridLife, nodeTorus, 5);
            Life.Validate(nodeOpen, nodeWarp16, 5);
            //Life.Validate(nodeWarp, nodeWarp16, 5); // should fail when warp grid is cropped.
            //Life.Validate(gridLife, nodeOpen, 5);   // should fail when gridLife wraps around

            // Performance.
            Life[] candidates = new Life[] {
                gridLife,
                nodeTorus,
                nodeOpen,
                nodeWarp,
                nodeWarp16,
                nodeWarp32,
                nodeWarp60
            };

            while (true)
            {
                Console.WriteLine();
                for (int i = 0; i < candidates.Count(); ++i)
                {
                    Life.Measure(ref candidates[i], 5);
                }
            }
        }
    }
}
