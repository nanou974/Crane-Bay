// -----------------------------------------------------------------------------
// Crane Bay - Fabrique de sprites procéduraux.
//
// Le prototype n'utilise AUCUNE image externe : toutes les formes sont
// dessinées au lancement dans UNE seule texture (atlas 256x256). Avantages :
//  - un seul matériau / une seule texture => les sprites sont regroupés en très
//    peu de draw calls (important sur Android d'entrée de gamme) ;
//  - contours sombres nets et anticrénelés (style anime) quelle que soit la
//    taille grâce au découpage 9-slice ;
//  - aucun asset à importer : le projet s'ouvre et se lance tel quel.
// Pour passer à de vrais graphismes, il suffit de remplacer ces sprites.
// -----------------------------------------------------------------------------
using UnityEngine;

namespace CraneBay.Game
{
    public static class SpriteFactory
    {
        public const float WorldPpu = 80f; // pixels par unité monde (contour 4 px = 0,05 unité)

        public static Sprite Rounded { get; private set; }     // rectangle arrondi + contour, 9-slice
        public static Sprite RoundedSmall { get; private set; } // idem pour petits éléments
        public static Sprite Circle { get; private set; }      // disque + contour
        public static Sprite Shadow { get; private set; }      // ombre douce, 9-slice
        public static Sprite Ring { get; private set; }        // contour seul (sélection), 9-slice
        public static Sprite Plain { get; private set; }       // carré blanc uni (1 unité)
        public static Sprite SoftDot { get; private set; }     // point flou (vagues, reflets)
        public static Sprite Container { get; private set; }   // conteneur 1,6 x 0,75 avec nervures

        // Versions pour l'interface (pixels par unité de l'UI = 100).
        public static Sprite UiRounded { get; private set; }
        public static Sprite UiRing { get; private set; }

        static Texture2D atlas;
        static Font font;

        public static Texture2D Atlas { get { Ensure(); return atlas; } }

        /// <summary>Police intégrée à Unity (aucun fichier à fournir).</summary>
        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static void Ensure()
        {
            if (atlas != null) return;
            const int size = 256;
            atlas = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "CraneBayAtlas",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);

            // Disposition de l'atlas (x, y, largeur, hauteur) :
            DrawRounded(px, size, 0, 0, 64, 64, 14, 4, true);      // Rounded
            DrawCircle(px, size, 64, 0, 64, 4);                    // Circle
            DrawShadow(px, size, 128, 0, 64, 64, 16, 12);          // Shadow
            DrawRounded(px, size, 192, 0, 32, 32, 6, 3, true);     // RoundedSmall
            FillRect(px, size, 224, 0, 16, 16);                    // Plain
            DrawSoftDot(px, size, 224, 16, 16);                    // SoftDot
            DrawContainer(px, size, 0, 64, 128, 60);               // Container
            DrawRounded(px, size, 128, 64, 64, 64, 14, 5, false);  // Ring

            atlas.SetPixels32(px);
            atlas.Apply(false, true); // true = libère la copie CPU (économie mémoire)

            var border20 = new Vector4(20, 20, 20, 20);
            Rounded = Make(new Rect(0, 0, 64, 64), WorldPpu, border20);
            Circle = Make(new Rect(64, 0, 64, 64), WorldPpu, Vector4.zero);
            Shadow = Make(new Rect(128, 0, 64, 64), WorldPpu, new Vector4(24, 24, 24, 24));
            RoundedSmall = Make(new Rect(192, 0, 32, 32), WorldPpu, new Vector4(9, 9, 9, 9));
            Plain = Make(new Rect(226, 2, 12, 12), 12f, Vector4.zero);
            SoftDot = Make(new Rect(224, 16, 16, 16), 16f, Vector4.zero);
            Container = Make(new Rect(0, 64, 128, 60), WorldPpu, Vector4.zero);
            Ring = Make(new Rect(128, 64, 64, 64), WorldPpu, border20);

            UiRounded = Make(new Rect(0, 0, 64, 64), 100f, border20);
            UiRing = Make(new Rect(128, 64, 64, 64), 100f, border20);
        }

        static Sprite Make(Rect r, float ppu, Vector4 border)
        {
            var s = Sprite.Create(atlas, r, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
            s.name = "CB_" + r.x + "_" + r.y;
            return s;
        }

        // ---------------------------------------------------------------------
        // Dessin par "distance signée" : donne des bords lisses (anticrénelés).
        // ---------------------------------------------------------------------

        static float RoundedRectSdf(float x, float y, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x) - (hw - r);
            float qy = Mathf.Abs(y) - (hh - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static void Put(Color32[] px, int size, int x, int y, Color c)
        {
            px[y * size + x] = c;
        }

        /// <summary>Rectangle arrondi blanc (dégradé léger) avec contour sombre, ou contour seul.</summary>
        static void DrawRounded(Color32[] px, int size, int ox, int oy, int w, int h, float radius, float outline, bool filled)
        {
            Color line = filled ? Palette.Outline : Color.white;
            float hw = w * 0.5f - 1f, hh = h * 0.5f - 1f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - w * 0.5f, cy = y + 0.5f - h * 0.5f;
                    float d = RoundedRectSdf(cx, cy, hw, hh, radius);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) continue;
                    Color c;
                    if (filled)
                    {
                        // Ombrage doux "cel-shading" : haut légèrement plus clair.
                        float v = (float)y / (h - 1);
                        float shade = Mathf.Lerp(0.9f, 1f, v);
                        Color fill = new Color(shade, shade, shade, 1f);
                        float t = Mathf.Clamp01(d + outline + 0.5f);
                        c = Color.Lerp(fill, line, t);
                    }
                    else
                    {
                        float inner = Mathf.Clamp01(-(d + outline) + 0.5f); // 1 à l'intérieur du contour
                        alpha *= 1f - inner;
                        c = line;
                    }
                    c.a = alpha;
                    Put(px, size, ox + x, oy + y, c);
                }
        }

        static void DrawCircle(Color32[] px, int size, int ox, int oy, int d, float outline)
        {
            float r = d * 0.5f - 1f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float cx = x + 0.5f - d * 0.5f, cy = y + 0.5f - d * 0.5f;
                    float dist = Mathf.Sqrt(cx * cx + cy * cy) - r;
                    float alpha = Mathf.Clamp01(0.5f - dist);
                    if (alpha <= 0f) continue;
                    float t = Mathf.Clamp01(dist + outline + 0.5f);
                    Color c = Color.Lerp(Color.white, Palette.Outline, t);
                    c.a = alpha;
                    Put(px, size, ox + x, oy + y, c);
                }
        }

        static void DrawShadow(Color32[] px, int size, int ox, int oy, int w, int h, float radius, float blur)
        {
            float hw = w * 0.5f - blur, hh = h * 0.5f - blur;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - w * 0.5f, cy = y + 0.5f - h * 0.5f;
                    float d = RoundedRectSdf(cx, cy, hw, hh, radius * 0.5f);
                    float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + blur * 0.5f) / blur));
                    if (a <= 0f) continue;
                    Put(px, size, ox + x, oy + y, new Color(0f, 0f, 0f, a));
                }
        }

        static void FillRect(Color32[] px, int size, int ox, int oy, int w, int h)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Put(px, size, ox + x, oy + y, Color.white);
        }

        static void DrawSoftDot(Color32[] px, int size, int ox, int oy, int d)
        {
            float r = d * 0.5f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float cx = x + 0.5f - r, cy = y + 0.5f - r;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(cx * cx + cy * cy) / r);
                    Put(px, size, ox + x, oy + y, new Color(1f, 1f, 1f, a * a));
                }
        }

        /// <summary>
        /// Conteneur vu de dessus : toit nervuré, portes à droite, pièces de coin
        /// sombres (comme les "corner castings" des vrais conteneurs ISO).
        /// Dessiné en blanc/gris pour être teinté par la couleur du conteneur.
        /// </summary>
        static void DrawContainer(Color32[] px, int size, int ox, int oy, int w, int h)
        {
            const float outline = 3f;
            float hw = w * 0.5f - 1f, hh = h * 0.5f - 1f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = x + 0.5f - w * 0.5f, cy = y + 0.5f - h * 0.5f;
                    float d = RoundedRectSdf(cx, cy, hw, hh, 4f);
                    float alpha = Mathf.Clamp01(0.5f - d);
                    if (alpha <= 0f) continue;

                    float shade = Mathf.Lerp(0.88f, 1f, (float)y / (h - 1));
                    // Nervures transversales du toit.
                    if (x > 8 && x < w - 14 && (x % 7) < 2) shade *= 0.86f;
                    // Côté portes (à droite) : deux barres verticales.
                    if (x >= w - 12 && x <= w - 10) shade *= 0.75f;
                    if (x >= w - 7 && x <= w - 6) shade *= 0.8f;
                    // Liseré intérieur plus clair (reflet doux).
                    if (y >= h - 7 && y <= h - 6 && x > 6 && x < w - 6) shade = Mathf.Min(1f, shade * 1.08f);
                    Color fill = new Color(shade, shade, shade, 1f);

                    // Pièces de coin.
                    bool corner = (x < 9 || x >= w - 9) && (y < 8 || y >= h - 8);
                    if (corner) fill = Color.Lerp(fill, Palette.Outline, 0.55f);

                    float t = Mathf.Clamp01(d + outline + 0.5f);
                    Color c = Color.Lerp(fill, Palette.Outline, t);
                    c.a = alpha;
                    Put(px, size, ox + x, oy + y, c);
                }
        }

        // ---------------------------------------------------------------------
        // Raccourcis de création d'objets (utilisés par toutes les vues).
        // ---------------------------------------------------------------------

        /// <summary>Crée un SpriteRenderer. Les sprites 9-slice prennent la taille exacte demandée.</summary>
        public static SpriteRenderer Make(string name, Transform parent, Sprite sprite, Color color,
                                          Vector2 size, Vector3 localPos, int order)
        {
            Ensure();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (sprite.border.sqrMagnitude > 0f)
            {
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
            }
            else
            {
                Vector2 b = sprite.bounds.size;
                go.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
            }
            return sr;
        }

        /// <summary>Texte dans le monde (TextMesh). height = hauteur approximative en unités.</summary>
        public static TextMesh MakeText(string name, Transform parent, string text, float height, Color color,
                                        Vector3 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.fontSize = 64;
            tm.characterSize = height * 10f / tm.fontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            tm.text = text;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            mr.sortingOrder = order;
            return tm;
        }
    }
}
