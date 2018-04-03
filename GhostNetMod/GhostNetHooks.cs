using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Detour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FMOD.Studio;
using Microsoft.Xna.Framework.Input;
using HookedMethod;
using HM = HookedMethod.HookedMethod;

namespace Celeste.Mod.Ghost.Net {
    public static class GhostNetHooks {

        private static Hook h_GetHairColor;
        private static Hook h_GetHairTexture;
        private static Hook h_PlayerPlayAudio;

        public static void Load() {
            h_GetHairColor = new Hook(
                MethodInfoWithDef.CreateAndResolveDef(typeof(PlayerHair).GetMethod("GetHairColor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)),
                GetHairColor
            );

            MethodInfo m_GetHairTexture = typeof(PlayerHair).GetMethod("GetHairTexture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m_GetHairTexture != null) {
                h_GetHairTexture = new Hook(
                    MethodInfoWithDef.CreateAndResolveDef(m_GetHairTexture),
                    GetHairTexture
                );
            }

            h_PlayerPlayAudio = new Hook(
                MethodInfoWithDef.CreateAndResolveDef(typeof(Player).GetMethod("Play", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)),
                PlayerPlayAudio
            );
        }

        public static object GetHairColor(HM hook, HM.OriginalMethod origM, HM.Parameters args) {
            // C# 7:
            // var (self, index) = args.As<PlayerHair, int>();
            // C# 6:
            PlayerHair self = (PlayerHair) args.RawParams[0];
            int index = (int) args.RawParams[1];

            Color colorOrig = origM.As<Color>(args.RawParams);
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

        public static object GetHairTexture(HM hook, HM.OriginalMethod origM, HM.Parameters args) {
            // C# 7:
            // var (self, index) = args.As<PlayerHair, int>();
            // C# 6:
            PlayerHair self = (PlayerHair) args.RawParams[0];
            int index = (int) args.RawParams[1];

            MTexture texOrig = origM.As<MTexture>(args.RawParams);
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

        public static object PlayerPlayAudio(HM hook, HM.OriginalMethod origM, HM.Parameters args) {
            Player self = (Player) args.RawParams[0];
            string sound = (string) args.RawParams[1];
            string param = (string) args.RawParams[2];
            float value = (float) args.RawParams[3];

            GhostNetModule.Instance?.Client?.SendUAudio(self, sound, param, value);

            return origM.As<EventInstance>(args.RawParams);
        }

    }
}
