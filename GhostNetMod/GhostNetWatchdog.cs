using System;
using System.Linq;
using System.Timers;
using Tmds.Systemd;

namespace Celeste.Mod.Ghost.Net {
    public static class GhostNetWatchdog {
        private static Timer watchdogTimer;
        
        private static bool forceRestart = false;
        
        public static void ForceRestart() {
            forceRestart = true;
        }
        
        public static int DuplicateUsers() {
            var names = GhostNetModule.Instance.Server.PlayerMap.Values.Select(e => e.Name);
            return names.Count() - names.Distinct().Count();
        }
        
        private static void Watchdog(object sender, ElapsedEventArgs e) {
            if (Environment.GetEnvironmentVariable("WATCHDOG_USEC") == null) return; // prevent error
            if (forceRestart) return; // fail if op forces restart
            
            if (DuplicateUsers() > 2) return; // more than 2 ghost users
            
            ServiceManager.Notify(ServiceState.Watchdog);
        }
        
        public static void InitializeWatchdog() {
            StopWatchdog(); // safety
            double seconds;
            if (!Double.TryParse(Environment.GetEnvironmentVariable("WATCHDOG_USEC"), out seconds)) return; // automatic null check
            
            double interval = seconds * 500; // seconds / 2 to ms
            
            watchdogTimer = new Timer(interval);
            watchdogTimer.Elapsed += Watchdog;
            watchdogTimer.Start();
        }
        
        public static void StopWatchdog() {
            if (watchdogTimer != null) watchdogTimer.Dispose();
            watchdogTimer = null;
        }
    }
}