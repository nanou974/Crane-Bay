// -----------------------------------------------------------------------------
// Crane Bay - Vérification de mise à jour : comparaison de versions et choix de
// la release GitHub à proposer (préversions comprises, brouillons ignorés).
// Les classes de données reprennent les champs de l'API GitHub
// (GET /repos/{owner}/{repo}/releases) pour être remplies par JsonUtility.
// Sans dépendance Unity (testable hors moteur).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace CraneBay.Core
{
    [Serializable]
    public class ReleaseAsset
    {
        public string name;
        public string browser_download_url;
    }

    [Serializable]
    public class ReleaseInfo
    {
        public string tag_name;
        public string name;
        public string body;
        public string html_url;
        public bool draft;
        public bool prerelease;
        public ReleaseAsset[] assets;

        /// <summary>Lien de l'APK joint à la release (sinon la page de la release).</summary>
        public string DownloadUrl
        {
            get
            {
                if (assets != null)
                    foreach (var a in assets)
                        if (a != null && !string.IsNullOrEmpty(a.browser_download_url)
                            && a.name != null && a.name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                            return a.browser_download_url;
                return html_url;
            }
        }

        public string Version => UpdateRules.Clean(tag_name);
    }

    /// <summary>Enveloppe pour JsonUtility, qui ne lit pas un tableau JSON à la racine.</summary>
    [Serializable]
    public class ReleaseList
    {
        public ReleaseInfo[] items;
    }

    public static class UpdateRules
    {
        public const string Owner = "nanou974";
        public const string Repo = "Crane-Bay";
        public static string ApiUrl => "https://api.github.com/repos/" + Owner + "/" + Repo + "/releases?per_page=10";

        /// <summary>"v0.1.9" -> "0.1.9" (préfixe v et espaces retirés).</summary>
        public static string Clean(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            v = v.Trim();
            if (v.StartsWith("v") || v.StartsWith("V")) v = v.Substring(1);
            return v;
        }

        /// <summary>Compare deux versions X.Y.Z (numériquement : 0.1.10 > 0.1.9). Négatif si a &lt; b.</summary>
        public static int Compare(string a, string b)
        {
            var pa = Parts(a);
            var pb = Parts(b);
            int n = Math.Max(pa.Count, pb.Count);
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Count ? pa[i] : 0, y = i < pb.Count ? pb[i] : 0;
                if (x != y) return x < y ? -1 : 1;
            }
            return 0;
        }

        static List<int> Parts(string v)
        {
            var res = new List<int>();
            foreach (var part in Clean(v).Split('.'))
            {
                // "3-beta" -> 3 : on ne garde que les chiffres de tête.
                int k = 0, val = 0;
                while (k < part.Length && char.IsDigit(part[k])) { val = val * 10 + (part[k] - '0'); k++; }
                res.Add(val);
            }
            return res;
        }

        /// <summary>Version valide (au moins un chiffre) ?</summary>
        public static bool IsVersion(string v)
        {
            v = Clean(v);
            return v.Length > 0 && char.IsDigit(v[0]);
        }

        /// <summary>La release la plus récente et plus récente que "current", ou null.</summary>
        public static ReleaseInfo NewerThan(IEnumerable<ReleaseInfo> releases, string current)
        {
            if (releases == null || !IsVersion(current)) return null;
            ReleaseInfo best = null;
            foreach (var r in releases)
            {
                if (r == null || r.draft || !IsVersion(r.tag_name)) continue;
                if (Compare(r.tag_name, current) <= 0) continue;
                if (best == null || Compare(r.tag_name, best.tag_name) > 0) best = r;
            }
            return best;
        }

        /// <summary>Notes affichées dans le jeu : sans balises Markdown simples, raccourcies.</summary>
        public static string ShortNotes(string body, int maxChars = 600)
        {
            if (string.IsNullOrEmpty(body)) return "";
            var lines = body.Replace("\r", "").Split('\n');
            var sb = new System.Text.StringBuilder();
            foreach (var raw in lines)
            {
                var l = raw.Trim();
                if (l.Length == 0) continue;
                while (l.StartsWith("#")) l = l.Substring(1).TrimStart();
                if (l.StartsWith("- ") || l.StartsWith("* ")) l = "• " + l.Substring(2);
                l = l.Replace("**", "").Replace("`", "");
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(l);
            }
            var s = sb.ToString();
            return s.Length <= maxChars ? s : s.Substring(0, maxChars - 1).TrimEnd() + "…";
        }
    }
}
