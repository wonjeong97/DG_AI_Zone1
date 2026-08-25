using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Scenes
{
    // TMP 텍스트가 한 줄씩 아래에서 위로 올라오며 페이드인되는 연출 공용 유틸
    public static class StoryLineAnimator
    {
        // storyText의 각 줄을 아래에서 위로 올리며 순차적으로 페이드인함. 보이는 문자가 없는 줄(간격용 빈 줄)은
        // 연출과 대기 없이 즉시 통과함. skipRequested가 true를 반환하면 남은 줄까지 즉시 표시하고 종료함.
        public static async UniTask AnimateAsync(TMP_Text text, float lineMoveDuration, float lineInterval, float lineYOffset, Func<bool> skipRequested, CancellationToken token)
        {
            if (text == null) return;

            // 호출부에서 미리 숨겨 둔 경우(maxVisibleCharacters=0)를 대비해 전체 노출로 되돌린 뒤 메쉬를 갱신함
            text.maxVisibleCharacters = int.MaxValue;

            // 본래 RGB는 유지하되 알파 1.0 기준으로 메쉬를 생성함
            Color baseColor = text.color;
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
            text.ForceMeshUpdate();

            TMP_TextInfo textInfo = text.textInfo;
            int totalLines = textInfo.lineCount;
            if (totalLines <= 0) return;

            // 원본 정점 위치 및 색상(불투명 255 기준) 캐싱
            int materialCount = textInfo.meshInfo.Length;
            Vector3[][] cachedVertices = new Vector3[materialCount][];
            Color32[][] cachedColors = new Color32[materialCount][];
            for (int m = 0; m < materialCount; m++)
            {
                Vector3[] verts = textInfo.meshInfo[m].vertices;
                Color32[] colors = textInfo.meshInfo[m].colors32;
                cachedVertices[m] = new Vector3[verts.Length];
                cachedColors[m] = new Color32[colors.Length];
                Array.Copy(verts, cachedVertices[m], verts.Length);
                for (int i = 0; i < colors.Length; i++)
                {
                    Color32 orig = colors[i];
                    cachedColors[m][i] = new Color32(orig.r, orig.g, orig.b, 255);
                }
            }

            // 초기 상태: 모든 버텍스의 알파를 0으로 숨김
            for (int m = 0; m < materialCount; m++)
            {
                Color32[] colors = textInfo.meshInfo[m].colors32;
                for (int i = 0; i < colors.Length; i++) colors[i].a = 0;
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

            bool skipped = false;

            // 라인별로 하나씩 올라오는 연출 진행
            for (int l = 0; l < totalLines && !skipped; l++)
            {
                TMP_LineInfo lineInfo = textInfo.lineInfo[l];

                // 보이는 문자가 없는 줄(간격용 빈 줄)은 연출과 대기 없이 즉시 통과함
                if (!LineHasVisibleChar(textInfo, lineInfo)) continue;

                float elapsed = 0f;
                while (elapsed < lineMoveDuration)
                {
                    if (skipRequested != null && skipRequested()) { skipped = true; break; }

                    float easeT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / lineMoveDuration));
                    float yOffset = Mathf.Lerp(-lineYOffset, 0f, easeT);
                    byte alpha = (byte)Mathf.Lerp(0, 255, easeT);

                    ApplyLineVertices(textInfo, lineInfo, cachedVertices, cachedColors, yOffset, alpha);
                    text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                // 해당 줄을 정위치/불투명으로 확정
                ApplyLineVertices(textInfo, lineInfo, cachedVertices, cachedColors, 0f, 255);
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

                if (!skipped && l < totalLines - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(lineInterval), cancellationToken: token);
                }
            }

            // 완료 또는 스킵 시 전체를 자연 상태(전체 표시/불투명)로 확정함
            if (text != null)
            {
                text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
                text.ForceMeshUpdate();
            }
        }

        // 해당 줄에 렌더링되는(스페이스가 아닌) 문자가 하나라도 있는지 반환함
        private static bool LineHasVisibleChar(TMP_TextInfo textInfo, TMP_LineInfo lineInfo)
        {
            for (int c = lineInfo.firstCharacterIndex; c <= lineInfo.lastCharacterIndex && c < textInfo.characterCount; c++)
            {
                if (textInfo.characterInfo[c].isVisible) return true;
            }
            return false;
        }

        // 한 줄에 속한 문자 정점들에 Y 오프셋과 알파를 적용함(캐싱된 원본 기준)
        private static void ApplyLineVertices(TMP_TextInfo textInfo, TMP_LineInfo lineInfo, Vector3[][] cachedVertices, Color32[][] cachedColors, float yOffset, byte alpha)
        {
            for (int c = lineInfo.firstCharacterIndex; c <= lineInfo.lastCharacterIndex && c < textInfo.characterCount; c++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[c];
                if (!charInfo.isVisible) continue;

                int matIdx = charInfo.materialReferenceIndex;
                int vertexIdx = charInfo.vertexIndex;

                Vector3[] destVerts = textInfo.meshInfo[matIdx].vertices;
                Color32[] destColors = textInfo.meshInfo[matIdx].colors32;

                for (int k = 0; k < 4; k++)
                {
                    Vector3 orig = cachedVertices[matIdx][vertexIdx + k];
                    destVerts[vertexIdx + k] = new Vector3(orig.x, orig.y + yOffset, orig.z);

                    Color32 origColor = cachedColors[matIdx][vertexIdx + k];
                    destColors[vertexIdx + k] = new Color32(origColor.r, origColor.g, origColor.b, alpha);
                }
            }
        }
    }
}
