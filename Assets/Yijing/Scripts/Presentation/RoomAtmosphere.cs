using UnityEngine;
using UnityEngine.UI;

namespace Yijing.Presentation
{
    // Lightweight UI geometry. No bitmap generation, imported particles or per-frame allocations.
    public sealed class RoomAtmosphere : MaskableGraphic
    {
        public bool Motion = true, Steam, Pouring;
        public float Fill;
        private float clock;
        private void Update() { if (Motion) clock += Time.unscaledDeltaTime; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < 35; i++) {
                float x = 168 + Mathf.Repeat(i * 71.7f, 272);
                float y = 59 + Mathf.Repeat(i * 37.4f + clock * (31 + i % 5 * 3), 232);
                Line(vh, new Vector2(x, -y), new Vector2(x - 2, -y - 12), .65f, new Color(.86f, .93f, .92f, .19f));
            }
            if (Steam) for (int j = 0; j < 3; j++) for (int k = 0; k < 11; k++) {
                float y = k * 3.4f; float phase = clock * .8f + j * 1.5f;
                var a = new Vector2(267 + j * 8 + Mathf.Sin(y * .1f + phase) * 3, -449 + y);
                var b = new Vector2(267 + j * 8 + Mathf.Sin((y + 3.4f) * .1f + phase) * 3, -445.6f + y);
                Line(vh, a, b, 1.4f, new Color(1, .98f, .91f, .20f * (1 - k / 12f)));
            }
            if (Pouring) {
                Line(vh, new Vector2(245, -404), new Vector2(278, -453), 2.3f, new Color(.92f, .90f, .73f, .75f));
                Line(vh, new Vector2(267, -461 + Fill * 8), new Vector2(289, -461 + Fill * 8), 2, new Color(.65f, .56f, .25f, .65f));
            }
        }
        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            var n = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
