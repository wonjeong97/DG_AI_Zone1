using System;

namespace Scenes
{
    /// <summary>
    /// 태양광 패널 자세 계산 공용 유틸 — SolarPanelPose(구 모델)와 SolarPanelModelPose(신 FBX)가 공유한다.
    /// </summary>
    internal static class PanelPoseMath
    {
        /// <summary>카메라 정면 기준 yaw. root가 이미 이 방향을 향하고 있다.</summary>
        public const float FrontYaw = 180f;

        /// <summary>
        /// 방향 문자열 → root 기준 상대 yaw. root가 FrontYaw(180°)를 향하므로 남쪽=0, 동쪽=-90, ….
        /// 알 수 없는 값이면 정면(0)을 반환한다.
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

            string digits = new string(Array.FindAll(angle.ToCharArray(), char.IsDigit));
            return int.TryParse(digits, out degrees) && degrees > 0;
        }
    }
}
