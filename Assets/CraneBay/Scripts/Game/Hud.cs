// -----------------------------------------------------------------------------
// Crane Bay - Interface (uGUI construite par code).
// Épurée et toujours visible :
//  - en haut : argent, état du navire, turbo, son, deux coffres avec timers ;
//  - en bas : les trois améliorations (niveau, effet, coût).
// L'interface respecte la "safe area" (encoches, coins arrondis des écrans).
// -----------------------------------------------------------------------------
using System;
using CraneBay.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraneBay.Game
{
    public class Hud : MonoBehaviour
    {
        public const float TopBarHeight = 210f;
        public const float BottomBarHeight = 340f;

        GameManager game;
        RectTransform safeRoot;
        CanvasScaler scaler;
        Rect appliedSafeArea;

        Text moneyText, infoText, turboText, toastText, soundText;
        CanvasGroup toastGroup;
        float toastTimer;
        double shownMoney;

        class ChestUi { public Button button; public Image image; public Text title; public Text timer; }
        readonly ChestUi[] chests = new ChestUi[2];

        class UpgradeUi { public Button button; public Image image; public Text title, level, effect, cost; }
        readonly UpgradeUi[] upgrades = new UpgradeUi[3];

        public void Init(GameManager gm)
        {
            game = gm;
            SpriteFactory.Ensure();
            BuildCanvas();
            shownMoney = game.Model.Data.money;
        }

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
            BuildBottomBar();
            BuildToast();
        }

        void BuildTopBar()
        {
            var bar = NewRect("BarreHaut", safeRoot);
            bar.anchorMin = new Vector2(0, 1); bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, TopBarHeight);
            bar.anchoredPosition = Vector2.zero;
            AddImage(bar.gameObject, SpriteFactory.UiRounded, Palette.UiPanel);

            moneyText = AddText(bar, "Argent", "0 $", 74, Palette.UiGold, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(36, -18), new Vector2(460, 92));
            infoText = AddText(bar, "Infos", "", 32, Palette.UiText, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(40, -112), new Vector2(560, 44));
            turboText = AddText(bar, "Turbo", "", 32, new Color(1f, 0.55f, 0.2f), TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(40, -154), new Vector2(400, 44));

            // Bouton son.
            var sound = MakeButton(bar, "Son", new Vector2(1, 1), new Vector2(-470, -24), new Vector2(110, 80), Palette.UiButton, out _);
            soundText = AddText((RectTransform)sound.transform, "Texte", "Son", 28, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 80), true);
            sound.onClick.AddListener(() => { game.ToggleSound(); RefreshSound(); });
            RefreshSound();

            chests[(int)ChestKind.Quotidien] = BuildChest(bar, "Coffre du jour", new Vector2(-244, -20), ChestKind.Quotidien);
            chests[(int)ChestKind.VingtMinutes] = BuildChest(bar, "Coffre 20 min", new Vector2(-20, -20), ChestKind.VingtMinutes);
        }

        ChestUi BuildChest(RectTransform bar, string title, Vector2 pos, ChestKind kind)
        {
            var ui = new ChestUi();
            ui.button = MakeButton(bar, title, new Vector2(1, 1), pos, new Vector2(212, 170), Palette.UiButton, out ui.image);
            var rt = (RectTransform)ui.button.transform;
            // Petit coffre dessiné avec deux rectangles (couvercle + serrure).
            var box = NewRect("Icone", rt);
            SetRect(box, new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(64, 44));
            AddImage(box.gameObject, SpriteFactory.UiRounded, new Color(0.65f, 0.38f, 0.2f));
            var band = NewRect("Bande", box);
            SetRect(band, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 44));
            AddImage(band.gameObject, SpriteFactory.UiRounded, Palette.UiGold);

            ui.title = AddText(rt, "Titre", title, 26, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(206, 40), true);
            ui.timer = AddText(rt, "Timer", "", 34, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(206, 48), true);
            ui.button.onClick.AddListener(() => game.OpenChest(kind));
            return ui;
        }

        void BuildBottomBar()
        {
            var bar = NewRect("BarreBas", safeRoot);
            bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0);
            bar.pivot = new Vector2(0.5f, 0);
            bar.sizeDelta = new Vector2(0, BottomBarHeight);
            bar.anchoredPosition = Vector2.zero;
            AddImage(bar.gameObject, SpriteFactory.UiRounded, Palette.UiPanel);

            AddText(bar, "TitreAmeliorations", "AMÉLIORATIONS", 28, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(600, 40), true);

            float w = 330f, gap = 18f;
            for (int i = 0; i < 3; i++)
            {
                var type = (UpgradeType)i;
                var ui = new UpgradeUi();
                float x = (i - 1) * (w + gap);
                ui.button = MakeButton(bar, GameRules.UpgradeNames[i], new Vector2(0.5f, 0), new Vector2(x, 20), new Vector2(w, 262), Palette.UiButton, out ui.image, pivotBottom: true);
                var rt = (RectTransform)ui.button.transform;
                ui.title = AddText(rt, "Nom", GameRules.UpgradeNames[i], 30, Palette.UiText, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(w - 16, 70), true);
                ui.level = AddText(rt, "Niveau", "", 28, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(w - 16, 40), true);
                ui.effect = AddText(rt, "Effet", "", 28, Palette.UiGreen, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -124), new Vector2(w - 16, 40), true);
                ui.cost = AddText(rt, "Cout", "", 40, Palette.UiGold, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(w - 16, 56), true);
                ui.button.onClick.AddListener(() => game.BuyUpgrade(type));
                upgrades[i] = ui;
            }
        }

        void BuildToast()
        {
            var rt = NewRect("Message", safeRoot);
            SetRect(rt, new Vector2(0.5f, 1), new Vector2(0, -TopBarHeight - 24), new Vector2(1000, 90), true);
            toastGroup = rt.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            AddImage(rt.gameObject, SpriteFactory.UiRounded, new Color(0.12f, 0.14f, 0.2f, 0.85f)).raycastTarget = false;
            toastText = AddText(rt, "Texte", "", 38, Palette.UiText, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 86), true);
            toastGroup.alpha = 0f;
        }

        // =====================================================================
        // Mise à jour
        // =====================================================================

        public void ShowToast(string message)
        {
            toastText.text = message;
            toastTimer = 2.2f;
            toastGroup.alpha = 1f;
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
            }

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
                            + "   •   " + m.Data.delivered + " livrés";
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

            for (int i = 0; i < 3; i++)
            {
                var type = (UpgradeType)i;
                var ui = upgrades[i];
                int lvl = m.Level(type);
                bool maxed = GameRules.IsMaxed(lvl);
                ui.level.text = "Niv. " + lvl + " / " + GameRules.UpgradeMaxLevel;
                ui.effect.text = maxed ? GameRules.EffectLabel(type, lvl)
                                       : GameRules.EffectLabel(type, lvl) + "  →  " + GameRules.EffectLabel(type, lvl + 1);
                ui.cost.text = maxed ? "MAX" : FormatMoney(m.NextUpgradeCost(type));
                bool can = m.CanBuy(type);
                ui.image.color = can ? Palette.UiButton : Palette.UiButtonOff;
                ui.cost.color = can ? Palette.UiGold : new Color(1, 1, 1, 0.55f);
            }
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

        /// <summary>Place un rectangle par rapport à une ancre (pivot = ancre, ou centré si demandé).</summary>
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
