using System;
using System.Linq;
using System.Collections.Generic;
using Rage;

namespace _1803RangerCallouts
{
    public static class LocationChooser
    {
        // ================= Park/Forest/Wilderness spawn points =================
        // ONLY your provided coordinates - no random additions
        private static readonly (Vector3 pos, float heading)[] wildernessSpawns =
        {
            // ============ YOUR ORIGINAL COORDINATES ============
            (new Vector3(948.31f, 4437.95f, 48.37f), 63.31f),
            (new Vector3(126.95f, 4415.67f, 73.32f), 247.51f),
            (new Vector3(-1001.47f, 4352.71f, 12.06f), 247.31f),
            
            // ============ PALETO FOREST ============
            (new Vector3(-593.14f, 5673.32f, 37.69f), 159.36f),
            
            // ============ BLAINE RANGER STATION AREA ============
            (new Vector3(-1398.83f, 5102.84f, 60.52f), 121.63f),
            (new Vector3(-670.55f, 5894.67f, 16.16f), 348.36f),
            (new Vector3(1400.65f, 4485.89f, 51.61f), 262.84f),
            (new Vector3(2941.37f, 5048.36f, 30.17f), 95.21f),
            
            // ============ NEW LOCATIONS - TONGVA HILLS/CHUMASH AREA ============
            (new Vector3(-1698.002f, 867.090f, 146.550f), 330.646f),
            (new Vector3(-1530.274f, 1414.908f, 122.534f), 313.971f),
            (new Vector3(-1472.943f, 2030.788f, 64.130f), 27.114f),
            (new Vector3(-1363.992f, 2161.210f, 51.292f), 329.349f),
            (new Vector3(-1153.751f, 2641.986f, 15.755f), 133.056f),
            (new Vector3(-1940.944f, 657.573f, 123.814f), 152.401f)
        };

        // ================= Get Random Wilderness Location =================
        public static (Vector3 position, float heading) GetRandomWildernessLocation(float minDistance = 300f, float maxDistance = 3000f)
        {
            try
            {
                Vector3 playerPos = Game.LocalPlayer.Character.Position;
                var random = new Random();

                // Log player position for debugging
                Game.LogTrivial($"[LocationChooser] Player at: X={playerPos.X:F1}, Y={playerPos.Y:F1}, Z={playerPos.Z:F1}");

                // ONLY use your provided locations - find valid ones within range
                var validSpawns = wildernessSpawns
                    .Where(s => s.pos.DistanceTo(playerPos) >= minDistance &&
                                s.pos.DistanceTo(playerPos) <= maxDistance)
                    .ToArray();

                if (validSpawns.Length > 0)
                {
                    // Found locations in preferred range, pick one randomly from your locations
                    var spawn = validSpawns[random.Next(validSpawns.Length)];
                    float distance = spawn.pos.DistanceTo(playerPos);
                    Game.LogTrivial($"[LocationChooser] Found {validSpawns.Length} of your locations in range {minDistance}-{maxDistance}m");
                    Game.LogTrivial($"[LocationChooser] Selected your location at {distance:F0}m away");
                    return spawn;
                }

                // If no locations in preferred range, find CLOSEST of your locations
                Game.LogTrivial($"[LocationChooser] None of your locations in range {minDistance}-{maxDistance}m, finding closest...");

                var closest = wildernessSpawns
                    .Where(s => s.pos.DistanceTo(playerPos) <= 5000f)
                    .OrderBy(s => s.pos.DistanceTo(playerPos))
                    .FirstOrDefault();

                if (closest.pos != Vector3.Zero)
                {
                    float actualDistance = closest.pos.DistanceTo(playerPos);
                    Game.LogTrivial($"[LocationChooser] Using your closest location at {actualDistance:F0}m away");
                    return closest;
                }

                // Ultimate fallback - use first of your locations
                Game.LogTrivial("[LocationChooser] No locations within 5000m, using first of your locations as fallback");
                return wildernessSpawns[0];
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[LocationChooser] Error: {ex.Message}");
                // Return your first location as safe default
                return (new Vector3(948.31f, 4437.95f, 48.37f), 63.31f);
            }
        }

        // ================= Get Closest Wilderness Location =================
        public static (Vector3 position, float heading) GetClosestWildernessLocation(float maxDistance = 5000f)
        {
            try
            {
                Vector3 playerPos = Game.LocalPlayer.Character.Position;

                // Find closest of your locations within max distance
                var closest = wildernessSpawns
                    .Where(s => s.pos.DistanceTo(playerPos) <= maxDistance)
                    .OrderBy(s => s.pos.DistanceTo(playerPos))
                    .FirstOrDefault();

                if (closest.pos != Vector3.Zero)
                {
                    float distance = closest.pos.DistanceTo(playerPos);
                    Game.LogTrivial($"[LocationChooser] Your closest location at {distance:F0}m away");
                    return closest;
                }
                else
                {
                    // If none within range, use your first location
                    Game.LogTrivial($"[LocationChooser] None of your locations within {maxDistance}m, using first location.");
                    return wildernessSpawns[0];
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[LocationChooser] Error: {ex.Message}");
                return (new Vector3(948.31f, 4437.95f, 48.37f), 63.31f);
            }
        }

        // ================= Get Random Location with Terrain Type =================
        public static (Vector3 position, float heading, string terrainType) GetRandomLocationWithTerrain()
        {
            var location = GetRandomWildernessLocation();
            string terrain = GetTerrainType(location.position);
            return (location.position, location.heading, terrain);
        }

        // ================= Get Terrain Type =================
        private static string GetTerrainType(Vector3 position)
        {
            float z = position.Z;

            if (z > 500f) return "Mountain Peak";
            if (z > 300f) return "High Mountain";
            if (z > 150f) return "Mountain Trail";
            if (z > 50f) return "Forest Hills";
            if (z > 20f) return "Forest";
            if (z > 5f) return "Woodland";
            return "Valley/Shore";
        }

        // ================= Get All Wilderness Locations =================
        public static (Vector3 pos, float heading)[] GetAllWildernessLocations()
        {
            return wildernessSpawns;
        }

        // ================= Get Wilderness Location by Index =================
        public static (Vector3 position, float heading) GetWildernessLocationByIndex(int index)
        {
            if (index >= 0 && index < wildernessSpawns.Length)
            {
                return wildernessSpawns[index];
            }
            else
            {
                Game.LogTrivial($"[LocationChooser] Invalid index {index}, returning first location.");
                return wildernessSpawns[0];
            }
        }

        // ================= Get Location Info =================
        public static string GetLocationInfo(Vector3 position)
        {
            try
            {
                var location = wildernessSpawns.FirstOrDefault(s => s.pos == position);
                if (location.pos != Vector3.Zero)
                {
                    return $"Your location at X={position.X:F1}, Y={position.Y:F1}, Z={position.Z:F1}";
                }
                return "Unknown location";
            }
            catch
            {
                return "Error getting location info";
            }
        }

        // ================= Validate Location =================
        public static bool IsLocationValid(Vector3 position, float maxDistanceFromRoad = 100f)
        {
            try
            {
                // Check if position is on a road or trail
                return World.GetStreetName(position).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        // ================= Get Locations Near Player =================
        public static (Vector3 pos, float heading)[] GetLocationsNearPlayer(float maxDistance = 2000f)
        {
            try
            {
                Vector3 playerPos = Game.LocalPlayer.Character.Position;

                var nearby = wildernessSpawns
                    .Where(s => s.pos.DistanceTo(playerPos) <= maxDistance)
                    .ToArray();

                Game.LogTrivial($"[LocationChooser] Found {nearby.Length} of your locations within {maxDistance}m of player");
                return nearby;
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[LocationChooser] Error finding nearby locations: {ex.Message}");
                return Array.Empty<(Vector3, float)>();
            }
        }

        // ================= Get Region Name =================
        public static string GetRegionName(Vector3 position)
        {
            float x = position.X;
            float y = position.Y;

            // Your original locations
            if (x > 900f && x < 1000f && y > 4400f && y < 4500f)
                return "East Park Entrance";

            if (x > 100f && x < 150f && y > 4400f && y < 4450f)
                return "North Park Trail";

            if (x > -1050f && x < -950f && y > 4300f && y < 4400f)
                return "South Park Boundary";

            // Paleto Forest
            if (x > -650f && x < -550f && y > 5600f && y < 5700f)
                return "Paleto Forest Trail";

            // Blaine Ranger Station area
            if (x > -1450f && x < -1350f && y > 5050f && y < 5150f)
                return "Blaine Ranger Station";

            if (x > -700f && x < -650f && y > 5850f && y < 5950f)
                return "North Blaine Wilderness";

            if (x > 1350f && x < 1450f && y > 4450f && y < 4550f)
                return "East Blaine Outlook";

            if (x > 2900f && x < 3000f && y > 5000f && y < 5100f)
                return "Senora Ranger Point";

            // NEW LOCATIONS - Tongva Hills/Chumash area
            if (x > -1750f && x < -1650f && y > 800f && y < 900f)
                return "Tongva Hills Lookout";

            if (x > -1600f && x < -1500f && y > 1350f && y < 1450f)
                return "Tongva Creek";

            if (x > -1500f && x < -1400f && y > 2000f && y < 2100f)
                return "Chumash Trailhead";

            if (x > -1400f && x < -1300f && y > 2100f && y < 2200f)
                return "Chumash Wilderness Camp";

            if (x > -1200f && x < -1100f && y > 2600f && y < 2700f)
                return "Raton Canyon Overlook";

            if (x > -2000f && x < -1900f && y > 600f && y < 700f)
                return "Tongva Peak Trail";

            return "Your Wilderness Area";
        }
    }
}