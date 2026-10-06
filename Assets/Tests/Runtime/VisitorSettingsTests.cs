using Data;
using NUnit.Framework;
using UnityEngine;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 운영 모드·체험자 이름 설정 — 관리자 페이지에서 바꾼 값은 앱을 다시 켜도 유지되고, 지우면 에셋 기본값으로 돌아가야 한다.
    /// 빌드에서는 런타임에 바꾼 SO 값이 재시작 때 사라지므로 PlayerPrefs로 유지하는 것이 핵심이다.
    /// </summary>
    public class VisitorSettingsTests
    {
        // 에디터 PlayerPrefs를 건드리므로 테스트 전 값을 기억했다가 되돌린다
        private bool _hadServerConnected;
        private int _serverConnected;
        private bool _hadVisitorName;
        private string _visitorName;

        private VisitorSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _hadServerConnected = PlayerPrefs.HasKey(VisitorSettings.ServerConnectedKey);
            _serverConnected = PlayerPrefs.GetInt(VisitorSettings.ServerConnectedKey);
            _hadVisitorName = PlayerPrefs.HasKey(VisitorSettings.VisitorNameKey);
            _visitorName = PlayerPrefs.GetString(VisitorSettings.VisitorNameKey);

            _settings = ScriptableObject.CreateInstance<VisitorSettings>();
            _settings.ClearOverrides();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);

            if (_hadServerConnected) PlayerPrefs.SetInt(VisitorSettings.ServerConnectedKey, _serverConnected);
            else PlayerPrefs.DeleteKey(VisitorSettings.ServerConnectedKey);

            if (_hadVisitorName) PlayerPrefs.SetString(VisitorSettings.VisitorNameKey, _visitorName);
            else PlayerPrefs.DeleteKey(VisitorSettings.VisitorNameKey);

            PlayerPrefs.Save();
        }

        /// <summary>
        /// 관리자 페이지에서 바꾼 적이 없으면 에셋 기본값(로컬 모드, '체험자')을 쓴다.
        /// </summary>
        [Test]
        public void 바꾼_적이_없으면_에셋_기본값을_쓴다()
        {
            Assert.IsFalse(_settings.IsServerConnected);
            Assert.AreEqual("체험자", _settings.VisitorName);
        }

        /// <summary>
        /// 바꾼 값은 새로 불러온 설정(앱 재시작)에서도 유지되고, 변경값을 지우면 기본값으로 돌아간다.
        /// </summary>
        [Test]
        public void 바꾼_값은_다시_불러와도_유지되고_지우면_기본값으로_돌아간다()
        {
            _settings.IsServerConnected = true;
            _settings.VisitorName = "홍길동";

            VisitorSettings reloaded = ScriptableObject.CreateInstance<VisitorSettings>();
            try
            {
                Assert.IsTrue(reloaded.IsServerConnected, "모드 변경이 유지되지 않음");
                Assert.AreEqual("홍길동", reloaded.VisitorName, "이름 변경이 유지되지 않음");

                reloaded.ClearOverrides();

                Assert.IsFalse(_settings.IsServerConnected);
                Assert.AreEqual("체험자", _settings.VisitorName);
            }
            finally
            {
                Object.DestroyImmediate(reloaded);
            }
        }
    }
}
