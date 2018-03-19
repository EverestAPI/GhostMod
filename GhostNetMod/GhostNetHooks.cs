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
        private static Hook h_GetTrailColor;
        private static Hook h_GetHairTexture;

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
            ChunkUUpdateE0 e0;
            if (ghost == null ||
                client == null ||
                !client.GhostPlayerIDs.TryGetValue(ghost, out playerID) ||
                !client.UpdateMap.TryGetValue(playerID, out frame) ||
                (e0 = frame?.UUpdateE0) == null)
                return colorOrig;

            if (index < 0 || e0.HairColors.Length <= index)
                return Color.Transparent;
            return e0.HairColors[index];
        }

        public static MTexture GetHairTexture(HM hook, HM.OriginalMethod origM, HM.Parameters args) {
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
            ChunkUUpdateE0 e0;
            if (ghost == null ||
                client == null ||
                !client.GhostPlayerIDs.TryGetValue(ghost, out playerID) ||
                !client.UpdateMap.TryGetValue(playerID, out frame) ||
                (e0 = frame?.UUpdateE0) == null)
                return texOrig;

            if (index < 0 || e0.HairColors.Length <= index)
                return texOrig;
            string texName = e0.HairTextures[index];
            if (!GFX.Game.Has(texName))
                return texOrig;
            return GFX.Game[texName];
        }

    }
}
