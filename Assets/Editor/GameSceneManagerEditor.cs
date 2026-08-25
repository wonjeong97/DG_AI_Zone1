using Data;
using Scenes;
using UnityEditor;

namespace Editor
{
    // GameSceneManager 인스펙터 — testLevel을 에셋 드래그 대신
    // 프로젝트 내 LevelData 목록 드랍다운으로 선택하게 한다.
    [CustomEditor(typeof(GameSceneManager))]
    public class GameSceneManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "testLevel")
                {
                    DrawTestLevelDropdown(prop);
                }
                else
                {
                    using (new EditorGUI.DisabledScope(prop.name == "m_Script"))
                        EditorGUILayout.PropertyField(prop, true);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawTestLevelDropdown(SerializedProperty prop)
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData");
            LevelData[] levels = new LevelData[guids.Length];
            string[] names = new string[guids.Length + 1];
            names[0] = "(없음)";

            int current = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                levels[i] = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                names[i + 1] = levels[i] ? levels[i].name : "(missing)";
                if (prop.objectReferenceValue == levels[i]) current = i + 1;
            }

            int selected = EditorGUILayout.Popup("Test Level", current, names);
            if (selected != current)
                prop.objectReferenceValue = selected == 0 ? null : levels[selected - 1];
        }
    }
}
