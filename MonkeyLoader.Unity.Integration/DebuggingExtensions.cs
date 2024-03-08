using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MonkeyLoader.Unity
{
    /// <summary>
    /// Contains extension methods to help debug Unity objects.
    /// </summary>
    public static class DebuggingExtensions
    {
        /// <summary>
        /// Returns the names and transforms of the full hierarchy the given game object.
        /// </summary>
        /// <param name="gameObject">The game object at which to start.</param>
        /// <returns>Every game object's name and transform as messages.</returns>
        public static IEnumerable<string> DebugHierarchy(this GameObject gameObject)
        {
            do
            {
                var transform = gameObject.transform;
                yield return $"{gameObject.name} (T: {transform.localPosition}; S: {transform.localScale}; R: {transform.rotation.eulerAngles})";

                gameObject = gameObject.transform.parent.gameObject;
            }
            while (gameObject != null);
        }

        /// <summary>
        /// Returns the names and transforms of the full hierarchy the given transform's game object.
        /// </summary>
        /// <param name="transform">The transform at which to start.</param>
        /// <returns>Every game object's name and transform as messages.</returns>
        public static IEnumerable<string> DebugHierarchy(this Transform transform)
            => DebugHierarchy(transform.gameObject);
    }
}