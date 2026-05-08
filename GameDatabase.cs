using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using System.Linq;

namespace CrosshairApp
{
    public static class GameDatabase
    {
        public static readonly Dictionary<string, string> Games = new()
        {
            { "cs2", "Counter-Strike 2" },
            { "csgo", "CS:GO" },
            { "VALORANT-Win64-Shipping", "Valorant" },
            { "r5apex", "Apex Legends" },
            { "Overwatch", "Overwatch 2" },
            { "RainbowSix", "Rainbow Six Siege" },
            { "cod", "Call of Duty" },
            { "ModernWarfare", "Call of Duty: Modern Warfare" },
            { "Warzone", "Warzone" },
            { "destiny2", "Destiny 2" },
            { "bf2042", "Battlefield 2042" },
            { "bfv", "Battlefield V" },
            { "bf4", "Battlefield 4" },
            { "HaloInfinite", "Halo Infinite" },
            { "QuakeChampions", "Quake Champions" },
            { "DOOMEternalx64vk", "DOOM Eternal" },
            { "Splitgate", "Splitgate" },
            { "DeepRockGalactic", "Deep Rock Galactic" },
            { "Insurgency", "Insurgency Sandstorm" },
            { "squad", "Squad" },
            { "Arma3", "Arma 3" },
            { "Titanfall2", "Titanfall 2" },
            { "GTA5", "GTA V" },
            { "RDR2", "Red Dead Redemption 2" },
            { "Cyberpunk2077", "Cyberpunk 2077" },
            { "eldenring", "Elden Ring" },
            { "TslGame", "PUBG" },
            { "FortniteClient-Win64-Shipping", "Fortnite" },
            { "RustClient", "Rust" },
            { "EscapeFromTarkov", "Escape from Tarkov" },
            { "HuntGame", "Hunt: Showdown" },
            { "DayZ_x64", "DayZ" },
            { "scum", "SCUM" },
            { "javaw", "Minecraft" },
            { "7DaysToDie", "7 Days to Die" },
            { "SeaOfThieves", "Sea of Thieves" },
            { "theforest", "The Forest" },
            { "SonsOfTheForest", "Sons of the Forest" },
            { "valheim", "Valheim" },
            { "ShooterGame", "ARK: Survival Evolved" },
            { "GreenHell", "Green Hell" },
            { "raft", "Raft" },
            { "paladins", "Paladins" },
            { "Warframe", "Warframe" },
            { "Borderlands3", "Borderlands 3" },
            { "Back4Blood", "Back 4 Blood" },
            { "left4dead2", "Left 4 Dead 2" },
            { "Payday2", "Payday 2" },
            { "Starfield", "Starfield" },
            { "Fallout4", "Fallout 4" },
            { "MetroExodus", "Metro Exodus" },
            { "ReadyOrNot", "Ready or Not" },
            { "Deathloop", "Deathloop" },
            { "Ghostrunner", "Ghostrunner" },
            { "Darktide", "Warhammer 40K: Darktide" },
            { "HLL", "Hell Let Loose" },
            { "PostScriptum", "Post Scriptum" },
            { "Enlisted", "Enlisted" },
            { "RocketLeague", "Rocket League" },
        };

        public static List<string> GetSteamPaths()
        {
            var paths = new List<string>();
            try
            {
                string[] defaultPaths = {
                    @"C:\Program Files (x86)\Steam\steamapps\common",
                    @"C:\Program Files\Steam\steamapps\common"
                };
                foreach (var p in defaultPaths)
                    if (Directory.Exists(p)) paths.Add(p);

                var steamKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
                if (steamKey != null)
                {
                    var steamPath = steamKey.GetValue("SteamPath")?.ToString();
                    if (!string.IsNullOrEmpty(steamPath))
                    {
                        var libFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                        if (File.Exists(libFile))
                        {
                            var lines = File.ReadAllLines(libFile);
                            foreach (var line in lines)
                            {
                                if (line.Contains("\"path\""))
                                {
                                    var parts = line.Split('"');
                                    if (parts.Length >= 4)
                                    {
                                        var libPath = Path.Combine(parts[3].Replace("\\\\", "\\"), "steamapps", "common");
                                        if (Directory.Exists(libPath) && !paths.Contains(libPath))
                                            paths.Add(libPath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                string[] steamDirs = {
                    Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "Steam", "steamapps", "common"),
                    Path.Combine(drive.RootDirectory.FullName, "Program Files", "Steam", "steamapps", "common"),
                    Path.Combine(drive.RootDirectory.FullName, "Steam", "steamapps", "common"),
                    Path.Combine(drive.RootDirectory.FullName, "Games", "Steam", "steamapps", "common")
                };
                foreach (var p in steamDirs)
                    if (Directory.Exists(p) && !paths.Contains(p)) paths.Add(p);
            }
            return paths;
        }

        public static List<string> GetEpicPaths()
        {
            var paths = new List<string>();
            try
            {
                var epicKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\EpicGames\Unreal Engine");
                if (epicKey != null)
                {
                    var epicPath = epicKey.GetValue("INSTALLDIR")?.ToString();
                    if (!string.IsNullOrEmpty(epicPath) && Directory.Exists(epicPath))
                        paths.Add(epicPath);
                }

                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    string[] epicDirs = {
                        Path.Combine(drive.RootDirectory.FullName, "Program Files", "Epic Games"),
                        Path.Combine(drive.RootDirectory.FullName, "Program Files (x86)", "Epic Games"),
                        Path.Combine(drive.RootDirectory.FullName, "Epic Games"),
                        Path.Combine(drive.RootDirectory.FullName, "Games", "Epic Games")
                    };
                    foreach (var p in epicDirs)
                        if (Directory.Exists(p) && !paths.Contains(p)) paths.Add(p);
                }
            }
            catch { }
            return paths;
        }

        public static Dictionary<string, string> DetectInstalledGames()
        {
            var found = new Dictionary<string, string>();
            var allPaths = new List<string>();
            allPaths.AddRange(GetSteamPaths());
            allPaths.AddRange(GetEpicPaths());

            foreach (var basePath in allPaths)
            {
                if (!Directory.Exists(basePath)) continue;
                try
                {
                    var dirs = Directory.GetDirectories(basePath);
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            var exes = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories).Take(20);
                            foreach (var exe in exes)
                            {
                                var procName = Path.GetFileNameWithoutExtension(exe);
                                if (Games.TryGetValue(procName, out var gameName))
                                    if (!found.ContainsValue(gameName))
                                        found[procName] = gameName;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return found;
        }
    }
}