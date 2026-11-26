// -----------------------------------------------------------------------------
// Crane Bay - Disposition du terminal en coordonnées monde (vue de dessus,
// orientation PORTRAIT). Largeur utile ~10 unités, de haut en bas :
//   mer + navire à quai  |  quai  |  parc de stockage  |  route camions  |  voie ferrée
// Les interfaces (argent, coffres en haut ; améliorations en bas) recouvrent
// les marges hautes et basses.
// -----------------------------------------------------------------------------
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public static class BoardLayout
    {
        // Taille d'un conteneur (40 pieds stylisé) vu de dessus.
        public static readonly Vector2 ContainerSize = new Vector2(1.6f, 0.75f);

        // Décalage vertical par niveau d'empilage : les conteneurs du dessous
        // dépassent légèrement, ce qui rend la hauteur de pile lisible.
        public const float TierOffsetY = 0.11f;
        public const float TierScale = 0.035f;

        // Demi-largeur visible garantie (le cadrage caméra s'y adapte).
        public const float HalfWidth = 5.1f;
        public const float MinHalfHeight = 8.9f;

        // --- Mer et navire ---
        public const float WaterBottom = 3.9f;
        public static readonly Vector3 ShipDock = new Vector3(0f, 5.4f, 0f);
        public const float ShipOffscreenX = 16f;
        const float ShipSlotSpacingX = 1.85f;
        const float ShipSlotSpacingY = 0.96f;
        const float ShipSlotCenterX = -0.55f;

        // --- Quai ---
        public const float QuayTop = 3.9f;
        public const float QuayBottom = 3.0f;

        // --- Parc ---
        const float YardSpacingX = 1.82f;
        const float YardSpacingY = 1.0f;
        const float YardTopRowY = 2.3f;
        public const float YardBottom = -1.45f;

        // --- Route ---
        public const float RoadY = -2.3f;
        public static readonly float[] TruckBayX = { -1.9f, 2.7f };
        public const float TruckCabOffset = -1.15f; // la cabine est à gauche (sens de marche)

        // --- Voie ferrée ---
        public const float RailY = -3.95f;
        public const float LocoX = -3.85f;
        public const float WagonSpacing = 1.85f;
        public const float FirstWagonX = -2.05f;

        // --- Grue ---
        public const float CraneLegX = 4.95f;
        public static readonly Vector2 CraneRest = new Vector2(0f, 0.8f);

        public static Vector3 YardSlotCenter(int slot)
        {
            int c = slot % GameRules.YardColumns;
            int r = slot / GameRules.YardColumns;
            float x = (c - (GameRules.YardColumns - 1) * 0.5f) * YardSpacingX;
            float y = YardTopRowY - r * YardSpacingY;
            return new Vector3(x, y, 0f);
        }

        /// <summary>Position locale (dans le repère du navire) d'un emplacement de cale.</summary>
        public static Vector3 ShipSlotLocal(int slot)
        {
            int c = slot % GameRules.ShipColumns;
            int r = slot / GameRules.ShipColumns;
            float x = ShipSlotCenterX + (c - (GameRules.ShipColumns - 1) * 0.5f) * ShipSlotSpacingX;
            float y = ((GameRules.ShipRows - 1) * 0.5f - r) * ShipSlotSpacingY;
            return new Vector3(x, y, 0f);
        }

        /// <summary>Décalage visuel d'un conteneur selon son niveau dans la pile.</summary>
        public static Vector3 TierOffset(int tier) => new Vector3(0f, tier * TierOffsetY, 0f);

        public static float TierScaleOf(int tier) => 1f + tier * TierScale;

        public static Vector3 WagonCenter(int index) => new Vector3(FirstWagonX + index * WagonSpacing, RailY, 0f);

        /// <summary>Taille de la zone tactile d'une pile (plus grande que le conteneur pour les doigts).</summary>
        public static readonly Vector2 SlotHitSize = new Vector2(1.8f, 0.98f);

        public static bool Inside(Vector2 p, Vector2 center, Vector2 size)
        {
            return Mathf.Abs(p.x - center.x) <= size.x * 0.5f && Mathf.Abs(p.y - center.y) <= size.y * 0.5f;
        }
    }
}
