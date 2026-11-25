using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif

namespace HISPlayerAPI
{
#if UNITY_EDITOR
    [OpenXRFeature(
        UiName = "HISPlayer XR Video Layer",
        Company = "HISPlayer",
        Desc = "Requests XR_FB_composition_layer_image_layout. Enabled automatically " +
               "whenever OpenXR's Composition Layers Support is on for Android.",
        Category = FeatureCategory.Feature,
        FeatureId = FEATURE_ID,
        Version = "1.0.0",
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        OpenxrExtensionStrings = IMAGE_LAYOUT_EXTENSION)]
#endif
    public class HISPlayerXRVideoLayerFeature : OpenXRFeature
    {
        public const string FEATURE_ID = "com.hisplayer.openxr.videolayer";

        public const string IMAGE_LAYOUT_EXTENSION = "XR_FB_composition_layer_image_layout";

        protected override bool OnInstanceCreate(ulong xrInstance)
        {
            bool enabled = OpenXRRuntime.IsExtensionEnabled(IMAGE_LAYOUT_EXTENSION);
            if (!enabled)
            {
                Debug.LogWarning("HISPLAYER - WARNING: this OpenXR runtime does not offer " + IMAGE_LAYOUT_EXTENSION);
            }

            return true;
        }
    }
}
