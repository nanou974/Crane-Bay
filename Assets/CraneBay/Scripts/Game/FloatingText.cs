// -----------------------------------------------------------------------------
// Crane Bay - Petits textes flottants ("+250 $") au-dessus des livraisons.
// Réserve d'objets réutilisés (pas d'allocation pendant le jeu).
// -----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

namespace CraneBay.Game
{
    public class FloatingText : MonoBehaviour
    {
        class Item { public TextMesh text; public float life; public Vector3 start; public Color color; }

        const float Life = 1.1f;
        readonly List<Item> items = new List<Item>();

        public void Spawn(Vector3 worldPos, string message, Color color)
        {
            Item item = null;
            foreach (var it in items) if (it.life <= 0f) { item = it; break; }
            if (item == null)
            {
                item = new Item { text = SpriteFactory.MakeText("TexteFlottant", transform, "", 0.5f, color, Vector3.zero, 70) };
                items.Add(item);
            }
            item.text.gameObject.SetActive(true);
            item.text.text = message;
            item.start = worldPos;
            item.color = color;
            item.life = Life;
            item.text.transform.position = worldPos;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var it in items)
            {
                if (it.life <= 0f) continue;
                it.life -= dt;
                float k = 1f - Mathf.Clamp01(it.life / Life);
                it.text.transform.position = it.start + new Vector3(0f, 0.9f * (1f - (1f - k) * (1f - k)), 0f);
                float s = k < 0.15f ? Mathf.Lerp(0.6f, 1.15f, k / 0.15f) : Mathf.Lerp(1.15f, 1f, (k - 0.15f) / 0.85f);
                it.text.transform.localScale = new Vector3(s, s, 1f);
                var c = it.color;
                c.a = 1f - Mathf.Clamp01((k - 0.6f) / 0.4f);
                it.text.color = c;
                if (it.life <= 0f) it.text.gameObject.SetActive(false);
            }
        }
    }
}
