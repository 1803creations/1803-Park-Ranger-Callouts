using System;
using System.Collections.Generic;
using LSPD_First_Response.Mod.API;
using Rage;

namespace _1803RangerCallouts
{
    public class Main : Plugin
    {
        private List<System.Type> registeredCallouts = new List<System.Type>();

        public override void Initialize()
        {
            Game.LogTrivial("[1803RangerCallouts] Initializing...");

            // Load settings first
            Settings.LoadSettings();

            Functions.OnOnDutyStateChanged += OnDutyChanged;
        }

        private void OnDutyChanged(bool onDuty)
        {
            if (onDuty)
            {
                // Unregister any previously registered callouts first
                UnregisterAllCallouts();

                // Register callouts based on INI settings
                if (Settings.EnableAnimalStruck)
                {
                    RegisterCallout(typeof(AnimalStruck));
                }

                if (Settings.EnableIllegalParkedCar)
                {
                    RegisterCallout(typeof(IllegalParkedCar));
                }

                if (Settings.EnableOffRoadViolation)
                {
                    RegisterCallout(typeof(OffRoadViolation));
                }

                Game.LogTrivial($"[1803RangerCallouts] Total callouts registered: {registeredCallouts.Count}");
            }
            else
            {
                // Clean up when going off duty
                UnregisterAllCallouts();
            }
        }

        private void RegisterCallout(System.Type calloutType)
        {
            try
            {
                Functions.RegisterCallout(calloutType);
                registeredCallouts.Add(calloutType);
                Game.LogTrivial($"[1803RangerCallouts] Registered: {calloutType.Name}");
            }
            catch (System.Exception ex)
            {
                Game.LogTrivial($"[1803RangerCallouts] Error registering {calloutType.Name}: {ex.Message}");
            }
        }

        private void UnregisterAllCallouts()
        {
            if (registeredCallouts.Count > 0)
            {
                Game.LogTrivial($"[1803RangerCallouts] Unregistering {registeredCallouts.Count} callouts...");
                registeredCallouts.Clear();
            }
        }

        public override void Finally()
        {
            UnregisterAllCallouts();
            Game.LogTrivial("[1803RangerCallouts] Plugin terminated.");
        }
    }
}