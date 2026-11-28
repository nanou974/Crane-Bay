// -----------------------------------------------------------------------------
// Crane Bay - Mini-jeu de détente : « Sortez le conteneur rouge ».
//
// Un parc de 6 x 6 cases rempli de conteneurs couchés (2 ou 3 cases), chacun
// orienté horizontalement ou verticalement et ne pouvant glisser que dans son
// sens. Le conteneur rouge, sur la 3e rangée, doit atteindre la sortie à
// droite. Un coup = un conteneur déplacé, quelle que soit la distance.
//
// Les niveaux sont générés (graine fixe par numéro de puzzle) puis résolus par
// un parcours en largeur : chaque puzzle est garanti résoluble et on connaît
// son nombre minimal de coups (pour la récompense "parfait").
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace CraneBay.Core
{
    public class PuzzlePiece
    {
        public bool horizontal;
        public int len;     // 2 ou 3 cases
        public int fixedAt; // rangée (horizontal) ou colonne (vertical)
        public int color;   // couleur d'affichage (ContainerColor), le rouge est réservé à la pièce 0

        public PuzzlePiece Clone() => new PuzzlePiece { horizontal = horizontal, len = len, fixedAt = fixedAt, color = color };
    }

    /// <summary>Récompense du mini-jeu : petite prime, doublée si résolu en un minimum de coups.</summary>
    public static class PuzzleRules
    {
        public static long Reward(int playerLevel, bool perfect) => (perfect ? 2 : 1) * 25L * Math.Max(1, playerLevel);
    }

    public class SlidePuzzle
    {
        public const int Size = 6;
        public const int ExitRow = 2;
        public const int Target = 0; // index du conteneur rouge
        const int Bits = 3;
        const ulong Mask = (1UL << Bits) - 1;
        const int MaxPieces = 64 / Bits;

        public readonly List<PuzzlePiece> pieces = new List<PuzzlePiece>();
        public int[] offsets;      // position mobile : colonne (horizontal) ou rangée (vertical)
        public int Moves { get; private set; }
        public int MinMoves { get; private set; }
        public int Index { get; private set; }

        int[] startOffsets;

        public bool IsSolved => offsets[Target] == Size - pieces[Target].len;

        public int Col(int i) => pieces[i].horizontal ? offsets[i] : pieces[i].fixedAt;
        public int Row(int i) => pieces[i].horizontal ? pieces[i].fixedAt : offsets[i];

        // =====================================================================
        // Jeu
        // =====================================================================

        /// <summary>Plage de positions atteignables par la pièce "i" sans traverser les autres.</summary>
        public void Range(int i, out int min, out int max)
        {
            var grid = Occupancy(offsets, i);
            var p = pieces[i];
            min = offsets[i];
            while (min > 0 && !Cell(grid, p, min - 1)) min--;
            max = offsets[i];
            while (max + p.len < Size && !Cell(grid, p, max + p.len)) max++;
        }

        /// <summary>Glisse la pièce "i" à la position "to" (un coup). Faux si bloqué.</summary>
        public bool Move(int i, int to)
        {
            if (i < 0 || i >= pieces.Count || IsSolved) return false;
            Range(i, out int min, out int max);
            if (to < min || to > max || to == offsets[i]) return false;
            offsets[i] = to;
            Moves++;
            return true;
        }

        public void Restart()
        {
            offsets = (int[])startOffsets.Clone();
            Moves = 0;
        }

        // =====================================================================
        // Génération
        // =====================================================================

        /// <summary>Coups minimaux visés pour le puzzle "index" (difficulté progressive).</summary>
        public static int TargetMoves(int index) => Math.Min(4 + index, 22);

        /// <summary>Puzzle n° "index" (même puzzle à chaque fois pour un même numéro).</summary>
        public static SlidePuzzle Generate(int index)
        {
            index = Math.Max(0, index);
            var rng = new Random(7919 * (index + 1) + 17);
            int target = TargetMoves(index);
            SlidePuzzle best = null;
            for (int attempt = 0; attempt < 150; attempt++)
            {
                int count = Math.Min(13, 7 + index / 3 + rng.Next(3));
                var p = RandomBoard(rng, count);
                if (p == null) continue;
                if (!p.PickHardStart(target)) continue;
                if (best == null || Math.Abs(p.MinMoves - target) < Math.Abs(best.MinMoves - target)) best = p;
                if (p.MinMoves >= target) break;
            }
            if (best == null) best = Fallback();
            best.Index = index;
            return best;
        }

        static SlidePuzzle RandomBoard(Random rng, int count)
        {
            var p = new SlidePuzzle();
            var offs = new List<int>();
            var grid = new bool[Size * Size];
            // Conteneur rouge : 2 cases sur la rangée de sortie, à gauche.
            p.pieces.Add(new PuzzlePiece { horizontal = true, len = 2, fixedAt = ExitRow, color = (int)ContainerColor.Rouge });
            offs.Add(rng.Next(2));
            Mark(grid, p.pieces[0], offs[0]);
            int[] colors = { (int)ContainerColor.Blanc, (int)ContainerColor.Bleu, (int)ContainerColor.Vert, (int)ContainerColor.Dore };
            for (int tries = 0; tries < 300 && p.pieces.Count < count; tries++)
            {
                var piece = new PuzzlePiece
                {
                    horizontal = rng.Next(2) == 0,
                    len = rng.Next(4) == 0 ? 3 : 2,
                    fixedAt = rng.Next(Size),
                    color = colors[rng.Next(colors.Length)],
                };
                // Un conteneur couché sur la rangée de sortie bloquerait le rouge pour toujours.
                if (piece.horizontal && piece.fixedAt == ExitRow) continue;
                int off = rng.Next(Size - piece.len + 1);
                if (Overlaps(grid, piece, off)) continue;
                p.pieces.Add(piece);
                offs.Add(off);
                Mark(grid, piece, off);
            }
            if (p.pieces.Count < 4 || p.pieces.Count > MaxPieces) return null;
            p.offsets = offs.ToArray();
            return p;
        }

        /// <summary>
        /// Explore toutes les positions atteignables depuis ce plateau, calcule la
        /// distance de chacune à la sortie, puis choisit comme départ une position
        /// au plus près de "target" coups (la plus difficile si aucune n'y arrive).
        /// </summary>
        bool PickHardStart(int target, int maxStates = 25000)
        {
            ulong start = Encode(offsets);
            var seen = new HashSet<ulong> { start };
            var order = new List<ulong> { start };
            var neighbors = new List<ulong>();
            for (int k = 0; k < order.Count; k++)
            {
                if (order.Count > maxStates) return false;
                Neighbors(order[k], neighbors);
                foreach (var n in neighbors) if (seen.Add(n)) order.Add(n);
            }

            // Distance à la sortie : parcours en largeur depuis toutes les positions gagnantes
            // (les mouvements sont réversibles).
            var dist = new Dictionary<ulong, int>(order.Count);
            var queue = new Queue<ulong>();
            foreach (var s in order)
                if (SolvedState(s)) { dist[s] = 0; queue.Enqueue(s); }
            if (queue.Count == 0) return false;
            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                int d = dist[s];
                Neighbors(s, neighbors);
                foreach (var n in neighbors)
                    if (!dist.ContainsKey(n)) { dist[n] = d + 1; queue.Enqueue(n); }
            }

            ulong pick = start;
            int pickD = -1;
            foreach (var kv in dist)
            {
                int d = kv.Value;
                bool better = pickD < 0
                    || (pickD < target && d > pickD)
                    || (d >= target && pickD >= target && d < pickD)
                    || (d == pickD && kv.Key < pick); // départage déterministe
                if (better) { pick = kv.Key; pickD = d; }
            }
            if (pickD < 1) return false;
            offsets = Decode(pick);
            startOffsets = (int[])offsets.Clone();
            MinMoves = pickD;
            Moves = 0;
            return true;
        }

        /// <summary>Nombre minimal de coups depuis la position actuelle (-1 si impossible).</summary>
        public int Solve(int maxStates = 200000)
        {
            ulong start = Encode(offsets);
            if (SolvedState(start)) return 0;
            var dist = new Dictionary<ulong, int> { [start] = 0 };
            var queue = new Queue<ulong>();
            queue.Enqueue(start);
            var neighbors = new List<ulong>();
            while (queue.Count > 0 && dist.Count < maxStates)
            {
                var s = queue.Dequeue();
                int d = dist[s];
                Neighbors(s, neighbors);
                foreach (var n in neighbors)
                {
                    if (dist.ContainsKey(n)) continue;
                    if (SolvedState(n)) return d + 1;
                    dist[n] = d + 1;
                    queue.Enqueue(n);
                }
            }
            return -1;
        }

        static SlidePuzzle Fallback()
        {
            // Puzzle simple de secours (ne devrait jamais servir).
            var p = new SlidePuzzle();
            p.pieces.Add(new PuzzlePiece { horizontal = true, len = 2, fixedAt = ExitRow, color = (int)ContainerColor.Rouge });
            p.pieces.Add(new PuzzlePiece { horizontal = false, len = 3, fixedAt = 4, color = (int)ContainerColor.Bleu });
            p.offsets = new[] { 0, 1 };
            p.startOffsets = (int[])p.offsets.Clone();
            p.MinMoves = 2;
            return p;
        }

        // =====================================================================
        // Outils internes
        // =====================================================================

        ulong Encode(int[] offs)
        {
            ulong s = 0;
            for (int i = 0; i < offs.Length; i++) s |= (ulong)offs[i] << (i * Bits);
            return s;
        }

        int[] Decode(ulong s)
        {
            var offs = new int[pieces.Count];
            for (int i = 0; i < offs.Length; i++) offs[i] = (int)((s >> (i * Bits)) & Mask);
            return offs;
        }

        bool SolvedState(ulong s) => (int)(s & Mask) == Size - pieces[Target].len;

        int[] nbOffs;
        readonly bool[] nbGrid = new bool[Size * Size];

        void Neighbors(ulong s, List<ulong> result)
        {
            result.Clear();
            int n = pieces.Count;
            if (nbOffs == null || nbOffs.Length != n) nbOffs = new int[n];
            Array.Clear(nbGrid, 0, nbGrid.Length);
            for (int i = 0; i < n; i++)
            {
                nbOffs[i] = (int)((s >> (i * Bits)) & Mask);
                Mark(nbGrid, pieces[i], nbOffs[i]);
            }
            for (int i = 0; i < n; i++)
            {
                var p = pieces[i];
                int o = nbOffs[i];
                ulong cleared = s & ~(Mask << (i * Bits));
                for (int t = o - 1; t >= 0 && !Cell(nbGrid, p, t); t--)
                    result.Add(cleared | ((ulong)t << (i * Bits)));
                for (int t = o + 1; t + p.len - 1 < Size && !Cell(nbGrid, p, t + p.len - 1); t++)
                    result.Add(cleared | ((ulong)t << (i * Bits)));
            }
        }

        bool[] Occupancy(int[] offs, int skip)
        {
            var grid = new bool[Size * Size];
            for (int i = 0; i < pieces.Count; i++) if (i != skip) Mark(grid, pieces[i], offs[i]);
            return grid;
        }

        /// <summary>La case numéro "t" sur l'axe de la pièce est-elle occupée ?</summary>
        static bool Cell(bool[] grid, PuzzlePiece p, int t)
        {
            int x = p.horizontal ? t : p.fixedAt;
            int y = p.horizontal ? p.fixedAt : t;
            return grid[y * Size + x];
        }

        static void Mark(bool[] grid, PuzzlePiece p, int off)
        {
            for (int k = 0; k < p.len; k++)
            {
                int x = p.horizontal ? off + k : p.fixedAt;
                int y = p.horizontal ? p.fixedAt : off + k;
                grid[y * Size + x] = true;
            }
        }

        static bool Overlaps(bool[] grid, PuzzlePiece p, int off)
        {
            for (int k = 0; k < p.len; k++)
            {
                int x = p.horizontal ? off + k : p.fixedAt;
                int y = p.horizontal ? p.fixedAt : off + k;
                if (grid[y * Size + x]) return true;
            }
            return false;
        }
    }
}
