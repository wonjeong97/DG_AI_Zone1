namespace Data
{
    // 현장에서 고치는 설정 JSON 값의 범위를 바로잡는 공용 도우미 — 음수 시간·간격은 UniTask.Delay가 예외를 내 연출이 멈춘다.
    public static class SettingsClamp
    {
        /// <summary>
        /// 음수면 0으로 바꾸고 바꿨다고 표시한다.
        /// </summary>
        public static int NonNegative(int value, ref bool changed)
        {
            if (value >= 0) return value;
            changed = true;
            return 0;
        }

        /// <summary>
        /// 음수면 0으로 바꾸고 바꿨다고 표시한다.
        /// </summary>
        public static float NonNegative(float value, ref bool changed)
        {
            if (value >= 0f) return value;
            changed = true;
            return 0f;
        }
    }
}
