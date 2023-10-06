﻿using FrooxEngine;
using HarmonyLib;
using MonkeyLoader.Patching;
using MonkeyLoader.Resonite.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MonkeyLoader.Resonite
{
    [HarmonyPatchCategory(nameof(ResoniteHooksMonkey))]
    [HarmonyPatch(typeof(Engine), nameof(Engine.Initialize))]
    [FeaturePatch<EngineInitialization>(PatchCompatibility.HookOnly)]
    internal sealed class ResoniteHooksMonkey : Monkey<ResoniteHooksMonkey>
    {
        protected override void OnLoaded()
        {
            Harmony.PatchCategory(nameof(ResoniteHooksMonkey));
        }

        [HarmonyPrefix]
        private static void initializePrefix()
        {
            Mod.Loader.LoggingHandler = new ResoniteLoggingHandler();

            Engine.Current.OnReady += onEngineReady;
            Engine.Current.OnShutdownRequest += onEngineShutdownRequested;
            Engine.Current.OnShutdown += onEngineShutdown;
        }

        private static void onEngineReady()
        {
            try
            {
                Mod.Loader.Monkeys
                    .SelectCastable<Monkey, IResoniteMonkey>()
                    .Select(resMonkey => (Delegate)resMonkey.OnEngineReady)
                    .TryInvokeAll();
            }
            catch (AggregateException ex)
            {
                Logger.Error(() => $"The EngineReady hook failed for some mods.{Environment.NewLine}{string.Join(Environment.NewLine, ex.InnerExceptions.Select(inEx => $"{inEx.Message}{Environment.NewLine}{inEx.StackTrace}"))}");
            }
        }

        private static void onEngineShutdown()
        {
            try
            {
                Mod.Loader.Monkeys
                    .SelectCastable<Monkey, IResoniteMonkey>()
                    .Select(resMonkey => (Delegate)resMonkey.OnEngineShutdown)
                    .TryInvokeAll();
            }
            catch (AggregateException ex)
            {
                Logger.Error(() => $"The EngineShutdown hook failed for some mods.{Environment.NewLine}{string.Join(Environment.NewLine, ex.InnerExceptions.Select(inEx => $"{inEx.Message}{Environment.NewLine}{inEx.StackTrace}"))}");
            }

            Mod.Loader.Shutdown();
        }

        private static void onEngineShutdownRequested(string reason)
        {
            try
            {
                Mod.Loader.Monkeys
                    .SelectCastable<Monkey, IResoniteMonkey>()
                    .Select(resMonkey => (Delegate)resMonkey.OnEngineShutdownRequested)
                    .TryInvokeAll(reason);
            }
            catch (AggregateException ex)
            {
                Logger.Error(() => $"The EngineShutdownRequested hook failed for some mods.{Environment.NewLine}{string.Join(Environment.NewLine, ex.InnerExceptions.Select(inEx => $"{inEx.Message}{Environment.NewLine}{inEx.StackTrace}"))}");
            }
        }
    }
}