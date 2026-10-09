using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;
using ZLogger;

namespace Scenes
{
    // 결과 패널의 한 줄 — 항목 이름과 값
    public readonly struct ResultRow
    {
        public readonly string Label;
        public readonly string Value;

        /// <summary>
        /// 결과 행의 항목 이름과 값을 정한다.
        /// </summary>
        public ResultRow(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    // 결과 패널의 '항목 | 값' 행 목록 — 템플릿 행을 행 수만큼 복제하고, 행이 많으면 높이를 줄여 컨테이너 안에 모두 넣는다.
    // 재생하면 위에서부터 한 행씩 페이드인한 뒤 값을 타자 효과로 채운다.
    public class ResultRowsView : MonoBehaviour
    {
        [Tooltip("행을 담는 컨테이너 (VerticalLayoutGroup) — 이 높이 안에 모든 행이 들어간다")]
        [SerializeField] private RectTransform container;

        [Tooltip("복제할 행 템플릿 (씬에서는 꺼 둔다)")]
        [SerializeField] private ResultRowView rowTemplate;

        [Tooltip("행 최대 높이 — 행이 적으면 이 높이로, 많으면 컨테이너 높이에 맞게 줄인다")]
        [SerializeField] private float maxRowHeight = 92f;

        [SerializeField] private float rowFadeDuration = 0.4f;

        private readonly List<ResultRowView> _rows = new();
        private IObjectResolver _resolver;
        private ILogger<ResultRowsView> _logger;

        /// <summary>
        /// 행 복제에 쓸 리졸버와 로거를 주입받는다 — 리졸버로 복제해야 행에도 로거가 주입된다.
        /// </summary>
        [Inject]
        public void Construct(IObjectResolver resolver, ILogger<ResultRowsView> logger)
        {
            _resolver = resolver;
            _logger = logger;
        }

        // 4_Result.json의 typewriterCharInterval로 덮어쓴다
        public float CharInterval { get; set; } = 0.06f;

        // 4_Result.json의 rowFadeDuration으로 덮어쓴다
        public float RowFadeDuration
        {
            get => rowFadeDuration;
            set => rowFadeDuration = value;
        }

        /// <summary>
        /// 행을 새로 만들어 채운다. 모든 행은 재생 전까지 투명하다.
        /// </summary>
        public void SetRows(IReadOnlyList<ResultRow> rows)
        {
            if (_resolver == null)
            {
                Debug.LogError("[ResultRowsView] Dependencies were not injected. Check that GameLifetimeScope injects scene root objects on load.");
                return;
            }

            if (!container || !rowTemplate)
            {
                if (_logger != null) _logger.ZLogWarning($"[ResultRowsView] {name}에 container 또는 rowTemplate이 할당되지 않았습니다.");
                return;
            }

            foreach (ResultRowView old in _rows)
                if (old) Destroy(old.gameObject);
            _rows.Clear();
            rowTemplate.gameObject.SetActive(false);

            float rowHeight = ComputeRowHeight(rows.Count);
            foreach (ResultRow row in rows)
            {
                ResultRowView view = _resolver.Instantiate(rowTemplate, container, false);
                // 복제본은 템플릿처럼 꺼진 채 생성되므로, Awake(컴포넌트 캐싱)가 돌도록 먼저 켠다
                view.gameObject.SetActive(true);
                view.Group.alpha = 0f;
                view.Layout.preferredHeight = rowHeight;
                view.Set(row.Label, row.Value);
                _rows.Add(view);
            }
        }

        /// <summary>
        /// 행 수에 맞춰 행 높이를 정한다 — 컨테이너 높이를 넘지 않는 선에서 최대 높이까지.
        /// </summary>
        private float ComputeRowHeight(int count)
        {
            if (count <= 0) return maxRowHeight;

            float spacing = 0f, padding = 0f;
            if (container.TryGetComponent(out VerticalLayoutGroup layout))
            {
                spacing = layout.spacing;
                padding = layout.padding.vertical;
            }

            float fit = (container.rect.height - padding - spacing * (count - 1)) / count;
            return Mathf.Clamp(fit, 0f, maxRowHeight);
        }

        /// <summary>
        /// 위에서부터 한 행씩 페이드인하고 값을 타자 효과로 채운다.
        /// </summary>
        public async UniTask PlayAsync(CancellationToken ct)
        {
            foreach (ResultRowView row in _rows)
            {
                await SceneFader.FadeCanvasGroupAsync(row.Group, 0f, 1f, rowFadeDuration, ct);
                if (!row.ValueText) continue;

                row.ValueText.CharInterval = CharInterval;
                await row.ValueText.PlayAsync(ct);
            }
        }
    }
}
