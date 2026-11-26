// -----------------------------------------------------------------------------
// Crane Bay - Palette de couleurs.
// Style "animation japonaise" : couleurs vives et franches, contours sombres
// nets (gérés dans SpriteFactory), ombres douces. Tout est centralisé ici pour
// ajuster l'ambiance sans toucher au reste du code.
// -----------------------------------------------------------------------------
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public static class Palette
    {
        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // --- Décor ---
        public static readonly Color Water = Hex("#3BA7D9");
        public static readonly Color WaterLight = Hex("#8FD8F2");
        public static readonly Color Quay = Hex("#E3DCCB");
        public static readonly Color QuayEdge = Hex("#2B3345");
        public static readonly Color Asphalt = Hex("#8C98A6");
        public static readonly Color YardMarking = Hex("#FFF1A8");
        public static readonly Color Road = Hex("#5E6878");
        public static readonly Color RoadMarking = Hex("#F7F7F2");
        public static readonly Color Ballast = Hex("#B39E86");
        public static readonly Color Sleeper = Hex("#6E4F3A");
        public static readonly Color Rail = Hex("#DDE3EA");
        public static readonly Color Grass = Hex("#7DCB6A");
        public static readonly Color GrassDark = Hex("#5DAE52");
        public static readonly Color Outline = Hex("#1F2433");
        public static readonly Color Shadow = new Color(0.08f, 0.10f, 0.18f, 0.35f);

        // --- Grue ---
        public static readonly Color Crane = Hex("#FFB627");
        public static readonly Color CraneDark = Hex("#E08A00");
        public static readonly Color Trolley = Hex("#FF6B35");

        // --- Navire ---
        public static readonly Color Hull = Hex("#D63A45");
        public static readonly Color Deck = Hex("#33415C");
        public static readonly Color Bridge = Hex("#F7F9FC");
        public static readonly Color Window = Hex("#3C6FB6");

        // --- Véhicules ---
        public static readonly Color[] TruckCabs = { Hex("#4BA3F0"), Hex("#F06B4B"), Hex("#8C6CF0"), Hex("#22B8A6") };
        public static readonly Color Chassis = Hex("#3A404D");
        public static readonly Color Loco = Hex("#2E9E63");
        public static readonly Color LocoStripe = Hex("#FFD23F");
        public static readonly Color Wagon = Hex("#4A5262");

        // --- Interface ---
        public static readonly Color UiPanel = new Color(0.12f, 0.14f, 0.20f, 0.92f);
        public static readonly Color UiButton = Hex("#3D5A80");
        public static readonly Color UiButtonOff = Hex("#5B6475");
        public static readonly Color UiGold = Hex("#FFD23F");
        public static readonly Color UiGreen = Hex("#3DDC84");
        public static readonly Color UiText = Hex("#FFFFFF");
        public static readonly Color Highlight = Hex("#FFFFFF");
        public static readonly Color ValidTarget = Hex("#3DDC84");

        // --- Conteneurs (même ordre que l'enum ContainerColor) ---
        static readonly Color[] containerColors =
        {
            Hex("#F2F4F7"), // Blanc
            Hex("#2F7DE1"), // Bleu
            Hex("#2DBE6C"), // Vert
            Hex("#E8483F"), // Rouge
            Hex("#F5C542"), // Doré
        };

        public static Color Of(ContainerColor c) => containerColors[(int)c];

        /// <summary>Couleur du chiffre de valeur : sombre sur fond clair, blanc sinon.</summary>
        public static Color LabelOf(ContainerColor c) =>
            (c == ContainerColor.Blanc || c == ContainerColor.Dore) ? Outline : Color.white;
    }
}
