using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using HuliacDev.Core;

namespace Scenes
{
    // TMP 텍스트가 한 줄씩 아래에서 위로 올라오며 페이드인되는 연출 공용 유틸
    public static class StoryLineAnimator
    {
        /// <summary>
        /// 이번 프레임에 마우스 또는 터치 눌림이 있었는지 반환한다 (연출 스킵용 기본 판정).
        /// AnimateAsync의 skipRequested 인자로 그대로 넘겨 쓴다.
        /// </summary>
        public static bool IsPointerPressedThisFrame()
        {
            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        /// <summary>
        /// text의 각 줄을 아래에서 위로 올리며 순차적으로 페이드인한다. 보이는 문자가 없는 줄(간격용 빈 줄)은
        /// 연출과 대기 없이 즉시 통과하고, skipRequested가 true를 반환하면 남은 줄까지 즉시 표시하고 종료한다.
        /// inactivityTimer를 넘기면 연출이 진행되는 동안 비활동 타이머를 멈춘다 — 입력이 없어도 사용자는
        /// 글을 읽고 있는 구간이라 타임아웃으로 타이틀에 튕기면 안 되며, 스킵이나 취소로 빠져나가도 반드시 재개한다.
        /// </summary>
        public static async UniTask AnimateAsync(TMP_Text text, float lineMoveDuration, float lineInterval, float lineYOffset,
            Func<bool> skipRequested, CancellationToken token, InactivityTimer inactivityTimer = null)
        {
            if (!text)
            {
                Debug.LogWarning("[StoryLineAnimator] 연출할 텍스트가 없어 건너뜁니다.");
                return;
            }

            if (inactivityTimer) inactivityTimer.Pause();
            try
            {
                await AnimateLinesAsync(text, lineMoveDuration, lineInterval, lineYOffset, skipRequested, token);
            }
            finally
            {
                if (inactivityTimer) inactivityTimer.Resume();
            }
        }

        /// <summary>
        /// 줄 단위 정점 캐싱 → 전체 숨김 → 줄마다 올라오며 페이드인 → 전체 확정 순서로 연출한다.
        /// </summary>
        private static async UniTask AnimateLinesAsync(TMP_Text text, float lineMoveDuration, float lineInterval, float lineYOffset, Func<bool> skipRequested, CancellationToken token)
        {

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

                // 정점 단위 보간이라 트윈 대상이 없으므로 DOVirtual로 진행하고, 스킵 입력이 오면 그 자리에서 Kill한다
                // (외부 Kill은 await를 정상 완료시키고, 취소 토큰은 트윈을 멈추며 예외로 빠져나간다).
                // DOVirtual.Float는 값 전달을 내부 OnUpdate로 구현하므로 .OnUpdate()를 따로 붙이면 보간 콜백이 덮어써진다 —
                // 스킵 검사도 반드시 보간 콜백 안에서 한다.
                Tween lineTween = null;
                lineTween = DOVirtual.Float(0f, 1f, lineMoveDuration, t =>
                    {
                        if (skipRequested != null && skipRequested())
                        {
                            skipped = true;
                            lineTween.Kill();
                            return;
                        }

                        float easeT = Mathf.SmoothStep(0f, 1f, t);
                        float yOffset = Mathf.Lerp(-lineYOffset, 0f, easeT);
                        byte alpha = (byte)Mathf.Lerp(0, 255, easeT);

                        ApplyLineVertices(textInfo, lineInfo, cachedVertices, cachedColors, yOffset, alpha);
                        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
                    })
                    .SetEase(Ease.Linear);
                await lineTween.ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);

                // 해당 줄을 정위치/불투명으로 확정
                ApplyLineVertices(textInfo, lineInfo, cachedVertices, cachedColors, 0f, 255);
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

                if (!skipped && l < totalLines - 1)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(lineInterval), cancellationToken: token);
                }
            }

            // 완료 또는 스킵 시 전체를 자연 상태(전체 표시/불투명)로 확정함
            if (text)
            {
                text.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
                text.ForceMeshUpdate();
            }
        }

        /// <summary>
        /// 해당 줄에 렌더링되는(스페이스가 아닌) 문자가 하나라도 있는지 반환한다.
        /// </summary>
        private static bool LineHasVisibleChar(TMP_TextInfo textInfo, TMP_LineInfo lineInfo)
        {
            for (int c = lineInfo.firstCharacterIndex; c <= lineInfo.lastCharacterIndex && c < textInfo.characterCount; c++)
            {
                if (textInfo.characterInfo[c].isVisible) return true;
            }
            return false;
        }

        /// <summary>
        /// 한 줄에 속한 문자 정점들에 Y 오프셋과 알파를 적용한다 (캐싱된 원본 기준).
        /// </summary>
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
