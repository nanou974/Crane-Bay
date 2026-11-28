// -----------------------------------------------------------------------------
// Crane Bay - Fenêtre « Nouvelle version disponible » (voir UpdateChecker).
// « Télécharger » ouvre l'APK dans le navigateur ; Android propose ensuite de
// l'installer par-dessus le jeu actuel (la progression est conservée).
// -----------------------------------------------------------------------------
using CraneBay.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CraneBay.Game
{
    public partial class Hud
    {
        GameObject updateRoot;
        Text updateTitle, updateNotes;
        ReleaseInfo updateRelease;

        public bool UpdateOpen => updateRoot != null && updateRoot.activeSelf;

        void BuildUpdatePanel()
        {
            updateRoot = new GameObject("MiseAJour", typeof(RectTransform));
            var root = (RectTransform)updateRoot.transform;
            root.SetParent(safeRoot, false);
            Stretch(root);
            var dim = AddImage(root.gameObject, SpriteFactory.UiRounded, new Color(0f, 0f, 0f, 0.7f));
            dim.type = Image.Type.Simple;

            var card = NewRect("Fiche", root);
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(960, 1000), true);
            card.pivot = new Vector2(0.5f, 0.5f);
            AddImage(card.gameObject, SpriteFactory.UiRounded, new Color(0.1f, 0.12f, 0.18f, 0.98f)).raycastTarget = false;

            updateTitle = AddText(card, "Titre", "", 52, Palette.UiGold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(920, 140), true);
            updateNotes = AddText(card, "Notes", "", 32, Palette.UiText, TextAnchor.UpperLeft,
                new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(900, 480), true);
            updateNotes.resizeTextForBestFit = true;
            updateNotes.resizeTextMinSize = 20;
            updateNotes.resizeTextMaxSize = 32;
            updateNotes.verticalOverflow = VerticalWrapMode.Truncate;
            AddText(card, "Conseil", "Le téléchargement s'ouvre dans le navigateur. Ouvrez ensuite le fichier pour installer la mise à jour : votre partie est conservée.",
                24, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(900, 90), true);

            var dl = MakeButton(card, "Telecharger", new Vector2(0.5f, 0), new Vector2(-225, 30), new Vector2(430, 116), Palette.UiGreen, out _, true);
            AddText((RectTransform)dl.transform, "Texte", "Télécharger", 46, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430, 116), true);
            dl.onClick.AddListener(() =>
            {
                if (updateRelease != null && !string.IsNullOrEmpty(updateRelease.DownloadUrl)) Application.OpenURL(updateRelease.DownloadUrl);
                game.CloseUpdate();
            });

            var later = MakeButton(card, "PlusTard", new Vector2(0.5f, 0), new Vector2(225, 30), new Vector2(430, 116), Palette.UiButton, out _, true);
            AddText((RectTransform)later.transform, "Texte", "Plus tard", 46, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(430, 116), true);
            later.onClick.AddListener(() => game.CloseUpdate());

            updateRoot.SetActive(false);
        }

        public void ShowUpdate(ReleaseInfo r)
        {
            updateRelease = r;
            updateTitle.text = "Nouvelle version " + r.Version + " !\n<size=34>" + (string.IsNullOrEmpty(r.name) ? "" : r.name) + "</size>";
            var notes = UpdateRules.ShortNotes(r.body);
            updateNotes.text = notes.Length > 0 ? notes : "Améliorations et corrections.";
            ClosePanel();
            updateRoot.SetActive(true);
            updateRoot.transform.SetAsLastSibling();
        }

        public void HideUpdate()
        {
            if (updateRoot != null) updateRoot.SetActive(false);
        }
    }
}
