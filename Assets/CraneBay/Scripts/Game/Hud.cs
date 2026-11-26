// -----------------------------------------------------------------------------
// Crane Bay - Interface (uGUI construite par code).
// Épurée et toujours visible :
//  - en haut : argent, niveau + barre d'XP, navire / météo, turbo, deux coffres ;
//  - bandeau "À venir" : les 3 prochains véhicules et leurs couleurs, boutons
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

        static readonly string[] UpgradeShortNames = { "Grue", "Navires", "Camions", "Parc", "2e grue" };

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
        }

        void BuildTopBar()
        {
            var bar = NewRect("BarreHaut", safeRoot);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, TopBarHeight);
            bar.anchoredPosition = Vector2.zero;
            AddImage(bar.gameObject, SpriteFactory.UiRounded, Palette.UiPanel);

            moneyText = AddText(bar, "Argent", "0 $", 72, Palette.UiGold, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(36, -12), new Vector2(560, 88));
            BuildXpRow(bar);
            infoText = AddText(bar, "Infos", "", 28, Palette.UiText, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(40, -152), new Vector2(600, 40));
            turboText = AddText(bar, "Turbo", "", 28, new Color(1f, 0.55f, 0.2f), TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(40, -194), new Vector2(600, 40));

            chests[(int)ChestKind.Quotidien] = BuildChest(bar, "Coffre du jour", new Vector2(-244, -20), ChestKind.Quotidien);
            chests[(int)ChestKind.VingtMinutes] = BuildChest(bar, "Coffre 20 min", new Vector2(-20, -20), ChestKind.VingtMinutes);
        }

        /// <summary>Ligne "Niv. 3  [barre d'XP]  45 / 120 XP".</summary>
        void BuildXpRow(RectTransform bar)
        {
            levelText = AddText(bar, "Niveau", "Niv. 1", 34, Palette.UiText, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(40, -104), new Vector2(150, 44));
            var back = NewRect("BarreXP", bar);
            SetRect(back, new Vector2(0, 1), new Vector2(190, -112), new Vector2(240, 28));
            AddImage(back.gameObject, SpriteFactory.UiRounded, new Color(0.05f, 0.06f, 0.1f, 1f)).raycastTarget = false;
            xpFill = NewRect("Remplissage", back);
            xpFill.anchorMin = Vector2.zero;
            xpFill.anchorMax = new Vector2(0f, 1f);
            xpFill.offsetMin = new Vector2(3, 3);
            xpFill.offsetMax = new Vector2(-3, -3);
            AddImage(xpFill.gameObject, SpriteFactory.UiRounded, new Color(0.35f, 0.75f, 1f)).raycastTarget = false;
            xpText = AddText(bar, "XP", "", 26, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(440, -104), new Vector2(170, 44));
        }

        ChestUi BuildChest(RectTransform bar, string title, Vector2 pos, ChestKind kind)
        {
            var ui = new ChestUi();
            ui.button = MakeButton(bar, title, new Vector2(1, 1), pos, new Vector2(212, 170), Palette.UiButton, out ui.image);
            var rt = (RectTransform)ui.button.transform;
            // Petit coffre dessiné avec deux rectangles (couvercle + serrure).
            var box = NewRect("Icone", rt);
            SetRect(box, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(64, 44), true);
            AddImage(box.gameObject, SpriteFactory.UiRounded, new Color(0.65f, 0.38f, 0.2f)).raycastTarget = false;
            var band = NewRect("Bande", box);
            SetRect(band, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 44), true);
            AddImage(band.gameObject, SpriteFactory.UiRounded, Palette.UiGold).raycastTarget = false;

            ui.title = AddText(rt, "Titre", title, 26, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(206, 40), true);
            ui.timer = AddText(rt, "Timer", "", 34, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(206, 48), true);
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

            AddText(strip, "Titre", "À venir", 26, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft,
                new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(110, 60));
            for (int i = 0; i < GameRules.UpcomingCount; i++)
            {
                var g = NewRect("Vehicule" + i, strip);
                SetRect(g, new Vector2(0, 0.5f), new Vector2(130 + i * 210, 0), new Vector2(200, 70));
                var ui = new UpcomingUi
                {
                    label = AddText(g, "Type", "", 22, Palette.UiText, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 0), new Vector2(200, 30)),
                    squares = new Image[GameRules.TrainWagons],
                };
                for (int k = 0; k < ui.squares.Length; k++)
                {
                    var sq = NewRect("Couleur" + k, g);
                    SetRect(sq, new Vector2(0, 0), new Vector2(k * 60, 4), new Vector2(54, 30));
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
            soundText = AddText((RectTransform)sound.transform, "Texte", "Son", 24, Palette.UiText, TextAnchor.MiddleCenter,
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

            float w = 200f, gap = 12f;
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

        void RefreshSound() => soundText.text = game.SoundMuted ? "Muet" : "Son";

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
            xpText.text = m.IsMaxLevel ? "MAX" : m.Data.xp + " / " + LevelRules.XpToNext(m.PlayerLevel) + " XP";
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
                    if (show) ui.squares[k].color = Palette.Of(m.upcoming[i].colors[k]);
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
