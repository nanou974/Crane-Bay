// -----------------------------------------------------------------------------
// Crane Bay - Vérification de mise à jour au lancement.
// Interroge l'API publique des releases GitHub (préversions comprises). Si une
// version plus récente que Application.version existe, la fenêtre « Nouvelle
// version » s'affiche (dès que l'aide, le mini-jeu ou la pause sont fermés).
// Hors ligne, en cas d'erreur ou dans l'éditeur : rien ne se passe.
// -----------------------------------------------------------------------------
using System.Collections;
using CraneBay.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace CraneBay.Game
{
    public class UpdateChecker : MonoBehaviour
    {
        const int TimeoutSeconds = 10;
        GameManager game;

        public void Init(GameManager gm)
        {
            game = gm;
            if (Application.isEditor) return; // la version de l'éditeur n'est pas celle d'un APK publié
            StartCoroutine(Check());
        }

        IEnumerator Check()
        {
            // Laisse le jeu démarrer tranquillement avant d'aller sur le réseau.
            yield return new WaitForSecondsRealtime(3f);
            if (Application.internetReachability == NetworkReachability.NotReachable) yield break;

            ReleaseInfo newer = null;
            using (var req = UnityWebRequest.Get(UpdateRules.ApiUrl))
            {
                req.timeout = TimeoutSeconds;
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) yield break;
                try
                {
                    var list = JsonUtility.FromJson<ReleaseList>("{\"items\":" + req.downloadHandler.text + "}");
                    if (list != null && list.items != null) newer = UpdateRules.NewerThan(list.items, Application.version);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[CraneBay] Lecture des releases impossible : " + e.Message);
                }
            }
            if (newer == null) yield break;

            // Attend que le joueur ne soit pas occupé (aide, mini-jeu, pause).
            while (game != null && !game.CanShowUpdate) yield return new WaitForSecondsRealtime(1f);
            if (game != null) game.OpenUpdate(newer);
        }
    }
}
