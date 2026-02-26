using System;
using System.Linq;
using System.Collections.Generic;
using Rage;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using System.Drawing;

namespace _1803RangerCallouts
{
    [CalloutInfo("Animal Struck", CalloutProbability.Medium)]
    public class AnimalStruck : Callout
    {
        private Vector3 _spawnPoint;
        private float _spawnHeading;

        private Ped _animal;
        private Ped _driver;
        private Ped _witness;
        private Ped _hiker;
        private Vehicle _struckVehicle;
        private Blip _vehicleBlip;
        private Blip _animalBlip;
        private Blip _witnessBlip;
        private Blip _waypointBlip;  // ADDED: Waypoint blip

        private int _scenario;
        private bool _calloutEnded = false;
        private bool _driverFled = false;
        private bool _interactionStarted = false;
        private DateTime _interactionStartTime;

        private static readonly Random Rnd = new Random();

        // Animal models - expanded for park ranger
        private readonly string[] ANIMAL_MODELS = {
            "a_c_deer",
            "a_c_deer",
            "a_c_boar",
            "a_c_coyote",
            "a_c_mtlion",      // Mountain lion
            "a_c_cow",         // Cow (from nearby farms)
            "a_c_husky",       // Wolf/dog hybrid
            "a_c_retriever"    // Domestic dog
        };

        // Off-road vehicles for park areas
        private readonly string[] OFFROAD_VEHICLES = {
            "BFINJECTION", "BIFTA", "DUBSTA3", "MESA3", "SANDKING",
            "REBEL", "REBEL2", "TROPHYTRUCK", "TROPHYTRUCK2", "DUNE"
        };

        // Civilian vehicles
        private readonly string[] CIVILIAN_VEHICLES = {
            "ASEA", "FELON", "INFERNUS", "F620", "SENTINEL",
            "BUFFALO", "DOMINATOR", "RAPIDGT", "COQUETTE", "ASTEROPE",
            "WASHINGTON", "BALLER", "CAVALCADE", "PATRIOT"
        };

        // Hiker clothing
        private readonly string[] HIKER_PEDS = {
            "A_M_Y_Hiker_01", "A_F_Y_Hiker_01", "A_M_Y_Hillbilly_01",
            "A_M_M_Hillbilly_01", "A_M_M_HasJew_01", "A_F_M_Farmer_01"
        };

        // Civilian ped models
        private readonly string[] CIVILIAN_PEDS = {
            "A_M_Y_Business_01", "A_F_Y_Hiker_01", "A_M_M_Skater_01",
            "A_F_M_Socialite_01", "A_M_Y_Business_02", "A_M_Y_Country_01"
        };

        // ================= SETUP =================
        public override bool OnBeforeCalloutDisplayed()
        {
            var loc = LocationChooser.GetRandomWildernessLocation(400f, 1500f);
            _spawnPoint = loc.position;
            _spawnHeading = loc.heading;

            CalloutPosition = _spawnPoint;
            CalloutMessage = "Animal Struck by Vehicle";
            CalloutAdvisory = "Respond to reports of an animal struck on park roadway";

            ShowCalloutAreaBlipBeforeAccepting(_spawnPoint, 60f);
            AddMinimumDistanceCheck(200f, _spawnPoint);

            return base.OnBeforeCalloutDisplayed();
        }

        public override bool OnCalloutAccepted()
        {
            Game.DisplayNotification("~b~Park Dispatch:~w~ Animal struck reported on park road. Proceed with caution.");

            // ADDED: Set waypoint to location
            if (Settings.ShowWaypoints)
            {
                _waypointBlip = new Blip(_spawnPoint)
                {
                    Color = Color.Red,
                    Name = "Animal Struck",
                    Scale = 1.0f
                };
                _waypointBlip.EnableRoute(Color.Red);
            }

            // Randomly choose a scenario (1-5)
            _scenario = Rnd.Next(1, 6);

            SpawnAnimal();

            switch (_scenario)
            {
                case 1:
                    SetupScenario1(); // Driver fled
                    break;
                case 2:
                    SetupScenario2(); // Just the animal
                    break;
                case 3:
                    SetupScenario3(); // Driver on scene
                    break;
                case 4:
                    SetupScenario4(); // Witness hiker
                    break;
                case 5:
                    SetupScenario5(); // Off-road vehicle hit animal
                    break;
            }

            return base.OnCalloutAccepted();
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

            // Check player distance to scene
            float distanceToScene = player.Position.DistanceTo(_spawnPoint);

            // Scenario 1: Driver fleeing
            if (_scenario == 1 && _struckVehicle != null && _struckVehicle.Exists() && !_driverFled)
            {
                // Make driver flee if player gets close
                if (distanceToScene < 100f)
                {
                    MakeDriverFlee();
                }

                // Update vehicle blip position
                if (_vehicleBlip != null && _vehicleBlip.Exists() && _struckVehicle.Exists())
                {
                    _vehicleBlip.Position = _struckVehicle.Position;
                }
            }

            // Scenario 3 & 5: Driver interaction
            if ((_scenario == 3 || _scenario == 5) && _driver != null && _driver.Exists() && !_interactionStarted)
            {
                if (distanceToScene < 15f && player.IsOnFoot)
                {
                    if (!_interactionStarted)
                    {
                        _interactionStarted = true;
                        _interactionStartTime = DateTime.Now;
                        Game.DisplayHelp("Approaching driver...");
                    }
                }
            }

            // Scenario 4: Hiker witness
            if (_scenario == 4 && _witness != null && _witness.Exists() && !_interactionStarted)
            {
                if (distanceToScene < 15f && player.IsOnFoot)
                {
                    if (!_interactionStarted)
                    {
                        _interactionStarted = true;
                        _interactionStartTime = DateTime.Now;
                        Game.DisplayHelp("Approaching witness...");
                    }
                }
            }

            if (_interactionStarted)
            {
                double elapsedSeconds = (DateTime.Now - _interactionStartTime).TotalSeconds;

                if (elapsedSeconds >= 5 && elapsedSeconds < 6)
                {
                    if (_scenario == 3 || _scenario == 5)
                    {
                        Game.DisplaySubtitle("Driver: ~b~Officer, it came out of nowhere! I couldn't stop in time.");
                        GameFiber.StartNew(() =>
                        {
                            GameFiber.Sleep(3000);
                            Game.DisplaySubtitle("Driver: ~b~My vehicle is damaged. Do I need to file a report?");
                        });
                    }
                    else if (_scenario == 4)
                    {
                        Game.DisplaySubtitle("Hiker: ~b~I saw the whole thing. The car was speeding and hit that poor animal.");
                        GameFiber.StartNew(() =>
                        {
                            GameFiber.Sleep(3000);
                            Game.DisplaySubtitle("Hiker: ~b~It was a dark colored truck, I think it had a dent in the front.");
                        });
                    }
                }
            }

            base.Process();
        }

        // ================= SCENARIO SETUP =================
        private void SpawnAnimal()
        {
            try
            {
                string animalModel = ANIMAL_MODELS[Rnd.Next(ANIMAL_MODELS.Length)];

                _animal = new Ped(animalModel, _spawnPoint, _spawnHeading)
                {
                    IsPersistent = true,
                    IsInvincible = true,
                    BlockPermanentEvents = true
                };

                // Make the animal appear dead
                if (_animal.Exists())
                {
                    _animal.Kill();
                }

                _animalBlip = _animal.AttachBlip();
                _animalBlip.Color = Color.Red;
                _animalBlip.Name = "Struck Animal";
                _animalBlip.Scale = 0.7f;
            }
            catch { }
        }

        private void SetupScenario1() // Driver fled
        {
            try
            {
                Game.DisplayNotification("~y~Dispatch Update:~w~ Witness reports driver fled scene. Vehicle heading toward main road.");

                // Spawn witness hiker
                float witnessAngle = _spawnHeading + 90f;
                Vector3 witnessPos = _spawnPoint + new Vector3(10f, 10f, 0f);
                witnessPos.Z = _spawnPoint.Z;

                _witness = new Ped(HIKER_PEDS[Rnd.Next(HIKER_PEDS.Length)], witnessPos, witnessAngle)
                {
                    IsPersistent = true,
                    IsInvincible = true,
                    BlockPermanentEvents = true
                };

                if (_witness.Exists())
                {
                    _witness.Tasks.StandStill(-1);
                }

                _witnessBlip = _witness.AttachBlip();
                _witnessBlip.Color = Color.Green;
                _witnessBlip.Name = "Witness";
                _witnessBlip.Scale = 0.6f;

                // Spawn damaged vehicle (not at scene, already fled)
                string vehicleModel = CIVILIAN_VEHICLES[Rnd.Next(CIVILIAN_VEHICLES.Length)];

                // Vehicle spawns 200m away in direction of heading
                float rad = MathHelper.ConvertDegreesToRadians(_spawnHeading);
                Vector3 vehicleDir = new Vector3(
                    (float)Math.Sin(rad),
                    -(float)Math.Cos(rad),
                    0f
                );

                Vector3 vehicleSpawnPos = _spawnPoint + vehicleDir * 200f;

                _struckVehicle = new Vehicle(vehicleModel, vehicleSpawnPos, _spawnHeading)
                {
                    IsPersistent = true,
                    IsEngineOn = true
                };

                if (!_struckVehicle.Exists()) return;

                // Add damage to vehicle
                AddRandomDamage(_struckVehicle);

                // Spawn driver
                _driver = new Ped(CIVILIAN_PEDS[Rnd.Next(CIVILIAN_PEDS.Length)], vehicleSpawnPos, _spawnHeading)
                {
                    IsPersistent = true,
                    IsInvincible = false,
                    BlockPermanentEvents = true
                };

                if (_driver.Exists())
                {
                    _driver.WarpIntoVehicle(_struckVehicle, -1);
                }

                // Make vehicle flee
                if (_driver.Exists())
                {
                    Vector3 targetPosition = vehicleSpawnPos + vehicleDir * 1000f;
                    _driver.Tasks.DriveToPosition(targetPosition, 20f, VehicleDrivingFlags.Normal);
                }

                // Add blip to fleeing vehicle
                _vehicleBlip = _struckVehicle.AttachBlip();
                _vehicleBlip.Color = Color.Yellow;
                _vehicleBlip.Name = "Fleeing Vehicle";
                _vehicleBlip.Scale = 0.8f;
                _vehicleBlip.IsFriendly = false;

                Game.DisplayHelp("Hiker witness on scene. Driver fled - locate the damaged vehicle.");
            }
            catch { }
        }

        private void SetupScenario2() // Just the animal
        {
            Game.DisplayNotification("~y~Park Dispatch:~w~ No witnesses on scene. Secure area and arrange for animal removal.");
            Game.DisplayHelp("Secure the area and wait for animal control.");
        }

        private void SetupScenario3() // Driver on scene
        {
            try
            {
                Game.DisplayNotification("~y~Park Dispatch:~w~ Driver still on scene. Assist as needed.");

                // Spawn damaged vehicle at scene
                string vehicleModel = CIVILIAN_VEHICLES[Rnd.Next(CIVILIAN_VEHICLES.Length)];

                // Park vehicle near animal
                float vehicleAngle = _spawnHeading + 30f;
                Vector3 vehiclePos = _spawnPoint + new Vector3(5f, 5f, 0f);
                vehiclePos.Z = _spawnPoint.Z;

                _struckVehicle = new Vehicle(vehicleModel, vehiclePos, vehicleAngle)
                {
                    IsPersistent = true,
                    IsEngineOn = false
                };

                if (!_struckVehicle.Exists()) return;

                // Add damage to vehicle
                AddRandomDamage(_struckVehicle);

                // Spawn driver near vehicle
                Vector3 driverPos = vehiclePos + new Vector3(3f, 2f, 0f);
                driverPos.Z = _spawnPoint.Z;

                _driver = new Ped(CIVILIAN_PEDS[Rnd.Next(CIVILIAN_PEDS.Length)], driverPos, _spawnHeading + 180f)
                {
                    IsPersistent = true,
                    IsInvincible = true,
                    BlockPermanentEvents = true
                };

                if (_driver.Exists())
                {
                    _driver.Tasks.StandStill(-1);
                }

                Game.DisplayHelp("Approach the driver to get their statement.");
            }
            catch { }
        }

        private void SetupScenario4() // Witness hiker
        {
            try
            {
                Game.DisplayNotification("~y~Park Dispatch:~w~ Hiker on scene reporting the incident.");

                // Spawn damaged vehicle (driver left scene)
                string vehicleModel = CIVILIAN_VEHICLES[Rnd.Next(CIVILIAN_VEHICLES.Length)];

                Vector3 vehiclePos = _spawnPoint + new Vector3(15f, 10f, 0f);
                vehiclePos.Z = _spawnPoint.Z;

                _struckVehicle = new Vehicle(vehicleModel, vehiclePos, _spawnHeading)
                {
                    IsPersistent = true,
                    IsEngineOn = false
                };

                if (_struckVehicle.Exists())
                {
                    AddRandomDamage(_struckVehicle);
                }

                // Spawn witness hiker
                Vector3 witnessPos = _spawnPoint + new Vector3(-5f, -5f, 0f);
                witnessPos.Z = _spawnPoint.Z;

                _witness = new Ped(HIKER_PEDS[Rnd.Next(HIKER_PEDS.Length)], witnessPos, _spawnHeading + 90f)
                {
                    IsPersistent = true,
                    IsInvincible = true,
                    BlockPermanentEvents = true
                };

                if (_witness.Exists())
                {
                    _witness.Tasks.StandStill(-1);
                }

                _witnessBlip = _witness.AttachBlip();
                _witnessBlip.Color = Color.Green;
                _witnessBlip.Name = "Witness";
                _witnessBlip.Scale = 0.6f;

                Game.DisplayHelp("Speak with the hiker who witnessed the incident.");
            }
            catch { }
        }

        private void SetupScenario5() // Off-road vehicle hit animal
        {
            try
            {
                Game.DisplayNotification("~y~Park Dispatch:~w~ Off-road vehicle involved in animal strike.");

                // Spawn off-road vehicle
                string vehicleModel = OFFROAD_VEHICLES[Rnd.Next(OFFROAD_VEHICLES.Length)];

                // Vehicle off the main road
                Vector3 vehiclePos = _spawnPoint + new Vector3(8f, 8f, 0f);
                vehiclePos.Z = _spawnPoint.Z;

                _struckVehicle = new Vehicle(vehicleModel, vehiclePos, _spawnHeading + 45f)
                {
                    IsPersistent = true,
                    IsEngineOn = false
                };

                if (!_struckVehicle.Exists()) return;

                // Add damage to vehicle
                AddRandomDamage(_struckVehicle);

                // Spawn driver (could be hiker type)
                string driverModel = HIKER_PEDS[Rnd.Next(HIKER_PEDS.Length)];

                Vector3 driverPos = vehiclePos + new Vector3(4f, 3f, 0f);
                driverPos.Z = _spawnPoint.Z;

                _driver = new Ped(driverModel, driverPos, _spawnHeading + 180f)
                {
                    IsPersistent = true,
                    IsInvincible = true,
                    BlockPermanentEvents = true
                };

                if (_driver.Exists())
                {
                    _driver.Tasks.StandStill(-1);
                }

                Game.DisplayHelp("Approach the off-road vehicle driver.");
            }
            catch { }
        }

        // ================= HELPER METHODS =================
        private void MakeDriverFlee()
        {
            try
            {
                if (_driverFled) return;
                _driverFled = true;

                if (_driver != null && _driver.Exists() && _struckVehicle != null && _struckVehicle.Exists())
                {
                    // Make driver flee more aggressively
                    _driver.Tasks.Clear();

                    float rad = MathHelper.ConvertDegreesToRadians(_spawnHeading);
                    Vector3 vehicleDir = new Vector3(
                        (float)Math.Sin(rad),
                        -(float)Math.Cos(rad),
                        0f
                    );

                    Vector3 fleePosition = _struckVehicle.Position + vehicleDir * 1000f;
                    _driver.Tasks.DriveToPosition(fleePosition, 25f, VehicleDrivingFlags.Emergency);

                    Game.DisplayNotification("~r~Vehicle spotted fleeing!~w~ In pursuit.");
                }
            }
            catch { }
        }

        private void AddRandomDamage(Vehicle vehicle)
        {
            try
            {
                if (!vehicle.Exists()) return;

                // Simple damage - reduce health
                vehicle.Health = 400; // Damaged vehicle
                vehicle.EngineHealth = 200f; // Damaged engine

                // Add some visual damage (through health reduction)
                if (Rnd.NextDouble() > 0.5)
                {
                    vehicle.Health = 250;
                }
            }
            catch { }
        }

        // ================= CLEANUP =================
        public override void End()
        {
            try
            {
                if (_calloutEnded) return;
                _calloutEnded = true;

                // ADDED: Clean up waypoint
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

                if (_animalBlip != null && _animalBlip.Exists())
                {
                    _animalBlip.Delete();
                }

                if (_witnessBlip != null && _witnessBlip.Exists())
                {
                    _witnessBlip.Delete();
                }

                // Don't clean up fleeing vehicle/driver in scenario 1 (let them escape)
                if (_scenario != 1)
                {
                    if (_driver != null && _driver.Exists())
                    {
                        _driver.Dismiss();
                    }

                    if (_struckVehicle != null && _struckVehicle.Exists())
                    {
                        _struckVehicle.Dismiss();
                    }
                }

                if (_witness != null && _witness.Exists())
                {
                    _witness.Dismiss();
                }

                if (_hiker != null && _hiker.Exists())
                {
                    _hiker.Dismiss();
                }

                if (_animal != null && _animal.Exists())
                {
                    _animal.Dismiss();
                }

                Game.DisplayNotification("~b~Park Dispatch:~w~ Animal strike call cleared.");
            }
            catch { }

            base.End();
        }

        public override void OnCalloutNotAccepted()
        {
            try
            {
                // ADDED: Clean up waypoint if callout not accepted
                if (_waypointBlip != null && _waypointBlip.Exists())
                {
                    _waypointBlip.Delete();
                }

                if (_animal != null && _animal.Exists())
                {
                    _animal.Dismiss();
                }
            }
            catch { }

            base.OnCalloutNotAccepted();
        }
    }
}