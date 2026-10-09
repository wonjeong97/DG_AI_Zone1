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
        // 인스펙터를 다시 그릴 때마다 프로젝트를 검색하지 않도록 선택할 때 한 번만 모은다
        private LevelData[] _levels;
        private string[] _names;

        /// <summary>
        /// 프로젝트의 LevelData 에셋 목록과 드롭다운 이름을 모은다.
        /// </summary>
        private void OnEnable()
        {
            string[] guids = AssetDatabase.FindAssets("t:LevelData");
            _levels = new LevelData[guids.Length];
            _names = new string[guids.Length + 1];
            _names[0] = "(없음)";

            for (int i = 0; i < guids.Length; i++)
            {
                _levels[i] = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                _names[i + 1] = _levels[i] ? _levels[i].name : "(missing)";
            }
        }

        /// <summary>
        /// testLevel만 드롭다운으로 그리고 나머지 필드는 기본 인스펙터로 그린다.
        /// </summary>
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

        /// <summary>
        /// 모아 둔 LevelData 목록을 드롭다운으로 보여주고 선택값을 testLevel에 반영한다.
        /// </summary>
        private void DrawTestLevelDropdown(SerializedProperty prop)
        {
            int current = 0;
            for (int i = 0; i < _levels.Length; i++)
                if (prop.objectReferenceValue == _levels[i]) current = i + 1;

            int selected = EditorGUILayout.Popup("Test Level", current, _names);
            if (selected != current)
                prop.objectReferenceValue = selected == 0 ? null : _levels[selected - 1];
        }
    }
}
