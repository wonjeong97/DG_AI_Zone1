using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using HuliacDev.UI;
using ZLogger;

namespace Admin
{
    // 체험자 이름 입력 창 — 키보드가 없는 전시 환경이라 화면 두벌식 키보드(맨 위 숫자열 포함)로 입력받는다.
    // 키 입력을 HangulComposer로 조합해 입력란에 보여 주고, 저장을 누르면 Open에 넘긴 콜백으로 이름을 돌려준다.
    public class VisitorNamePanel : MonoBehaviour
    {
        [Tooltip("키 버튼들의 부모 — 버튼 이름(Button_q, Button_1, Button_shift 등)으로 각 키의 역할을 찾는다")]
        [SerializeField] private Transform keyboardRoot;
        [Tooltip("입력한 이름을 보여 주는 입력란 — 화면 키보드로만 입력받으므로 직접 터치는 막는다")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button closeButton;
        [Tooltip("Shift가 켜져 있는 동안 Shift 키 배경색")]
        [SerializeField] private Color shiftPressedColor = new(0.55f, 0.7f, 0.95f, 1f);

        private const string ShiftKey    = "Button_shift";
        private const string DeleteKey   = "Button_delete";
        private const string LanguageKey = "Button_lang";
        private const string SpaceKey    = "Button_Space";

        // 지우기 키를 이 시간 이상 누르고 있으면 한 글자 대신 전체를 지운다
        private const float DeleteHoldSeconds = 1.5f;

        private readonly static Dictionary<string, char> BaseMap = new()
        {
            { "Button_q", 'ㅂ' }, { "Button_w", 'ㅈ' }, { "Button_e", 'ㄷ' }, { "Button_r", 'ㄱ' }, { "Button_t", 'ㅅ' },
            { "Button_y", 'ㅛ' }, { "Button_u", 'ㅕ' }, { "Button_i", 'ㅑ' }, { "Button_o", 'ㅐ' }, { "Button_p", 'ㅔ' },
            { "Button_a", 'ㅁ' }, { "Button_s", 'ㄴ' }, { "Button_d", 'ㅇ' }, { "Button_f", 'ㄹ' }, { "Button_g", 'ㅎ' },
            { "Button_h", 'ㅗ' }, { "Button_j", 'ㅓ' }, { "Button_k", 'ㅏ' }, { "Button_l", 'ㅣ' },
            { "Button_z", 'ㅋ' }, { "Button_x", 'ㅌ' }, { "Button_c", 'ㅊ' }, { "Button_v", 'ㅍ' },
            { "Button_b", 'ㅠ' }, { "Button_n", 'ㅜ' }, { "Button_m", 'ㅡ' }
        };

        // 두벌식에서 Shift로 겹자음/겹모음이 되는 키만 담는다. 나머지 키는 Shift가 눌려도 그대로다
        private readonly static Dictionary<string, char> ShiftMap = new()
        {
            { "Button_q", 'ㅃ' }, { "Button_w", 'ㅉ' }, { "Button_e", 'ㄸ' }, { "Button_r", 'ㄲ' }, { "Button_t", 'ㅆ' },
            { "Button_o", 'ㅒ' }, { "Button_p", 'ㅖ' }
        };

        // 맨 위 숫자열 — 한/영·Shift와 상관없이 숫자를 그대로 입력한다
        private readonly static Dictionary<string, char> NumberMap = new()
        {
            { "Button_1", '1' }, { "Button_2", '2' }, { "Button_3", '3' }, { "Button_4", '4' }, { "Button_5", '5' },
            { "Button_6", '6' }, { "Button_7", '7' }, { "Button_8", '8' }, { "Button_9", '9' }, { "Button_0", '0' }
        };

        // Shift 상태 — 한글은 Off/Once만 쓰고(글자 입력 시 자동 해제), 영어는 Off → Once → Lock 순으로 순환한다
        private enum ShiftState
        {
            Off,
            Once,
            Lock
        }

        // 키에 보이는 글자 문자열 캐시 — 자판·Shift를 바꿀 때마다 키 26개의 문자열을 새로 만들지 않는다.
        // 같은 문자열 인스턴스를 넣으면 TMP도 텍스트가 같다고 보고 메시를 다시 만들지 않는다
        private readonly static Dictionary<char, string> LabelStrings = new();

        private readonly HangulComposer _composer = new();
        private readonly IdleCloseTimer _idleTimer = new();

        // 이 시간(초) 동안 입력이 없으면 저장하지 않고 창을 닫는다 — 관리자 화면과 같은 값(Admin.json idleCloseSeconds)을 열 때 받는다
        private float _idleCloseSeconds = Constants.Admin.DefaultIdleCloseSeconds;
        private readonly Dictionary<string, TMP_Text> _keyLabels = new();
        private Image _shiftButtonImage;
        private Color _shiftNormalColor;
        private ShiftState _shiftState = ShiftState.Off;
        private bool _isEnglish;

        // 지우기 키를 누른 시각 — 음수면 누르고 있지 않은 상태
        private float _deletePressTime = -1f;
        private bool _deleteHoldTriggered;

        private Action<string> _onSaved;

        private ILogger<VisitorNamePanel> _logger;
        private SoundManager _soundManager;

        /// <summary>
        /// 로거와 사운드 매니저를 주입받는다.
        /// </summary>
        [Inject]
        public void Construct(ILogger<VisitorNamePanel> logger, SoundManager soundManager)
        {
            _logger = logger;
            _soundManager = soundManager;
        }

        /// <summary>
        /// 키와 저장·닫기 버튼에 동작을 연결한다 (패널이 처음 켜질 때 한 번 실행).
        /// </summary>
        private void Awake()
        {
            if (!keyboardRoot || !inputField)
            {
                if (_logger != null) _logger.ZLogWarning($"[VisitorNamePanel] keyboardRoot 또는 inputField가 할당되지 않아 키보드를 쓸 수 없습니다.");
            }
            else
            {
                BindKeys();
                BlockInputFieldPointerInput();
            }

            if (saveButton) saveButton.onClick.AddListener(OnSaveClicked);
            else if (_logger != null) _logger.ZLogWarning($"[VisitorNamePanel] saveButton이 할당되지 않아 이름을 저장할 수 없습니다.");

            if (closeButton) closeButton.onClick.AddListener(OnCloseClicked);
            else if (_logger != null) _logger.ZLogWarning($"[VisitorNamePanel] closeButton이 할당되지 않았습니다.");
        }

        /// <summary>
        /// 저장·닫기 버튼 연결을 해제한다 (키 버튼은 이 패널의 자식이라 함께 파괴된다).
        /// </summary>
        private void OnDestroy()
        {
            if (saveButton) saveButton.onClick.RemoveListener(OnSaveClicked);
            if (closeButton) closeButton.onClick.RemoveListener(OnCloseClicked);
        }

        /// <summary>
        /// 입력을 비운 한글 자판으로 창을 연다.
        /// </summary>
        public void Open(Action<string> onSaved, float idleCloseSeconds)
        {
            // 저장하면 입력한 이름으로 onSaved를 부르고, idleCloseSeconds 동안 입력이 없으면 닫는다.
            _onSaved = onSaved;
            _idleCloseSeconds = idleCloseSeconds;
            _isEnglish = false;
            gameObject.SetActive(true);
            _idleTimer.Restart();
            ClearInput();
        }

        /// <summary>
        /// 창을 닫는다.
        /// </summary>
        public void Close()
        {
            _onSaved = null;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 입력 없이 정해진 시간이 지나면 저장하지 않고 창을 닫고, 지우기 키를 정해진 시간 이상 누르고 있으면 입력 전체를 지운다
        /// (창이 열려 있을 때만 실행됨).
        /// </summary>
        private void Update()
        {
            if (_idleTimer.HasExpired(_idleCloseSeconds))
            {
                if (_logger != null) _logger.ZLogInformation($"[VisitorNamePanel] {_idleCloseSeconds}초 동안 입력이 없어 이름 입력 창을 닫습니다.");
                Close();
                return;
            }

            if (_deletePressTime < 0f || Time.unscaledTime - _deletePressTime < DeleteHoldSeconds) return;

            _deletePressTime = -1f;
            _deleteHoldTriggered = true;
            ClearInput();
        }

        /// <summary>
        /// 지우기 키를 누른 채 창이 닫혀도 다음에 열 때 길게 누르기가 남지 않게 한다.
        /// </summary>
        private void OnDisable()
        {
            _deletePressTime = -1f;
            _deleteHoldTriggered = false;
        }

        /// <summary>
        /// 화면 키보드로만 입력받아야 하므로 입력란을 직접 터치해 커서를 옮기거나 글자를 선택하지 못하게 막는다.
        /// </summary>
        private void BlockInputFieldPointerInput()
        {
            // 표시는 그대로 두기 위해 interactable 대신 raycastTarget만 끈다.
            List<Graphic> graphics = new List<Graphic>();
            CollectInHierarchy(inputField.transform, graphics);
            foreach (Graphic graphic in graphics)
                graphic.raycastTarget = false;
        }

        /// <summary>
        /// 부모 아래 계층을 직계 자식부터 차례로 내려가며 지정 컴포넌트를 모은다 (꺼진 오브젝트 포함).
        /// </summary>
        private static void CollectInHierarchy<T>(Transform parent, List<T> into) where T : Component
        {
            // 화면 키보드는 줄·키 묶음으로 깊이가 섞여 있고 키마다 이름으로 역할이 정해져 있어,
            // 인스펙터에 수십 개 키를 하나씩 연결하는 대신 계층을 직접 순회해 이름으로 고른다 — 빠진 키는 BindKeys가 경고한다.
            if (parent.TryGetComponent(out T own)) into.Add(own);
            foreach (Transform child in parent)
                CollectInHierarchy(child, into);
        }

        /// <summary>
        /// keyboardRoot 아래 버튼을 이름으로 찾아 각 역할에 맞는 동작을 연결하고, 찾지 못한 키를 경고한다.
        /// </summary>
        private void BindKeys()
        {
            List<Button> buttons = new List<Button>();
            CollectInHierarchy(keyboardRoot, buttons);

            HashSet<string> boundKeys = new HashSet<string>();
            foreach (Button button in buttons)
            {
                string keyName = button.name;
                if (BindKey(button, keyName)) boundKeys.Add(keyName);
            }

            WarnMissingKeys(boundKeys);
        }

        /// <summary>
        /// 키 버튼 하나에 이름에 맞는 동작을 연결하고, 알 수 없는 이름이면 false를 돌려준다.
        /// </summary>
        private bool BindKey(Button button, string keyName)
        {
            if (BaseMap.ContainsKey(keyName))
            {
                button.onClick.AddListener(() => OnLetterPressed(keyName));

                TMP_Text label = FindLabel(button.transform);
                if (label) _keyLabels[keyName] = label;
                else if (_logger != null) _logger.ZLogWarning($"[VisitorNamePanel] {keyName} 키에 글자 TMP가 없어 자판을 바꿔도 글자가 바뀌지 않습니다.");
            }
            else if (NumberMap.TryGetValue(keyName, out char number))
            {
                button.onClick.AddListener(() => OnRawPressed(number));
            }
            else if (keyName == ShiftKey)
            {
                button.onClick.AddListener(OnShiftPressed);

                _shiftButtonImage = button.image;
                if (_shiftButtonImage) _shiftNormalColor = _shiftButtonImage.color;
                else if (_logger != null) _logger.ZLogWarning($"[VisitorNamePanel] Shift 키에 Image가 없어 눌림 표시를 바꿀 수 없습니다.");
            }
            else if (keyName == DeleteKey)
            {
                BindDeleteHold(button);
                return true;
            }
            else if (keyName == LanguageKey)
            {
                button.onClick.AddListener(OnLangPressed);
            }
            else if (keyName == SpaceKey)
            {
                button.onClick.AddListener(() => OnRawPressed(' '));
            }
            else
            {
                return false;
            }

            button.onClick.AddListener(OnKeyClicked);
            return true;
        }

        /// <summary>
        /// 키 버튼 아래에서 글자를 보여 주는 TMP를 찾는다 (없으면 null).
        /// </summary>
        private static TMP_Text FindLabel(Transform keyButton)
        {
            List<TMP_Text> labels = new List<TMP_Text>();
            CollectInHierarchy(keyButton, labels);
            return labels.Count > 0 ? labels[0] : null;
        }

        /// <summary>
        /// 화면 키보드에 있어야 하는 키 중 찾지 못한 것을 경고한다 — 키 이름을 바꾸면 그 키가 조용히 동작하지 않기 때문이다.
        /// </summary>
        private void WarnMissingKeys(HashSet<string> boundKeys)
        {
            if (_logger == null) return;

            foreach (string key in BaseMap.Keys)
                if (!boundKeys.Contains(key)) _logger.ZLogWarning($"[VisitorNamePanel] 화면 키보드에 {key} 키가 없습니다.");
            foreach (string key in NumberMap.Keys)
                if (!boundKeys.Contains(key)) _logger.ZLogWarning($"[VisitorNamePanel] 화면 키보드에 {key} 키가 없습니다.");
            foreach (string key in new[] { ShiftKey, DeleteKey, LanguageKey, SpaceKey })
                if (!boundKeys.Contains(key)) _logger.ZLogWarning($"[VisitorNamePanel] 화면 키보드에 {key} 키가 없습니다.");
        }

        /// <summary>
        /// 키 효과음을 내고, 클릭 뒤에도 남는 EventSystem 선택을 풀어 버튼이 눌린 색으로 굳지 않게 한다.
        /// </summary>
        private void OnKeyClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            ClearSelection();
        }

        /// <summary>
        /// EventSystem의 선택 상태를 푼다.
        /// </summary>
        private static void ClearSelection()
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>
        /// 지우기 키에 누름/뗌 이벤트를 연결한다.
        /// </summary>
        private void BindDeleteHold(Button button)
        {
            // 짧게 누르면 한 단계만 지우고, 길게 누르면 전체를 지운다.
            if (!button.TryGetComponent(out EventTrigger trigger))
                trigger = button.gameObject.AddComponent<EventTrigger>();

            AddTriggerEntry(trigger, EventTriggerType.PointerDown, OnDeletePointerDown);
            AddTriggerEntry(trigger, EventTriggerType.PointerUp, OnDeletePointerUp);
        }

        /// <summary>
        /// EventTrigger에 지정한 종류의 이벤트 항목을 추가한다.
        /// </summary>
        private static void AddTriggerEntry(EventTrigger trigger, EventTriggerType type, UnityAction action)
        {
            EventTrigger.Entry entry = new() { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        /// <summary>
        /// 지우기 키를 누른 순간부터 길게 누르기 시간을 잰다.
        /// </summary>
        private void OnDeletePointerDown()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            _deleteHoldTriggered = false;
            _deletePressTime = Time.unscaledTime;
        }

        /// <summary>
        /// 지우기 키에서 손을 떼면 시간 재기를 멈추고, 전체 삭제가 일어나지 않았으면 한 단계만 지운다.
        /// </summary>
        private void OnDeletePointerUp()
        {
            _deletePressTime = -1f;
            if (!_deleteHoldTriggered)
            {
                _composer.Backspace();
                RefreshInputField();
            }

            ClearSelection();
        }

        /// <summary>
        /// 글자 키 입력을 지금 자판(한글/영어)과 Shift 상태에 맞는 문자로 바꿔 컴포저에 넣는다.
        /// </summary>
        private void OnLetterPressed(string keyName)
        {
            bool shiftActive = _shiftState != ShiftState.Off;
            bool accepted;

            if (_isEnglish)
            {
                char english = EnglishCharOf(keyName);
                char c = shiftActive ? char.ToUpperInvariant(english) : english;
                accepted = _composer.TryAppendRaw(c, Constants.Admin.VisitorNameMaxLength);
            }
            else
            {
                char c = shiftActive && ShiftMap.TryGetValue(keyName, out char shifted) ? shifted : BaseMap[keyName];
                accepted = _composer.TryPush(c, Constants.Admin.VisitorNameMaxLength);
            }

            // 최대 글자 수를 넘겨 입력이 무시된 경우엔 화면과 Shift 상태를 그대로 둔다
            if (!accepted) return;

            RefreshInputField();
            ReleaseOneShotShift();
        }

        /// <summary>
        /// 숫자·띄어쓰기처럼 조합하지 않는 문자를 그대로 붙인다 (조합 중이던 음절은 먼저 확정된다).
        /// </summary>
        private void OnRawPressed(char c)
        {
            if (!_composer.TryAppendRaw(c, Constants.Admin.VisitorNameMaxLength)) return;

            RefreshInputField();
            ReleaseOneShotShift();
        }

        /// <summary>
        /// Shift를 누를 때마다 상태를 바꾼다.
        /// </summary>
        private void OnShiftPressed()
        {
            // 한글은 Off/Once만, 영어는 Off → Once(1회 대문자) → Lock(대문자 고정) → Off 순이다.
            if (_isEnglish)
            {
                _shiftState = _shiftState switch
                {
                    ShiftState.Off => ShiftState.Once,
                    ShiftState.Once => ShiftState.Lock,
                    _ => ShiftState.Off
                };
            }
            else
            {
                _shiftState = _shiftState == ShiftState.Off ? ShiftState.Once : ShiftState.Off;
            }

            RefreshShiftVisual();
            RefreshKeyLabels();
        }

        /// <summary>
        /// Once 상태였던 Shift는 글자 입력 한 번으로 풀리고, Lock은 유지된다.
        /// </summary>
        private void ReleaseOneShotShift()
        {
            if (_shiftState != ShiftState.Once) return;

            _shiftState = ShiftState.Off;
            RefreshShiftVisual();
            RefreshKeyLabels();
        }

        /// <summary>
        /// 한글과 영어 자판을 번갈아 바꾸고 Shift를 끈다.
        /// </summary>
        private void OnLangPressed()
        {
            _isEnglish = !_isEnglish;
            _shiftState = ShiftState.Off;
            RefreshShiftVisual();
            RefreshKeyLabels();
        }

        /// <summary>
        /// 버튼 이름(Button_q 등)의 마지막 글자를 영문 자판 문자로 쓴다 — 버튼 이름이 곧 영문 자판 위치다.
        /// </summary>
        private static char EnglishCharOf(string keyName)
        {
            return keyName[keyName.Length - 1];
        }

        /// <summary>
        /// 자판과 Shift 상태에 맞춰 각 글자 키에 보이는 문자를 다시 쓴다.
        /// </summary>
        private void RefreshKeyLabels()
        {
            bool shiftActive = _shiftState != ShiftState.Off;

            foreach (KeyValuePair<string, TMP_Text> pair in _keyLabels)
            {
                char label;
                if (_isEnglish)
                {
                    char english = EnglishCharOf(pair.Key);
                    label = shiftActive ? char.ToUpperInvariant(english) : english;
                }
                else
                {
                    label = shiftActive && ShiftMap.TryGetValue(pair.Key, out char shifted) ? shifted : BaseMap[pair.Key];
                }

                pair.Value.text = LabelOf(label);
            }
        }

        /// <summary>
        /// 글자 하나짜리 문자열을 캐시에서 꺼내 준다 (처음 쓰는 글자만 만든다).
        /// </summary>
        private static string LabelOf(char c)
        {
            if (!LabelStrings.TryGetValue(c, out string text))
            {
                text = c.ToString();
                LabelStrings[c] = text;
            }
            return text;
        }

        /// <summary>
        /// Shift 키 배경색을 눌림/기본 상태에 맞게 바꾼다.
        /// </summary>
        private void RefreshShiftVisual()
        {
            // 키를 누를 때마다 도는 자리라, 참조가 없다는 경고는 BindKeys에서 한 번만 남긴다
            if (!_shiftButtonImage) return;

            _shiftButtonImage.color = _shiftState != ShiftState.Off ? shiftPressedColor : _shiftNormalColor;
        }

        /// <summary>
        /// 조합 상태와 입력란을 모두 비우고 Shift를 끈다.
        /// </summary>
        private void ClearInput()
        {
            _composer.Clear();
            _shiftState = ShiftState.Off;
            RefreshShiftVisual();
            RefreshKeyLabels();
            RefreshInputField();
        }

        /// <summary>
        /// 컴포저의 현재 문자열을 입력란에 반영하고, 저장 버튼을 누를 수 있는지 갱신한다.
        /// </summary>
        private void RefreshInputField()
        {
            string text = _composer.Text;
            if (inputField) inputField.SetTextWithoutNotify(text);
            if (saveButton) saveButton.interactable = IsSavableName(text);
        }

        /// <summary>
        /// 저장할 수 있는 이름인지 본다.
        /// </summary>
        public static bool IsSavableName(string text)
        {
            // 비었거나 첫 글자·끝 글자가 띄어쓰기면 문장 안에서 어긋난 여백으로 보이므로 막는다
            // (띄어쓰기만 넣은 이름도 첫 글자가 띄어쓰기라 함께 막힌다).
            if (string.IsNullOrEmpty(text)) return false;

            return !char.IsWhiteSpace(text[0]) && !char.IsWhiteSpace(text[text.Length - 1]);
        }

        /// <summary>
        /// 입력한 이름을 확정해 콜백으로 넘기고 창을 닫는다.
        /// </summary>
        private void OnSaveClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);

            string visitorName = _composer.Text;
            if (!IsSavableName(visitorName)) return;

            Action<string> onSaved = _onSaved;
            Close();
            onSaved?.Invoke(visitorName);
        }

        /// <summary>
        /// 닫기 효과음을 내고 저장하지 않은 채 창을 닫는다.
        /// </summary>
        private void OnCloseClicked()
        {
            if (_soundManager) _soundManager.PlaySFX(Constants.Sounds.ButtonClick);
            Close();
        }
    }
}
