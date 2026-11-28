// -----------------------------------------------------------------------------
// Crane Bay - Écran d'aide (affiché au premier lancement, puis depuis le menu pause).
//
// Le jeu est assombri ; chaque élément de l'interface est entouré d'un cadre doré
// avec un numéro, et une fiche au centre explique « comment jouer » puis à quoi
// sert chaque numéro. Le port est en pause tant que l'aide est ouverte.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using CraneBay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CraneBay.Game
{
    public partial class Hud
    {
        class HelpItem
        {
            public RectTransform[] targets;
            public RectTransform ring, badge;
        }

        GameObject helpRoot;
        readonly List<HelpItem> helpItems = new List<HelpItem>();

        public bool HelpOpen => helpRoot != null && helpRoot.activeSelf;

        static readonly string[] HelpLabels =
        {
            "<b>Argent</b> : il sert à acheter les améliorations.",
            "<b>Niveau et XP</b> : chaque livraison remplit la barre. Niveau suivant = prime et nouveautés.",
            "<b>Coffres</b> : un cadeau chaque jour et toutes les 20 minutes.",
            "<b>Pause</b> : arrête le temps du port.",
            "<b>À venir</b> : les prochains camions et trains, avec leurs couleurs.",
            "<b>Contrats & records</b> : les missions du jour et vos meilleurs scores.",
            "<b>Son</b> : sons et musique, sons seuls, ou muet.",
            "<b>Améliorations</b> : grue plus rapide, navires plus fréquents, revenus…",
            "<b>Détente</b> : mini-jeu de conteneurs, le port est en pause.",
        };

        void BuildHelp()
        {
            helpRoot = new GameObject("Aide", typeof(RectTransform));
            var root = (RectTransform)helpRoot.transform;
            root.SetParent(safeRoot, false);
            Stretch(root);
            var dim = AddImage(root.gameObject, SpriteFactory.UiRounded, new Color(0f, 0f, 0f, 0.72f));
            dim.type = Image.Type.Simple;
            // Le fond bloque les touches : seul « C'est parti ! » ferme l'aide.

            // Cibles, dans le même ordre que HelpLabels.
            var targets = new List<RectTransform[]>
            {
                new[] { moneyText.rectTransform },
                new[] { levelText.rectTransform, (RectTransform)xpText.transform.parent },
                new[] { (RectTransform)chests[0].button.transform, (RectTransform)chests[1].button.transform },
                new[] { (RectTransform)pauseButton.transform },
                UpcomingRects(),
                new[] { (RectTransform)contractsButton.transform },
                new[] { (RectTransform)soundButton.transform },
                UpgradeRects(),
                new[] { (RectTransform)relaxButton.transform },
            };
            for (int i = 0; i < targets.Count; i++)
            {
                var item = new HelpItem { targets = targets[i] };
                item.ring = NewRect("Cadre" + (i + 1), root);
                item.ring.anchorMin = item.ring.anchorMax = item.ring.pivot = new Vector2(0.5f, 0.5f);
                AddImage(item.ring.gameObject, SpriteFactory.UiRing, Palette.UiGold).raycastTarget = false;
                item.badge = NewRect("Numero" + (i + 1), root);
                item.badge.anchorMin = item.badge.anchorMax = item.badge.pivot = new Vector2(0.5f, 0.5f);
                item.badge.sizeDelta = new Vector2(54, 54);
                var b = AddImage(item.badge.gameObject, SpriteFactory.Circle, Palette.UiGold);
                b.type = Image.Type.Simple;
                b.raycastTarget = false;
                var n = AddText(item.badge, "N", (i + 1).ToString(), 34, new Color(0.1f, 0.1f, 0.15f), TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 54), true);
                n.GetComponent<Outline>().enabled = false;
                helpItems.Add(item);
            }

            // Fiche centrale : comment jouer + légende des numéros.
            var card = NewRect("Fiche", root);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1000, 1000), true);
            card.pivot = new Vector2(0.5f, 0.5f);
            AddImage(card.gameObject, SpriteFactory.UiRounded, new Color(0.1f, 0.12f, 0.18f, 0.97f)).raycastTarget = false;
            AddText(card, "Titre", "Bienvenue au port !", 54, Palette.UiGold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(960, 70), true);
            var how = AddText(card, "CommentJouer",
                "Touchez un conteneur du navire, puis sa destination : le parc, ou le camion / train qui demande cette couleur. " +
                "Vous pouvez aussi le faire glisser. Livrez vite : chaque véhicule a une jauge de patience.",
                30, Palette.UiText, TextAnchor.UpperLeft, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(940, 150), true);
            how.supportRichText = true;

            var legend = new System.Text.StringBuilder();
            for (int i = 0; i < HelpLabels.Length; i++)
            {
                if (i > 0) legend.Append('\n');
                legend.Append("<color=#FFD23F>").Append(i + 1).Append("</color>  ").Append(HelpLabels[i]);
            }
            var list = AddText(card, "Legende", legend.ToString(), 28, Palette.UiText, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1), new Vector2(0, -262), new Vector2(940, 600), true);
            list.supportRichText = true;
            list.lineSpacing = 1.05f;
            list.resizeTextForBestFit = true;
            list.resizeTextMinSize = 20;
            list.resizeTextMaxSize = 28;
            list.verticalOverflow = VerticalWrapMode.Truncate;

            var ok = MakeButton(card, "Compris", new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(480, 106), Palette.UiGreen, out _, true);
            AddText((RectTransform)ok.transform, "Texte", "C'est parti !", 46, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 106), true);
            ok.onClick.AddListener(() => game.CloseHelp());

            helpRoot.SetActive(false);
        }

        RectTransform[] UpcomingRects()
        {
            var r = new RectTransform[upcomingUi.Length];
            for (int i = 0; i < r.Length; i++) r[i] = (RectTransform)upcomingUi[i].label.transform.parent;
            return r;
        }

        RectTransform[] UpgradeRects()
        {
            var r = new RectTransform[upgrades.Length];
            for (int i = 0; i < r.Length; i++) r[i] = (RectTransform)upgrades[i].button.transform;
            return r;
        }

        public void ShowHelp()
        {
            ClosePanel();
            helpRoot.SetActive(true);
            helpRoot.transform.SetAsLastSibling();
            PlaceHelpRings();
        }

        public void HideHelp()
        {
            if (helpRoot != null) helpRoot.SetActive(false);
        }

        readonly Vector3[] corners = new Vector3[4];

        /// <summary>Cadre doré autour de chaque groupe d'éléments (recalculé à chaque image : rotation, safe area).</summary>
        void PlaceHelpRings()
        {
            if (!HelpOpen) return;
            var root = (RectTransform)helpRoot.transform;
            foreach (var item in helpItems)
            {
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                foreach (var t in item.targets)
                {
                    if (t == null) continue;
                    t.GetWorldCorners(corners);
                    foreach (var c in corners)
                    {
                        Vector2 p = root.InverseTransformPoint(c);
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                }
                if (min.x > max.x) continue;
                const float pad = 10f;
                item.ring.anchoredPosition = (min + max) * 0.5f;
                item.ring.sizeDelta = max - min + new Vector2(pad * 2, pad * 2);
                // Numéro sur le coin haut gauche du cadre, rentré dans l'écran si besoin.
                var half = root.rect.size * 0.5f;
                var bp = new Vector2(min.x - pad + 8, max.y + pad - 8);
                bp.x = Mathf.Clamp(bp.x, -half.x + 30, half.x - 30);
                bp.y = Mathf.Clamp(bp.y, -half.y + 30, half.y - 30);
                item.badge.anchoredPosition = bp;
            }
        }
    }
}
