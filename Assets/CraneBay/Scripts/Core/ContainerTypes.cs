// -----------------------------------------------------------------------------
// Crane Bay - Types de base des conteneurs.
// Ce fichier ne dépend PAS de UnityEngine : il est compilé tel quel par les
// tests unitaires (dossier Tests/CoreTests) en dehors de Unity.
// -----------------------------------------------------------------------------
using System;

namespace CraneBay.Core
{
    /// <summary>
    /// Couleurs de conteneurs. L'ordre sert d'index dans les tables de GameRules
    /// (valeur, probabilité d'apparition). Ne pas réordonner : la sauvegarde
    /// stocke la couleur sous forme d'entier.
    /// </summary>
    public enum ContainerColor
    {
        Blanc = 0,
        Bleu = 1,
        Vert = 2,
        Rouge = 3,
        Dore = 4,
    }

    /// <summary>
    /// Un conteneur logique : un identifiant unique (pour retrouver sa
    /// représentation visuelle) et une couleur. Classe sérialisable par
    /// JsonUtility (champs publics, pas de propriétés).
    /// </summary>
    [Serializable]
    public class ContainerInfo
    {
        public int id;
        public int color;

        public ContainerInfo() { }

        public ContainerInfo(int id, ContainerColor color)
        {
            this.id = id;
            this.color = (int)color;
        }

        /// <summary>Couleur typée (la sauvegarde garde un int).</summary>
        public ContainerColor Color => (ContainerColor)color;
    }
}
