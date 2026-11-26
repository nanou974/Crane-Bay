// -----------------------------------------------------------------------------
// Crane Bay - Sauvegarde locale en JSON.
// Fichier : Application.persistentDataPath/cranebay_save.json
// (sur Android : /storage/emulated/0/Android/data/<package>/files/).
// Écriture "atomique" : on écrit d'abord un fichier temporaire puis on remplace
// l'ancien, pour ne jamais corrompre la sauvegarde si le jeu est tué pendant
// l'écriture. Aucun compte, aucune connexion réseau.
// -----------------------------------------------------------------------------
using System;
using System.IO;
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public static class SaveSystem
    {
        const string FileName = "cranebay_save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Charge la sauvegarde, ou renvoie null s'il n'y en a pas (nouvelle partie).</summary>
        public static SaveData Load()
        {
            string path = FilePath;
            try
            {
                if (!File.Exists(path))
                {
                    // Reprise après une écriture interrompue juste avant le remplacement.
                    if (File.Exists(path + ".tmp")) File.Move(path + ".tmp", path);
                    else return null;
                }
                string json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<SaveData>(json);
                return data;
            }
            catch (Exception e)
            {
                // Sauvegarde illisible : on la met de côté (pour diagnostic) et on repart à zéro.
                Debug.LogWarning("[CraneBay] Sauvegarde illisible, nouvelle partie : " + e.Message);
                try { File.Copy(path, path + ".corrompu", true); } catch { /* ignoré */ }
                return null;
            }
        }

        public static void Save(SaveData data)
        {
            if (data == null) return;
            string path = FilePath;
            string tmp = path + ".tmp";
            try
            {
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CraneBay] Échec de sauvegarde : " + e.Message);
            }
        }

        /// <summary>Efface la progression (utilisé par le menu de debug de l'éditeur).</summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
                if (File.Exists(FilePath + ".tmp")) File.Delete(FilePath + ".tmp");
            }
            catch (Exception e) { Debug.LogWarning("[CraneBay] " + e.Message); }
        }
    }
}
