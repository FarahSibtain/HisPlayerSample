using System;
using UnityEditor;

// ==========================
// Field Configuration
// ==========================
[Serializable]
public class FieldConfig
{
    public string propertyName;                         // Name of the property in the SerializedObject
    public string label;                                // Optional label to display
    public FieldType type;                              // Type of field
    public bool visible = true;                         // Whether the field should be shown
    public float extraSpacing = 2f;                     // Extra spacing after the field
    public bool boldLabel = false;                      // Display the label in bold
    public string tooltip;                              // Optional tooltip
    public Func<SerializedProperty, bool> condition;    // Dynamic condition for showing/hiding the field
}

public enum FieldType
{
    Simple,     // Standard PropertyField
    Foldout,    // Expandable group
    Label,      // Just a text label
    Custom      // Custom rendering logic
}