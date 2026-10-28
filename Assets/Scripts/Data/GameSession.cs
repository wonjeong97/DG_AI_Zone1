using System.Collections.Generic;
using Game.Runtime;

namespace Data
{
    // 한 판(체험자 한 명)의 진행 상태 — GameLifetimeScope가 싱글턴으로 등록해 씬 사이에서 공유한다.
    // 앱을 껐다 켜면 새 인스턴스로 시작하므로 부팅 때 따로 초기화하지 않는다.
    public class GameSession
    {
        public LevelData currentLevel;

        // 결과 화면까지 간 레벨(0부터, 넘어가기 포함) — 레벨 선택 화면이 V를 붙인다.
        // QR로 확인한 체험자는 서버에 기록이 있는 레벨도 들어가고, 타이틀로 돌아오면 ResetProgress가 비운다
        public readonly HashSet<int> completedLevelIndices = new();

        // 마지막 실행 결과 — 결과 씬 표시용
        public int lastScore;
        public string lastQuestionTime;
        public string lastDirection;  // 레벨1 결과 스테이지의 패널 방향

        // 코딩 패널에 조립한 블록 모양 — 결과 화면 '나의 코딩 결과'에 같은 블록으로 다시 조립한다
        public ProgramShape lastProgram;

        // 컴파일에 성공해 채점까지 끝난 결과인지 — 넘어가기/미완료와 구분한다.
        // 레벨마다 채워지는 값이 달라 개별 필드로 판정하면 분기가 계속 늘고,
        // 값 추출이 실패한 경우(예: 조건식 파싱 실패) 정상 플레이가 스킵으로 잘못 처리된다.
        public bool hasCodingResult;

        // 관리자 페이지에서 고른 레벨(0부터) — 2_Story가 레벨 선택 화면을 건너뛰고 이 레벨을 바로 고른 뒤 -1로 비운다
        public int pendingStoryLevelIndex = -1;

        // 관리자 페이지의 레벨 이동으로 시작한 판인지 — 스토리의 < 버튼과 결과 화면의 다음 버튼이 타이틀의 관리자 화면으로 돌아간다.
        // 타이틀에 돌아오면 판이 끝나므로 GameLifetimeScope가 비운다
        public bool isAdminLevelJump;

        // 다음에 타이틀에 들어오면 비밀번호 없이 관리자 화면을 바로 연다 — 타이틀의 AdminTrigger가 열고 비운다
        public bool openAdminOnTitle;

        /// <summary>
        /// 체험자 한 명의 체험이 끝나 타이틀로 돌아오면 다음 체험자가 처음부터 시작하도록 진행도를 비운다.
        /// </summary>
        public void ResetProgress()
        {
            completedLevelIndices.Clear();
            currentLevel = null;
            lastQuestionTime = null;
            pendingStoryLevelIndex = -1;
            isAdminLevelJump = false;
            ResetLastResult();
        }

        /// <summary>
        /// 결과 씬 표시용 값을 초기화한다 — 게임 씬 진입 시, 그리고 넘어가기로 실패 처리할 때.
        /// </summary>
        public void ResetLastResult()
        {
            // 문제 값(lastQuestionTime)은 결과 씬에서도 계속 쓰이므로 건드리지 않는다.
            lastScore = 0;
            lastDirection = null;
            lastProgram = null;
            hasCodingResult = false;
        }
    }
}
