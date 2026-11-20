using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class HISPlayerXRLayerAvailability
{
    private const string FEATURE_HELPERS_TYPE =
        "UnityEditor.XR.OpenXR.Features.FeatureHelpers, Unity.XR.OpenXR.Editor";

    private const string COMPOSITION_LAYERS_FEATURE_ID = "com.unity.openxr.feature.compositionlayers";

    private const double CACHE_SECONDS = 1.0;

    private static bool _isAvailable;
    private static double _evaluatedAt = double.NegativeInfinity;
    private static bool _warned;

    internal static bool IsAvailable
    {
        get
        {
            if (EditorApplication.timeSinceStartup - _evaluatedAt > CACHE_SECONDS)
            {
                _isAvailable = Evaluate();
                _evaluatedAt = EditorApplication.timeSinceStartup;
            }
            return _isAvailable;
        }
    }

    internal static void Invalidate()
    {
        _evaluatedAt = double.NegativeInfinity;
    }

    private static bool Evaluate()
    {
        try
        {
            Type featureHelpers = Type.GetType(FEATURE_HELPERS_TYPE);
            if (featureHelpers == null)
            {
                return false;
            }

            MethodInfo getFeature = featureHelpers.GetMethod(
                "GetFeatureWithIdForBuildTarget",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(BuildTargetGroup), typeof(string) },
                null);
            if (getFeature == null)
            {
                return CannotTell("FeatureHelpers.GetFeatureWithIdForBuildTarget was not found");
            }

            object feature = getFeature.Invoke(null,
                new object[] { BuildTargetGroup.Android, COMPOSITION_LAYERS_FEATURE_ID });
            if (feature == null)
            {
                return false;
            }

            PropertyInfo enabled = feature.GetType().GetProperty(
                "enabled", BindingFlags.Public | BindingFlags.Instance);
            if (enabled == null || enabled.PropertyType != typeof(bool))
            {
                return CannotTell("OpenXRFeature.enabled was not found");
            }

            return (bool)enabled.GetValue(feature);
        }
        catch (Exception e)
        {
            return CannotTell(e.Message);
        }
    }

    private static bool CannotTell(string reason)
    {
        if (!_warned)
        {
            _warned = true;
            Debug.LogWarning("HISPLAYER - WARNING: could not work out whether OpenXR's \"Composition " +
                "Layers Support\" is enabled, so the XR video layer options are being shown anyway - " +
                reason);
        }
        return true;
    }
}
