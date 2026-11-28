// -----------------------------------------------------------------------------
// Crane Bay - Contrôles tactiles (et souris dans l'éditeur).
//
// Deux façons de jouer, utilisables indifféremment :
//  1) TAP-TAP : toucher une pile (sélection), puis toucher la destination.
//  2) GLISSER-DÉPOSER : poser le doigt sur une pile, glisser jusqu'à la
//     destination, relâcher.
// Les touches sur l'interface (boutons) sont ignorées ici.
// Utilise l'ancien "Input Manager" de Unity (aucun package supplémentaire).
// -----------------------------------------------------------------------------
using CraneBay.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CraneBay.Game
{
    public class InputController : MonoBehaviour
    {
        const float DragThresholdPixels = 18f;

        GameManager game;
        Camera cam;

        bool pressing;
        bool dragging;
        bool ignoreGesture;
        Vector2 downScreen;
        Location downLoc = Location.None;

        public void Init(GameManager gm, Camera camera)
        {
            game = gm;
            cam = camera;
            // Évite que chaque toucher génère aussi un "clic souris" en double.
            if (Input.touchSupported) Input.simulateMouseWithTouches = false;
        }

        void Update()
        {
            if (game == null || game.Paused) return;
            if (!ReadPointer(out var screen, out bool down, out bool up, out int pointerId)) return;

            if (down)
            {
                ignoreGesture = IsOverUi(pointerId);
                if (ignoreGesture) return;
                pressing = true;
                dragging = false;
                downScreen = screen;
                downLoc = game.HitTest(ToWorld(screen));
                return;
            }

            if (!pressing || ignoreGesture)
            {
                if (up) { pressing = false; ignoreGesture = false; }
                return;
            }

            Vector2 world = ToWorld(screen);

            if (!dragging && (screen - downScreen).magnitude > DragThresholdPixels * Mathf.Max(1f, Screen.dpi / 160f))
            {
                // Début de glisser : uniquement depuis une pile dont on peut saisir le dessus.
                if (game.IsPickable(downLoc))
                {
                    dragging = true;
                    game.BeginDrag(downLoc);
                }
            }

            if (dragging) game.UpdateDrag(world);

            if (up)
            {
                var upLoc = game.HitTest(world);
                if (dragging) game.EndDrag(downLoc, upLoc);
                else game.OnTap(upLoc);
                pressing = false;
                dragging = false;
            }
        }

        Vector2 ToWorld(Vector2 screen)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            return new Vector2(w.x, w.y);
        }

        static bool ReadPointer(out Vector2 pos, out bool down, out bool up, out int pointerId)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                pos = t.position;
                down = t.phase == TouchPhase.Began;
                up = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                pointerId = t.fingerId;
                return true;
            }
            pos = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            up = Input.GetMouseButtonUp(0);
            pointerId = -1;
            return down || up || Input.GetMouseButton(0);
        }

        static bool IsOverUi(int pointerId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }
    }
}
