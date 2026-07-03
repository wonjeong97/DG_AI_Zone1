using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace DG.Scenes
{
    [RequireComponent(typeof(Text))]
    public class TypewriterText : MonoBehaviour
    {
        [SerializeField] private float charInterval = 0.03f;

        private Text _text;
        private string _fullText;

        private void Awake()
        {
            _text = GetComponent<Text>();
            _fullText = _text.text;
            _text.text = "";
        }

        // 재생 전 표시할 전체 텍스트 교체 (씬 기본 텍스트 대체용)
        public void SetText(string text) => _fullText = text;

        public async UniTask PlayAsync(CancellationToken ct = default)
        {
            for (int i = 0; i <= _fullText.Length; i++)
            {
                _text.text = _fullText.Substring(0, i);
                await UniTask.Delay((int)(charInterval * 1000), cancellationToken: ct);
            }
        }
    }
}
