using System;
using System.Collections.Generic;
using System.Text;

namespace Admin
{
    // 두벌식 자모 입력을 완성형 한글 음절로 실시간 조합한다.
    // 초성/중성/종성을 상태로 들고 있다가, 겹모음/겹받침 결합과 종성의 다음 음절 초성 이동(값 → 갑사)까지 처리한다.
    public sealed class HangulComposer
    {
        // RenderActiveChar가 조합 중인 음절이 없음을 알리는 값
        private const char NoActiveSyllable = '\0';

        private const int SyllableBase = 0xAC00;
        private const int JungCount = 21;
        private const int JongCount = 28;

        private readonly static char[] Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ".ToCharArray();
        private readonly static char[] Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ".ToCharArray();
        private readonly static char[] Jong = "\0ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ".ToCharArray();

        // 초성 index → 같은 자음의 단순 종성 index. ㄸ/ㅃ/ㅉ는 종성이 없어 -1
        private readonly static int[] ChoToSimpleJong =
        {
            1, 2, 4, 7, -1, 8, 16, 17, -1, 19, 20, 21, 22, -1, 23, 24, 25, 26, 27
        };

        // (기존 종성 index, 추가된 자음) → 겹받침 index
        private readonly static Dictionary<(int jong, char add), int> JongCombine = new()
        {
            { (1, 'ㅅ'), 3 }, { (4, 'ㅈ'), 5 }, { (4, 'ㅎ'), 6 },
            { (8, 'ㄱ'), 9 }, { (8, 'ㅁ'), 10 }, { (8, 'ㅂ'), 11 }, { (8, 'ㅅ'), 12 },
            { (8, 'ㅌ'), 13 }, { (8, 'ㅍ'), 14 }, { (8, 'ㅎ'), 15 }, { (17, 'ㅅ'), 18 }
        };

        // 겹받침 index → (남을 종성 index, 다음 음절 초성으로 넘어갈 자음). 모음이 뒤따라올 때 분리하는 데 쓴다
        private readonly static Dictionary<int, (int first, char second)> JongSplit = new()
        {
            { 3, (1, 'ㅅ') }, { 5, (4, 'ㅈ') }, { 6, (4, 'ㅎ') },
            { 9, (8, 'ㄱ') }, { 10, (8, 'ㅁ') }, { 11, (8, 'ㅂ') }, { 12, (8, 'ㅅ') },
            { 13, (8, 'ㅌ') }, { 14, (8, 'ㅍ') }, { 15, (8, 'ㅎ') }, { 18, (17, 'ㅅ') }
        };

        // (기존 모음, 추가된 모음) → 겹모음
        private readonly static Dictionary<(char jung, char add), char> JungCombine = new()
        {
            { ('ㅗ', 'ㅏ'), 'ㅘ' }, { ('ㅗ', 'ㅐ'), 'ㅙ' }, { ('ㅗ', 'ㅣ'), 'ㅚ' },
            { ('ㅜ', 'ㅓ'), 'ㅝ' }, { ('ㅜ', 'ㅔ'), 'ㅞ' }, { ('ㅜ', 'ㅣ'), 'ㅟ' },
            { ('ㅡ', 'ㅣ'), 'ㅢ' }
        };

        private readonly StringBuilder _committed = new();

        private int _cho = -1;
        private int _jung = -1;
        private int _jong = 0;

        // 완성된 텍스트와 지금 조합 중인 음절을 합친, 현재 시점의 전체 표시 문자열
        public string Text
        {
            get
            {
                char active = RenderActiveChar();
                if (active == NoActiveSyllable) return _committed.ToString();

                // 조합 중인 음절을 잠깐 붙여 한 번에 문자열로 만든 뒤 떼어 낸다 — 키를 누를 때마다 문자열을 두 번 만들지 않는다
                _committed.Append(active);
                string text = _committed.ToString();
                _committed.Length--;
                return text;
            }
        }

        // 현재 시점의 전체 표시 길이. 조합 중인 음절은 언제나 한 글자라 길이를 따로 셀 수 있다.
        // 길이만 필요한 자리에서 Text를 부르면 화면에 쓰이지도 않을 문자열이 입력마다 만들어진다
        private int TextLength => _committed.Length + (RenderActiveChar() == NoActiveSyllable ? 0 : 1);

        /// <summary>
        /// 자모 하나를 입력한다. 초성/중성/종성 자리와 겹모음·겹받침 결합 여부를 판단해 조합을 이어가거나
        /// 현재 음절을 완성 텍스트로 확정하고 새 음절을 시작한다.
        /// </summary>
        public void Push(char jamo)
        {
            int choIndex = Array.IndexOf(Cho, jamo);
            if (choIndex >= 0)
            {
                PushCho(choIndex, jamo);
                return;
            }

            int jungIndex = Array.IndexOf(Jung, jamo);
            if (jungIndex >= 0)
            {
                PushJung(jungIndex, jamo);
            }
        }

        /// <summary>
        /// 조합 없이 문자 하나를 그대로 확정 텍스트에 붙인다. 영문·숫자처럼 조합이 필요 없는 문자를 넣을 때 쓴다.
        /// 조합 중이던 한글 음절이 있으면 먼저 확정한 뒤 이어 붙인다.
        /// </summary>
        public void AppendRaw(char c)
        {
            Commit();
            _committed.Append(c);
        }

        /// <summary>
        /// 결과 길이가 maxLength를 넘지 않을 때만 자모를 입력한다. 넘으면 입력 전 상태를 그대로 두고 false를 돌려준다.
        /// 이미 maxLength에 도달했어도 조합 중인 음절에 받침을 더하는 것처럼 길이가 늘지 않는 입력은 허용한다.
        /// </summary>
        public bool TryPush(char jamo, int maxLength)
        {
            // Push는 _committed에 덧붙이기만 하고 지우지 않으므로, 되돌릴 때는 길이만 잘라내면 된다
            int committedLength = _committed.Length;
            int cho = _cho;
            int jung = _jung;
            int jong = _jong;

            Push(jamo);
            if (TextLength <= maxLength) return true;

            _committed.Length = committedLength;
            _cho = cho;
            _jung = jung;
            _jong = jong;
            return false;
        }

        /// <summary>
        /// 길이가 maxLength 미만일 때만 문자 하나를 그대로 붙인다. AppendRaw는 항상 한 글자만 늘리므로
        /// 미리 길이만 확인하면 된다.
        /// </summary>
        public bool TryAppendRaw(char c, int maxLength)
        {
            if (TextLength >= maxLength) return false;

            AppendRaw(c);
            return true;
        }

        /// <summary>
        /// 조합 중인 음절을 한 단계 되돌린다(종성 → 중성 → 초성 순으로 제거). 조합 중인 음절이
        /// 없으면 이미 확정된 텍스트의 마지막 글자를 지운다.
        /// </summary>
        public void Backspace()
        {
            if (_cho >= 0)
            {
                if (_jong != 0)
                {
                    _jong = JongSplit.TryGetValue(_jong, out (int first, char second) split) ? split.first : 0;
                }
                else if (_jung >= 0)
                {
                    _jung = -1;
                }
                else
                {
                    _cho = -1;
                }

                return;
            }

            if (_committed.Length > 0)
            {
                _committed.Remove(_committed.Length - 1, 1);
            }
        }

        /// <summary>
        /// 조합 상태와 완성된 텍스트를 모두 비운다.
        /// </summary>
        public void Clear()
        {
            _committed.Clear();
            _cho = -1;
            _jung = -1;
            _jong = 0;
        }

        /// <summary>
        /// 자음 하나를 조합 중인 음절에 넣는다. 비어 있으면 초성이 되고, 초성과 중성이 이미 있으면
        /// 종성으로 붙거나 앞선 종성과 겹받침(ㄱ+ㅅ → ㄳ 등)으로 합쳐진다.
        /// 종성이 될 수 없는 자음(ㄸ/ㅃ/ㅉ)이거나 더 합칠 수 없으면 지금까지를 확정하고 새 음절을 시작한다.
        /// </summary>
        private void PushCho(int choIndex, char jamo)
        {
            if (_cho < 0)
            {
                _cho = choIndex;
                return;
            }

            if (_jung < 0)
            {
                // 초성만 있는 상태에서 자음이 또 들어오면 조합할 수 없으므로 확정하고 새로 시작한다
                Commit();
                _cho = choIndex;
                return;
            }

            if (_jong == 0)
            {
                int simpleJong = ChoToSimpleJong[choIndex];
                if (simpleJong < 0)
                {
                    // ㄸ/ㅃ/ㅉ는 종성이 될 수 없으므로 확정하고 새로 시작한다
                    Commit();
                    _cho = choIndex;
                    return;
                }

                _jong = simpleJong;
                return;
            }

            if (JongCombine.TryGetValue((_jong, jamo), out int combinedJong))
            {
                _jong = combinedJong;
                return;
            }

            Commit();
            _cho = choIndex;
        }

        /// <summary>
        /// 모음 하나를 조합 중인 음절에 넣는다. 중성이 비었으면 그 자리에 들어가고, 이미 있으면
        /// 앞선 모음과 겹모음(ㅗ+ㅏ → ㅘ 등)으로 합쳐진다.
        /// 종성이 있는 상태에서 모음이 오면 그 종성을 다음 음절의 초성으로 넘긴다(겹받침이면 뒤쪽만 넘긴다).
        /// 초성 없는 모음은 조합될 수 없는 낱자이므로 그대로 확정 텍스트에 붙인다.
        /// </summary>
        private void PushJung(int jungIndex, char jamo)
        {
            if (_cho < 0)
            {
                // 초성 없는 모음은 조합될 수 없는 낱자이므로 그대로 확정 텍스트에 붙인다
                Commit();
                _committed.Append(jamo);
                return;
            }

            if (_jung < 0)
            {
                _jung = jungIndex;
                return;
            }

            if (_jong == 0)
            {
                char currentJung = Jung[_jung];
                if (JungCombine.TryGetValue((currentJung, jamo), out char combinedJung))
                {
                    _jung = Array.IndexOf(Jung, combinedJung);
                    return;
                }

                Commit();
                _committed.Append(jamo);
                return;
            }

            // 종성이 있는 상태에서 모음이 들어오면, 종성을 다음 음절의 초성으로 넘긴다
            if (JongSplit.TryGetValue(_jong, out (int first, char second) split))
            {
                _jong = split.first;
                Commit();
                _cho = Array.IndexOf(Cho, split.second);
                _jung = jungIndex;
            }
            else
            {
                char jongChar = Jong[_jong];
                _jong = 0;
                Commit();
                _cho = Array.IndexOf(Cho, jongChar);
                _jung = jungIndex;
            }
        }

        /// <summary>
        /// 조합 중인 음절을 확정 텍스트에 붙이고 조합 상태를 비운다.
        /// </summary>
        private void Commit()
        {
            char active = RenderActiveChar();
            if (active != NoActiveSyllable) _committed.Append(active);

            _cho = -1;
            _jung = -1;
            _jong = 0;
        }

        /// <summary>
        /// 지금 조합 중인 음절을 글자 하나로 만든다. 초성만/중성만 있으면 그 낱자를, 둘 다 있으면
        /// 완성형 음절을 돌려주고, 조합 중인 것이 없으면 NoActiveSyllable을 돌려준다.
        /// </summary>
        private char RenderActiveChar()
        {
            if (_cho < 0 && _jung < 0) return NoActiveSyllable;
            if (_cho < 0) return Jung[_jung];
            if (_jung < 0) return Cho[_cho];

            return (char)(SyllableBase + (_cho * JungCount + _jung) * JongCount + _jong);
        }
    }
}
