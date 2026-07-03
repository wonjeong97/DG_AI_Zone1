namespace DG.Game
{
    // Value 블록의 값 타입. Command 블록은 허용할 타입을 지정한다 (None = 모든 타입 허용).
    public enum ValueKind
    {
        None,       // 미지정 — Command에서는 모든 타입 허용
        Direction,  // 방향 (동쪽, 서쪽 …)
        Count,      // 개수 (20개, 40개 …)
        Angle       // 각도 (30도, 45도 …)
    }
}
