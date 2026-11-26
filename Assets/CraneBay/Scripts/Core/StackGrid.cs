// -----------------------------------------------------------------------------
// Crane Bay - Grille de piles de conteneurs (parc de stockage, cales du navire).
// Règle d'empilage : on ne peut prendre QUE le conteneur du dessus d'une pile,
// et on ne peut pas dépasser la hauteur maximale. C'est le cœur du casse-tête.
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace CraneBay.Core
{
    /// <summary>Une pile verticale : index 0 = conteneur au sol.</summary>
    [Serializable]
    public class StackColumn
    {
        public List<ContainerInfo> items = new List<ContainerInfo>();
    }

    /// <summary>
    /// Grille de "columns x rows" emplacements, chacun pouvant contenir une pile
    /// de "maxHeight" conteneurs au plus. Sérialisable par JsonUtility.
    /// </summary>
    [Serializable]
    public class StackGrid
    {
        public int columns;
        public int rows;
        public int maxHeight;
        public List<StackColumn> stacks = new List<StackColumn>();

        public StackGrid() { }

        public StackGrid(int columns, int rows, int maxHeight)
        {
            this.columns = columns;
            this.rows = rows;
            this.maxHeight = maxHeight;
            stacks = new List<StackColumn>(columns * rows);
            for (int i = 0; i < columns * rows; i++) stacks.Add(new StackColumn());
        }

        /// <summary>Nombre total d'emplacements au sol.</summary>
        public int SlotCount => columns * rows;

        /// <summary>Vérifie que la grille chargée correspond aux dimensions attendues.</summary>
        public bool HasShape(int c, int r, int h)
        {
            return columns == c && rows == r && maxHeight == h && stacks != null && stacks.Count == c * r;
        }

        public bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        public int SlotIndex(int column, int row) => row * columns + column;
        public int ColumnOf(int slot) => slot % columns;
        public int RowOf(int slot) => slot / columns;

        /// <summary>Hauteur actuelle de la pile.</summary>
        public int Height(int slot) => IsValidSlot(slot) ? stacks[slot].items.Count : 0;

        /// <summary>Conteneur au sommet de la pile, ou null si vide.</summary>
        public ContainerInfo Top(int slot)
        {
            if (!IsValidSlot(slot)) return null;
            var items = stacks[slot].items;
            return items.Count > 0 ? items[items.Count - 1] : null;
        }

        /// <summary>Conteneur à un niveau donné (0 = sol), ou null.</summary>
        public ContainerInfo At(int slot, int tier)
        {
            if (!IsValidSlot(slot)) return null;
            var items = stacks[slot].items;
            return tier >= 0 && tier < items.Count ? items[tier] : null;
        }

        /// <summary>La grue peut-elle saisir quelque chose ici ? (uniquement le dessus)</summary>
        public bool CanPick(int slot) => Height(slot) > 0;

        /// <summary>Reste-t-il de la place en hauteur sur cette pile ?</summary>
        public bool CanPlace(int slot) => IsValidSlot(slot) && Height(slot) < maxHeight;

        /// <summary>Retire et renvoie le conteneur du dessus (null si impossible).</summary>
        public ContainerInfo Pick(int slot)
        {
            if (!CanPick(slot)) return null;
            var items = stacks[slot].items;
            var top = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            return top;
        }

        /// <summary>Pose un conteneur au sommet. Renvoie false si la pile est pleine.</summary>
        public bool Place(int slot, ContainerInfo container)
        {
            if (container == null || !CanPlace(slot)) return false;
            stacks[slot].items.Add(container);
            return true;
        }

        /// <summary>Nombre total de conteneurs dans la grille.</summary>
        public int TotalCount()
        {
            int n = 0;
            for (int i = 0; i < stacks.Count; i++) n += stacks[i].items.Count;
            return n;
        }

        public int Capacity => SlotCount * maxHeight;
        public bool IsEmpty => TotalCount() == 0;

        /// <summary>Compte les conteneurs de chaque couleur (ajoute au tableau fourni).</summary>
        public void AddColorCounts(int[] counts)
        {
            for (int i = 0; i < stacks.Count; i++)
                foreach (var c in stacks[i].items)
                    if (c.color >= 0 && c.color < counts.Length) counts[c.color]++;
        }

        /// <summary>
        /// Nombre de conteneurs posés au-dessus du premier conteneur de la couleur
        /// demandée dans la pile (utile pour l'aide visuelle). -1 si absent.
        /// </summary>
        public int BlockersAbove(int slot, ContainerColor color)
        {
            if (!IsValidSlot(slot)) return -1;
            var items = stacks[slot].items;
            for (int t = items.Count - 1; t >= 0; t--)
                if (items[t].color == (int)color) return items.Count - 1 - t;
            return -1;
        }
    }
}
