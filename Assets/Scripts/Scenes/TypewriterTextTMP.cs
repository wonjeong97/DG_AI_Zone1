using System.Threading;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using VContainer;
using ZLogger;

namespace Scenes
{
    // TMP 타이프라이터 — maxVisibleCharacters로 노출량만 늘려 리치텍스트 태그(<font>/<material> 등)가 잘리지 않음.
    [RequireComponent(typeof(TMP_Text))]
    public class TypewriterTextTMP : MonoBehaviour
    {
        [SerializeField] private float charInterval = 0.03f;

        // 4_Result.json의 typewriterCharInterval로 덮어쓰기 위해 공개. 지정하지 않으면 인스펙터 값을 쓴다.
        public float CharInterval
        {
            get => charInterval;
            set => charInterval = value;
        }

        private TMP_Text _text;
        private ILogger<TypewriterTextTMP> _logger;

        /// <summary>
        /// 로거를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<TypewriterTextTMP> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 텍스트 컴포넌트를 캐싱하고 처음엔 아무 글자도 보이지 않게 한다 (씬 주입은 Awake 뒤라 실패 로그는 Start에서 남긴다).
        /// </summary>
        private void Awake()
        {
            if (TryGetComponent(out _text))
                _text.maxVisibleCharacters = 0;
        }

        /// <summary>
        /// 텍스트 컴포넌트를 찾지 못했으면 로그를 남긴다 — RequireComponent로 보장되지만 실패 시 그 자리에서 드러나게 한다.
        /// </summary>
        private void Start()
        {
            if (!_text && _logger != null)
                _logger.ZLogError($"[TypewriterTextTMP] {name}에 TMP_Text가 없습니다.");
        }

        /// <summary>
        /// 재생 전 표시할 전체 텍스트를 교체한다 (리치텍스트 태그 포함 가능).
        /// </summary>
        public void SetText(string text)
        {
            // 꺼진 채 복제된 오브젝트는 Awake 전에 불릴 수 있다
            if (!_text && !TryGetComponent(out _text))
            {
                if (_logger != null) _logger.ZLogError($"[TypewriterTextTMP] {name}에 TMP_Text가 없어 텍스트를 바꾸지 못했습니다.");
                return;
            }

            _text.text = text;
            _text.maxVisibleCharacters = 0;
            _text.ForceMeshUpdate();
        }

        /// <summary>
        /// 한 글자씩 노출 개수를 늘려 타이핑되듯 보여준다.
        /// </summary>
        public async UniTask PlayAsync(CancellationToken ct = default)
        {
            if (!_text && !TryGetComponent(out _text))
            {
                if (_logger != null) _logger.ZLogError($"[TypewriterTextTMP] {name}에 TMP_Text가 없어 타이핑 연출을 건너뜁니다.");
                return;
            }

            _text.ForceMeshUpdate();
            int total = _text.textInfo.characterCount;
            for (int i = 0; i <= total; i++)
            {
                _text.maxVisibleCharacters = i;
                // 4_Result.json에 음수를 적으면 Delay가 예외를 내 결과 연출이 멈추므로 0 이상으로 제한한다
                await UniTask.Delay((int)(Mathf.Max(0f, charInterval) * 1000), cancellationToken: ct);
            }
        }
    }
}
