namespace Scenes
{
    /// <summary>
    /// 태양광 패널 자세 계산 유틸 — 방향·각도 라벨을 SolarPanelModelPose가 쓰는 회전값으로 바꾼다.
    /// </summary>
    internal static class PanelPoseMath
    {
        /// <summary>카메라 정면 기준 yaw. root가 이미 이 방향을 향하고 있다.</summary>
        public const float FrontYaw = 180f;

        /// <summary>
        /// 방향 문자열을 root 기준 상대 yaw로 바꾼다 (root가 FrontYaw(180°)를 향하므로 남쪽=0, 동쪽=-90, 알 수 없는 값은 정면 0).
        /// </summary>
        public static float DirectionToLocalYaw(string direction)
        {
            float worldYaw = direction switch
            {
                Constants.Directions.North => 0f,
                Constants.Directions.East  => 90f,
                Constants.Directions.South => 180f,
                Constants.Directions.West  => 270f,
                _ => FrontYaw,
            };
            return worldYaw - FrontYaw;
        }

        /// <summary>
        /// "30도" 같은 라벨에서 각도를 읽는다. 숫자가 없거나 0 이하면 false를 반환한다.
        /// </summary>
        public static bool TryParseAngleDegrees(string angle, out int degrees)
        {
            degrees = 0;
            if (string.IsNullOrEmpty(angle)) return false;

            // 숫자만 이어 읽는다 — 문자열을 새로 만들지 않는다
            foreach (char c in angle)
                if (char.IsDigit(c)) degrees = degrees * 10 + (c - '0');
            return degrees > 0;
        }
    }
}
