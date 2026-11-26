// -----------------------------------------------------------------------------
// Crane Bay - Représentation visuelle d'un conteneur.
// Trois éléments seulement (ombre douce, caisse teintée, chiffre de valeur),
// tous issus de l'atlas commun pour limiter les draw calls.
// -----------------------------------------------------------------------------
using CraneBay.Core;
using UnityEngine;

namespace CraneBay.Game
{
    public class ContainerView : MonoBehaviour
    {
        public ContainerInfo Info { get; private set; }

        SpriteRenderer shadow;
        SpriteRenderer body;
        TextMesh label;
        MeshRenderer labelRenderer;

        float squashTimer;
        Vector3 baseScale = Vector3.one;
        float lift; // 0 = posé, 1 = soulevé par la grue

        public static ContainerView Create(Transform parent, ContainerInfo info)
        {
            var go = new GameObject("Conteneur_" + info.id);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ContainerView>();
            view.Build(info);
            return view;
        }

        void Build(ContainerInfo info)
        {
            Info = info;
            var size = BoardLayout.ContainerSize;
            shadow = SpriteFactory.Make("Ombre", transform, SpriteFactory.Shadow, Palette.Shadow,
                size + new Vector2(0.35f, 0.35f), new Vector3(0.08f, -0.1f, 0f), 10);
            body = SpriteFactory.Make("Caisse", transform, SpriteFactory.Container, Palette.Of(info.Color),
                size, Vector3.zero, 11);
            label = SpriteFactory.MakeText("Valeur", transform, GameRules.ValueOf(info.Color).ToString(), 0.34f,
                Palette.LabelOf(info.Color), new Vector3(-0.08f, 0f, 0f), 12);
            labelRenderer = label.GetComponent<MeshRenderer>();
        }

        /// <summary>
        /// Ordre d'affichage selon le niveau d'empilage : plus haut = dessiné
        /// par-dessus. Tous les conteneurs d'un même niveau partagent les mêmes
        /// ordres, ce qui permet à Unity de les regrouper (batching).
        /// </summary>
        public void SetTier(int tier)
        {
            int baseOrder = 10 + tier * 3;
            shadow.sortingOrder = baseOrder;
            body.sortingOrder = baseOrder + 1;
            labelRenderer.sortingOrder = baseOrder + 2;
            baseScale = Vector3.one * BoardLayout.TierScaleOf(tier);
            transform.localScale = baseScale;
        }

        /// <summary>Le conteneur est porté par la grue (au-dessus de tout le reste).</summary>
        public void SetCarried()
        {
            shadow.sortingOrder = 44;
            body.sortingOrder = 45;
            labelRenderer.sortingOrder = 46;
            baseScale = Vector3.one;
        }

        /// <summary>Hauteur de levage 0..1 : grossit et éloigne l'ombre pour simuler la hauteur.</summary>
        public void SetLift(float value)
        {
            lift = Mathf.Clamp01(value);
            float s = 1f + 0.14f * lift;
            transform.localScale = baseScale * s;
            shadow.transform.localPosition = new Vector3(0.08f + 0.35f * lift, -0.1f - 0.45f * lift, 0f);
            var c = Palette.Shadow;
            c.a *= 1f - 0.4f * lift;
            shadow.color = c;
        }

        /// <summary>Petit écrasement à la pose : donne du "poids" au conteneur.</summary>
        public void PlayLanding()
        {
            squashTimer = 0.18f;
            SetLift(0f);
        }

        /// <summary>Rend le conteneur semi-transparent (fantôme de glisser-déposer).</summary>
        public void SetAlpha(float a)
        {
            var c = body.color; c.a = a; body.color = c;
            var l = label.color; l.a = a; label.color = l;
            shadow.enabled = a >= 0.99f;
        }

        void Update()
        {
            if (squashTimer <= 0f) return;
            squashTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(squashTimer / 0.18f);
            float k = Mathf.Sin(t * Mathf.PI) * 0.08f;
            transform.localScale = new Vector3(baseScale.x * (1f + k), baseScale.y * (1f - k), 1f);
            if (squashTimer <= 0f) transform.localScale = baseScale;
        }
    }
}
