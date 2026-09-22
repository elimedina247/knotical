using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Knotical.Editor
{
    [CustomPropertyDrawer(typeof(FloatRange))]
    [CustomPropertyDrawer(typeof(IntRange))]
    public class RangeDrawer : PropertyDrawer
    {
        private const float FieldWidth = 52f;
        private const float Gap = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty min = property.FindPropertyRelative("Min");
            SerializedProperty max = property.FindPropertyRelative("Max");
            bool isInt = min.propertyType == SerializedPropertyType.Integer;
            RangeAttribute bounds = typeof(LevelSettings).GetField(property.name)?.GetCustomAttribute<RangeAttribute>();

            label = EditorGUI.BeginProperty(position, label, property);
            Rect content = EditorGUI.PrefixLabel(position, label);
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var minRect = new Rect(content.x, content.y, FieldWidth, content.height);
            var maxRect = new Rect(content.xMax - FieldWidth, content.y, FieldWidth, content.height);
            var sliderRect = new Rect(minRect.xMax + Gap, content.y, Mathf.Max(0f, maxRect.x - minRect.xMax - 2f * Gap), content.height);

            float lo = isInt ? min.intValue : min.floatValue;
            float hi = isInt ? max.intValue : max.floatValue;

            EditorGUI.BeginChangeCheck();
            lo = isInt ? EditorGUI.IntField(minRect, (int)lo) : EditorGUI.FloatField(minRect, lo);
            hi = isInt ? EditorGUI.IntField(maxRect, (int)hi) : EditorGUI.FloatField(maxRect, hi);
            if (bounds != null && sliderRect.width > 20f) EditorGUI.MinMaxSlider(sliderRect, ref lo, ref hi, bounds.min, bounds.max);
            if (EditorGUI.EndChangeCheck())
            {
                if (bounds != null)
                {
                    lo = Mathf.Clamp(lo, bounds.min, bounds.max);
                    hi = Mathf.Clamp(hi, bounds.min, bounds.max);
                }
                hi = Mathf.Max(lo, hi);
                if (isInt)
                {
                    min.intValue = Mathf.RoundToInt(lo);
                    max.intValue = Mathf.RoundToInt(hi);
                }
                else
                {
                    min.floatValue = lo;
                    max.floatValue = hi;
                }
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}
