#if HISPLAYER_COMPOSITION_LAYERS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.XR.CompositionLayers;
using Unity.XR.CompositionLayers.Layers;
using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace HISPlayerAPI
{
    [Preserve]
    internal static class HISPlayerCompositionLayerSceneExit
    {
        private class State
        {
            public readonly List<IDisposable> detachedRigs = new List<IDisposable>();
        }

        [Preserve]
        public static object Prepare()
        {
            IDictionary rigTable = ProjectionDataTable();
            var state = new State();
            int disabled = 0;

            foreach (CompositionLayer layer in Object.FindObjectsByType<CompositionLayer>(FindObjectsSortMode.None))
            {
                // The default scene layer is Unity's own eye buffer; it is not destroyed with the scene.
                if (layer.LayerData is DefaultLayerData)
                    continue;

                /*int id = layer.GetInstanceID();
                if (rigTable != null && rigTable.Contains(id))
                {
                    if (rigTable[id] is IDisposable data)
                        state.detachedRigs.Add(data);
                    rigTable.Remove(id);
                }*/

                if (layer.enabled)
                {
                    layer.enabled = false;
                    ++disabled;
                }
            }

            if (disabled == 0 && state.detachedRigs.Count == 0)
                return null;

            return state;
        }

        [Preserve]
        public static void Complete(object state)
        {
            if (!(state is State s))
                return;

            foreach (IDisposable data in s.detachedRigs)
            {
                try
                {
                    data.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("HISPLAYER - WARNING: ReleaseAsync could not release a projection rig: " + e.Message);
                }
            }
            s.detachedRigs.Clear();
        }

        private static IDictionary ProjectionDataTable()
        {
            try
            {
                Type handler = Type.GetType(
                    "UnityEngine.XR.OpenXR.CompositionLayers.OpenXRProjectionLayer, Unity.XR.OpenXR");
                FieldInfo field = handler?.GetField("m_ProjectionData",
                    BindingFlags.NonPublic | BindingFlags.Static);
                return field?.GetValue(null) as IDictionary;
            }
            catch (Exception e)
            {
                Debug.LogWarning("HISPLAYER - WARNING: ReleaseAsync could not reach the projection rig table: " + e.Message);
                return null;
            }
        }
    }
}
#endif
