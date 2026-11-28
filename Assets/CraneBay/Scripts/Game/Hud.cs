// -----------------------------------------------------------------------------
// Crane Bay - Interface (uGUI construite par code).
// Épurée et toujours visible :
//  - en haut : gros compteur d'argent à gauche, au centre le niveau avec une
//    large barre d'XP, navire / météo et turbo, à droite les deux coffres ;
//  - bandeau "À venir" : les 3 prochains véhicules (centrés) et leurs couleurs, boutons
//    Contrats & records et Son ;
//  - en bas : les cinq améliorations (niveau, effet, coût) ;
//  - fenêtre Contrats du jour & Records, bannière de passage de niveau,
//    messages courts en file d'attente.
// L'interface respecte la "safe area" (encoches, coins arrondis des écrans).
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using CraneBay.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraneBay.Game
{
    public class Hud : MonoBehaviour
    {
        public const float TopBarHeight = 250f;
        public const float StripHeight = 84f;
        public const float BottomBarHeight = 340f;

        GameManager game;
        RectTransform safeRoot;
        CanvasScaler scaler;
        Rect appliedSafeArea;

        Text moneyText, infoText, turboText, toastText, soundText, levelText, xpText;
        RectTransform xpFill;
        CanvasGroup toastGroup;
        float toastTimer;
        readonly Queue<string> toastQueue = new Queue<string>();
        double shownMoney;

        class ChestUi { public Button button; public Image image; public Text title; public Text timer; }
        readonly ChestUi[] chests = new ChestUi[2];

        class UpgradeUi { public Button button; public Image image; public Text title, level, effect, cost; }
        readonly UpgradeUi[] upgrades = new UpgradeUi[GameRules.UpgradeCount];

        class UpcomingUi { public Text label; public Image[] squares; }
        readonly UpcomingUi[] upcomingUi = new UpcomingUi[GameRules.UpcomingCount];

        // Fenêtre contrats & records.
        GameObject panel;
        class ContractUi { public Text desc, reward, progress; public RectTransform fill; public Image fillImage; }
        readonly ContractUi[] contractUi = new ContractUi[ContractRules.Count];
        Text recordsText;

        // Bannière de passage de niveau.
        RectTransform banner;
        CanvasGroup bannerGroup;
        Text bannerTitle, bannerSub;
        float bannerTimer;
        const float BannerDuration = 3.2f;

        static readonly string[] UpgradeShortNames = { "Grue", "Navires", "Camions", "Parc", "2e grue", "Revenus" };

        public void Init(GameManager gm)
        {
            game = gm;
            SpriteFactory.Ensure();
            BuildCanvas();
            shownMoney = game.Model.Data.money;
        }

        public bool PanelOpen => panel != null && panel.activeSelf;

        // =====================================================================
        // Construction
        // =====================================================================

        void BuildCanvas()
        {
            var canvasGo = new GameObject("Interface", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            safeRoot = NewRect("SafeArea", canvasGo.transform);
            Stretch(safeRoot);
            ApplySafeArea();

            BuildTopBar();
            BuildStrip();
            BuildBottomBar();
            BuildToast();
            BuildBanner();
            BuildPanel();
            BuildPauseOverlay();
        }

        void BuildTopBar()
        {
            var bar = NewRect("BarreHaut", safeRoot);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, TopBarHeight);
            bar.anchoredPosition = Vector2.zero;
            AddImage(bar.gameObject, SpriteFactory.UiRounded, Palette.UiPanel);

            // Gauche : le compteur d'argent, en très gros (rétréci si le montant est long).
            moneyText = AddText(bar, "Argent", "0 $", 100, Palette.UiGold, TextAnchor.MiddleLeft,
                new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(MoneyWidth, 190));
            moneyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            moneyText.resizeTextForBestFit = true;
            moneyText.resizeTextMinSize = 40;
            moneyText.resizeTextMaxSize = 100;

            // Milieu : niveau + large barre d'XP, puis navire / météo et turbo.
            BuildXpRow(bar);
            infoText = AddText(bar, "Infos", "", 34, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(CenterOffset, -110), new Vector2(CenterWidth, 50), true);
            turboText = AddText(bar, "Turbo", "", 28, new Color(1f, 0.55f, 0.2f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(CenterOffset, -170), new Vector2(CenterWidth, 44), true);

            // Droite : les deux coffres.
            chests[(int)ChestKind.Quotidien] = BuildChest(bar, "Coffre\ndu jour", new Vector2(-20 - ChestWidth - 12, -20), ChestKind.Quotidien);
            chests[(int)ChestKind.VingtMinutes] = BuildChest(bar, "Coffre\n20 min", new Vector2(-20, -20), ChestKind.VingtMinutes);
        }

        // Disposition de la barre du haut (largeur de référence 1080) :
        // argent à gauche, bloc niveau/XP au centre, coffres à droite.
        const float MoneyWidth = 290f;
        const float ChestWidth = 150f;
        const float CenterWidth = 420f;
        // Le bloc central est décalé à droite du centre de l'écran, entre l'argent et les coffres.
        const float CenterOffset = (MoneyWidth + 28f - 2f * ChestWidth - 52f) * 0.5f;

        /// <summary>Ligne "Niv. 3  [large barre d'XP avec 45 / 120 XP dedans]", au centre.</summary>
        void BuildXpRow(RectTransform bar)
        {
            float left = CenterOffset - CenterWidth * 0.5f;
            levelText = AddText(bar, "Niveau", "Niv. 1", 40, Palette.UiText, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 1), new Vector2(left + 60f, -46), new Vector2(120, 56), true);
            var back = NewRect("BarreXP", bar);
            SetRect(back, new Vector2(0.5f, 1), new Vector2(left + 125f + (CenterWidth - 125f) * 0.5f, -52), new Vector2(CenterWidth - 125f, 44), true);
            AddImage(back.gameObject, SpriteFactory.UiRounded, new Color(0.05f, 0.06f, 0.1f, 1f)).raycastTarget = false;
            xpFill = NewRect("Remplissage", back);
            xpFill.anchorMin = Vector2.zero;
            xpFill.anchorMax = new Vector2(0f, 1f);
            xpFill.offsetMin = new Vector2(3, 3);
            xpFill.offsetMax = new Vector2(-3, -3);
            AddImage(xpFill.gameObject, SpriteFactory.UiRounded, new Color(0.35f, 0.75f, 1f)).raycastTarget = false;
            // Texte "77 / 98 XP" posé sur la barre.
            xpText = AddText(back, "XP", "", 26, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CenterWidth - 125f, 44), true);
        }

        ChestUi BuildChest(RectTransform bar, string title, Vector2 pos, ChestKind kind)
        {
            var ui = new ChestUi();
            ui.button = MakeButton(bar, title, new Vector2(1, 1), pos, new Vector2(ChestWidth, 200), Palette.UiButton, out ui.image);
            var rt = (RectTransform)ui.button.transform;
            // Petit coffre dessiné avec deux rectangles (couvercle + serrure).
            var box = NewRect("Icone", rt);
            SetRect(box, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(64, 44), true);
            AddImage(box.gameObject, SpriteFactory.UiRounded, new Color(0.65f, 0.38f, 0.2f)).raycastTarget = false;
            var band = NewRect("Bande", box);
            SetRect(band, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 44), true);
            AddImage(band.gameObject, SpriteFactory.UiRounded, Palette.UiGold).raycastTarget = false;

            ui.title = AddText(rt, "Titre", title, 24, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(ChestWidth - 6, 64), true);
            ui.timer = AddText(rt, "Timer", "", 32, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(ChestWidth - 6, 48), true);
            ui.button.onClick.AddListener(() => game.OpenChest(kind));
            return ui;
        }

        /// <summary>Bandeau "À venir" + boutons Contrats et Son.</summary>
        void BuildStrip()
        {
            var strip = NewRect("AVenir", safeRoot);
            strip.anchorMin = new Vector2(0, 1); strip.anchorMax = new Vector2(1, 1);
            strip.pivot = new Vector2(0.5f, 1);
            strip.sizeDelta = new Vector2(-24, StripHeight);
            strip.anchoredPosition = new Vector2(0, -TopBarHeight - 6);
            AddImage(strip.gameObject, SpriteFactory.UiRounded, new Color(0.12f, 0.14f, 0.2f, 0.78f));

            // Bouton pause (deux barres verticales dessinées), à gauche du bandeau.
            var pause = MakeButton(strip, "Pause", new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(70, 66), Palette.UiButton, out _);
            foreach (var bx in new[] { -10f, 10f })
            {
                var barRt = NewRect("Barre", (RectTransform)pause.transform);
                SetRect(barRt, new Vector2(0.5f, 0.5f), new Vector2(bx, 0), new Vector2(12, 34), true);
                AddImage(barRt.gameObject, SpriteFactory.UiRounded, Palette.UiText).raycastTarget = false;
            }
            pause.onClick.AddListener(() => game.TogglePause());

            AddText(strip, "Titre", "À venir", 26, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft,
                new Vector2(0, 0.5f), new Vector2(90, 0), new Vector2(110, 60));
            for (int i = 0; i < GameRules.UpcomingCount; i++)
            {
                var g = NewRect("Vehicule" + i, strip);
                // Les véhicules à venir sont centrés sous le bloc niveau / XP.
                SetRect(g, new Vector2(0.5f, 0.5f), new Vector2(CenterOffset + (i - 1) * 160, 0), new Vector2(150, 70), true);
                var ui = new UpcomingUi
                {
                    label = AddText(g, "Type", "", 22, Palette.UiText, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(150, 30), true),
                    squares = new Image[GameRules.TrainWagons],
                };
                for (int k = 0; k < ui.squares.Length; k++)
                {
                    var sq = NewRect("Couleur" + k, g);
                    SetRect(sq, new Vector2(0.5f, 0), new Vector2((k - 1) * 48, 4), new Vector2(42, 28), true);
                    ui.squares[k] = AddImage(sq.gameObject, SpriteFactory.UiRounded, Color.white);
                    ui.squares[k].raycastTarget = false;
                }
                upcomingUi[i] = ui;
            }

            var contracts = MakeButton(strip, "Contrats", new Vector2(1, 0.5f), new Vector2(-112, 0), new Vector2(170, 66), Palette.UiButton, out _);
            AddText((RectTransform)contracts.transform, "Texte", "Contrats\n& records", 22, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 66), true);
            contracts.onClick.AddListener(TogglePanel);

            var sound = MakeButton(strip, "Son", new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(96, 66), Palette.UiButton, out _);
            soundText = AddText((RectTransform)sound.transform, "Texte", "Son", 20, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 66), true);
            sound.onClick.AddListener(() => { game.ToggleSound(); RefreshSound(); });
            RefreshSound();
        }

        void BuildBottomBar()
        {
            var bar = NewRect("BarreBas", safeRoot);
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, BottomBarHeight);
            bar.anchoredPosition = Vector2.zero;
            AddImage(bar.gameObject, SpriteFactory.UiRounded, Palette.UiPanel);

            AddText(bar, "TitreAmeliorations", "AMÉLIORATIONS", 26, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(600, 40), true);

            float w = 166f, gap = 10f;
            for (int i = 0; i < GameRules.UpgradeCount; i++)
            {
                var type = (UpgradeType)i;
                var ui = new UpgradeUi();
                float x = (i - (GameRules.UpgradeCount - 1) * 0.5f) * (w + gap);
                ui.button = MakeButton(bar, GameRules.UpgradeNames[i], new Vector2(0.5f, 0), new Vector2(x, 20), new Vector2(w, 262), Palette.UiButton, out ui.image, pivotBottom: true);
                var rt = (RectTransform)ui.button.transform;
                ui.title = AddText(rt, "Nom", UpgradeShortNames[i], 30, Palette.UiText, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(w - 10, 50), true);
                ui.level = AddText(rt, "Niveau", "", 24, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -62), new Vector2(w - 10, 36), true);
                ui.effect = AddText(rt, "Effet", "", 22, Palette.UiGreen, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(w - 10, 70), true);
                ui.cost = AddText(rt, "Cout", "", 32, Palette.UiGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(w - 10, 56), true);
                ui.button.onClick.AddListener(() => game.BuyUpgrade(type));
                upgrades[i] = ui;
            }
        }

        void BuildToast()
        {
            var rt = NewRect("Message", safeRoot);
            SetRect(rt, new Vector2(0.5f, 1), new Vector2(0, -TopBarHeight - StripHeight - 20), new Vector2(1000, 130), true);
            toastGroup = rt.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            AddImage(rt.gameObject, SpriteFactory.UiRounded, new Color(0.12f, 0.14f, 0.2f, 0.85f)).raycastTarget = false;
            toastText = AddText(rt, "Texte", "", 36, Palette.UiText, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 126), true);
            toastGroup.alpha = 0f;
        }

        void BuildBanner()
        {
            banner = NewRect("BanniereNiveau", safeRoot);
            SetRect(banner, new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(1000, 360), true);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;
            bannerTitle = AddText(banner, "Titre", "", 130, Palette.UiGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(1000, 170), true);
            var o = bannerTitle.GetComponent<Outline>();
            o.effectDistance = new Vector2(5, -5);
            bannerSub = AddText(banner, "SousTitre", "", 40, Palette.UiText, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(980, 190), true);
            bannerGroup.alpha = 0f;
        }

        GameObject pauseOverlay;

        /// <summary>Écran de pause : fond sombre (bloque le jeu), "PAUSE", Reprendre / Quitter.</summary>
        void BuildPauseOverlay()
        {
            pauseOverlay = new GameObject("Pause", typeof(RectTransform));
            var root = (RectTransform)pauseOverlay.transform;
            root.SetParent(safeRoot, false);
            Stretch(root);
            var dim = AddImage(root.gameObject, SpriteFactory.UiRounded, new Color(0, 0, 0, 0.55f));
            dim.type = Image.Type.Simple;

            AddText(root, "Titre", "PAUSE", 130, Palette.UiGold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(900, 170), true).GetComponent<Outline>().effectDistance = new Vector2(5, -5);

            var resume = MakeButton(root, "Reprendre", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(520, 130), Palette.UiGreen, out _);
            ((RectTransform)resume.transform).pivot = new Vector2(0.5f, 0.5f);
            AddText((RectTransform)resume.transform, "Texte", "Reprendre", 52, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 130), true);
            resume.onClick.AddListener(() => game.TogglePause());

            var quit = MakeButton(root, "Quitter", new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(520, 110), Palette.UiButton, out _);
            ((RectTransform)quit.transform).pivot = new Vector2(0.5f, 0.5f);
            AddText((RectTransform)quit.transform, "Texte", "Sauvegarder et quitter", 38, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520, 110), true);
            quit.onClick.AddListener(() => game.QuitGame());

            pauseOverlay.SetActive(false);
        }

        public void SetPaused(bool paused)
        {
            if (paused) ClosePanel();
            pauseOverlay.SetActive(paused);
            pauseOverlay.transform.SetAsLastSibling();
        }

        void BuildPanel()
        {
            panel = new GameObject("ContratsRecords", typeof(RectTransform));
            var root = (RectTransform)panel.transform;
            root.SetParent(safeRoot, false);
            Stretch(root);
            // Fond sombre qui bloque les touches derrière (toucher le fond = fermer).
            // Il est frère (et non parent) de la fenêtre : toucher la fenêtre ne la ferme pas.
            var dimRt = NewRect("Fond", root);
            Stretch(dimRt);
            var dim = AddImage(dimRt.gameObject, SpriteFactory.UiRounded, new Color(0, 0, 0, 0.6f));
            dim.type = Image.Type.Simple;
            var dimButton = dimRt.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(TogglePanel);

            var box = NewRect("Fenetre", root);
            SetRect(box, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(980, 1340), true);
            box.pivot = new Vector2(0.5f, 0.5f);
            AddImage(box.gameObject, SpriteFactory.UiRounded, new Color(0.14f, 0.17f, 0.25f, 0.98f));

            AddText(box, "Titre", "CONTRATS DU JOUR", 44, Palette.UiGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(940, 60), true);
            AddText(box, "Aide", "Renouvelés chaque jour. Récompense versée dès qu'un contrat est rempli.", 24, new Color(1, 1, 1, 0.7f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -84), new Vector2(940, 40), true);

            for (int i = 0; i < ContractRules.Count; i++)
            {
                var row = NewRect("Contrat" + i, box);
                SetRect(row, new Vector2(0.5f, 1), new Vector2(0, -140 - i * 170), new Vector2(920, 150), true);
                AddImage(row.gameObject, SpriteFactory.UiRounded, Palette.UiButton).raycastTarget = false;
                var ui = new ContractUi
                {
                    desc = AddText(row, "Objectif", "", 32, Palette.UiText, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(24, -10), new Vector2(880, 46)),
                    reward = AddText(row, "Recompense", "", 26, Palette.UiGold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(24, -58), new Vector2(560, 36)),
                    progress = AddText(row, "Progression", "", 26, Palette.UiText, TextAnchor.MiddleRight, new Vector2(1, 1), new Vector2(-24, -58), new Vector2(300, 36)),
                };
                var back = NewRect("Barre", row);
                SetRect(back, new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(872, 26), true);
                AddImage(back.gameObject, SpriteFactory.UiRounded, new Color(0.05f, 0.06f, 0.1f, 1f)).raycastTarget = false;
                ui.fill = NewRect("Remplissage", back);
                ui.fill.anchorMin = Vector2.zero;
                ui.fill.anchorMax = new Vector2(0f, 1f);
                ui.fill.offsetMin = new Vector2(3, 3);
                ui.fill.offsetMax = new Vector2(-3, -3);
                ui.fillImage = AddImage(ui.fill.gameObject, SpriteFactory.UiRounded, Palette.UiGreen);
                ui.fillImage.raycastTarget = false;
                contractUi[i] = ui;
            }

            AddText(box, "TitreRecords", "STATISTIQUES & RECORDS", 40, Palette.UiGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -660), new Vector2(940, 56), true);
            recordsText = AddText(box, "Records", "", 30, Palette.UiText, TextAnchor.UpperLeft, new Vector2(0.5f, 1), new Vector2(0, -726), new Vector2(880, 470), true);

            var close = MakeButton(box, "Fermer", new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(360, 96), Palette.UiGreen, out _, pivotBottom: true);
            AddText((RectTransform)close.transform, "Texte", "Fermer", 36, Palette.Outline, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360, 96), true);
            close.onClick.AddListener(TogglePanel);
            panel.SetActive(false);
        }

        // =====================================================================
        // Mise à jour
        // =====================================================================

        /// <summary>Ajoute un message court (affichés l'un après l'autre).</summary>
        public void ShowToast(string message)
        {
            if (toastTimer > 0.5f && toastQueue.Count < 4) { toastQueue.Enqueue(message); return; }
            DisplayToast(message);
        }

        void DisplayToast(string message)
        {
            toastText.text = message;
            toastTimer = 2.2f;
            toastGroup.alpha = 1f;
        }

        /// <summary>Grande annonce du passage de niveau (avec le feu d'artifice).</summary>
        public void ShowLevelBanner(int level, string details)
        {
            bannerTitle.text = "NIVEAU " + level + " !";
            bannerSub.text = details;
            bannerTimer = BannerDuration;
        }

        public void ClosePanel() { if (PanelOpen) TogglePanel(); }

        void TogglePanel()
        {
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) RefreshPanel(TimeUtil.NowUnix);
        }

        void RefreshSound() => soundText.text = game.SoundLabel;

        void Update()
        {
            if (game == null) return;
            ApplySafeArea();

            // Compteur d'argent animé (monte progressivement vers la vraie valeur).
            double target = game.Model.Data.money;
            if (Math.Abs(shownMoney - target) < 1) shownMoney = target;
            else shownMoney += (target - shownMoney) * Mathf.Clamp01(Time.deltaTime * 10f);
            moneyText.text = FormatMoney((long)Math.Round(shownMoney));

            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.4f);
                if (toastTimer <= 0f && toastQueue.Count > 0) DisplayToast(toastQueue.Dequeue());
            }

            // Bannière : apparition rebondie, maintien, disparition.
            if (bannerTimer > 0f)
            {
                bannerTimer -= Time.deltaTime;
                float t = 1f - bannerTimer / BannerDuration;
                float scale = t < 0.12f ? Mathf.Lerp(0.3f, 1.15f, t / 0.12f) : t < 0.2f ? Mathf.Lerp(1.15f, 1f, (t - 0.12f) / 0.08f) : 1f + 0.03f * Mathf.Sin(Time.time * 6f);
                banner.localScale = new Vector3(scale, scale, 1f);
                bannerGroup.alpha = Mathf.Clamp01(bannerTimer / 0.5f);
            }
            else if (bannerGroup.alpha > 0f) bannerGroup.alpha = 0f;

            // Pulsation des coffres prêts.
            float pulse = 1f + 0.05f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int i = 0; i < 2; i++)
            {
                bool ready = game.Model.ChestRemaining((ChestKind)i, TimeUtil.NowUnix) <= 0;
                chests[i].button.transform.localScale = ready ? new Vector3(pulse, pulse, 1) : Vector3.one;
            }
        }

        /// <summary>Rafraîchit les textes (appelé quelques fois par seconde, pas à chaque image).</summary>
        public void Refresh(double now)
        {
            var m = game.Model;
            infoText.text = (m.Data.shipDocked ? "Navire à quai" : "Navire dans " + ChestLogic.Format(m.SecondsUntilShip(now)).Replace("Prêt !", "0:00"))
                            + "   •   " + Ambience.WeatherNames[(int)Ambience.WeatherAt(now)];
            levelText.text = "Niv. " + m.PlayerLevel;
            xpFill.anchorMax = new Vector2(m.XpRatio, 1f);
            xpText.text = m.IsMaxLevel ? "MAX" : m.LevelUpPending ? "Niveau prêt !" : m.Data.xp + " / " + LevelRules.XpToNext(m.PlayerLevel) + " XP";
            turboText.text = m.TurboActive(now) ? "TURBO GRUE x2  " + ChestLogic.Format(m.Data.turboEndUnix - now) : "";

            for (int i = 0; i < 2; i++)
            {
                double rem = m.ChestRemaining((ChestKind)i, now);
                var ui = chests[i];
                bool ready = rem <= 0;
                ui.timer.text = ChestLogic.Format(rem);
                ui.image.color = ready ? Palette.UiGold : Palette.UiButtonOff;
                ui.title.color = ready ? Palette.Outline : Palette.UiText;
                ui.timer.color = ready ? Palette.Outline : Palette.UiText;
            }

            for (int i = 0; i < GameRules.UpgradeCount; i++)
            {
                var type = (UpgradeType)i;
                var ui = upgrades[i];
                int lvl = m.Level(type);
                bool maxed = GameRules.IsMaxed(type, lvl);
                ui.level.text = "Niv. " + lvl + "/" + GameRules.MaxLevelOf(type);
                ui.effect.text = maxed ? GameRules.EffectLabel(type, lvl)
                                       : GameRules.EffectLabel(type, lvl) + "\n→ " + GameRules.EffectLabel(type, lvl + 1);
                ui.cost.text = maxed ? "MAX" : FormatMoney(m.NextUpgradeCost(type));
                bool can = m.CanBuy(type);
                ui.image.color = can ? Palette.UiButton : Palette.UiButtonOff;
                ui.cost.color = can ? Palette.UiGold : new Color(1, 1, 1, 0.55f);
            }

            // Véhicules annoncés.
            for (int i = 0; i < upcomingUi.Length; i++)
            {
                var ui = upcomingUi[i];
                bool has = i < m.upcoming.Count;
                ui.label.text = has ? (m.upcoming[i].kind == VehicleKind.Train ? "Train" : "Camion") : "";
                for (int k = 0; k < ui.squares.Length; k++)
                {
                    bool show = has && k < m.upcoming[i].colors.Count;
                    ui.squares[k].enabled = show;
                    if (!show) continue;
                    ui.squares[k].color = Palette.Of(m.upcoming[i].colors[k]);
                    // Carrés centrés sous le nom du véhicule (1 pour un camion, 3 pour un train).
                    int count = m.upcoming[i].colors.Count;
                    var rt = ui.squares[k].rectTransform;
                    rt.anchoredPosition = new Vector2((k - (count - 1) * 0.5f) * 48f, rt.anchoredPosition.y);
                }
            }

            if (PanelOpen) RefreshPanel(now);
        }

        void RefreshPanel(double now)
        {
            var d = game.Model.Data;
            for (int i = 0; i < contractUi.Length; i++)
            {
                var ui = contractUi[i];
                if (i >= d.contracts.Count) { ui.desc.text = ""; ui.reward.text = ""; ui.progress.text = ""; ui.fill.anchorMax = new Vector2(0, 1); continue; }
                var c = d.contracts[i];
                ui.desc.text = c.Describe();
                ui.reward.text = "+" + FormatMoney(c.rewardMoney) + "   +" + c.rewardXp + " XP";
                ui.progress.text = c.done ? "Terminé" : Math.Min(c.progress, c.target) + " / " + c.target;
                ui.fill.anchorMax = new Vector2(c.target > 0 ? Mathf.Clamp01((float)c.progress / c.target) : 0f, 1f);
                ui.fillImage.color = c.done ? Palette.UiGold : Palette.UiGreen;
            }
            double untilReset = (ContractRules.DayIndex(now) + 1) * 86400.0 - now;
            recordsText.text =
                "Nouveaux contrats dans " + ChestLogic.Format(untilReset) + "\n\n" +
                "Niveau atteint : " + d.level + "  (" + LevelRules.ThemeNames[(int)LevelRules.ThemeFor(d.level)] + ")\n" +
                "Conteneurs livrés : " + d.delivered + "\n" +
                "Livraisons rapides : " + d.fastDeliveries + "\n" +
                "Navires déchargés : " + d.shipsUnloaded + "\n" +
                "Trains complets : " + d.trainsCompleted + "\n" +
                "Argent gagné au total : " + FormatMoney(d.totalEarned) + "\n\n" +
                "Record de déchargement : " + (d.bestUnloadSeconds > 0 ? ChestLogic.Format(d.bestUnloadSeconds) : "—") + "\n" +
                "Plus gros train : " + (d.bestTrainEarned > 0 ? FormatMoney(d.bestTrainEarned) : "—") + "\n" +
                "Plus grosse livraison : " + (d.bestSingleDelivery > 0 ? FormatMoney(d.bestSingleDelivery) : "—");
        }

        public static string FormatMoney(long v)
        {
            // Séparateur de milliers à la française (espace).
            return v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ") + " $";
        }

        void ApplySafeArea()
        {
            // Sur écran large (tablette), on s'aligne sur la hauteur pour éviter une interface géante.
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            scaler.matchWidthOrHeight = aspect > 9f / 16f ? 1f : 0f;

            Rect sa = Screen.safeArea;
            if (sa == appliedSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            appliedSafeArea = sa;
            safeRoot.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safeRoot.offsetMin = Vector2.zero;
            safeRoot.offsetMax = Vector2.zero;
        }

        // =====================================================================
        // Petits utilitaires uGUI
        // =====================================================================

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        /// <summary>Place un rectangle par rapport à une ancre (pivot = ancre, ou centré horizontalement si demandé).</summary>
        static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, bool centerPivotX = false)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = centerPivotX ? new Vector2(0.5f, anchor.y) : anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        static Text AddText(RectTransform parent, string name, string text, int size, Color color, TextAnchor align,
                            Vector2 anchor, Vector2 pos, Vector2 box, bool centerPivotX = false)
        {
            var rt = NewRect(name, parent);
            SetRect(rt, anchor, pos, box, centerPivotX);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = SpriteFactory.Font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            // Contour net "anime" autour des textes.
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.07f, 0.08f, 0.12f, 0.9f);
            o.effectDistance = new Vector2(2, -2);
            return t;
        }

        static Button MakeButton(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color,
                                 out Image image, bool pivotBottom = false)
        {
            var rt = NewRect("Bouton_" + name, parent);
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = pivotBottom ? new Vector2(0.5f, 0f) : anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            image = AddImage(rt.gameObject, SpriteFactory.UiRounded, color);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = image;
            var cb = b.colors;
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cb.highlightedColor = Color.white;
            b.colors = cb;
            return b;
        }
    }
}
