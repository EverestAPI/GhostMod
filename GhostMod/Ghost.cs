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

namespace Celeste.Mod.Ghost {
    public class Ghost : Actor {

        public GhostManager Manager;

        public Player Player;

        public PlayerSprite Sprite;
        public PlayerHair Hair;
        public int MachineState;

        public GhostData Data;
        public int FrameIndex = 0;
        public GhostFrame? ForcedFrame;
        public GhostFrame PrevFrame => ForcedFrame ?? (Data == null ? default(GhostFrame) : Data[FrameIndex - 1]);
        public GhostFrame Frame => ForcedFrame ?? (Data == null ? default(GhostFrame) : Data[FrameIndex]);
        public bool AutoForward = true;

        public GhostName Name;

        public Color Color = Color.White;

        protected float alpha;
        protected float alphaHair;

        public Ghost(Player player)
            : this(player, null) {
        }
        public Ghost(Player player, GhostData data)
            : base(player.Position) {
            Player = player;
            Data = data;

            Depth = 1;

            Sprite = new PlayerSprite(player.Sprite.Mode);
            Sprite.HairCount = player.Sprite.HairCount;
            Add(Hair = new PlayerHair(Sprite));
            Add(Sprite);

            Hair.Color = Player.NormalHairColor;

            Name = new GhostName(this, Data?.Name ?? "");
        }

        public override void Added(Scene scene) {
            base.Added(scene);

            Hair.Facing = Frame.Data.Facing;
            Hair.Start();
            UpdateHair();

            Scene.Add(Name);
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);

            Name.RemoveSelf();
        }

        public void UpdateHair() {
            if (!Frame.Data.IsValid)
                return;

            Hair.Color = new Color(
                (Frame.Data.HairColor.R * Color.R) / 255,
                (Frame.Data.HairColor.G * Color.G) / 255,
                (Frame.Data.HairColor.B * Color.B) / 255,
                (Frame.Data.HairColor.A * Color.A) / 255
            );
            Hair.Alpha = alphaHair;
            Hair.Facing = Frame.Data.Facing;
            Hair.SimulateMotion = Frame.Data.HairSimulateMotion;
        }

        public void UpdateSprite() {
            if (!Frame.Data.IsValid)
                return;

            Position = Frame.Data.Position;
            Sprite.Rotation = Frame.Data.Rotation;
            Sprite.Scale = Frame.Data.Scale;
            Sprite.Scale.X = Sprite.Scale.X * (float) Frame.Data.Facing;
            Sprite.Color = new Color(
                (Frame.Data.Color.R * Color.R) / 255,
                (Frame.Data.Color.G * Color.G) / 255,
                (Frame.Data.Color.B * Color.B) / 255,
                (Frame.Data.Color.A * Color.A) / 255
            ) * alpha;

            Sprite.Rate = Frame.Data.SpriteRate;
            Sprite.Justify = Frame.Data.SpriteJustify;

            try {
                if (Sprite.CurrentAnimationID != Frame.Data.CurrentAnimationID)
                    Sprite.Play(Frame.Data.CurrentAnimationID);
                Sprite.SetAnimationFrame(Frame.Data.CurrentAnimationFrame);
            } catch {
                // Play likes to fail randomly as the ID doesn't exist in an underlying dict.
                // Let's ignore this for now.
            }
        }

        public override void Update() {
            Visible = ForcedFrame != null || ((GhostModule.Settings.Mode & GhostModuleMode.Play) == GhostModuleMode.Play);
            Visible &= Frame.Data.IsValid;
            if (ForcedFrame == null && Data != null && Data.Dead)
                Visible &= GhostModule.Settings.ShowDeaths;
            if (ForcedFrame == null && Data != null && !string.IsNullOrEmpty(GhostModule.Settings.NameFilter))
                Visible &= string.IsNullOrEmpty(Data.Name) || GhostModule.Settings.NameFilter.Equals(Data.Name, StringComparison.InvariantCultureIgnoreCase);

            if (ForcedFrame == null && Data != null && Player.InControl && AutoForward) {
                do {
                    FrameIndex++;
                } while (
                    (PrevFrame.Data.IsValid && !PrevFrame.Data.InControl) || // Skip any frames we're not in control in.
                    (!PrevFrame.Data.IsValid && FrameIndex < Data.Frames.Count) // Skip any frames not containing the data chunk.
                );
            }

            if (Data != null && Data.Opacity != null) {
                alpha = Data.Opacity.Value;
                alphaHair = Data.Opacity.Value;
            } else {
                float dist = (Player.Position - Position).LengthSquared();
                dist -= GhostModule.Settings.InnerRadiusDist;
                if (dist < 0f)
                    dist = 0f;
                if (GhostModule.Settings.BorderSize == 0) {
                    dist = dist < GhostModule.Settings.InnerRadiusDist ? 0f : 1f;
                } else {
                    dist /= GhostModule.Settings.BorderSizeDist;
                }
                alpha = Calc.LerpClamp(GhostModule.Settings.InnerOpacityFactor, GhostModule.Settings.OuterOpacityFactor, dist);
                alphaHair = Calc.LerpClamp(GhostModule.Settings.InnerHairOpacityFactor, GhostModule.Settings.OuterHairOpacityFactor, dist);
            }

            UpdateSprite();
            UpdateHair();

            Visible &= alpha > 0f;

            Name.Alpha = Visible ? alpha : 0f;

            base.Update();
        }

    }
}
