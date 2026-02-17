using HISPlayerAPI;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEditorInternal;

// =====================================
// Custom Inspector for HISPlayerManager
// =====================================
[CustomEditor(typeof(HISPlayerManager), true)]
public class HISPlayerInspectorGUI : Editor
{
    private ReorderableList multiStreamReorderableList;

    // Centralized field configuration
    private readonly List<FieldConfig> fieldConfigs = new()
    {
        // Render Mode
        new FieldConfig {
            propertyName = "renderMode",
            label = "Render Mode",
            type = FieldType.Simple,
            tooltip = "Texture for rendering"
        },

        // Render Texture
        new FieldConfig {
            propertyName = "renderTexture",
            label = "Render Texture",
            type = FieldType.Simple,
            tooltip = "Reference to the rendering RenderTexture",
            condition = (element) =>
            {
                var mode = element.FindPropertyRelative("renderMode");
                return (HISPlayerRenderMode)mode.enumValueIndex == HISPlayerRenderMode.RenderTexture;
            }
        },

        // Material
        new FieldConfig {
            propertyName = "material",
            label = "Material",
            type = FieldType.Simple,
            tooltip = "Reference to the rendering Material",
            condition = (element) =>
            {
                var mode = element.FindPropertyRelative("renderMode");
                return (HISPlayerRenderMode)mode.enumValueIndex == HISPlayerRenderMode.Material;
            }
        },

        // Raw Image
        new FieldConfig {
            propertyName = "rawImage",
            label = "Raw Image",
            type = FieldType.Simple,
            tooltip = "Reference to the rendering RawImage",
            condition = (element) =>
            {
                var mode = element.FindPropertyRelative("renderMode");
                return (HISPlayerRenderMode)mode.enumValueIndex == HISPlayerRenderMode.RawImage;
            }
        },

        // URLs
        new FieldConfig {
            propertyName = "url",
            label = "URLs",
            type = FieldType.Foldout,
            tooltip = "List of URLs for the streams"
        },

        // URLs Mime Types
        new FieldConfig {
            propertyName = "urlMimeTypes",
            label = "URLs Mime Types",
            type = FieldType.Foldout,
            tooltip = "List of URL MIME Types for each URL in the URL list"
        },

        // AutoPlay
        new FieldConfig {
            propertyName = "autoPlay",
            label = "Auto Play",
            type = FieldType.Simple,
            tooltip = "True - Play the stream automatically after player set-up"
        },

        // Loop Playback
        new FieldConfig {
            propertyName = "loopPlayback",
            label = "Loop Playback",
            type = FieldType.Simple,
            tooltip = "True - Repeat the current playback when it finishes"
        },

        // Auto Transition
        new FieldConfig {
            propertyName = "autoTransition",
            label = "Auto Transition",
            type = FieldType.Simple,
            tooltip = "True - Automatically changes to the next playback in the list if possible. This action won't have effect when loopPlayback is true"
        },

        // Audio Output Label
        new FieldConfig {
            label = "Audio Output (Android)",
            type = FieldType.Label,
            boldLabel = true
        },

        // Unity Audio
        new FieldConfig {
            propertyName = "unityAudio",
            label = "Unity Audio",
            type = FieldType.Simple,
            tooltip = "True - Retrieves the audio data for Unity AudioSource instead of direct speaker output. Cannot be combined with Ambisonic Audio.",
            condition = (element) => element.FindPropertyRelative("ambisonicAudio").enumValueIndex == 0
        },

        // Ambisonic Audio
        new FieldConfig {
            propertyName = "ambisonicAudio",
            label = "Ambisonic Audio",
            type = FieldType.Simple,
            tooltip = "Ambisonic audio supporting ambiX format from first order to 3rd order, and TBE format. Enabling Ambisonic will disable Unity Audio.",
            condition = (element) => !element.FindPropertyRelative("unityAudio").boolValue
        },

        // DRM Label
        new FieldConfig {
            label = "DRM (Android)",
            type = FieldType.Label,
            boldLabel = true,
            condition = (element) =>
            {
                var dependencyFound = element.FindPropertyRelative("enableDRM");
                return dependencyFound != null;
            }
        },

        // DRM
        new FieldConfig {
            propertyName = "enableDRM",
            label = "Enable DRM",
            type = FieldType.Simple,
            tooltip = "Use DRM for protected streams"
        },

        // Key Server URI
        new FieldConfig {
            propertyName = "keyServerURI",
            label = "Key Server URI",
            type = FieldType.Foldout,
            tooltip = "Widevine Key",
            condition = (element) => element.FindPropertyRelative("enableDRM").boolValue
        },

        // DRM Tokens
        new FieldConfig {
            propertyName = "DRMTokens",
            label = "DRM Tokens",
            type = FieldType.Foldout,
            tooltip = "DRM Tokens",
            condition = (element) => element.FindPropertyRelative("enableDRM").boolValue
        },
    };

    private void OnEnable()
    {
        // Create reorderable list for multi-stream properties
        multiStreamReorderableList = new ReorderableList(
            serializedObject: serializedObject,
            elements: serializedObject.FindProperty("multiStreamProperties"),
            draggable: true,
            displayHeader: true,
            displayAddButton: true,
            displayRemoveButton: true
        );

        // List header
        multiStreamReorderableList.drawHeaderCallback = (Rect rect) =>
        {
            // Numbers needed to adjust
            float fullWidth = EditorGUIUtility.currentViewWidth - 23;
            Rect fullRect = new Rect(rect.x - 6, rect.y - 1, fullWidth, rect.height);

            GUIStyle headerFixed = new GUIStyle("RL Header");
            headerFixed.fixedWidth = fullWidth;
            headerFixed.fontStyle = FontStyle.Bold;
            headerFixed.fontSize = 14;
            headerFixed.alignment = TextAnchor.MiddleCenter;

            EditorGUI.LabelField(fullRect, "Multi Stream Properties", headerFixed);
        };

        // Draw each element in the list
        multiStreamReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            SerializedProperty element = multiStreamReorderableList.serializedProperty.GetArrayElementAtIndex(index);

            // Foldout for each list element
            GUIStyle indexStyle = new GUIStyle("Foldout");
            indexStyle.fontStyle = FontStyle.Bold;
            indexStyle.fontSize = 13;
            int arrowDistance = 10;
            element.isExpanded = EditorGUI.Foldout(
                new Rect(rect.x + arrowDistance, rect.y, rect.width - arrowDistance, EditorGUIUtility.singleLineHeight),
                element.isExpanded,
                $"Element {index}",
                true,
                indexStyle
            );

            if (element.isExpanded)
            {
                float yOffset = rect.y + EditorGUIUtility.singleLineHeight + 4; // Margin with the Element label
                float lineHeight = EditorGUIUtility.singleLineHeight;
                int distCenter = 2;

                // Iterate through all configured fields
                foreach (var config in fieldConfigs)
                {
                    if (!config.visible) continue;
                    if (config.condition != null && !config.condition(element)) continue;

                    switch (config.type)
                    {
                        case FieldType.Simple:
                            var sp = element.FindPropertyRelative(config.propertyName);
                            if (sp != null)
                            {
                                EditorGUI.PropertyField(
                                    new Rect(rect.x - distCenter, yOffset, rect.width, lineHeight),
                                    sp,
                                    new GUIContent(config.label ?? sp.displayName, config.tooltip),
                                    true
                                );
                                yOffset += lineHeight + config.extraSpacing;
                            }
                            break;

                        case FieldType.Foldout:
                            var fold = element.FindPropertyRelative(config.propertyName);
                            if (fold != null)
                            {
                                float height = EditorGUI.GetPropertyHeight(fold, fold.isExpanded);
                                EditorGUI.PropertyField(
                                    new Rect(rect.x - distCenter, yOffset, rect.width, height),
                                    fold,
                                    new GUIContent(config.label ?? fold.displayName, config.tooltip),
                                    true
                                );
                                yOffset += height + config.extraSpacing;
                            }
                            break;

                        case FieldType.Label:
                            var style = config.boldLabel ? EditorStyles.boldLabel : EditorStyles.label;
                            EditorGUI.LabelField(
                                new Rect(rect.x - distCenter, yOffset, rect.width, lineHeight),
                                config.label, style
                            );
                            yOffset += lineHeight + config.extraSpacing;
                            break;

                        case FieldType.Custom:
                            break;
                    }
                }
            }
        };

        // Dynamic height calculation for list elements
        multiStreamReorderableList.elementHeightCallback = (int index) =>
        {
            SerializedProperty element = multiStreamReorderableList.serializedProperty.GetArrayElementAtIndex(index);
            if (!element.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight;

            foreach (var config in fieldConfigs)
            {
                if (!config.visible) continue;
                if (config.condition != null && !config.condition(element)) continue;

                switch (config.type)
                {
                    case FieldType.Simple:
                        var sp = element.FindPropertyRelative(config.propertyName);
                        if (sp != null)
                            height += EditorGUIUtility.singleLineHeight + config.extraSpacing;
                        break;

                    case FieldType.Foldout:
                        var fold = element.FindPropertyRelative(config.propertyName);
                        if (fold != null)
                            height += EditorGUI.GetPropertyHeight(fold, fold.isExpanded) + config.extraSpacing;
                        break;

                    case FieldType.Label:
                        height += EditorGUIUtility.singleLineHeight + config.extraSpacing;
                        break;

                    case FieldType.Custom:
                        break;
                }
            }

            height += 10;   // Margin with the next element
            return height;
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Separate base class and derived class properties
        System.Type baseType = typeof(HISPlayerManager);
        List<SerializedProperty> baseProperties = new();
        List<SerializedProperty> derivedProperties = new();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        // Iterate through all serialized properties
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script")
            {
                // Script reference (read-only)
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(iterator, true);
                EditorGUI.EndDisabledGroup();
                continue;
            }

            if (iterator.name == "multiStreamProperties")
                continue;

            var fieldInfo = baseType.GetField(
                iterator.name,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance
            );

            if (fieldInfo != null && fieldInfo.DeclaringType == baseType)
                baseProperties.Add(iterator.Copy());
            else
                derivedProperties.Add(iterator.Copy());
        }

        // Draw base class properties
        EditorGUILayout.Space(1);
        foreach (var prop in baseProperties)
            EditorGUILayout.PropertyField(prop, true);

        // Draw custom list
        EditorGUILayout.Space(2);
        multiStreamReorderableList.DoLayoutList();

        // Draw derived class properties
        foreach (var prop in derivedProperties)
            EditorGUILayout.PropertyField(prop, true);

        serializedObject.ApplyModifiedProperties();
    }
}
