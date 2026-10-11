namespace Network
{
    // 결과 화면 다음 버튼·스토리 화면 < 버튼·비활동 타임아웃·앱 종료가 겹치거나 버튼을 연타해도 enter 한 번에 exit는 한 번만 나가게 한다.
    // exit 없이 다시 enter를 보내면 새로 고른 레벨로 바뀐다.
    /// <summary>
    /// 아두이노에 enter를 보낸 뒤 아직 exit를 보내지 않은 레벨을 기억한다.
    /// </summary>
    public class ArduinoEnterState
    {
        private const int NoLevel = -1;

        private int _levelIndex = NoLevel;

        /// <summary>
        /// enter를 보낸 레벨 순번(0부터)을 기억한다.
        /// </summary>
        public void MarkEntered(int levelIndex) => _levelIndex = levelIndex;

        /// <summary>
        /// exit를 보낼 레벨을 꺼낸다 — enter 뒤 처음 한 번만 true를 돌려준다.
        /// </summary>
        public bool TryTakeExit(out int levelIndex)
        {
            levelIndex = _levelIndex;
            if (levelIndex < 0) return false;

            _levelIndex = NoLevel;
            return true;
        }
    }
}
