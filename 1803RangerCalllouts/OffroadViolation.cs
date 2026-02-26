using System;
using System.Linq;
using Rage;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using LSPD_First_Response.Engine.Scripting.Entities;
using System.Drawing;

namespace _1803RangerCallouts
{
    [CalloutInfo("Off-Road Violation", CalloutProbability.Medium)]
    public class OffRoadViolation : Callout
    {
        private Vector3 _spawnPoint;
        private float _spawnHeading;

        private Vehicle _suspectVehicle;
        private Vehicle _secondVehicle;
        private Ped _driver;
        private Ped _passenger;
        private Ped _secondDriver;
        private Blip _vehicleBlip;
        private Blip _driverBlip;
        private Blip _waypointBlip;

        private int _scenario;
        private bool _calloutEnded = false;
        private bool _driverFled = false;
        private bool _interactionStarted = false;
        private DateTime _interactionStartTime;
        private bool _pursuitInitiated = false;
        private bool _playerNearScene = false;
        private bool _pursuitActive = false;
        private LHandle _currentPursuit;

        private static readonly Random Rnd = new Random();

        // Off-road vehicles - using only reliable models
        private readonly string[] OFFROAD_VEHICLES = {
            "BFINJECTION", "BIFTA", "MESA3", "SANDKING",
            "REBEL", "REBEL2", "DUNE", "BLAZER", "QUAD"
        };

        // Off-road clothing peds - using only reliable models that definitely exist
        private readonly string[] OFFROAD_PEDS = {
            "A_M_Y_Hiker_01",
            "A_M_Y_Hillbilly_01",
            "A_M_M_Hillbilly_01",
            "A_M_Y_Country_01",
            "A_M_Y_Beach_01",
            "A_M_Y_Beach_02",
            "A_M_Y_Beach_03"
        };

        // Fallback ped models that ALWAYS work
        private readonly string[] FALLBACK_PEDS = {
            "a_m_y_beach_01",
            "a_m_y_beach_02",
            "a_m_y_beach_03",
            "a_m_y_country_01",
            "a_m_m_country_01"
        };

        // ================= SETUP =================
        public override bool OnBeforeCalloutDisplayed()
        {
            var loc = LocationChooser.GetRandomWildernessLocation(400f, 1500f);
            _spawnPoint = loc.position;
            _spawnHeading = loc.heading;

            CalloutPosition = _spawnPoint;
            CalloutMessage = "Off-Road Vehicle Violation";
            CalloutAdvisory = "OHV vehicles reported in restricted wilderness area";

            ShowCalloutAreaBlipBeforeAccepting(_spawnPoint, 80f);
            AddMinimumDistanceCheck(150f, _spawnPoint);

            return base.OnBeforeCalloutDisplayed();
        }

        public override bool OnCalloutAccepted()
        {
            try
            {
                Game.LogTrivial("[OffRoadViolation] Callout accepted - starting setup");
                Game.DisplayNotification("~b~Park Dispatch:~w~ Off-road vehicles reported in protected wilderness area. Respond Code 3.");

                // Set waypoint to location
                if (Settings.ShowWaypoints)
                {
                    _waypointBlip = new Blip(_spawnPoint)
                    {
                        Color = Color.Orange,
                        Name = "Off-Road Violation",
                        Scale = 1.0f
                    };
                    _waypointBlip.EnableRoute(Color.Orange);
                }

                // Randomly choose a scenario (1-3)
                _scenario = Rnd.Next(1, 4);
                Game.LogTrivial($"[OffRoadViolation] Selected scenario: {_scenario}");

                // Ensure we have a valid ground position
                float? groundZNullable = World.GetGroundZ(_spawnPoint, true, false);
                float groundZ = groundZNullable ?? _spawnPoint.Z;
                _spawnPoint = new Vector3(_spawnPoint.X, _spawnPoint.Y, groundZ);

                // Small delay to ensure world is loaded
                GameFiber.Sleep(500);

                switch (_scenario)
                {
                    case 1:
                        SetupScenario1(); // Cooperative
                        break;
                    case 2:
                        SetupScenario2(); // Fleeing - waits for player
                        break;
                    case 3:
                        SetupScenario3(); // Multiple vehicles
                        break;
                }

                // Verify peds spawned, if not force spawn them
                GameFiber.StartNew(() => VerifyPedsSpawned());

                return base.OnCalloutAccepted();
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error in OnCalloutAccepted: {ex.Message}");
                return base.OnCalloutAccepted();
            }
        }

        // ================= SCENARIO SETUP =================
        private void SetupScenario1() // Cooperative - subjects stopped and waiting
        {
            try
            {
                Game.LogTrivial("[OffRoadViolation] Setting up Scenario 1 - Cooperative");
                Game.DisplayNotification("~y~Dispatch Update:~w~ Subjects stopped and are waiting for ranger.");

                // Spawn vehicle
                if (!SpawnVehicle(out _suspectVehicle, _spawnPoint, _spawnHeading, false))
                {
                    Game.LogTrivial("[OffRoadViolation] Failed to spawn vehicle in Scenario 1");
                    return;
                }

                // Add damage from off-roading
                _suspectVehicle.Health = 700;
                _suspectVehicle.DirtLevel = 1.0f;

                // Spawn driver near vehicle
                Vector3 driverPos = _spawnPoint + new Vector3(2f, 2f, 0f);
                float? driverGroundZ = World.GetGroundZ(driverPos, true, false);
                driverPos.Z = driverGroundZ ?? _spawnPoint.Z;

                // Try multiple times to spawn driver
                for (int i = 0; i < 3; i++)
                {
                    if (SpawnPed(out _driver, OFFROAD_PEDS, driverPos, _spawnHeading + 90f))
                    {
                        break;
                    }
                    GameFiber.Sleep(100);
                }

                if (_driver != null && _driver.Exists())
                {
                    _driver.Tasks.StandStill(-1);

                    _driverBlip = _driver.AttachBlip();
                    _driverBlip.Color = Color.Green;
                    _driverBlip.Name = "Driver";
                    _driverBlip.Scale = 0.7f;

                    Game.LogTrivial("[OffRoadViolation] Driver spawned successfully");
                }
                else
                {
                    // Force spawn with fallback
                    Game.LogTrivial("[OffRoadViolation] Failed to spawn driver, forcing fallback");
                    ForceSpawnPed(out _driver, driverPos, _spawnHeading + 90f);
                    if (_driver != null && _driver.Exists())
                    {
                        _driver.Tasks.StandStill(-1);
                        _driverBlip = _driver.AttachBlip();
                        _driverBlip.Color = Color.Green;
                        _driverBlip.Name = "Driver";
                        _driverBlip.Scale = 0.7f;
                    }
                }

                // 50% chance of passenger
                if (Rnd.NextDouble() > 0.5)
                {
                    Vector3 passengerPos = _spawnPoint + new Vector3(-2f, 2f, 0f);
                    float? passengerGroundZ = World.GetGroundZ(passengerPos, true, false);
                    passengerPos.Z = passengerGroundZ ?? _spawnPoint.Z;

                    // Try multiple times for passenger
                    for (int i = 0; i < 3; i++)
                    {
                        if (SpawnPed(out _passenger, OFFROAD_PEDS, passengerPos, _spawnHeading - 90f))
                        {
                            break;
                        }
                        GameFiber.Sleep(100);
                    }

                    if (_passenger != null && _passenger.Exists())
                    {
                        _passenger.Tasks.StandStill(-1);
                        Game.LogTrivial("[OffRoadViolation] Passenger spawned successfully");
                    }
                }

                AddVehicleBlip(_suspectVehicle, "Suspect Vehicle", Color.Red);
                Game.DisplayHelp("Subjects are stopped. Approach and issue citation/warning.");
                Game.LogTrivial("[OffRoadViolation] Scenario 1 setup complete");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error in SetupScenario1: {ex.Message}");
            }
        }

        private void SetupScenario2() // Fleeing - waits for player approach
        {
            try
            {
                Game.LogTrivial("[OffRoadViolation] Setting up Scenario 2 - Fleeing (will wait for player)");
                Game.DisplayNotification("~y~Dispatch Update:~w~ Subjects are stopped in the area. Respond to location.");

                // Spawn vehicle at scene - Engine ON but they appear stopped
                if (!SpawnVehicle(out _suspectVehicle, _spawnPoint, _spawnHeading, true))
                {
                    Game.LogTrivial("[OffRoadViolation] Failed to spawn vehicle in Scenario 2");
                    return;
                }

                // Add damage from off-roading
                _suspectVehicle.Health = 700;
                _suspectVehicle.DirtLevel = 1.0f;

                // Spawn driver IN the vehicle - DO NOT MOVE, just sit there
                bool driverSpawned = false;
                for (int i = 0; i < 3; i++)
                {
                    if (SpawnPed(out _driver, OFFROAD_PEDS, _spawnPoint, _spawnHeading))
                    {
                        driverSpawned = true;
                        break;
                    }
                    GameFiber.Sleep(100);
                }

                if (driverSpawned && _driver != null && _driver.Exists())
                {
                    _driver.WarpIntoVehicle(_suspectVehicle, -1);
                    // IMPORTANT: Do not assign any tasks - they just sit in the vehicle
                    Game.LogTrivial("[OffRoadViolation] Driver spawned and sitting in vehicle - waiting for player");
                }
                else
                {
                    // Force spawn with fallback
                    Game.LogTrivial("[OffRoadViolation] Failed to spawn driver, forcing fallback");
                    ForceSpawnPed(out _driver, _spawnPoint, _spawnHeading);
                    if (_driver != null && _driver.Exists())
                    {
                        _driver.WarpIntoVehicle(_suspectVehicle, -1);
                        Game.LogTrivial("[OffRoadViolation] Fallback driver spawned - waiting for player");
                    }
                }

                AddVehicleBlip(_suspectVehicle, "Suspect Vehicle", Color.Red);
                Game.DisplayHelp("Approach the stopped vehicle carefully.");
                Game.LogTrivial("[OffRoadViolation] Scenario 2 setup complete - driver will flee when player approaches");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error in SetupScenario2: {ex.Message}");
            }
        }

        private void SetupScenario3() // Multiple vehicles
        {
            try
            {
                Game.LogTrivial("[OffRoadViolation] Setting up Scenario 3 - Multiple vehicles");
                Game.DisplayNotification("~y~Dispatch Update:~w~ Multiple off-road vehicles in area. Subjects are riding trails.");

                // Spawn primary vehicle at scene
                if (!SpawnVehicle(out _suspectVehicle, _spawnPoint, _spawnHeading, true))
                {
                    Game.LogTrivial("[OffRoadViolation] Failed to spawn primary vehicle");
                    return;
                }

                // Spawn driver in primary vehicle
                bool driverSpawned = false;
                for (int i = 0; i < 3; i++)
                {
                    if (SpawnPed(out _driver, OFFROAD_PEDS, _spawnPoint, _spawnHeading))
                    {
                        driverSpawned = true;
                        break;
                    }
                    GameFiber.Sleep(100);
                }

                if (driverSpawned && _driver != null && _driver.Exists())
                {
                    _driver.WarpIntoVehicle(_suspectVehicle, -1);
                    _driver.Tasks.CruiseWithVehicle(10f, VehicleDrivingFlags.Normal);
                    Game.LogTrivial("[OffRoadViolation] Primary driver spawned and driving");
                }
                else
                {
                    ForceSpawnPed(out _driver, _spawnPoint, _spawnHeading);
                    if (_driver != null && _driver.Exists())
                    {
                        _driver.WarpIntoVehicle(_suspectVehicle, -1);
                        _driver.Tasks.CruiseWithVehicle(10f, VehicleDrivingFlags.Normal);
                    }
                }

                // Spawn second vehicle nearby
                Vector3 secondSpawnPos = _spawnPoint + new Vector3(20f, -20f, 0f);
                float? secondGroundZ = World.GetGroundZ(secondSpawnPos, true, false);
                secondSpawnPos.Z = secondGroundZ ?? _spawnPoint.Z;
                float secondHeading = _spawnHeading + 45f;

                if (SpawnVehicle(out _secondVehicle, secondSpawnPos, secondHeading, true))
                {
                    bool secondDriverSpawned = false;
                    for (int i = 0; i < 3; i++)
                    {
                        if (SpawnPed(out _secondDriver, OFFROAD_PEDS, secondSpawnPos, secondHeading))
                        {
                            secondDriverSpawned = true;
                            break;
                        }
                        GameFiber.Sleep(100);
                    }

                    if (secondDriverSpawned && _secondDriver != null && _secondDriver.Exists())
                    {
                        _secondDriver.WarpIntoVehicle(_secondVehicle, -1);
                        _secondDriver.Tasks.CruiseWithVehicle(10f, VehicleDrivingFlags.Normal);
                        Game.LogTrivial("[OffRoadViolation] Second driver spawned and driving");
                    }
                }

                AddVehicleBlip(_suspectVehicle, "Primary Vehicle", Color.Red);
                Game.DisplayHelp("Multiple vehicles in area. Stop them and issue citations.");
                Game.LogTrivial("[OffRoadViolation] Scenario 3 setup complete");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error in SetupScenario3: {ex.Message}");
            }
        }

        // ================= HELPER METHODS =================
        private bool SpawnVehicle(out Vehicle vehicle, Vector3 position, float heading, bool engineOn)
        {
            try
            {
                string model = OFFROAD_VEHICLES[Rnd.Next(OFFROAD_VEHICLES.Length)];
                Game.LogTrivial($"[OffRoadViolation] Attempting to spawn vehicle: {model}");

                vehicle = new Vehicle(model, position, heading)
                {
                    IsPersistent = true,
                    IsEngineOn = engineOn
                };

                if (!vehicle.Exists())
                {
                    Game.LogTrivial($"[OffRoadViolation] Failed to spawn vehicle: {model}");

                    // Try a different vehicle
                    model = "SANDKING";
                    vehicle = new Vehicle(model, position, heading)
                    {
                        IsPersistent = true,
                        IsEngineOn = engineOn
                    };

                    if (!vehicle.Exists())
                    {
                        Game.LogTrivial("[OffRoadViolation] Fallback vehicle also failed");
                        return false;
                    }
                }

                Game.LogTrivial($"[OffRoadViolation] Vehicle spawned successfully: {model}");
                return true;
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error spawning vehicle: {ex.Message}");
                vehicle = null;
                return false;
            }
        }

        private bool SpawnPed(out Ped ped, string[] pedModels, Vector3 position, float heading)
        {
            try
            {
                string model = pedModels[Rnd.Next(pedModels.Length)];
                Game.LogTrivial($"[OffRoadViolation] Attempting to spawn ped: {model}");

                ped = new Ped(model, position, heading)
                {
                    IsPersistent = true,
                    IsInvincible = (_scenario == 1 || _scenario == 3),
                    BlockPermanentEvents = true
                };

                if (!ped.Exists())
                {
                    Game.LogTrivial($"[OffRoadViolation] Failed to spawn ped: {model}");
                    return false;
                }

                Game.LogTrivial("[OffRoadViolation] Ped spawned successfully");
                return true;
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error spawning ped: {ex.Message}");
                ped = null;
                return false;
            }
        }

        private void ForceSpawnPed(out Ped ped, Vector3 position, float heading)
        {
            ped = null;

            // Try each fallback ped until one works
            foreach (string model in FALLBACK_PEDS)
            {
                try
                {
                    Game.LogTrivial($"[OffRoadViolation] Force spawning fallback ped: {model}");
                    ped = new Ped(model, position, heading)
                    {
                        IsPersistent = true,
                        IsInvincible = (_scenario == 1 || _scenario == 3),
                        BlockPermanentEvents = true
                    };

                    if (ped.Exists())
                    {
                        Game.LogTrivial("[OffRoadViolation] Fallback ped spawned successfully");
                        return;
                    }
                }
                catch
                {
                    // Try next model
                }
            }

            Game.LogTrivial("[OffRoadViolation] All fallback peds failed");
        }

        private void VerifyPedsSpawned()
        {
            GameFiber.Sleep(2000); // Wait 2 seconds

            // Check if driver exists, if not force spawn
            if (_scenario == 1 || _scenario == 2 || _scenario == 3)
            {
                if (_driver == null || !_driver.Exists())
                {
                    Game.LogTrivial("[OffRoadViolation] Driver missing after setup, force spawning");

                    if (_scenario == 2)
                    {
                        // For scenario 2, driver should be in vehicle
                        ForceSpawnPed(out _driver, _spawnPoint, _spawnHeading);
                        if (_driver != null && _driver.Exists() && _suspectVehicle != null && _suspectVehicle.Exists())
                        {
                            _driver.WarpIntoVehicle(_suspectVehicle, -1);
                            Game.LogTrivial("[OffRoadViolation] Driver placed in vehicle - waiting for player");
                        }
                    }
                    else
                    {
                        Vector3 driverPos = _spawnPoint + new Vector3(2f, 2f, 0f);
                        float? driverGroundZ = World.GetGroundZ(driverPos, true, false);
                        driverPos.Z = driverGroundZ ?? _spawnPoint.Z;

                        ForceSpawnPed(out _driver, driverPos, _spawnHeading + 90f);

                        if (_driver != null && _driver.Exists())
                        {
                            _driver.Tasks.StandStill(-1);

                            _driverBlip = _driver.AttachBlip();
                            _driverBlip.Color = Color.Green;
                            _driverBlip.Name = "Driver";
                            _driverBlip.Scale = 0.7f;
                        }
                    }
                }
            }
        }

        private void AddVehicleBlip(Vehicle vehicle, string name, Color color)
        {
            if (vehicle != null && vehicle.Exists())
            {
                _vehicleBlip = vehicle.AttachBlip();
                _vehicleBlip.Color = color;
                _vehicleBlip.Name = name;
                _vehicleBlip.Scale = 0.8f;
            }
        }

        // ================= LOOP =================
        public override void Process()
        {
            if (_calloutEnded) return;

            try
            {
                Ped player = Game.LocalPlayer.Character;
                if (!player.Exists() || player.IsDead)
                {
                    End();
                    return;
                }

                // Check if player is near the scene
                float distanceToScene = player.Position.DistanceTo(_spawnPoint);

                // Scenario 2: ONLY trigger pursuit when player gets close
                if (_scenario == 2 && !_pursuitInitiated)
                {
                    // Check if player is within range - they should NOT flee until player is close
                    if (!_playerNearScene && distanceToScene < 100f) // 100 meter trigger distance
                    {
                        if (_driver != null && _driver.Exists() && _suspectVehicle != null && _suspectVehicle.Exists())
                        {
                            _playerNearScene = true;
                            Game.LogTrivial($"[OffRoadViolation] Player within 100m - NOW triggering pursuit");
                            Game.DisplayNotification("~y~The suspects have spotted you and are fleeing!");
                            GameFiber.StartNew(() => InitiatePursuit());
                        }
                        else
                        {
                            Game.LogTrivial("[OffRoadViolation] Cannot trigger pursuit - driver or vehicle missing");

                            // Try to recover missing driver
                            if (_driver == null || !_driver.Exists())
                            {
                                Game.LogTrivial("[OffRoadViolation] Attempting to recover missing driver");
                                ForceSpawnPed(out _driver, _spawnPoint, _spawnHeading);

                                if (_driver != null && _driver.Exists() && _suspectVehicle != null && _suspectVehicle.Exists())
                                {
                                    _driver.WarpIntoVehicle(_suspectVehicle, -1);
                                    Game.LogTrivial("[OffRoadViolation] Driver recovered, now triggering pursuit");
                                    _playerNearScene = true;
                                    GameFiber.StartNew(() => InitiatePursuit());
                                }
                            }
                        }
                    }
                }

                // Monitor pursuit status
                if (_pursuitInitiated && _currentPursuit != null)
                {
                    try
                    {
                        if (!Functions.IsPursuitStillRunning(_currentPursuit))
                        {
                            Game.LogTrivial("[OffRoadViolation] Pursuit has ended");
                            _pursuitActive = false;
                            _currentPursuit = null;
                        }
                    }
                    catch
                    {
                        _pursuitActive = false;
                        _currentPursuit = null;
                    }
                }

                // Update fleeing vehicle blip if pursuit is active
                if (_scenario == 2 && _pursuitInitiated && _suspectVehicle != null && _suspectVehicle.Exists())
                {
                    if (_vehicleBlip != null && _vehicleBlip.Exists())
                    {
                        _vehicleBlip.Position = _suspectVehicle.Position;
                    }
                }

                // Scenario 1: Interaction
                if (_scenario == 1 && _driver != null && _driver.Exists() && !_interactionStarted)
                {
                    float distanceToDriver = player.Position.DistanceTo(_driver.Position);

                    if (distanceToDriver < 10f && player.IsOnFoot)
                    {
                        _interactionStarted = true;
                        _interactionStartTime = DateTime.Now;
                        Game.DisplayHelp("Approaching off-road vehicle operator...");
                    }
                }

                if (_interactionStarted)
                {
                    double elapsedSeconds = (DateTime.Now - _interactionStartTime).TotalSeconds;

                    if (elapsedSeconds >= 3 && elapsedSeconds < 4)
                    {
                        Game.DisplaySubtitle("Driver: ~b~Sorry officer, we didn't know this area was restricted.");
                    }
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error in Process: {ex.Message}");
            }

            base.Process();
        }

        // ================= PURSUIT METHODS =================
        private void InitiatePursuit()
        {
            try
            {
                if (_pursuitInitiated || _driverFled) return;

                _pursuitInitiated = true;
                _driverFled = true;

                Game.LogTrivial("[OffRoadViolation] InitiatePursuit called - driver now fleeing");

                if (_driver != null && _driver.Exists() && _suspectVehicle != null && _suspectVehicle.Exists())
                {
                    Game.LogTrivial("[OffRoadViolation] Driver and vehicle exist, starting pursuit");

                    // Make sure driver is in vehicle
                    if (!_driver.IsInVehicle(_suspectVehicle, false))
                    {
                        _driver.WarpIntoVehicle(_suspectVehicle, -1);
                    }

                    // Clear any existing tasks (they were just sitting there)
                    _driver.Tasks.Clear();

                    // Make driver flee aggressively in a random direction
                    float fleeAngle = _spawnHeading + 180f + Rnd.Next(-30, 30); // Add some randomness
                    float rad = MathHelper.ConvertDegreesToRadians(fleeAngle);

                    Vector3 fleeDir = new Vector3(
                        (float)Math.Sin(rad),
                        -(float)Math.Cos(rad),
                        0f
                    );

                    Vector3 fleePosition = _spawnPoint + fleeDir * 1000f;

                    // Start fleeing - they were stopped, now they flee
                    _driver.Tasks.DriveToPosition(fleePosition, 60f, VehicleDrivingFlags.Emergency);

                    // Update blip
                    if (_vehicleBlip != null && _vehicleBlip.Exists())
                    {
                        _vehicleBlip.Name = "Fleeing Vehicle";
                        _vehicleBlip.Color = Color.Red;
                        _vehicleBlip.IsFriendly = false;
                    }

                    Game.DisplayNotification("~r~PURSUIT INITIATED!~w~ The suspects are fleeing.");

                    // Create LSPDFR pursuit
                    _currentPursuit = Functions.CreatePursuit();

                    if (_currentPursuit != null)
                    {
                        Functions.AddPedToPursuit(_currentPursuit, _driver);
                        Functions.SetPursuitIsActiveForPlayer(_currentPursuit, true);
                        _pursuitActive = true;

                        Game.LogTrivial("[OffRoadViolation] Pursuit created and activated successfully");

                        // Add secondary driver to pursuit if exists
                        if (_secondDriver != null && _secondDriver.Exists())
                        {
                            Functions.AddPedToPursuit(_currentPursuit, _secondDriver);
                            Game.LogTrivial("[OffRoadViolation] Added second driver to pursuit");
                        }
                    }
                    else
                    {
                        Game.LogTrivial("[OffRoadViolation] Failed to create pursuit");
                    }
                }
                else
                {
                    Game.LogTrivial("[OffRoadViolation] Driver or vehicle missing for pursuit");
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error in InitiatePursuit: {ex.Message}");
            }
        }

        // ================= CLEANUP =================
        public override void End()
        {
            try
            {
                if (_calloutEnded) return;
                _calloutEnded = true;

                Game.LogTrivial("[OffRoadViolation] Ending callout - cleaning up");

                // Clean up waypoint
                if (_waypointBlip != null && _waypointBlip.Exists())
                {
                    _waypointBlip.DisableRoute();
                    _waypointBlip.Delete();
                }

                // Clean up blips
                if (_vehicleBlip != null && _vehicleBlip.Exists())
                    _vehicleBlip.Delete();

                if (_driverBlip != null && _driverBlip.Exists())
                    _driverBlip.Delete();

                // Clean up entities
                if (_driver != null && _driver.Exists())
                    _driver.Dismiss();

                if (_passenger != null && _passenger.Exists())
                    _passenger.Dismiss();

                if (_secondDriver != null && _secondDriver.Exists())
                    _secondDriver.Dismiss();

                if (_suspectVehicle != null && _suspectVehicle.Exists())
                    _suspectVehicle.Dismiss();

                if (_secondVehicle != null && _secondVehicle.Exists())
                    _secondVehicle.Dismiss();

                Game.DisplayNotification("~b~Park Dispatch:~w~ Off-road violation call cleared.");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[OffRoadViolation] Error in End: {ex.Message}");
            }

            base.End();
        }

        public override void OnCalloutNotAccepted()
        {
            try
            {
                if (_waypointBlip != null && _waypointBlip.Exists())
                    _waypointBlip.Delete();
            }
            catch { }

            base.OnCalloutNotAccepted();
        }
    }
}