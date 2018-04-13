using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FMOD.Studio;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.Ghost.Net {
    public static class GhostNetHooks {

        public static void Load() {
            On.Celeste.PlayerHair.GetHairColor += GetHairColor;
            On.Celeste.PlayerHair.GetHairTexture += GetHairTexture;
            On.Celeste.Player.Play += PlayerPlayAudio;
        }

        public static Color GetHairColor(On.Celeste.PlayerHair.orig_GetHairColor orig, PlayerHair self, int index) {
            Color colorOrig = orig(self, index);
            Ghost ghost = self.Entity as Ghost;
            GhostNetClient client = GhostNetModule.Instance.Client;
            uint playerID;
            GhostNetFrame frame;
            ChunkUUpdate update;
            if (ghost == null ||
                client == null ||
                !client.GhostPlayerIDs.TryGetValue(ghost, out playerID) ||
                !client.UpdateMap.TryGetValue(playerID, out frame) ||
                (update = frame) == null)
                return colorOrig;

            if (index < 0 || update.HairColors.Length <= index)
                return Color.Transparent;
            return update.HairColors[index];
        }

        public static MTexture GetHairTexture(On.Celeste.PlayerHair.orig_GetHairTexture orig, PlayerHair self, int index) {
            MTexture texOrig = orig(self, index);
            Ghost ghost = self.Entity as Ghost;
            GhostNetClient client = GhostNetModule.Instance.Client;
            uint playerID;
            GhostNetFrame frame;
            ChunkUUpdate update;
            if (ghost == null ||
                client == null ||
                !client.GhostPlayerIDs.TryGetValue(ghost, out playerID) ||
                !client.UpdateMap.TryGetValue(playerID, out frame) ||
                (update = frame) == null)
                return texOrig;

            if (index < 0 || update.HairColors.Length <= index)
                return texOrig;
            string texName = update.HairTextures[index];
            if (!GFX.Game.Has(texName))
                return texOrig;
            return GFX.Game[texName];
        }

        public static EventInstance PlayerPlayAudio(On.Celeste.Player.orig_Play orig, Player self, string sound, string param, float value) {
            GhostNetModule.Instance?.Client?.SendUAudio(self, sound, param, value);
            return orig(self, sound, param, value);
        }

    }
}
