using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ZLogger;

namespace Scenes
{
    // 결과 패널의 행 하나 — '항목 | 값' 한 줄. ResultRowsView가 템플릿을 복제해 채운다.
    [RequireComponent(typeof(CanvasGroup), typeof(LayoutElement))]
    public class ResultRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TypewriterTextTMP valueText;

        private CanvasGroup _group;
        private LayoutElement _layout;
        private ILogger<ResultRowView> _logger;

        /// <summary>
        /// 로거를 주입받는다 (ResultRowsView가 리졸버로 복제할 때 주입된다).
        /// </summary>
        [Inject]
        public void Construct(ILogger<ResultRowView> logger)
        {
            _logger = logger;
        }

        // 상위 오브젝트가 꺼진 상태에서 복제되면 Awake가 늦게 돌 수 있어, 처음 쓸 때 가져온다
        public CanvasGroup Group
        {
            get
            {
                if (!_group && !TryGetComponent(out _group) && _logger != null)
                    _logger.ZLogError($"[ResultRowView] {name}에 CanvasGroup이 없습니다.");
                return _group;
            }
        }

        public LayoutElement Layout
        {
            get
            {
                if (!_layout && !TryGetComponent(out _layout) && _logger != null)
                    _logger.ZLogError($"[ResultRowView] {name}에 LayoutElement가 없습니다.");
                return _layout;
            }
        }

        public TypewriterTextTMP ValueText => valueText;

        /// <summary>
        /// 항목 이름과 값을 채운다. 값은 재생 전까지 보이지 않는다(타이프라이터).
        /// </summary>
        public void Set(string label, string value)
        {
            if (labelText) labelText.text = label;
            else if (_logger != null) _logger.ZLogWarning($"[ResultRowView] {name}에 labelText가 할당되지 않았습니다.");

            if (valueText) valueText.SetText(value);
            else if (_logger != null) _logger.ZLogWarning($"[ResultRowView] {name}에 valueText가 할당되지 않았습니다.");
        }
    }
}
