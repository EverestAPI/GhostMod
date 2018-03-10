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
    public class GhostNetEmote : Entity {

        public static float Size = 256f;

        public Entity Tracking;

        public MTexture Icon;
        public string Text;

        protected Camera Camera;

        public float Alpha = 1f;

        public bool Pop = false;
        protected float popupTime;

        protected GhostNetEmote(Entity tracking)
            : base(Vector2.Zero) {
            Tracking = tracking;

            Tag = GhostModuleBackCompat.TagSubHUD;
        }

        public GhostNetEmote(Entity tracking, MTexture icon)
            : this(tracking) {
            Icon = icon;
        }

        public GhostNetEmote(Entity tracking, string text)
            : this(tracking) {
            Text = text;
        }

        public override void Render() {
            base.Render();

            float popupAlpha = 1f;
            float popupScale = 1f;

            // Update can halt in the pause menu.
            if (Pop) {
                popupTime += Engine.DeltaTime;
                if (popupTime < 0.1f) {
                    float t = popupTime / 0.1f;
                    // Pop in.
                    popupAlpha = Ease.CubeOut(t);
                    popupScale = Ease.ElasticOut(t);

                } else if (popupTime < 1f) {
                    // Stay.
                    popupAlpha = 1f;
                    popupScale = 1f;

                } else if (popupTime < 2f) {
                    float t = popupTime - 1f;
                    // Fade out.
                    popupAlpha = 1f - Ease.CubeIn(t);
                    popupScale = 1f - 0.4f * Ease.CubeIn(t);

                } else {
                    // Destroy.
                    RemoveSelf();
                    return;
                }
            }

            float alpha = Alpha * popupAlpha;

            if (alpha <= 0f || (Icon == null && string.IsNullOrEmpty(Text)))
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
            // - name offset - popup offset
            pos.Y -= 16f + 4f;
            pos = Camera.CameraToScreen(pos) / Camera.Viewport.Width * 1920f;

            if (Icon != null) {
                Vector2 size = new Vector2(Icon.Width, Icon.Height);
                float scale = (Size / Math.Max(size.X, size.Y)) * 0.5f * popupScale;
                size *= scale;

                pos = pos.Clamp(
                    0f + size.X * 0.5f, 0f + size.Y * 1f,
                    1920f - size.X * 0.5f, 1080f
                );

                Icon.DrawJustified(
                    pos,
                    new Vector2(0.5f, 1f),
                    Color.White * alpha,
                    Vector2.One * scale
                );

            } else {
                Vector2 size = ActiveFont.Measure(Text);
                float scale = (Size / Math.Max(size.X, size.Y)) * 0.5f * popupScale;
                size *= scale;

                pos = pos.Clamp(
                    0f + size.X * 0.5f, 0f + size.Y * 1f,
                    1920f - size.X * 0.5f, 1080f
                );

                ActiveFont.DrawOutline(
                    Text,
                    pos,
                    new Vector2(0.5f, 1f),
                    Vector2.One * scale,
                    Color.White * alpha,
                    2f,
                    Color.Black * alpha * alpha * alpha
                );
            }
        }

    }
}
