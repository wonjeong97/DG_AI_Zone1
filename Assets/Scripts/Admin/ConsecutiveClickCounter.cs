namespace Admin
{
    // 제한 시간 안에 정해진 횟수만큼 연속으로 눌렸는지 센다.
    // 템플릿 GameCloser와 같은 규칙 — 첫 클릭부터 제한 시간이 지나면 지금 클릭을 새 1회로 센다.
    public class ConsecutiveClickCounter
    {
        private readonly int _targetCount;
        private readonly float _timeWindow;

        private int _count;
        private float _firstClickTime;

        public int Count => _count;

        /// <summary>
        /// 목표 횟수와 연속 클릭으로 인정하는 시간(초)을 정한다.
        /// </summary>
        public ConsecutiveClickCounter(int targetCount, float timeWindow)
        {
            _targetCount = targetCount;
            _timeWindow = timeWindow;
        }

        /// <summary>
        /// 클릭 한 번을 기록하고, 목표 횟수에 닿으면 다음 연속 클릭을 위해 카운트를 비운 뒤 true를 돌려준다.
        /// </summary>
        public bool Register(float now)
        {
            if (_count == 0 || now - _firstClickTime > _timeWindow)
            {
                _count = 1;
                _firstClickTime = now;
            }
            else
            {
                _count++;
            }

            if (_count < _targetCount) return false;

            _count = 0;
            return true;
        }
    }
}
