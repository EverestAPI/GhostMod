using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost.Net {
    public class GhostNetIconWheel : Entity {

        public Entity Tracking;

        protected Camera Camera;

        public float Alpha = 1f;

        protected float time = 0f;

        public bool Shown = false;
        protected bool popupShown = false;
        protected float popupTime = 100f;

        public float Angle = 0f;

        public int Selected = -1;
        protected int PrevSelected;
        protected float selectedTime = 0f;

        public MTexture BG = GFX.Gui["ghostnetmod/iconwheel/bg"];
        public MTexture Line = GFX.Gui["ghostnetmod/iconwheel/line"];
        public MTexture Indicator = GFX.Gui["ghostnetmod/iconwheel/indicator"];

        public GhostNetIconWheel(Entity tracking)
            : base(Vector2.Zero) {
            Tracking = tracking;

            Tag = GhostModuleBackCompat.TagSubHUD;
            Depth = 1;
        }

        public override void Render() {
            base.Render();

            string[] icons = GhostNetModule.Settings.Icons;

            // Update can halt in the pause menu.

            if (Shown) {
                Angle = Input.MountainAim.Value.Angle();
                float angle = (float) ((Angle + Math.PI * 2f) % (Math.PI * 2f));
                float start = (-0.5f / icons.Length) * 2f * (float) Math.PI;
                if (2f * (float) Math.PI + start < angle) {
                    // Angle should be start < angle < 0, but is (TAU + start) < angle < TAU
                    angle -= 2f * (float) Math.PI;
                }
                for (int i = 0; i < icons.Length; i++) {
                    float min = ((i - 0.5f) / icons.Length) * 2f * (float) Math.PI;
                    float max = ((i + 0.5f) / icons.Length) * 2f * (float) Math.PI;
                    if (min <= angle && angle <= max) {
                        Selected = i;
                        break;
                    }
                }
            }

            time += Engine.DeltaTime;

            if (!Shown) {
                Selected = -1;
            }
            selectedTime += Engine.DeltaTime;
            if (PrevSelected != Selected) {
                selectedTime = 0f;
                PrevSelected = Selected;
            }

            float popupAlpha;
            float popupScale;

            popupTime += Engine.DeltaTime;
            if (Shown && !popupShown) {
                popupTime = 0f;
            } else if ((Shown && popupTime > 1f) ||
                (!Shown && popupTime < 1f)) {
                popupTime = 1f;
            }
            popupShown = Shown;

            if (popupTime < 0.2f) {
                float t = popupTime / 0.2f;
                // Pop in.
                popupAlpha = Ease.CubeOut(t);
                popupScale = Ease.ElasticOut(t);

            } else if (popupTime < 1f) {
                // Stay.
                popupAlpha = 1f;
                popupScale = 1f;

            } else {
                float t = (popupTime - 1f) / 0.4f;
                // Fade out.
                popupAlpha = 1f - Ease.CubeIn(t);
                popupScale = 1f - 0.2f * Ease.CubeIn(t);
            }

            float alpha = Alpha * popupAlpha;

            if (alpha <= 0f)
                return;

            if (Tracking == null)
                return;

            Level level = SceneAs<Level>();
            if (level == null)
                return;

            if (Camera == null)
                Camera = level.Camera;
            if (Camera == null)
                return;

            Vector2 pos = Tracking.Position;
            pos.Y -= 8f;
            pos = Camera.CameraToScreen(pos) / Camera.Viewport.Width * 1920f;

            float radius = BG.Width * 0.5f * 0.75f * popupScale;

            pos = pos.Clamp(
                0f + radius, 0f + radius,
                1920f - radius, 1080f - radius
            );

            // Draw.Circle(pos, radius, Color.Black * 0.8f * alpha * alpha, radius * 0.6f * (1f + 0.2f * (float) Math.Sin(time)), 8);
            BG.DrawCentered(
                pos,
                Color.White * alpha * alpha * alpha,
                Vector2.One * popupScale
            );

            Indicator.DrawCentered(
                pos,
                Color.White * alpha * alpha * alpha,
                Vector2.One * popupScale,
                Angle
            );

            for (int i = 0; i < icons.Length; i++) {
                string iconName = icons[i];
                if (string.IsNullOrEmpty(iconName) || !GFX.Gui.Has(iconName))
                    continue;
                MTexture icon = GFX.Gui[iconName];
                if (icon == null)
                    continue;

                float a = (i / (float) icons.Length) * 2f * (float) Math.PI;

                Vector2 iconPos = pos + new Vector2(
                    (float) Math.Cos(a),
                    (float) Math.Sin(a)
                ) * radius;

                Vector2 iconSize = new Vector2(icon.Width, icon.Height);
                float iconScale = (GhostNetIcon.Size / Math.Max(icon.Width, icon.Height)) * 0.25f * popupScale;
                iconSize *= iconScale;

                if (Selected == i) {
                    if (selectedTime < 0.1f) {
                        iconSize *= 1.2f - 0.2f * Ease.CubeIn(selectedTime / 0.5f);
                    }
                    icon.DrawCentered(
                        iconPos,
                        Color.White * alpha,
                        Vector2.One * iconScale * (1f + (float) Math.Sin(time * 1.8f) * 0.05f),
                        (float) Math.Sin(time * 2f) * 0.05f
                    );

                } else {
                    icon.DrawCentered(
                        iconPos,
                        Color.White * alpha * 0.7f,
                        Vector2.One * iconScale
                    );
                }

                Line.DrawCentered(
                    pos,
                    Color.White * alpha * alpha * alpha,
                    Vector2.One * popupScale,
                    ((i + 0.5f) / icons.Length) * 2f * (float) Math.PI
                );
            }
        }

    }
}
