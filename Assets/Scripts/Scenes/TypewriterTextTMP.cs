using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Scenes
{
    // TMP 버전 타이프라이터 — maxVisibleCharacters로 노출량만 늘려 리치텍스트 태그(<font>/<material> 등)가 잘리지 않음.
    // 레거시 Text용 TypewriterText와 API(SetText/PlayAsync)를 맞춰 사용처를 최소 변경으로 대체.
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

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _text.maxVisibleCharacters = 0;
        }

        // 재생 전 표시할 전체 텍스트 교체 (리치텍스트 태그 포함 가능)
        public void SetText(string text)
        {
            _text.text = text;
            _text.maxVisibleCharacters = 0;
            _text.ForceMeshUpdate();
        }

        public async UniTask PlayAsync(CancellationToken ct = default)
        {
            _text.ForceMeshUpdate();
            int total = _text.textInfo.characterCount;
            for (int i = 0; i <= total; i++)
            {
                _text.maxVisibleCharacters = i;
                await UniTask.Delay((int)(charInterval * 1000), cancellationToken: ct);
            }
        }
    }
}
