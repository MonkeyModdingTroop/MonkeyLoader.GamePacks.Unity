using MonkeyLoader.Meta;
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
        private static IUnityMonkeyInternal[] UnityMonkeys
        {
            get
            {
                var monkeys = Mod.Loader.Mods
                    .GetMonkeysAscending()
                    .SelectCastable<IMonkey, IUnityMonkeyInternal>()
                    .ToArray();

                return monkeys;
            }
        }

        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => Enumerable.Empty<IFeaturePatch>();

        protected override bool OnLoaded()
        {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            Application.quitting += () => Mod.Loader.Shutdown();

            return true;
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            Info(() => "First Scene Loaded! Executing OnFirstSceneReady hooks on UnityMonkeys!");

            var unityMonkeys = UnityMonkeys;
            Logger.Trace(() => "Running FirstSceneReady hooks in this order:");
            Logger.Trace(unityMonkeys.Select(uM => new Func<object>(() => $"{uM.Mod.Title}/{uM.Name}")));

            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
            var sw = Stopwatch.StartNew();

            foreach (var unityMonkey in unityMonkeys)
                unityMonkey.FirstSceneReady(scene);

            Info(() => $"Done executing OnFirstSceneReady hooks on UnityMonkeys in {sw.ElapsedMilliseconds}ms!");
        }
    }
}