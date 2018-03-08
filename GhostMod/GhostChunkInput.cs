using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.Ghost {
    public struct GhostChunkInput {

        public const string Chunk = "input";
        public bool IsValid;

        public int MoveX;
        public int MoveY;

        public Vector2 Aim;
        public Vector2 MountainAim;

        public int Buttons;
        public bool ESC {
            get {
                return (Buttons & (int) ButtonMask.ESC) == (int) ButtonMask.ESC;
            }
            set {
                Buttons &= (int) ~ButtonMask.ESC;
                if (value)
                    Buttons |= (int) ButtonMask.ESC;
            }
        }
        public bool Pause {
            get {
                return (Buttons & (int) ButtonMask.Pause) == (int) ButtonMask.Pause;
            }
            set {
                Buttons &= (int) ~ButtonMask.Pause;
                if (value)
                    Buttons |= (int) ButtonMask.Pause;
            }
        }
        public bool MenuLeft {
            get {
                return (Buttons & (int) ButtonMask.MenuLeft) == (int) ButtonMask.MenuLeft;
            }
            set {
                Buttons &= (int) ~ButtonMask.MenuLeft;
                if (value)
                    Buttons |= (int) ButtonMask.MenuLeft;
            }
        }
        public bool MenuRight {
            get {
                return (Buttons & (int) ButtonMask.MenuRight) == (int) ButtonMask.MenuRight;
            }
            set {
                Buttons &= (int) ~ButtonMask.MenuRight;
                if (value)
                    Buttons |= (int) ButtonMask.MenuRight;
            }
        }
        public bool MenuUp {
            get {
                return (Buttons & (int) ButtonMask.MenuUp) == (int) ButtonMask.MenuUp;
            }
            set {
                Buttons &= (int) ~ButtonMask.MenuUp;
                if (value)
                    Buttons |= (int) ButtonMask.MenuUp;
            }
        }
        public bool MenuDown {
            get {
                return (Buttons & (int) ButtonMask.MenuDown) == (int) ButtonMask.MenuDown;
            }
            set {
                Buttons &= (int) ~ButtonMask.MenuDown;
                if (value)
                    Buttons |= (int) ButtonMask.MenuDown;
            }
        }
        public bool MenuConfirm {
            get {
                return (Buttons & (int) ButtonMask.MenuConfirm) == (int) ButtonMask.MenuConfirm;
            }
            set {
                Buttons &= (int) ~ButtonMask.MenuConfirm;
                if (value)
                    Buttons |= (int) ButtonMask.MenuConfirm;
            }
        }
        public bool MenuCancel {
            get {
                return (Buttons & (int) ButtonMask.MenuCancel) == (int) ButtonMask.MenuCancel;
            }
            set {
                Buttons &= (int) ~ButtonMask.MenuCancel;
                if (value)
                    Buttons |= (int) ButtonMask.MenuCancel;
            }
        }
        public bool MenuJournal {
            get {
                return (Buttons & (int) ButtonMask.MenuJournal) == (int) ButtonMask.MenuJournal;
            }
            set {
                Buttons &= (int) ~ButtonMask.MenuJournal;
                if (value)
                    Buttons |= (int) ButtonMask.MenuJournal;
            }
        }
        public bool QuickRestart {
            get {
                return (Buttons & (int) ButtonMask.QuickRestart) == (int) ButtonMask.QuickRestart;
            }
            set {
                Buttons &= (int) ~ButtonMask.QuickRestart;
                if (value)
                    Buttons |= (int) ButtonMask.QuickRestart;
            }
        }
        public bool Jump {
            get {
                return (Buttons & (int) ButtonMask.Jump) == (int) ButtonMask.Jump;
            }
            set {
                Buttons &= (int) ~ButtonMask.Jump;
                if (value)
                    Buttons |= (int) ButtonMask.Jump;
            }
        }
        public bool Dash {
            get {
                return (Buttons & (int) ButtonMask.Dash) == (int) ButtonMask.Dash;
            }
            set {
                Buttons &= (int) ~ButtonMask.Dash;
                if (value)
                    Buttons |= (int) ButtonMask.Dash;
            }
        }
        public bool Grab {
            get {
                return (Buttons & (int) ButtonMask.Grab) == (int) ButtonMask.Grab;
            }
            set {
                Buttons &= (int) ~ButtonMask.Grab;
                if (value)
                    Buttons |= (int) ButtonMask.Grab;
            }
        }
        public bool Talk {
            get {
                return (Buttons & (int) ButtonMask.Talk) == (int) ButtonMask.Talk;
            }
            set {
                Buttons &= (int) ~ButtonMask.Talk;
                if (value)
                    Buttons |= (int) ButtonMask.Talk;
            }
        }

        public void Read(BinaryReader reader) {
            IsValid = true;

            MoveX = reader.ReadInt32();
            MoveY = reader.ReadInt32();

            Aim = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            MountainAim = new Vector2(reader.ReadSingle(), reader.ReadSingle());

            Buttons = reader.ReadInt32();
        }

        public void Write(BinaryWriter writer) {
            writer.Write(MoveX);
            writer.Write(MoveY);

            writer.Write(Aim.X);
            writer.Write(Aim.Y);

            writer.Write(MountainAim.X);
            writer.Write(MountainAim.Y);

            writer.Write(Buttons);
        }

        [Flags]
        public enum ButtonMask : int {
            ESC = 1 << 0,
            Pause = 1 << 1,
            MenuLeft = 1 << 2,
            MenuRight = 1 << 3,
            MenuUp = 1 << 4,
            MenuDown = 1 << 5,
            MenuConfirm = 1 << 6,
            MenuCancel = 1 << 7,
            MenuJournal = 1 << 8,
            QuickRestart = 1 << 9,
            Jump = 1 << 10,
            Dash = 1 << 11,
            Grab = 1 << 12,
            Talk = 1 << 13
        }

    }
}
