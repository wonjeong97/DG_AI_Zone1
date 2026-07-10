#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using DG.Scenes;

namespace DG.Editor
{
    public class TextToTMProConverter : EditorWindow
    {
        [MenuItem("Tools/Rebind TMP References Only")]
        public static void RebindOnlyActiveScene()
        {
            // 1. 타이프라이터 누락 복구
            var tmps = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            foreach (var tmp in tmps)
            {
                if (!tmp.gameObject.scene.isLoaded) continue;
                GameObject go = tmp.gameObject;

                // 레거시 TypewriterText가 아직 있다면 삭제 후 신규 추가
                var legacy = go.GetComponent<TypewriterText>();
                if (legacy != null)
                {
                    Undo.DestroyObjectImmediate(legacy);
                    Undo.AddComponent<TypewriterTextTMP>(go);
                    Debug.Log($"[Typewriter Fix] 레거시를 지우고 TypewriterTextTMP를 추가했습니다: {go.name}");
                }
                
                // Result_Player 또는 Result_AI 하위의 'Text (Legacy)'에 타이프라이터가 누락된 경우 추가
                if (go.name.Contains("Text (Legacy)") && go.GetComponent<TypewriterTextTMP>() == null)
                {
                    if (go.transform.parent != null && (go.transform.parent.name.Contains("Player") || go.transform.parent.name.Contains("AI")))
                    {
                        Undo.AddComponent<TypewriterTextTMP>(go);
                        Debug.Log($"[Typewriter Fix] 누락된 TypewriterTextTMP를 추가했습니다: {go.transform.parent.name}/{go.name}");
                    }
                }
            }

            // 2. 컴포넌트 Missing 레퍼런스 자동 복구 시도
            RebindManagerReferences(null);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("레퍼런스 복구 및 마이그레이션 확인이 완료되었습니다. 씬을 저장해 주세요.");
        }

        [MenuItem("Tools/Convert UI Text to TMPro")]
        public static void ConvertAllInActiveScene()
        {
            // 기본/폴백 폰트 로드
            TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/GamtanRoadTantan SDF");
            if (defaultFont == null)
            {
                Debug.LogError("기본 GamtanRoadTantan SDF Font Asset을 찾을 수 없습니다. Resources/Fonts & Materials/ 경로를 확인하세요.");
                return;
            }

            // 1. 현재 활성화된 씬의 모든 Text 검색 (비활성화 포함)
            Text[] texts = Resources.FindObjectsOfTypeAll<Text>();
            List<Text> sceneTexts = new List<Text>();
            foreach (var t in texts)
            {
                if (t.gameObject.scene.isLoaded)
                {
                    sceneTexts.Add(t);
                }
            }

            Debug.Log($"씬 내에서 {sceneTexts.Count}개의 레거시 Text 컴포넌트를 발견했습니다. 변환을 시작합니다.");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Convert Text to TMPro");
            int groupIndex = Undo.GetCurrentGroup();

            Dictionary<GameObject, TextData> convertedData = new Dictionary<GameObject, TextData>();

            foreach (var textComp in sceneTexts)
            {
                GameObject go = textComp.gameObject;
                
                // 기존 폰트에 어울리는 TMP 폰트 에셋 결정
                TMP_FontAsset matchedFont = defaultFont;
                if (textComp.font != null)
                {
                    string oldFontName = textComp.font.name.ToLower();
                    if (oldFontName.Contains("neodgm"))
                    {
                        // neodgm용 SDF 로드 시도
                        TMP_FontAsset neoFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/neodgm SDF");
                        if (neoFont != null) matchedFont = neoFont;
                        else Debug.LogWarning($"[Font Fallback] {go.name}의 neodgm 폰트용 'neodgm SDF' 에셋이 없어 기본 폰트로 대체합니다.");
                    }
                    else if (oldFontName.Contains("dunggeunmo"))
                    {
                        // DungGeunMo용 SDF 로드 시도
                        TMP_FontAsset dgFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/DungGeunMo SDF");
                        if (dgFont != null) matchedFont = dgFont;
                        else Debug.LogWarning($"[Font Fallback] {go.name}의 DungGeunMo 폰트용 'DungGeunMo SDF' 에셋이 없어 기본 폰트로 대체합니다.");
                    }
                }

                // 데이터 백업
                TextData data = new TextData
                {
                    text = textComp.text,
                    fontSize = textComp.fontSize,
                    color = textComp.color,
                    alignment = ConvertAlignment(textComp.alignment),
                    lineSpacing = textComp.lineSpacing,
                    richText = textComp.supportRichText
                };

                convertedData[go] = data;

                // 타이프라이터 스크립트 마이그레이션 지원 (의존성이 있으므로 Text 컴포넌트보다 먼저 삭제해야 함)
                TypewriterText legacyTypewriter = go.GetComponent<TypewriterText>();
                if (legacyTypewriter != null)
                {
                    Undo.DestroyObjectImmediate(legacyTypewriter);
                }

                // 기존 Text 삭제 (Undo 등록)
                Undo.DestroyObjectImmediate(textComp);

                // TextMeshProUGUI 추가 (Undo 등록)
                TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);
                tmp.text = data.text;
                tmp.font = matchedFont;
                tmp.fontSize = data.fontSize;
                tmp.color = data.color;
                tmp.alignment = data.alignment;
                tmp.lineSpacing = (data.lineSpacing - 1) * 100;
                tmp.richText = data.richText;

                // 새 타이프라이터 추가
                if (legacyTypewriter != null)
                {
                    Undo.AddComponent<TypewriterTextTMP>(go);
                }

                Debug.Log($"변환 완료: {go.name}");
            }

            // 2. 컴포넌트 Missing 레퍼런스 자동 복구 시도
            RebindManagerReferences(convertedData);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(groupIndex);
            
            Debug.Log("마이그레이션이 완료되었습니다. 씬을 저장해 주세요.");
        }

        private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
        {
            return anchor switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
        }

        private static void RebindManagerReferences(Dictionary<GameObject, TextData> convertedData)
        {
            // GameSceneManager 복구
            var gameManagers = Resources.FindObjectsOfTypeAll<DG.Scenes.GameSceneManager>();
            foreach (var mgr in gameManagers)
            {
                if (!mgr.gameObject.scene.isLoaded) continue;
                
                SerializedObject so = new SerializedObject(mgr);
                SerializedProperty prop = so.FindProperty("questionText");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    foreach (var go in convertedData.Keys)
                    {
                        if (go.name.Contains("Question") || go.name.Contains("question"))
                        {
                            prop.objectReferenceValue = go.GetComponent<TextMeshProUGUI>();
                            so.ApplyModifiedProperties();
                            Debug.Log($"[Rebind] GameSceneManager의 questionText 참조를 {go.name}으로 자동 복구했습니다.");
                            break;
                        }
                    }
                }
            }

            // ResultSequence 복구
            var resultSequences = Resources.FindObjectsOfTypeAll<DG.Scenes.ResultSequence>();
            foreach (var seq in resultSequences)
            {
                if (!seq.gameObject.scene.isLoaded) continue;

                SerializedObject so = new SerializedObject(seq);
                
                // playerText (TypewriterTextTMP)
                RebindProperty(so, "playerText", typeof(TypewriterTextTMP), "Player");
                // aiText (TypewriterTextTMP)
                RebindProperty(so, "aiText", typeof(TypewriterTextTMP), "Ai");
                
                // playerEffText (TextMeshProUGUI) — playerEffGroup오브젝트에 같이 붙어 있음
                SerializedProperty pEffGroup = so.FindProperty("playerEffGroup");
                SerializedProperty pEffText = so.FindProperty("playerEffText");
                if (pEffGroup != null && pEffGroup.objectReferenceValue != null && pEffText != null && pEffText.objectReferenceValue == null)
                {
                    CanvasGroup group = pEffGroup.objectReferenceValue as CanvasGroup;
                    if (group != null)
                    {
                        pEffText.objectReferenceValue = group.GetComponent<TextMeshProUGUI>();
                        so.ApplyModifiedProperties();
                        Debug.Log("[Rebind] ResultSequence의 playerEffText 참조를 EnergyEfficiency 오브젝트에서 복구했습니다.");
                    }
                }

                // aiEffText (TextMeshProUGUI) — aiEffGroup오브젝트에 같이 붙어 있음
                SerializedProperty aEffGroup = so.FindProperty("aiEffGroup");
                SerializedProperty aEffText = so.FindProperty("aiEffText");
                if (aEffGroup != null && aEffGroup.objectReferenceValue != null && aEffText != null && aEffText.objectReferenceValue == null)
                {
                    CanvasGroup group = aEffGroup.objectReferenceValue as CanvasGroup;
                    if (group != null)
                    {
                        aEffText.objectReferenceValue = group.GetComponent<TextMeshProUGUI>();
                        so.ApplyModifiedProperties();
                        Debug.Log("[Rebind] ResultSequence의 aiEffText 참조를 EnergyEfficiency 오브젝트에서 복구했습니다.");
                    }
                }
            }
        }

        private static void RebindProperty(SerializedObject so, string propName, System.Type compType, string nameKeyword)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null && prop.objectReferenceValue == null)
            {
                var candidates = Resources.FindObjectsOfTypeAll(compType);
                foreach (var cand in candidates)
                {
                    Component comp = cand as Component;
                    if (comp != null && comp.gameObject.scene.isLoaded)
                    {
                        bool nameMatches = comp.name.ToLower().Contains(nameKeyword.ToLower());
                        bool parentMatches = comp.transform.parent != null && comp.transform.parent.name.ToLower().Contains(nameKeyword.ToLower());
                        if (nameMatches || parentMatches)
                        {
                            prop.objectReferenceValue = comp;
                            so.ApplyModifiedProperties();
                            Debug.Log($"[Rebind] {so.targetObject.name}의 {propName} 참조를 {comp.name} 컴포넌트로 자동 복구했습니다.");
                            break;
                        }
                    }
                }
            }
        }

        private struct TextData
        {
            public string text;
            public int fontSize;
            public Color color;
            public TextAlignmentOptions alignment;
            public float lineSpacing;
            public bool richText;
        }
    }
}
#endif
