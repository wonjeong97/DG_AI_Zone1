using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using Wonjeong.Network;

namespace Network
{
    public class APIManager : ApiManagerBase
    {
        // 전시 특성상 아웃트로(마지막 씬)에서 발생한 타임아웃은 중도 이탈이 아니라 정상 관람 완료이므로
        // move_idle_timeout 대신 move_idle로 집계되어야 함
        protected override void OnInactivityTimeout()
        {
            if (SceneManager.GetActiveScene().name == Constants.Scenes.Outro)
            {
                SendMoveIdleLogAsync().Forget();
                return;
            }

            base.OnInactivityTimeout();
        }
    }
}