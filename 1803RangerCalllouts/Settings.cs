using System;
using System.IO;
using Rage;

namespace _1803RangerCallouts
{
    public static class Settings
    {
        // INI file path
        private static readonly string iniPath = @"Plugins/LSPDFR/1803RangerCallouts.ini";

        // Callout enable flags
        public static bool EnableAnimalStruck { get; private set; } = true;
        public static bool EnableIllegalParkedCar { get; private set; } = true;
        public static bool EnableOffRoadViolation { get; private set; } = true;

        // Display settings
        public static bool ShowCalloutMessages { get; private set; } = true;
        public static bool ShowBlips { get; private set; } = true;
        public static bool ShowWaypoints { get; private set; } = true;

        public static void LoadSettings()
        {
            try
            {
                Game.LogTrivial("[1803RangerCallouts] Loading settings from: " + iniPath);

                // Create default INI if it doesn't exist
                if (!File.Exists(iniPath))
                {
                    CreateDefaultIni();
                }

                // Read INI file
                var ini = new InitializationFile(iniPath);

                // Load callout settings
                EnableAnimalStruck = ini.ReadBoolean("Callouts", "EnableAnimalStruck", true);
                EnableIllegalParkedCar = ini.ReadBoolean("Callouts", "EnableIllegalParkedCar", true);
                EnableOffRoadViolation = ini.ReadBoolean("Callouts", "EnableOffRoadViolation", true);

                // Load display settings
                ShowCalloutMessages = ini.ReadBoolean("Display", "ShowCalloutMessages", true);
                ShowBlips = ini.ReadBoolean("Display", "ShowBlips", true);
                ShowWaypoints = ini.ReadBoolean("Display", "ShowWaypoints", true);

                Game.LogTrivial("[1803RangerCallouts] Settings loaded successfully");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[1803RangerCallouts] Error loading settings: {ex.Message}");
            }
        }

        private static void CreateDefaultIni()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(iniPath))
                {
                    writer.WriteLine("[Callouts]");
                    writer.WriteLine("; Enable or disable individual callouts (true/false)");
                    writer.WriteLine("EnableAnimalStruck = true");
                    writer.WriteLine("EnableIllegalParkedCar = true");
                    writer.WriteLine("EnableOffRoadViolation = true");
                    writer.WriteLine();
                    writer.WriteLine("[Display]");
                    writer.WriteLine("; General display settings");
                    writer.WriteLine("ShowCalloutMessages = true");
                    writer.WriteLine("ShowBlips = true");
                    writer.WriteLine("ShowWaypoints = true");
                }

                Game.LogTrivial("[1803RangerCallouts] Default INI file created");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[1803RangerCallouts] Error creating default INI: {ex.Message}");
            }
        }
    }
}