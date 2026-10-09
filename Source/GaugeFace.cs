using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ApocaChaseCamera
{
    // Instrument artwork is drawn by us, never copied from an item slot.
    internal sealed class GaugeFace : MaskableGraphic
    {
        internal bool Tachometer, GearFace;
        private float fraction;
        private struct Quad
        {
            internal Vector3 A, B, C, D;
            internal Color Tint;
        }
        private readonly List<Quad> artwork = new List<Quad>(180);
        private Rect artworkRect;
        private bool hasArtwork, artworkTachometer, artworkGearFace;
        private int needleIndex;
        private float centerX, centerY, radius, stroke;
        internal int ArtworkBuilds { get; private set; }
        internal float Fraction
        {
            get { return fraction; }
            set {
                float next = CameraMath.Clamp(HudMath.Reading(value), 0f, 1f);
                if (Math.Abs(next - fraction) < 0.001f) return;
                fraction = next; SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float width = rect.xMax - rect.xMin, height = rect.yMax - rect.yMin;
            if (width <= 0f || height <= 0f) return;
            if (!hasArtwork || artworkRect.xMin != rect.xMin || artworkRect.xMax != rect.xMax ||
                artworkRect.yMin != rect.yMin || artworkRect.yMax != rect.yMax ||
                artworkTachometer != Tachometer || artworkGearFace != GearFace)
                BuildArtwork(rect, width, height);
            for (int i = 0; i < needleIndex; i++) Append(mesh, artwork[i]);
            if (!GearFace)
            {
                Color needle = Tachometer && fraction >= 0.9f ? new Color(0.96f, 0.25f, 0.12f, 1f) :
                    new Color(0.94f, 0.70f, 0.31f, 1f);
                Quad line;
                if (Line(Point(centerX, centerY, 0f, 180f * (1f - fraction)),
                    Point(centerX, centerY, radius * 0.69f, 180f * (1f - fraction)), stroke * 1.8f, needle, out line))
                    Append(mesh, line);
            }
            // The centre hub stays above the moving needle, as in the original art.
            for (int i = needleIndex; i < artwork.Count; i++) Append(mesh, artwork[i]);
        }

        private void BuildArtwork(Rect rect, float width, float height)
        {
            artwork.Clear(); artworkRect = rect; artworkTachometer = Tachometer; artworkGearFace = GearFace;
            hasArtwork = true; ArtworkBuilds++;
            float x = (rect.xMin + rect.xMax) * 0.5f;
            float y = rect.yMin + height * (GearFace ? 0.5f : 0.30f);
            centerX = x; centerY = y;
            radius = Math.Min(width * 0.43f, height * (GearFace ? 0.43f : 0.63f));
            stroke = Math.Max(0.7f, radius * 0.035f);
            Color brass = new Color(0.72f, 0.57f, 0.36f, 0.9f);
            Color rust = new Color(0.37f, 0.22f, 0.12f, 1f);
            Color ivory = new Color(0.90f, 0.84f, 0.68f, 1f);
            if (GearFace)
            {
                Arc(artwork, x, y, radius, 0f, 360f, stroke * 2f, rust);
                Arc(artwork, x, y, radius - stroke * 2f, 0f, 360f, stroke, brass);
                for (int i = 0; i < 12; i++)
                    Radial(artwork, x, y, i * 30f, radius * 0.86f, radius * 0.94f, stroke, brass);
                needleIndex = artwork.Count;
                return;
            }
            Arc(artwork, x, y, radius, 0f, 180f, stroke * 2.4f, rust);
            Arc(artwork, x, y, radius - stroke * 1.5f, 0f, 180f, stroke, brass);
            if (Tachometer)
                Arc(artwork, x, y, radius - stroke * 4f, 0f, 18f, stroke * 2.5f,
                    new Color(0.76f, 0.20f, 0.10f, 1f));
            for (int i = 0; i <= 16; i++)
                Radial(artwork, x, y, 180f - i * 180f / 16f,
                    radius * (i % 4 == 0 ? 0.72f : 0.81f), radius * 0.91f,
                    stroke * (i % 4 == 0 ? 1.25f : 0.8f), ivory);
            needleIndex = artwork.Count;
            Arc(artwork, x, y, stroke * 2f, 0f, 360f, stroke * 1.5f, brass);
        }

        private static void Arc(List<Quad> mesh, float x, float y, float radius,
            float start, float end, float stroke, Color tint)
        {
            int steps = Math.Max(1, (int)Math.Ceiling((end - start) / 5f));
            for (int i = 0; i < steps; i++)
            {
                Vector3 a = Point(x, y, radius, start + (end - start) * i / steps);
                Vector3 b = Point(x, y, radius, start + (end - start) * (i + 1) / steps);
                Quad line;
                if (Line(a, b, stroke, tint, out line)) mesh.Add(line);
            }
        }

        private static void Radial(List<Quad> mesh, float x, float y, float angle,
            float inner, float outer, float stroke, Color tint)
        {
            Quad line;
            if (Line(Point(x, y, inner, angle), Point(x, y, outer, angle), stroke, tint, out line)) mesh.Add(line);
        }

        private static Vector3 Point(float x, float y, float radius, float angle)
        {
            double radians = angle * Math.PI / 180.0;
            return new Vector3(x + radius * (float)Math.Cos(radians), y + radius * (float)Math.Sin(radians), 0f);
        }

        private static bool Line(Vector3 a, Vector3 b, float width, Color tint, out Quad line)
        {
            line = new Quad();
            float dx = b.x - a.x, dy = b.y - a.y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.0001f) return false;
            float px = -dy / length * width * 0.5f, py = dx / length * width * 0.5f;
            line.A = new Vector3(a.x + px, a.y + py, 0f);
            line.B = new Vector3(b.x + px, b.y + py, 0f);
            line.C = new Vector3(b.x - px, b.y - py, 0f);
            line.D = new Vector3(a.x - px, a.y - py, 0f);
            line.Tint = tint; return true;
        }

        private static void Append(VertexHelper mesh, Quad line)
        {
            int index = mesh.currentVertCount;
            mesh.AddVert(line.A, line.Tint, Vector2.zero);
            mesh.AddVert(line.B, line.Tint, Vector2.zero);
            mesh.AddVert(line.C, line.Tint, Vector2.zero);
            mesh.AddVert(line.D, line.Tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
