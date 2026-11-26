// -----------------------------------------------------------------------------
// Crane Bay - Représentation visuelle d'un conteneur.
// Éléments issus de l'atlas commun (peu de draw calls) :
//  - ombre douce, caisse teintée (40 pieds = pleine longueur, 20 pieds = moitié) ;
//  - chiffre de la valeur actuelle ;
//  - badge "groupe froid" (frigorifique) : carré blanc à pastille cyan, qui
//    vire au rouge quand la marchandise se dégrade ;
//  - losange orange (matières dangereuses).
// -----------------------------------------------------------------------------
using System.Collections.Generic;
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
        SpriteRenderer reeferDot;
        readonly List<SpriteRenderer> badges = new List<SpriteRenderer>();
        int shownValue = -1;

        float squashTimer;
        Vector3 baseScale = Vector3.one;

        public static readonly Color HazardOrange = new Color(1f, 0.55f, 0.1f);
        static readonly Color ReeferCyan = new Color(0.3f, 0.9f, 1f);

        public static ContainerView Create(Transform parent, ContainerInfo info)
        {
            var go = new GameObject("Conteneur_" + info.id);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ContainerView>();
            view.Build(info);
            return view;
        }

        /// <summary>Taille à l'écran selon la longueur (20 ou 40 pieds).</summary>
        public static Vector2 SizeOf(ContainerInfo info)
        {
            var s = BoardLayout.ContainerSize;
            return info.IsSmall ? new Vector2(s.x * 0.5f - 0.04f, s.y) : s;
        }

        void Build(ContainerInfo info)
        {
            Info = info;
            var size = SizeOf(info);
            shadow = SpriteFactory.Make("Ombre", transform, SpriteFactory.Shadow, Palette.Shadow,
                size + new Vector2(0.35f, 0.35f), new Vector3(0.08f, -0.1f, 0f), 10);
            body = SpriteFactory.Make("Caisse", transform, SpriteFactory.Container, Palette.Of(info.Color),
                size, Vector3.zero, 11);
            float labelX = info.IsSmall ? 0f : -0.08f;
            label = SpriteFactory.MakeText("Valeur", transform, "", info.IsSmall ? 0.26f : 0.34f,
                Palette.LabelOf(info.Color), new Vector3(labelX, info.IsSmall ? -0.1f : 0f, 0f), 12);
            labelRenderer = label.GetComponent<MeshRenderer>();

            float edge = size.x * 0.5f - 0.17f;
            if (info.reefer)
            {
                // Groupe frigorifique (côté gauche).
                var unit = SpriteFactory.Make("GroupeFroid", transform, SpriteFactory.RoundedSmall, Color.white,
                    new Vector2(0.24f, 0.24f), new Vector3(-edge, info.IsSmall ? 0.17f : 0f, 0f), 12);
                reeferDot = SpriteFactory.Make("Fraicheur", transform, SpriteFactory.Circle, ReeferCyan,
                    new Vector2(0.13f, 0.13f), unit.transform.localPosition, 12);
                badges.Add(unit);
                badges.Add(reeferDot);
            }
            if (info.hazard)
            {
                // Losange "matières dangereuses" (côté droit).
                var diamond = SpriteFactory.Make("Danger", transform, SpriteFactory.RoundedSmall, HazardOrange,
                    new Vector2(0.24f, 0.24f), new Vector3(edge, info.IsSmall ? 0.17f : 0f, 0f), 12);
                diamond.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                badges.Add(diamond);
            }
            RefreshValue();
        }

        /// <summary>Met à jour le chiffre (les frigorifiques perdent de la valeur) et la pastille de fraîcheur.</summary>
        public void RefreshValue()
        {
            int v = GameRules.ValueOf(Info);
            if (v != shownValue)
            {
                shownValue = v;
                label.text = v.ToString();
            }
            if (reeferDot != null)
            {
                float f = (GameRules.Freshness(Info) - GameRules.ReeferMinFactor) / (1f - GameRules.ReeferMinFactor);
                var c = Color.Lerp(new Color(1f, 0.25f, 0.2f), ReeferCyan, f);
                c.a = reeferDot.color.a;
                reeferDot.color = c;
            }
        }

        /// <summary>
        /// Ordre d'affichage selon le niveau d'empilage : plus haut = dessiné
        /// par-dessus. Tous les conteneurs d'un même niveau partagent les mêmes
        /// ordres, ce qui permet à Unity de les regrouper (batching).
        /// </summary>
        public void SetTier(int tier) => SetOrders(10 + tier * 3, Vector3.one * BoardLayout.TierScaleOf(tier));

        /// <summary>Le conteneur est porté par la grue (au-dessus de tout le reste).</summary>
        public void SetCarried() => SetOrders(44, Vector3.one);

        void SetOrders(int baseOrder, Vector3 scale)
        {
            shadow.sortingOrder = baseOrder;
            body.sortingOrder = baseOrder + 1;
            labelRenderer.sortingOrder = baseOrder + 2;
            foreach (var b in badges) b.sortingOrder = baseOrder + 2;
            baseScale = scale;
            transform.localScale = baseScale;
        }

        /// <summary>Hauteur de levage 0..1 : grossit et éloigne l'ombre pour simuler la hauteur.</summary>
        public void SetLift(float value)
        {
            float lift = Mathf.Clamp01(value);
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
            foreach (var b in badges) { var bc = b.color; bc.a = a; b.color = bc; }
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
