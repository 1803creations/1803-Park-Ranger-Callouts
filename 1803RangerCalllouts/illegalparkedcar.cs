using System;
using System.Linq;
using Rage;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using System.Drawing;

namespace _1803RangerCallouts
{
    [CalloutInfo("Illegal Parked Car", CalloutProbability.Medium)]
    public class IllegalParkedCar : Callout
    {
        private Vector3 _spawnPoint;
        private float _spawnHeading;

        private Vehicle _illegalVehicle;
        private Ped _vehicleOwner;
        private Blip _vehicleBlip;
        private Blip _ownerBlip;
        private Blip _waypointBlip;

        private int _scenario;
        private bool _calloutEnded = false;
        private bool _interactionStarted = false;
        private DateTime _interactionStartTime;

        private static readonly Random Rnd = new Random();

        // Vehicle models
        private readonly string[] VEHICLE_MODELS = {
            "BFINJECTION", "BIFTA", "DUBSTA3", "MESA3", "SANDKING",
            "REBEL", "REBEL2", "TROPHYTRUCK", "TROPHYTRUCK2", "DUNE",
            "PATRIOT", "BALLER", "CAVALCADE", "FQ2", "HABANERO"
        };

        // Civilian ped models
        private readonly string[] CIVILIAN_PEDS = {
            "A_M_Y_Hiker_01", "A_F_Y_Hiker_01", "A_M_Y_Hillbilly_01",
            "A_M_M_Hillbilly_01", "A_M_Y_Country_01", "A_F_Y_Country_01",
            "A_M_Y_Business_01", "A_F_Y_Socialite_01"
        };

        // ================= SETUP =================
        public override bool OnBeforeCalloutDisplayed()
        {
            var loc = LocationChooser.GetRandomWildernessLocation(400f, 1500f);
            _spawnPoint = loc.position;
            _spawnHeading = loc.heading;

            CalloutPosition = _spawnPoint;
            CalloutMessage = "Illegally Parked Vehicle";
            CalloutAdvisory = "Vehicle parked in restricted wilderness area";

            ShowCalloutAreaBlipBeforeAccepting(_spawnPoint, 50f);
            AddMinimumDistanceCheck(150f, _spawnPoint);

            return base.OnBeforeCalloutDisplayed();
        }

        public override bool OnCalloutAccepted()
        {
            Game.DisplayNotification("~b~Park Dispatch:~w~ Vehicle reported illegally parked in restricted area. Respond Code 2.");

            // Set waypoint to location
            if (Settings.ShowWaypoints)
            {
                _waypointBlip = new Blip(_spawnPoint)
                {
                    Color = Color.Yellow,
                    Name = "Illegal Parking",
                    Scale = 1.0f
                };
                _waypointBlip.EnableRoute(Color.Yellow);
            }

            // Randomly choose a scenario (1-3)
            _scenario = Rnd.Next(1, 4);

            switch (_scenario)
            {
                case 1:
                    SetupScenario1(); // Empty vehicle
                    break;
                case 2:
                    SetupScenario2(); // Owner nearby
                    break;
                case 3:
                    SetupScenario3(); // Owner camping
                    break;
            }

            return base.OnCalloutAccepted();
        }

        // ================= SCENARIO SETUP =================
        private void SetupScenario1() // Empty vehicle
        {
            try
            {
                Game.DisplayNotification("~y~Dispatch Update:~w~ Vehicle appears empty. No occupants in the area.");

                // Spawn vehicle
                string vehicleModel = VEHICLE_MODELS[Rnd.Next(VEHICLE_MODELS.Length)];

                // Randomize parking angle
                float parkingAngle = _spawnHeading + Rnd.Next(-30, 30);

                _illegalVehicle = new Vehicle(vehicleModel, _spawnPoint, parkingAngle)
                {
                    IsPersistent = true,
                    IsEngineOn = false
                };
                _illegalVehicle.LockStatus = VehicleLockStatus.Unlocked;

                if (!_illegalVehicle.Exists()) return;

                // Add blip
                _vehicleBlip = _illegalVehicle.AttachBlip();
                _vehicleBlip.Color = Color.Red;
                _vehicleBlip.Name = "Illegal Vehicle";
                _vehicleBlip.Scale = 0.8f;

                Game.DisplayHelp("Vehicle is unattended. Check registration and arrange for tow if necessary.");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[IllegalParkedCar] Error in SetupScenario1: {ex.Message}");
            }
        }

        private void SetupScenario2() // Owner nearby
        {
            try
            {
                Game.DisplayNotification("~y~Dispatch Update:~w~ Owner reportedly nearby. Possibly hiking in the area.");

                // Spawn vehicle
                string vehicleModel = VEHICLE_MODELS[Rnd.Next(VEHICLE_MODELS.Length)];

                _illegalVehicle = new Vehicle(vehicleModel, _spawnPoint, _spawnHeading)
                {
                    IsPersistent = true,
                    IsEngineOn = false
                };
                _illegalVehicle.LockStatus = VehicleLockStatus.Locked;

                if (!_illegalVehicle.Exists()) return;

                // Spawn owner 50-100m away (hiking) - use same Z as spawn point
                float ownerAngle = _spawnHeading + Rnd.Next(-60, 60);
                float rad = MathHelper.ConvertDegreesToRadians(ownerAngle);

                Vector3 ownerDir = new Vector3(
                    (float)Math.Sin(rad),
                    -(float)Math.Cos(rad),
                    0f
                );

                Vector3 ownerPos = _spawnPoint + ownerDir * 75f;
                ownerPos.Z = _spawnPoint.Z; // Use the same Z coordinate

                _vehicleOwner = new Ped(CIVILIAN_PEDS[Rnd.Next(CIVILIAN_PEDS.Length)], ownerPos, ownerAngle)
                {
                    IsPersistent = true,
                    IsInvincible = true,
                    BlockPermanentEvents = true
                };

                if (_vehicleOwner.Exists())
                {
                    // Make owner walk back toward vehicle
                    _vehicleOwner.Tasks.Wander();

                    _ownerBlip = _vehicleOwner.AttachBlip();
                    _ownerBlip.Color = Color.Green;
                    _ownerBlip.Name = "Vehicle Owner";
                    _ownerBlip.Scale = 0.7f;
                }

                _vehicleBlip = _illegalVehicle.AttachBlip();
                _vehicleBlip.Color = Color.Red;
                _vehicleBlip.Name = "Illegal Vehicle";
                _vehicleBlip.Scale = 0.8f;

                Game.DisplayHelp("Owner is nearby. Locate them or wait by the vehicle.");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[IllegalParkedCar] Error in SetupScenario2: {ex.Message}");
            }
        }

        private void SetupScenario3() // Owner camping
        {
            try
            {
                Game.DisplayNotification("~y~Dispatch Update:~w~ Owner set up camp nearby. Vehicle blocking trail access.");

                // Spawn vehicle
                string vehicleModel = VEHICLE_MODELS[Rnd.Next(VEHICLE_MODELS.Length)];

                _illegalVehicle = new Vehicle(vehicleModel, _spawnPoint, _spawnHeading)
                {
                    IsPersistent = true,
                    IsEngineOn = false
                };
                _illegalVehicle.LockStatus = VehicleLockStatus.Unlocked;

                if (!_illegalVehicle.Exists()) return;

                // Spawn owner at campsite - use same Z as spawn point
                float campAngle = _spawnHeading + 180f;
                Vector3 campPos = _spawnPoint + new Vector3(15f, 15f, 0f);
                campPos.Z = _spawnPoint.Z; // Use the same Z coordinate

                _vehicleOwner = new Ped(CIVILIAN_PEDS[Rnd.Next(CIVILIAN_PEDS.Length)], campPos, campAngle)
                {
                    IsPersistent = true,
                    IsInvincible = true,
                    BlockPermanentEvents = true
                };

                if (_vehicleOwner.Exists())
                {
                    // Owner sitting on ground
                    _vehicleOwner.Tasks.StandStill(-1);

                    _ownerBlip = _vehicleOwner.AttachBlip();
                    _ownerBlip.Color = Color.Green;
                    _ownerBlip.Name = "Camper";
                    _ownerBlip.Scale = 0.7f;
                }

                _vehicleBlip = _illegalVehicle.AttachBlip();
                _vehicleBlip.Color = Color.Red;
                _vehicleBlip.Name = "Illegal Vehicle";
                _vehicleBlip.Scale = 0.8f;

                Game.DisplayHelp("Camper is at a nearby site. Approach and inform them of parking violation.");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[IllegalParkedCar] Error in SetupScenario3: {ex.Message}");
            }
        }

        // ================= LOOP =================
        public override void Process()
        {
            if (_calloutEnded) return;

            Ped player = Game.LocalPlayer.Character;
            if (!player.Exists() || player.IsDead)
            {
                End();
                return;
            }

            // Scenario 2 & 3: Interaction with owner
            if ((_scenario == 2 || _scenario == 3) && _vehicleOwner != null && _vehicleOwner.Exists() && !_interactionStarted)
            {
                float distanceToOwner = player.Position.DistanceTo(_vehicleOwner.Position);

                if (distanceToOwner < 10f && player.IsOnFoot)
                {
                    if (!_interactionStarted)
                    {
                        _interactionStarted = true;
                        _interactionStartTime = DateTime.Now;
                        Game.DisplayHelp("Approaching vehicle owner...");
                    }
                }
            }

            if (_interactionStarted)
            {
                double elapsedSeconds = (DateTime.Now - _interactionStartTime).TotalSeconds;

                if (elapsedSeconds >= 3 && elapsedSeconds < 4)
                {
                    if (_scenario == 2)
                    {
                        Game.DisplaySubtitle("Owner: ~b~Oh, is my car parked illegally? I was just going for a short hike.");
                        GameFiber.StartNew(() =>
                        {
                            GameFiber.Sleep(3000);
                            Game.DisplaySubtitle("Owner: ~b~I didn't see any signs. I'll move it right away, officer.");
                        });
                    }
                    else if (_scenario == 3)
                    {
                        Game.DisplaySubtitle("Camper: ~b~Sorry officer, I didn't realize I was blocking the trail.");
                        GameFiber.StartNew(() =>
                        {
                            GameFiber.Sleep(3000);
                            Game.DisplaySubtitle("Camper: ~b~Is there somewhere nearby I can park legally?");
                        });
                    }
                }
            }

            base.Process();
        }

        // ================= CLEANUP =================
        public override void End()
        {
            try
            {
                if (_calloutEnded) return;
                _calloutEnded = true;

                // Clean up waypoint
                if (_waypointBlip != null && _waypointBlip.Exists())
                {
                    _waypointBlip.DisableRoute();
                    _waypointBlip.Delete();
                }

                // Clean up blips
                if (_vehicleBlip != null && _vehicleBlip.Exists())
                {
                    _vehicleBlip.Delete();
                }

                if (_ownerBlip != null && _ownerBlip.Exists())
                {
                    _ownerBlip.Delete();
                }

                // Clean up entities
                if (_vehicleOwner != null && _vehicleOwner.Exists())
                {
                    _vehicleOwner.Dismiss();
                }

                if (_illegalVehicle != null && _illegalVehicle.Exists())
                {
                    _illegalVehicle.Dismiss();
                }

                Game.DisplayNotification("~b~Park Dispatch:~w~ Illegal parking call cleared.");
            }
            catch (Exception ex)
            {
                Game.LogTrivial($"[IllegalParkedCar] Error in End: {ex.Message}");
            }

            base.End();
        }

        public override void OnCalloutNotAccepted()
        {
            try
            {
                if (_waypointBlip != null && _waypointBlip.Exists())
                {
                    _waypointBlip.Delete();
                }
            }
            catch { }

            base.OnCalloutNotAccepted();
        }
    }
}