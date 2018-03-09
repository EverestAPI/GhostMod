﻿using FMOD.Studio;
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
    public class GhostNetPopupWheel : Entity {

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

        public Color TextSelectColorA = Calc.HexToColor("84FF54");
        public Color TextSelectColorB = Calc.HexToColor("FCFF59");

        public GhostNetPopupWheel(Entity tracking)
            : base(Vector2.Zero) {
            Tracking = tracking;

            Tag = GhostModuleBackCompat.TagSubHUD;
            Depth = -1;
        }

        public override void Render() {
            base.Render();

            string[] emotes = GhostNetModule.Settings.Emotes;

            // Update can halt in the pause menu.

            if (Shown) {
                Angle = Input.MountainAim.Value.Angle();
                float angle = (float) ((Angle + Math.PI * 2f) % (Math.PI * 2f));
                float start = (-0.5f / emotes.Length) * 2f * (float) Math.PI;
                if (2f * (float) Math.PI + start < angle) {
                    // Angle should be start < angle < 0, but is (TAU + start) < angle < TAU
                    angle -= 2f * (float) Math.PI;
                }
                for (int i = 0; i < emotes.Length; i++) {
                    float min = ((i - 0.5f) / emotes.Length) * 2f * (float) Math.PI;
                    float max = ((i + 0.5f) / emotes.Length) * 2f * (float) Math.PI;
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

            if (popupTime < 0.1f) {
                float t = popupTime / 0.1f;
                // Pop in.
                popupAlpha = Ease.CubeOut(t);
                popupScale = Ease.ElasticOut(t);

            } else if (popupTime < 1f) {
                // Stay.
                popupAlpha = 1f;
                popupScale = 1f;

            } else {
                float t = (popupTime - 1f) / 0.2f;
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

            float selectedScale = 1.2f - 0.2f * Calc.Clamp(Ease.CubeOut(selectedTime / 0.1f), 0f, 1f) + (float) Math.Sin(time * 1.8f) * 0.05f;

            for (int i = 0; i < emotes.Length; i++) {
                string emote = emotes[i];
                if (string.IsNullOrEmpty(emote))
                    continue;

                float a = (i / (float) emotes.Length) * 2f * (float) Math.PI;
                Vector2 emotePos = pos + new Vector2(
                    (float) Math.Cos(a),
                    (float) Math.Sin(a)
                ) * radius;

                if (emote.StartsWith("i:")) {
                    string iconPath = emote.Substring(2);
                    if (!GFX.Gui.Has(iconPath))
                        continue;
                    MTexture icon = GFX.Gui[iconPath];
                    if (icon == null)
                        continue;

                    Vector2 iconSize = new Vector2(icon.Width, icon.Height);
                    float iconScale = (GhostNetPopup.Size / Math.Max(iconSize.X, iconSize.Y)) * 0.24f * popupScale;

                    icon.DrawCentered(
                        emotePos,
                        Color.White * (Selected == i ? (Calc.BetweenInterval(selectedTime, 0.1f) ? 0.9f : 1f) : 0.7f) * alpha,
                        Vector2.One * (Selected == i ? selectedScale : 1f) * iconScale,
                        Selected == i ? (float) Math.Sin(time * 2f) * 0.05f : 0f
                    );

                } else {
                    Vector2 textSize = ActiveFont.Measure(emote);
                    float textScale = (GhostNetPopup.Size / Math.Max(textSize.X, textSize.Y)) * 0.24f * popupScale;

                    ActiveFont.DrawOutline(
                        emote,
                        emotePos,
                        new Vector2(0.5f, 0.5f),
                        Vector2.One * (Selected == i ? selectedScale : 1f) * textScale,
                        (Selected == i ? (Calc.BetweenInterval(selectedTime, 0.1f) ? TextSelectColorA : TextSelectColorB) : Color.LightSlateGray) * alpha,
                        2f,
                        Color.Black * alpha * alpha * alpha
                    );
                }

                Line.DrawCentered(
                    pos,
                    Color.White * alpha * alpha * alpha,
                    Vector2.One * popupScale,
                    ((i + 0.5f) / emotes.Length) * 2f * (float) Math.PI
                );
            }
        }

    }
}