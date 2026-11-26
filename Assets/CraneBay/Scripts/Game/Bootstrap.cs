// -----------------------------------------------------------------------------
// Crane Bay - Démarrage automatique.
// Garantit que le jeu se lance même si la scène ouverte est vide : après le
// chargement de n'importe quelle scène, s'il n'existe pas de GameManager, on
// en crée un. La scène fournie (Assets/Scenes/Main.unity, générée par le menu
// Crane Bay > Configurer le projet) en contient déjà un.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CraneBay.Game
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureGame()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            new GameObject("CraneBay").AddComponent<GameManager>();
        }
    }
}
