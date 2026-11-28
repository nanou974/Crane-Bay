// -----------------------------------------------------------------------------
// Crane Bay - Écran du mini-jeu de détente « Sortez le conteneur rouge ».
//
// Gros bouton rond flottant « Détente » (il clignote pendant l'attente du navire).
// Le port est mis en pause pendant le puzzle (voir GameManager.OpenPuzzle).
// On fait glisser les conteneurs dans leur sens ; le rouge sort par la droite.
// Les puzzles sont générés sur un fil séparé pour ne pas figer l'écran.
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using CraneBay.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraneBay.Game
{
    public partial class Hud
    {
        static readonly Color RelaxColor = new Color(0.16f, 0.7f, 0.72f);
        const float PuzzleCell = 150f;
        const float PuzzleBoard = PuzzleCell * SlidePuzzle.Size;

        Button relaxButton;
        Image relaxImage;

        GameObject puzzleRoot;
        RectTransform puzzleBoard;
        Text puzzleTitle, puzzleMoves, puzzleResult;
        GameObject puzzleNextButton, puzzleRestartButton;
        readonly List<PuzzlePieceUi> pieceUis = new List<PuzzlePieceUi>();

        SlidePuzzle puzzle;
        bool rewardGiven;
        float exitAnim = -1f;

        // Génération en arrière-plan.
        readonly object puzzleLock = new object();
        SlidePuzzle readyPuzzle;
        int readyIndex = -1, requestedIndex = -1;

        public bool PuzzleOpen => puzzleRoot != null && puzzleRoot.activeSelf;

        // =====================================================================
        // Construction
        // =====================================================================

        const float RelaxSize = 150f;

        /// <summary>
        /// Gros bouton rond flottant, en bas à droite juste au-dessus des améliorations :
        /// zone vide du terrain (sous la voie ferrée, à droite des wagons).
        /// </summary>
        void BuildRelaxButton()
        {
            relaxButton = MakeButton(safeRoot, "Detente", new Vector2(1, 0), new Vector2(-22, BottomBarHeight + 14),
                new Vector2(RelaxSize, RelaxSize), RelaxColor, out relaxImage);
            relaxImage.sprite = SpriteFactory.Circle;
            relaxImage.type = Image.Type.Simple;
            var rt = (RectTransform)relaxButton.transform;
            // Ombre portée et liseré blanc pour bien détacher le bouton du décor.
            var shadow = relaxImage.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.45f);
            shadow.effectDistance = new Vector2(5, -7);
            var outline = relaxImage.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4, -4);

            // Pictogramme : mini-plateau avec un conteneur rouge qui sort par la droite.
            var board = NewRect("Plateau", rt);
            SetRect(board, new Vector2(0.5f, 0.5f), new Vector2(-6, 14), new Vector2(76, 62), true);
            board.pivot = new Vector2(0.5f, 0.5f);
            AddImage(board.gameObject, SpriteFactory.UiRounded, new Color(0.08f, 0.25f, 0.3f, 0.85f)).raycastTarget = false;
            var red = NewRect("Rouge", board);
            SetRect(red, new Vector2(0.5f, 0.5f), new Vector2(8, 0), new Vector2(46, 18), true);
            red.pivot = new Vector2(0.5f, 0.5f);
            AddImage(red.gameObject, SpriteFactory.UiRounded, Palette.Of(ContainerColor.Rouge)).raycastTarget = false;
            foreach (var bx in new[] { -26f, 20f })
            {
                var blk = NewRect("Bloc", board);
                SetRect(blk, new Vector2(0.5f, 0.5f), new Vector2(bx, bx < 0 ? 14 : -18), new Vector2(16, 26), true);
                blk.pivot = new Vector2(0.5f, 0.5f);
                AddImage(blk.gameObject, SpriteFactory.UiRounded, Color.white).raycastTarget = false;
            }
            AddText(rt, "Fleche", "→", 34, Palette.UiGold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(46, 14), new Vector2(40, 40), true);
            AddText(rt, "Texte", "Détente", 26, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0, 20), new Vector2(RelaxSize, 34), true);
            relaxButton.onClick.AddListener(() => game.OpenPuzzle());
        }

        void BuildPuzzle()
        {
            puzzleRoot = new GameObject("MiniJeu", typeof(RectTransform));
            var root = (RectTransform)puzzleRoot.transform;
            root.SetParent(safeRoot, false);
            Stretch(root);
            var dim = AddImage(root.gameObject, SpriteFactory.UiRounded, new Color(0.04f, 0.07f, 0.11f, 0.94f));
            dim.type = Image.Type.Simple;

            puzzleTitle = AddText(root, "Titre", "Détente", 64, Palette.UiGold, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, 700), new Vector2(1000, 90), true);
            AddText(root, "Consigne", "Faites glisser les conteneurs pour sortir le rouge par la droite.\nLe port est en pause.", 30,
                new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 610), new Vector2(1000, 90), true);

            // Plateau : un petit parc vu de dessus, avec une sortie à droite de la rangée du rouge.
            var frame = NewRect("Cadre", root);
            SetRect(frame, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(PuzzleBoard + 36, PuzzleBoard + 36), true);
            frame.pivot = new Vector2(0.5f, 0.5f);
            AddImage(frame.gameObject, SpriteFactory.UiRounded, new Color(0.25f, 0.28f, 0.33f));

            puzzleBoard = NewRect("Plateau", frame);
            SetRect(puzzleBoard, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PuzzleBoard, PuzzleBoard), true);
            puzzleBoard.pivot = new Vector2(0.5f, 0.5f);
            AddImage(puzzleBoard.gameObject, SpriteFactory.UiRounded, new Color(0.42f, 0.45f, 0.5f)).type = Image.Type.Simple;
            for (int y = 0; y < SlidePuzzle.Size; y++)
                for (int x = 0; x < SlidePuzzle.Size; x++)
                {
                    var cell = NewRect("Case", puzzleBoard);
                    SetBoardRect(cell, x, y, 1, 1, 6f);
                    AddImage(cell.gameObject, SpriteFactory.UiRounded, new Color(0.36f, 0.39f, 0.44f)).raycastTarget = false;
                }

            // Sortie : ouverture dorée dans le cadre, à droite de la rangée du rouge.
            var exit = NewRect("Sortie", frame);
            SetRect(exit, new Vector2(1f, 0.5f), new Vector2(0, BoardY(SlidePuzzle.ExitRow, 1)), new Vector2(40, PuzzleCell - 10), false);
            exit.pivot = new Vector2(0.5f, 0.5f);
            AddImage(exit.gameObject, SpriteFactory.UiRounded, Palette.UiGold).raycastTarget = false;

            puzzleMoves = AddText(root, "Coups", "", 40, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, -520), new Vector2(1000, 60), true);
            puzzleResult = AddText(root, "Resultat", "", 52, Palette.UiGreen, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0, -600), new Vector2(1000, 80), true);

            var restart = MakeButton(root, "Recommencer", new Vector2(0.5f, 0.5f), new Vector2(-270, -720), new Vector2(480, 120), Palette.UiButton, out _);
            ((RectTransform)restart.transform).pivot = new Vector2(0.5f, 0.5f);
            AddText((RectTransform)restart.transform, "Texte", "Recommencer", 42, Palette.UiText, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 120), true);
            restart.onClick.AddListener(RestartPuzzle);
            puzzleRestartButton = restart.gameObject;

            var next = MakeButton(root, "Suivant", new Vector2(0.5f, 0.5f), new Vector2(-270, -720), new Vector2(480, 120), Palette.UiGreen, out _);
            ((RectTransform)next.transform).pivot = new Vector2(0.5f, 0.5f);
            AddText((RectTransform)next.transform, "Texte", "Puzzle suivant", 42, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 120), true);
            next.onClick.AddListener(() => LoadPuzzle(game.Model.Data.puzzlesSolved));
            puzzleNextButton = next.gameObject;

            var back = MakeButton(root, "Retour", new Vector2(0.5f, 0.5f), new Vector2(270, -720), new Vector2(480, 120), PauseColor, out _);
            ((RectTransform)back.transform).pivot = new Vector2(0.5f, 0.5f);
            AddText((RectTransform)back.transform, "Texte", "Retour au port", 42, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480, 120), true);
            back.onClick.AddListener(() => game.ClosePuzzle());

            puzzleRoot.SetActive(false);
            RequestPuzzle(game.Model.Data.puzzlesSolved); // prêt avant même l'ouverture
        }

        // Coordonnées du plateau (origine au centre, y vers le haut ; rangée 0 en haut).
        static float BoardX(int x, int w) => -PuzzleBoard * 0.5f + (x + w * 0.5f) * PuzzleCell;
        static float BoardY(int y, int h) => PuzzleBoard * 0.5f - (y + h * 0.5f) * PuzzleCell;

        static void SetBoardRect(RectTransform rt, int x, int y, int w, int h, float inset)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w * PuzzleCell - inset * 2, h * PuzzleCell - inset * 2);
            rt.anchoredPosition = new Vector2(BoardX(x, w), BoardY(y, h));
        }

        // =====================================================================
        // Ouverture, chargement, résolution
        // =====================================================================

        public void ShowPuzzle()
        {
            ClosePanel();
            puzzleRoot.SetActive(true);
            puzzleRoot.transform.SetAsLastSibling();
            if (puzzle == null || puzzle.IsSolved || puzzle.Index != game.Model.Data.puzzlesSolved)
                LoadPuzzle(game.Model.Data.puzzlesSolved);
        }

        public void HidePuzzle()
        {
            if (puzzleRoot != null) puzzleRoot.SetActive(false);
        }

        void LoadPuzzle(int index)
        {
            puzzle = null;
            exitAnim = -1f;
            rewardGiven = false;
            ClearPieces();
            puzzleTitle.text = "Détente · Puzzle n° " + (index + 1);
            puzzleMoves.text = "Préparation du puzzle…";
            puzzleResult.text = "";
            puzzleNextButton.SetActive(false);
            puzzleRestartButton.SetActive(true);
            RequestPuzzle(index);
        }

        /// <summary>Lance la génération du puzzle "index" sur un fil séparé (si pas déjà prêt).</summary>
        void RequestPuzzle(int index)
        {
            lock (puzzleLock)
            {
                if (readyIndex == index || requestedIndex == index) return;
                requestedIndex = index;
            }
            var t = new Thread(() =>
            {
                var p = SlidePuzzle.Generate(index);
                lock (puzzleLock)
                {
                    if (requestedIndex != index) return; // demande dépassée entre-temps
                    readyPuzzle = p;
                    readyIndex = index;
                    requestedIndex = -1;
                }
            }) { IsBackground = true, Name = "Puzzle" };
            t.Start();
        }

        void UpdatePuzzle()
        {
            // Bouton Détente : clignote pendant l'attente du navire (temps mort idéal).
            if (relaxImage != null)
            {
                bool waiting = !game.Model.Data.shipDocked && !game.Paused;
                float k = waiting ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
                relaxImage.color = Color.Lerp(RelaxColor, new Color(0.55f, 1f, 0.95f), k);
                float s = 1f + 0.08f * k;
                relaxButton.transform.localScale = new Vector3(s, s, 1f);
            }
            if (!PuzzleOpen) return;

            int wanted = game.Model.Data.puzzlesSolved;
            if (puzzle == null)
            {
                lock (puzzleLock)
                {
                    if (readyIndex == wanted && readyPuzzle != null) { puzzle = readyPuzzle; readyPuzzle = null; readyIndex = -1; }
                }
                if (puzzle != null) { BuildPieces(); RefreshPuzzleText(); }
                return;
            }

            // Sortie du conteneur rouge.
            if (exitAnim >= 0f && pieceUis.Count > 0)
            {
                exitAnim += Time.unscaledDeltaTime;
                var red = pieceUis[SlidePuzzle.Target].Rect;
                var p = red.anchoredPosition;
                p.x = BoardX(puzzle.Col(SlidePuzzle.Target), 2) + exitAnim * exitAnim * 2400f;
                red.anchoredPosition = p;
                if (exitAnim > 0.6f) { red.gameObject.SetActive(false); exitAnim = -1f; }
            }
        }

        void ClearPieces()
        {
            foreach (var ui in pieceUis) if (ui != null) Destroy(ui.gameObject);
            pieceUis.Clear();
        }

        void BuildPieces()
        {
            ClearPieces();
            for (int i = 0; i < puzzle.pieces.Count; i++)
            {
                var pc = puzzle.pieces[i];
                var rt = NewRect(i == SlidePuzzle.Target ? "Rouge" : "Conteneur" + i, puzzleBoard);
                var color = Palette.Of((ContainerColor)pc.color);
                var img = AddImage(rt.gameObject, SpriteFactory.UiRounded, color);
                // Nervures du conteneur (dans le sens de la longueur).
                for (int k = 1; k < pc.len * 3; k++)
                {
                    var rib = NewRect("Nervure", rt);
                    float f = (float)k / (pc.len * 3);
                    rib.anchorMin = pc.horizontal ? new Vector2(f, 0.18f) : new Vector2(0.18f, f);
                    rib.anchorMax = pc.horizontal ? new Vector2(f, 0.82f) : new Vector2(0.82f, f);
                    rib.sizeDelta = pc.horizontal ? new Vector2(4, 0) : new Vector2(0, 4);
                    var ri = AddImage(rib.gameObject, SpriteFactory.UiRounded, new Color(0, 0, 0, 0.16f));
                    ri.type = Image.Type.Simple;
                    ri.raycastTarget = false;
                }
                if (i == SlidePuzzle.Target)
                    AddText(rt, "Fleche", "→", 64, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 100), true);
                var ui = rt.gameObject.AddComponent<PuzzlePieceUi>();
                ui.Init(this, i, img);
                pieceUis.Add(ui);
                PlacePiece(i);
            }
        }

        void PlacePiece(int i)
        {
            var pc = puzzle.pieces[i];
            SetBoardRect(pieceUis[i].Rect, puzzle.Col(i), puzzle.Row(i), pc.horizontal ? pc.len : 1, pc.horizontal ? 1 : pc.len, 9f);
        }

        void RefreshPuzzleText()
        {
            if (puzzle == null) return;
            puzzleMoves.text = "Coups : " + puzzle.Moves + "   ·   Record possible : " + puzzle.MinMoves;
        }

        void RestartPuzzle()
        {
            if (puzzle == null || puzzle.IsSolved) return;
            puzzle.Restart();
            for (int i = 0; i < pieceUis.Count; i++) PlacePiece(i);
            RefreshPuzzleText();
            game.PlayUi(Sfx.Select);
        }

        // --- Glisser un conteneur ---------------------------------------------

        int dragPiece = -1, dragMin, dragMax;
        Vector2 dragStartLocal;
        float dragStartPos;

        internal void PieceDown(int i, PointerEventData e)
        {
            if (puzzle == null || puzzle.IsSolved) return;
            dragPiece = i;
            puzzle.Range(i, out dragMin, out dragMax);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(puzzleBoard, e.position, e.pressEventCamera, out dragStartLocal);
            var pc = puzzle.pieces[i];
            dragStartPos = pc.horizontal ? pieceUis[i].Rect.anchoredPosition.x : pieceUis[i].Rect.anchoredPosition.y;
        }

        internal void PieceDrag(int i, PointerEventData e)
        {
            if (dragPiece != i || puzzle == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(puzzleBoard, e.position, e.pressEventCamera, out var local);
            var pc = puzzle.pieces[i];
            var rt = pieceUis[i].Rect;
            var p = rt.anchoredPosition;
            int w = pc.horizontal ? pc.len : 1, h = pc.horizontal ? 1 : pc.len;
            if (pc.horizontal)
            {
                float lo = BoardX(dragMin, w), hi = BoardX(dragMax, w);
                p.x = Mathf.Clamp(dragStartPos + local.x - dragStartLocal.x, lo, hi);
            }
            else
            {
                // Rangée plus grande = plus bas à l'écran.
                float top = BoardY(dragMin, h), bottom = BoardY(dragMax, h);
                p.y = Mathf.Clamp(dragStartPos + local.y - dragStartLocal.y, bottom, top);
            }
            rt.anchoredPosition = p;
        }

        internal void PieceUp(int i)
        {
            if (dragPiece != i || puzzle == null) return;
            dragPiece = -1;
            var pc = puzzle.pieces[i];
            var p = pieceUis[i].Rect.anchoredPosition;
            int w = pc.horizontal ? pc.len : 1, h = pc.horizontal ? 1 : pc.len;
            float cells = pc.horizontal
                ? (p.x + PuzzleBoard * 0.5f) / PuzzleCell - w * 0.5f
                : (PuzzleBoard * 0.5f - p.y) / PuzzleCell - h * 0.5f;
            int to = Mathf.Clamp(Mathf.RoundToInt(cells), dragMin, dragMax);
            if (puzzle.Move(i, to)) game.PlayUi(Sfx.Drop);
            PlacePiece(i);
            RefreshPuzzleText();
            if (puzzle.IsSolved) OnPuzzleSolved();
        }

        void OnPuzzleSolved()
        {
            if (rewardGiven) return;
            rewardGiven = true;
            bool perfect = puzzle.Moves <= puzzle.MinMoves;
            long reward = game.CompletePuzzle(perfect);
            exitAnim = 0f;
            puzzleResult.text = (perfect ? "Parfait ! " : "Bravo ! ") + "+" + FormatMoney(reward);
            puzzleResult.color = perfect ? Palette.UiGold : Palette.UiGreen;
            puzzleMoves.text = "Résolu en " + puzzle.Moves + " coups" + (perfect ? " (le minimum !)" : " (minimum : " + puzzle.MinMoves + ")");
            puzzleRestartButton.SetActive(false);
            puzzleNextButton.SetActive(true);
            // Prépare déjà le suivant.
            RequestPuzzle(game.Model.Data.puzzlesSolved);
        }
    }

    /// <summary>Un conteneur du mini-jeu : relaie le glisser au Hud.</summary>
    public class PuzzlePieceUi : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        Hud hud;
        int index;
        public RectTransform Rect { get; private set; }

        public void Init(Hud h, int i, Image img)
        {
            hud = h;
            index = i;
            Rect = (RectTransform)transform;
            img.raycastTarget = true;
        }

        public void OnPointerDown(PointerEventData e) => hud.PieceDown(index, e);
        public void OnDrag(PointerEventData e) => hud.PieceDrag(index, e);
        public void OnPointerUp(PointerEventData e) => hud.PieceUp(index);
    }
}
