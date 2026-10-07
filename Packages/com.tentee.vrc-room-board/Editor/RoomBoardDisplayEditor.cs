#if UNITY_EDITOR
using System.Collections.Generic;
using RoomBoard;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RoomBoardEditor
{
    [CustomEditor(typeof(RoomBoardDisplay))]
    [CanEditMultipleObjects]
    public class RoomBoardDisplayEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;

            serializedObject.Update();
            SerializedProperty property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "textScale") continue;
                if (property.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property, true);
                }
                else
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
            serializedObject.ApplyModifiedProperties();

            SerializedProperty scaleProperty = serializedObject.FindProperty("textScale");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("文字サイズ", EditorStyles.boldLabel);
            EditorGUI.showMixedValue = scaleProperty.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            float newScale = EditorGUILayout.Slider("倍率", scaleProperty.floatValue, 0.5f, 3f);
            bool scaleChanged = EditorGUI.EndChangeCheck();
            EditorGUI.showMixedValue = false;

            EditorGUILayout.HelpBox(
                "掲示板の幅はそのままで、文字・行の高さ・余白を拡大します。1 行に入る名前は減ります。名前が「…」で切れるときは Max Names Per Area を減らすと、残りが「ほか N 人」で表示されます。掲示板全体を大きくしたい場合は Transform の Scale を変えてください。",
                MessageType.Info);

            if (scaleChanged)
            {
                foreach (Object item in targets)
                {
                    RoomBoardDisplay display = item as RoomBoardDisplay;
                    if (display != null) ApplyTextScale(display, newScale);
                }
            }
        }

        public static void ApplyTextScale(RoomBoardDisplay display, float newScale)
        {
            if (display == null) return;
            newScale = Mathf.Clamp(newScale, 0.5f, 3f);
            float old = display.textScale <= 0f ? 1f : display.textScale;
            if (Mathf.Approximately(old, newScale)) return;
            float ratio = newScale / old;

            List<Object> touched = new List<Object>();
            AddObject(touched, display);

            TextMeshProUGUI title = display.titleText;
            LayoutElement titleLayout = title == null ? null : title.GetComponent<LayoutElement>();
            AddObject(touched, title);
            AddObject(touched, titleLayout);

            GameObject row = display.rowTemplate;
            LayoutElement rowLayout = row == null ? null : row.GetComponent<LayoutElement>();
            AddObject(touched, rowLayout);

            Transform[] children = new Transform[4];
            TextMeshProUGUI[] texts = new TextMeshProUGUI[3];
            if (row != null)
            {
                children[0] = row.transform.Find("Accent");
                children[1] = row.transform.Find("AreaName");
                children[2] = row.transform.Find("Count");
                children[3] = row.transform.Find("Names");
                texts[0] = children[1] == null ? null : children[1].GetComponent<TextMeshProUGUI>();
                texts[1] = children[2] == null ? null : children[2].GetComponent<TextMeshProUGUI>();
                texts[2] = children[3] == null ? null : children[3].GetComponent<TextMeshProUGUI>();
                for (int i = 0; i < children.Length; i++)
                    AddObject(touched, children[i] == null ? null : children[i].GetComponent<RectTransform>());
                for (int i = 0; i < texts.Length; i++) AddObject(touched, texts[i]);
            }

            RectTransform rowContainer = display.rowContainer;
            VerticalLayoutGroup rowGroup = rowContainer == null ? null : rowContainer.GetComponent<VerticalLayoutGroup>();
            Transform panel = rowContainer == null ? null : rowContainer.parent;
            VerticalLayoutGroup panelGroup = panel == null ? null : panel.GetComponent<VerticalLayoutGroup>();
            AddObject(touched, rowGroup);
            AddObject(touched, panelGroup);

            if (touched.Count > 0) Undo.RecordObjects(touched.ToArray(), "Room Board Text Scale");

            if (title != null) title.fontSize *= ratio;
            if (titleLayout != null) titleLayout.preferredHeight *= ratio;
            if (rowLayout != null)
            {
                rowLayout.preferredHeight *= ratio;
                rowLayout.minHeight *= ratio;
            }
            for (int i = 0; i < texts.Length; i++)
                if (texts[i] != null) texts[i].fontSize *= ratio;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null) continue;
                RectTransform rect = children[i].GetComponent<RectTransform>();
                if (rect == null) continue;
                rect.offsetMin *= ratio;
                rect.offsetMax *= ratio;
            }
            if (rowGroup != null) rowGroup.spacing *= ratio;
            // Padding is int and the slider applies many small ratios while dragging, so rounding would
            // pin it in place; the outer margin stays fixed and only float spacing scales.
            if (panelGroup != null) panelGroup.spacing *= ratio;

            display.textScale = newScale;
            UdonSharpEditorUtility.CopyProxyToUdon(display);
            for (int i = 0; i < touched.Count; i++)
            {
                EditorUtility.SetDirty(touched[i]);
                if (PrefabUtility.IsPartOfPrefabInstance(touched[i]))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(touched[i]);
            }

            RectTransform panelRect = panel == null ? null : panel.GetComponent<RectTransform>();
            if (panelRect != null) LayoutRebuilder.MarkLayoutForRebuild(panelRect);
        }

        private static void AddObject(List<Object> objects, Object item)
        {
            if (item != null && !objects.Contains(item)) objects.Add(item);
        }
    }
}
#endif
