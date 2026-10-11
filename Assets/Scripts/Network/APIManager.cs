using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using HuliacDev.Network;
using ZLogger;

namespace Network
{
    public class APIManager : ApiManagerBase
    {
        /// <summary>
        /// 비활동 타임아웃 로그를 지금 씬에 맞게 보낸다 — 타이틀에서는 보내지 않고, 아웃트로에서는 move_idle로 집계한다.
        /// </summary>
        protected override void OnInactivityTimeout()
        {
            string sceneName = SceneManager.GetActiveScene().name;

            // 타이틀(첫 씬)은 이미 대기 화면이라 비활동 타임아웃이 나도 로그를 보내지 않는다
            // — QR로 확인한 체험자가 시작하기를 누르지 않아 QR 대기로 돌아갈 때는 TitleSceneManager가 move_idle_timeout을 직접 보낸다.
            if (sceneName == Constants.Scenes.Title)
            {
                if (Logger != null) Logger.ZLogInformation($"[APIManager] 타이틀은 이미 대기 화면이라 비활동 타임아웃 로그(move_idle_timeout)를 보내지 않습니다.");
                return;
            }

            // 아웃트로(마지막 씬)에서 발생한 타임아웃은 중도 이탈이 아니라 정상 관람 완료이므로
            // move_idle_timeout 대신 move_idle로 집계한다.
            if (sceneName == Constants.Scenes.Outro)
            {
                SendMoveIdleLogAsync().Forget();
                return;
            }

            base.OnInactivityTimeout();
        }
    }
}
