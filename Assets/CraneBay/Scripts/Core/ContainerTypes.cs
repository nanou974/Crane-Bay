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
    /// Un conteneur logique. Classe sérialisable par JsonUtility (champs
    /// publics uniquement).
    ///  - size : 40 (grand, standard) ou 20 pieds (petit, vaut moins cher).
    ///    Règle d'empilage réelle : un 40 pieds ne se pose que sur le sol ou
    ///    sur un autre 40 pieds (jamais sur un 20 pieds).
    ///  - reefer : conteneur frigorifique. Hors d'un emplacement électrifié,
    ///    sa marchandise se dégrade et sa valeur baisse.
    ///  - hazard : matières dangereuses. Interdit à côté (ou sur/sous) un autre
    ///    conteneur dangereux.
    /// </summary>
    [Serializable]
    public class ContainerInfo
    {
        public int id;
        public int color;
        public int size = 40;
        public bool reefer;
        public bool hazard;
        public float unplugged; // secondes passées débranché (frigorifiques)

        public ContainerInfo() { }

        public ContainerInfo(int id, ContainerColor color, int size = 40, bool reefer = false, bool hazard = false)
        {
            this.id = id;
            this.color = (int)color;
            this.size = size == 20 ? 20 : 40;
            this.reefer = reefer;
            this.hazard = hazard;
        }

        /// <summary>Couleur typée (la sauvegarde garde un int).</summary>
        public ContainerColor Color => (ContainerColor)color;

        public bool IsSmall => size == 20;
    }
}
