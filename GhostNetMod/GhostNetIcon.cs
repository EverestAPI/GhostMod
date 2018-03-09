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
    public class GhostNetIcon : Entity {

        public static float Size = 256f;

        public Entity Tracking;
        public MTexture Icon;

        protected Camera Camera;

        public float Alpha = 1f;

        public bool Pop = false;
        protected float popupTime;

        public GhostNetIcon(Entity tracking, MTexture icon)
            : base(Vector2.Zero) {
            Tracking = tracking;
            Icon = icon;

            Tag = GhostModuleBackCompat.TagSubHUD;
            Depth = 1;
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
                    popupScale = 1f - 0.2f * Ease.CubeIn(t);

                } else {
                    // Destroy.
                    RemoveSelf();
                }
            }

            float alpha = Alpha * popupAlpha;

            if (alpha <= 0f || Icon == null)
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
            pos.Y -= 32f;
            pos = Camera.CameraToScreen(pos) / Camera.Viewport.Width * 1920f;

            Vector2 size = new Vector2(Icon.Width, Icon.Height);
            float scale = (GhostNetIcon.Size / Math.Max(Icon.Width, Icon.Height)) * 0.5f * popupScale;
            size *= scale;

            pos = pos.Clamp(
                0f + size.X * 0.5f, 0f + size.Y * 1f,
                1920f - size.X * 0.5f, 1080f
            );

            Icon.DrawJustified(
                pos,
                new Vector2(0.5f, 0.5f),
                Color.White * alpha,
                Vector2.One * scale
            );
        }

    }
}
