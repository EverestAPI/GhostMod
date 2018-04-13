using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FMOD.Studio;

namespace Celeste.Mod.Ghost {
    public class GhostModule : EverestModule {

        public static GhostModule Instance;

        public override Type SettingsType => typeof(GhostModuleSettings);
        public static GhostModuleSettings Settings => (GhostModuleSettings) Instance._Settings;

        public static bool SettingsOverridden = false;

        public static string PathGhosts { get; internal set; }

        public GhostManager GhostManager;
        public GhostRecorder GhostRecorder;

        public Guid Run;

        public GhostModule() {
            Instance = this;
            
        }

        public override void Load() {
            PathGhosts = Path.Combine(Everest.PathSettings, "Ghosts");
            if (!Directory.Exists(PathGhosts))
                Directory.CreateDirectory(PathGhosts);

            On.Celeste.Level.LoadLevel += OnLoadLevel;
            Everest.Events.Level.OnExit += OnExit;
            On.Celeste.Player.Die += OnDie;
        }

        public override void Unload() {
            On.Celeste.Level.LoadLevel -= OnLoadLevel;
            Everest.Events.Level.OnExit -= OnExit;
            On.Celeste.Player.Die -= OnDie;
        }

        public void OnLoadLevel(On.Celeste.Level.orig_LoadLevel orig, Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
            orig(level, playerIntro, isFromLoader);
            
            if (isFromLoader) {
                GhostManager?.RemoveSelf();
                GhostManager = null;
                GhostRecorder?.RemoveSelf();
                GhostRecorder = null;
                Run = Guid.NewGuid();
            }

            Step(level);
        }

        public void OnExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
            if (mode == LevelExit.Mode.Completed ||
                mode == LevelExit.Mode.CompletedInterlude) {
                Step(level);
            }
        }

        public void Step(Level level) {
            if (Settings.Mode == GhostModuleMode.Off)
                return;

            string target = level.Session.Level;
            Logger.Log("ghost", $"Stepping into {level.Session.Area.GetSID()} {target}");

            Player player = level.Tracker.GetEntity<Player>();

            // Write the ghost, even if we haven't gotten an IL PB.
            // Maybe we left the level prematurely earlier?
            if (GhostRecorder?.Data != null &&
                (Settings.Mode & GhostModuleMode.Record) == GhostModuleMode.Record) {
                GhostRecorder.Data.Target = target;
                GhostRecorder.Data.Run = Run;
                GhostRecorder.Data.Write();
            }

            GhostManager?.RemoveSelf();

            level.Add(GhostManager = new GhostManager(player, level));

            if (GhostRecorder != null)
                GhostRecorder.RemoveSelf();
            level.Add(GhostRecorder = new GhostRecorder(player));
            GhostRecorder.Data = new GhostData(level.Session);
            GhostRecorder.Data.Name = Settings.Name;
        }

        public PlayerDeadBody OnDie(On.Celeste.Player.orig_Die orig, Player player, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats) {
            PlayerDeadBody corpse = orig(player, direction, evenIfInvincible, registerDeathInStats);

            if (GhostRecorder == null || GhostRecorder.Data == null)
                return corpse;

            // This is hacky, but it works:
            // Check the stack trace for Celeste.Level+* <Pause>*
            // and throw away the data when we're just retrying.
            foreach (StackFrame frame in new StackTrace().GetFrames()) {
                MethodBase method = frame?.GetMethod();
                if (method == null || method.DeclaringType == null)
                    continue;
                if (!method.DeclaringType.FullName.StartsWith("Celeste.Level+") ||
                    !method.Name.StartsWith("<Pause>"))
                    continue;

                GhostRecorder.Data = null;
                return corpse;
            }

            GhostRecorder.Data.Dead = true;

            return corpse;
        }

        public override void CreateModMenuSection(TextMenu menu, bool inGame, FMOD.Studio.EventInstance snapshot) {
            if (SettingsOverridden && !Settings.AlwaysShowSettings) {
                menu.Add(new TextMenu.SubHeader(Dialog.Clean("modoptions_ghostmodule_overridden") + " | v." + Metadata.VersionString));
                return;
            }

            base.CreateModMenuSection(menu, inGame, snapshot);
        }

    }
}
