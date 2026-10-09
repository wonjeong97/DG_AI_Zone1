#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 빌드 씬의 EventSystem이 키보드 탐색 이벤트를 보내지 않는지 검증한다 —
    /// 바코드 스캐너는 키보드처럼 글자와 Enter를 보내므로, 켜져 있으면 플레이 중 QR을 찍을 때 마지막에 누른 버튼이 다시 눌린다.
    /// </summary>
    public class SceneEventSystemTests
    {
        private const string NavigationOn = "m_sendNavigationEvents: 1";

        /// <summary>
        /// 빌드에 포함된 모든 씬의 EventSystem은 Send Navigation Events가 꺼져 있다.
        /// </summary>
        [Test]
        public void 빌드_씬의_EventSystem은_키보드_탐색_이벤트를_보내지_않는다()
        {
            Assert.IsNotEmpty(EditorBuildSettings.scenes, "빌드 씬 목록이 비어 있음");

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;

                string text = File.ReadAllText(scene.path);
                StringAssert.DoesNotContain(NavigationOn, text, $"{scene.path}의 EventSystem이 키보드 탐색 이벤트를 보냄");
            }
        }
    }
}
#endif
