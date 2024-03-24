﻿using MonkeyLoader.Meta;
using MonkeyLoader.Patching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MonkeyLoader.Unity
{
    internal sealed class UnityHooks : Monkey<UnityHooks>
    {
        private Scene _firstScene;

        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => Enumerable.Empty<IFeaturePatch>();

        protected override bool OnLoaded()
        {
            SceneManager.sceneLoaded += OnFirstSceneReady;
            Application.quitting += () => Mod.Loader.Shutdown();

            return true;
        }

        private void LateRunFirstSceneReady(MonkeyLoader loader, IEnumerable<Mod> mods)
        {
            Logger.Info(() => "New mods were loaded! Executing OnFirstSceneReady hooks on UnityMonkeys!");

            RunFirstSceneReadyHooks(mods);
        }

        private void OnFirstSceneReady(Scene scene, LoadSceneMode sceneMode)
        {
            _firstScene = scene;
            SceneManager.sceneLoaded -= OnFirstSceneReady;

            Mod.Loader.ModsRan += LateRunFirstSceneReady;
            Logger.Info(() => "First Scene Loaded! Executing OnFirstSceneReady hooks on UnityMonkeys!");

            RunFirstSceneReadyHooks(Mod.Loader.Mods);
        }

        private void RunFirstSceneReadyHooks(IEnumerable<Mod> mods)
        {
            var unityMonkeys = mods
                    .GetMonkeysAscending()
                    .SelectCastable<IMonkey, IUnityMonkeyInternal>()
                    .ToArray();

            Logger.Trace(() => "Running FirstSceneReady hooks in this order:");
            Logger.Trace(unityMonkeys.Select(uM => new Func<object>(() => $"{uM.Mod.Title}/{uM.Name}")));

            var sw = Stopwatch.StartNew();

            foreach (var unityMonkey in unityMonkeys)
                unityMonkey.FirstSceneReady(_firstScene);

            Logger.Info(() => $"Done executing OnFirstSceneReady hooks on UnityMonkeys in {sw.ElapsedMilliseconds}ms!");
        }
    }
}