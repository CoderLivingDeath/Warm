#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[CustomPropertyDrawer(typeof(SelectionPopupAttribute))]
public class SelectionPopupDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (SelectionPopupAttribute)attribute;
        object target = property.serializedObject.targetObject;
        Type targetType = target.GetType();

        // ������� ������: ������ �������� SelectedItem[]
        var items = ResolveSelectedItems(target, attr.ValueSourceName);

        if (items == null || items.Length == 0)
        {
            EditorGUI.LabelField(position, label.text, "<��� ��������� ��������>");
            return;
        }

        // ����� ������� ��������� �� Name
        int currentIndex = Array.FindIndex(items, itm => itm.Name == property.stringValue);

        if (!string.IsNullOrEmpty(attr.Placeholder))
        {
            // �������� ������ label � ������ � placeholder

            var labelWidth = EditorGUIUtility.labelWidth;
            var labelRect = new Rect(position.x, position.y, labelWidth, position.height);
            var buttonRect = new Rect(position.x + labelWidth, position.y, position.width - labelWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            if (GUI.Button(buttonRect, attr.Placeholder, EditorStyles.popup))
            {
                GenericMenu menu = new GenericMenu();

                for (int i = 0; i < items.Length; i++)
                {
                    var item = items[i];

                    if (!item.IsActive)
                    {
                        menu.AddDisabledItem(new GUIContent(item.DisplayName));
                        continue;
                    }

                    menu.AddItem(new GUIContent(item.DisplayName), item.IsSelected, () =>
                    {
                        property.serializedObject.Update();
                        property.stringValue = item.Name;
                        property.serializedObject.ApplyModifiedProperties();
                        CallCallback(attr, targetType, target, property.stringValue);
                    });
                }

                menu.DropDown(buttonRect);
            }
        }
        else
        {
            // ������� ��������� � EditorGUI.Popup. ����������� SelectedItem[] � ������ ������������ ���.
            string[] displayNames = items.Select(it => it.DisplayName).ToArray();

            // Find index of current
            int curIndex = Mathf.Max(0, Array.FindIndex(items, itm => itm.Name == property.stringValue));

            // ������ �������� �������� ����� ���� �������
            int[] activeIndices = items.Select((it, idx) => new { it, idx })
                                       .Where(x => x.it.IsActive)
                                       .Select(x => x.idx)
                                       .ToArray();

            int newIndex = EditorGUI.Popup(position, label.text, curIndex, displayNames);

            // ���� ��������� ������� ��������� � ������
            if (newIndex != curIndex && activeIndices.Contains(newIndex))
            {
                property.stringValue = items[newIndex].Name;
                CallCallback(attr, targetType, target, property.stringValue);
            }
        }
    }

    private void CallCallback(SelectionPopupAttribute attr, Type targetType, object target, string value)
    {
        if (!string.IsNullOrEmpty(attr.CallbackName))
        {
            var method = targetType.GetMethod(attr.CallbackName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                var param = method.GetParameters();
                if (param.Length == 1 && param[0].ParameterType == typeof(string))
                    method.Invoke(target, new object[] { value });
                else
                    method.Invoke(target, null);
            }
            else
            {
                Debug.LogWarning($"SelectionPopup callback '{attr.CallbackName}' not found on {targetType.Name}");
            }
        }
    }

    /// <summary>
    /// �������� SelectedItem[] ����� ��������/����/����� ��������� �� �����.
    /// </summary>
    private SelectItem[] ResolveSelectedItems(object target, string sourceName)
    {
        Type type = target.GetType();

        // Field
        FieldInfo field = type.GetField(sourceName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (field != null)
        {
            object value = field.GetValue(target);
            if (value is SelectItem[] arr) return arr;
            if (value is IEnumerable<SelectItem> en) return en.ToArray();
        }

        // Property
        PropertyInfo prop = type.GetProperty(sourceName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (prop != null)
        {
            object value = prop.GetValue(target, null);
            if (value is SelectItem[] arr) return arr;
            if (value is IEnumerable<SelectItem> en) return en.ToArray();
        }

        // Method (��� ����������)
        MethodInfo method = type.GetMethod(sourceName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (method != null && method.GetParameters().Length == 0)
        {
            object value = method.Invoke(target, null);
            if (value is SelectItem[] arr) return arr;
            if (value is IEnumerable<SelectItem> en) return en.ToArray();
        }

        Debug.LogError($"SelectionPopup: �������� '{sourceName}' �� ������ ��� �� ���������� ������ SelectedItem/list.");
        return new SelectItem[0];
    }
}
#endif
