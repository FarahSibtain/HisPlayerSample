#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HISPlayerAPI
{
    [InitializeOnLoad]
    internal class HISPlayerXROpenXRFeatureSetup : IPreprocessBuildWithReport
    {
        private const string FEATURE_HELPERS_TYPE =
            "UnityEditor.XR.OpenXR.Features.FeatureHelpers, Unity.XR.OpenXR.Editor";

        private const string COMPOSITION_LAYERS_FEATURE_ID =
            "com.unity.openxr.feature.compositionlayers";

        private static bool _warned;

        public int callbackOrder
        {
            get { return 0; }
        }

        static HISPlayerXROpenXRFeatureSetup()
        {
            EditorApplication.delayCall += Ensure;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report != null && report.summary.platform != BuildTarget.Android)
            {
                return;
            }
            Ensure();
        }

        private static void Ensure()
        {
            try
            {
                Type featureHelpers = Type.GetType(FEATURE_HELPERS_TYPE);
                if (featureHelpers == null)
                {
                    return;
                }

                MethodInfo getFeature = featureHelpers.GetMethod(
                    "GetFeatureWithIdForBuildTarget",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(BuildTargetGroup), typeof(string) },
                    null);
                if (getFeature == null)
                {
                    Warn("FeatureHelpers.GetFeatureWithIdForBuildTarget was not found");
                    return;
                }

                object compositionLayers = getFeature.Invoke(null,
                    new object[] { BuildTargetGroup.Android, COMPOSITION_LAYERS_FEATURE_ID });
                if (compositionLayers == null || !IsEnabled(compositionLayers))
                {
                    return;
                }

                object feature = getFeature.Invoke(null,
                    new object[] { BuildTargetGroup.Android,
                        HISPlayerXRVideoLayerFeature.FEATURE_ID });
                if (feature == null)
                {
                    MethodInfo refresh = featureHelpers.GetMethod(
                        "RefreshFeatures",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(BuildTargetGroup) },
                        null);
                    if (refresh == null)
                    {
                        Warn("FeatureHelpers.RefreshFeatures was not found");
                        return;
                    }

                    refresh.Invoke(null, new object[] { BuildTargetGroup.Android });
                    feature = getFeature.Invoke(null,
                        new object[] { BuildTargetGroup.Android,
                            HISPlayerXRVideoLayerFeature.FEATURE_ID });
                }

                if (feature == null || IsEnabled(feature))
                {
                    return;
                }

                SetEnabled(feature, true);
                var asset = feature as UnityEngine.Object;
                if (asset != null)
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }
            }
            catch (Exception e)
            {
                Warn(e.Message);
            }
        }

        private static bool IsEnabled(object feature)
        {
            PropertyInfo enabled = feature.GetType().GetProperty(
                "enabled", BindingFlags.Public | BindingFlags.Instance);
            if (enabled == null || enabled.PropertyType != typeof(bool))
            {
                Warn("OpenXRFeature.enabled was not found");

                return true;
            }
            return (bool)enabled.GetValue(feature);
        }

        private static void SetEnabled(object feature, bool value)
        {
            PropertyInfo enabled = feature.GetType().GetProperty(
                "enabled", BindingFlags.Public | BindingFlags.Instance);
            if (enabled != null && enabled.CanWrite)
            {
                enabled.SetValue(feature, value);
            }
        }

        private static void Warn(string reason)
        {
            if (_warned)
            {
                return;
            }

            _warned = true;
            Debug.LogWarning("HISPLAYER - WARNING: could not enable the \"HISPlayer XR Video " +
                "Layer\" OpenXR feature automatically. Enable it by hand in Project Settings > XR " +
                "Plug-in Management > OpenXR > Android - " + reason);
        }
    }
}
#endif
