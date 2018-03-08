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
    public class GhostManager : Entity {

        public List<Ghost> Ghosts = new List<Ghost>();

        public Player Player;

        public readonly static Color ColorGold = new Color(1f, 1f, 0f, 1f);
        public readonly static Color ColorNeutral = new Color(1f, 1f, 1f, 1f);

        public GhostManager(Player player, Level level)
            : base(Vector2.Zero) {
            Player = player;

            Tag = Tags.HUD;

            // Read and add all ghosts.
            GhostData.ForAllGhosts(level.Session, (i, ghostData) => {
                Ghost ghost = new Ghost(player, ghostData);
                level.Add(ghost);
                Ghosts.Add(ghost);
                return true;
            });
        }

        public override void Removed(Scene scene) {
            base.Removed(scene);

            // Remove any dead ghosts (heh)
            for (int i = Ghosts.Count - 1; i > -1; --i) {
                Ghost ghost = Ghosts[i];
                if (ghost.Player != Player)
                    ghost.RemoveSelf();
            }
            Ghosts.Clear();
        }

        public override void Render() {
            /* Proposed colors:
             * blue - full run PB (impossible)
             * silver - chapter PB (feasible)
             * gold - room PB (done)
             */

            // Gold is the easiest: Find fastest active ghost.
            Ghost fastest = null;
            foreach (Ghost ghost in Ghosts) {
                // While we're at it, reset all colors.
                ghost.Color = ColorNeutral;

                if (!ghost.Frame.Data.IsValid)
                    continue;
                
                if (fastest == null || ghost.Data.Frames.Count < fastest.Data.Frames.Count) {
                    fastest = ghost;
                }
            }

            if (fastest != null) {
                fastest.Color = ColorGold;
            }

            base.Render();
        }

    }
}
