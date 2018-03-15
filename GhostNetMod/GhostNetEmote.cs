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

        public string Value;

        protected Camera Camera;

        public float Alpha = 1f;

        public bool Pop = false;
        protected float popupTime;

        protected float time;

        protected GhostNetEmote(Entity tracking)
            : base(Vector2.Zero) {
            Tracking = tracking;

            Tag = GhostModuleBackCompat.TagSubHUD;
        }

        public GhostNetEmote(Entity tracking, string value)
            : this(tracking) {
            Value = value;
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

            time += Engine.DeltaTime;

            MTexture icon = null;
            string text = null;

            if (IsIcon(Value)) {
                icon = GetIcon(Value, time);

            } else {
                text = Value;
            }

            float alpha = Alpha * popupAlpha;

            if (alpha <= 0f || (icon == null && string.IsNullOrWhiteSpace(text)))
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

            if (icon != null) {
                Vector2 size = new Vector2(icon.Width, icon.Height);
                float scale = (Size / Math.Max(size.X, size.Y)) * 0.5f * popupScale;
                size *= scale;

                pos = pos.Clamp(
                    0f + size.X * 0.5f, 0f + size.Y * 1f,
                    1920f - size.X * 0.5f, 1080f
                );

                icon.DrawJustified(
                    pos,
                    new Vector2(0.5f, 1f),
                    Color.White * alpha,
                    Vector2.One * scale
                );

            } else {
                Vector2 size = ActiveFont.Measure(text);
                float scale = (Size / Math.Max(size.X, size.Y)) * 0.5f * popupScale;
                size *= scale;

                pos = pos.Clamp(
                    0f + size.X * 0.5f, 0f + size.Y * 1f,
                    1920f - size.X * 0.5f, 1080f
                );

                ActiveFont.DrawOutline(
                    text,
                    pos,
                    new Vector2(0.5f, 1f),
                    Vector2.One * scale,
                    Color.White * alpha,
                    2f,
                    Color.Black * alpha * alpha * alpha
                );
            }
        }

        public static bool IsText(string emote) {
            return !IsIcon(emote);
        }

        public static bool IsIcon(string emote) {
            return GetIconAtlas(ref emote) != null;
        }

        public static Atlas GetIconAtlas(ref string emote) {
            if (emote.StartsWith("i:")) {
                emote = emote.Substring(2);
                return GFX.Gui;
            }

            if (emote.StartsWith("g:")) {
                emote = emote.Substring(2);
                return GFX.Game;
            }

            if (emote.StartsWith("p:")) {
                emote = emote.Substring(2);
                return GFX.Portraits;
            }

            return null;
        }

        public static MTexture GetIcon(string emote, float time) {
            Atlas atlas;
            if ((atlas = GetIconAtlas(ref emote)) == null)
                return null;

            List<string> iconPaths = new List<string>(emote.Split(' '));
            int fps;
            if (iconPaths.Count > 1 && int.TryParse(iconPaths[0], out fps)) {
                iconPaths.RemoveAt(0);
            } else {
                fps = 7; // Default FPS.
            }

            List<MTexture> icons = iconPaths.SelectMany(iconPath => {
                iconPath = iconPath.Trim();
                List<MTexture> subs = atlas.GetAtlasSubtextures(iconPath);
                if (subs.Count != 0)
                    return subs;
                if (atlas.Has(iconPath))
                    return new List<MTexture>() { atlas[iconPath] };
                if (iconPath.ToLowerInvariant() == "end")
                    return new List<MTexture>() { null };
                return new List<MTexture>();
            }).ToList();

            if (icons.Count == 0)
                return null;

            int index = (int) Math.Floor(time * fps);

            if (index >= icons.Count - 1 && icons[icons.Count - 1] == null)
                return icons[icons.Count - 2];

            return icons[index % icons.Count];
        }

    }
}
