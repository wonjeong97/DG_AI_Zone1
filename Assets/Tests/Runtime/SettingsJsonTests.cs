using System;
using System.IO;
using System.Reflection;
using Data;
using NUnit.Framework;
using UnityEngine;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 빌드 뒤 현장에서 고치는 StreamingAssets 설정 JSON이 설정 클래스와 맞는지, 키가 빠진 예전 파일도 기본값으로 읽히는지 검증한다.
    /// </summary>
    public class SettingsJsonTests
    {
        /// <summary>
        /// 영상·관리자·타이틀 설정 JSON에 설정 클래스의 모든 항목이 같은 이름으로 들어 있다 —
        /// 키 이름이 틀리면 JsonUtility가 조용히 기본값을 써서 현장에서 고쳐도 반영되지 않는다.
        /// </summary>
        [TestCase(typeof(CommonSettings), Constants.SettingsFiles.Common)]
        [TestCase(typeof(AdminSettings), Constants.SettingsFiles.Admin)]
        [TestCase(typeof(TitleSceneSettings), Constants.SettingsFiles.Title)]
        public void 설정_JSON에_모든_항목이_같은_이름으로_있다(Type settingsType, string fileName)
        {
            string json = ReadSettingsJson(fileName);

            foreach (FieldInfo field in settingsType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                StringAssert.Contains(string.Concat("\"", field.Name, "\""), json, $"{fileName}.json에 {field.Name} 키가 없음");
        }

        /// <summary>
        /// 00_Common.json이 가리키는 로봇 영상과 기본 영상이 StreamingAssets에 있다 — 영상 파일 이름만 바꾸고 JSON을 고치지 않으면 기본 영상으로 돌아간다.
        /// </summary>
        [Test]
        public void 로봇_영상_파일이_StreamingAssets에_있다()
        {
            CommonSettings settings = JsonUtility.FromJson<CommonSettings>(ReadSettingsJson(Constants.SettingsFiles.Common));

            Assert.IsTrue(File.Exists(Path.Combine(Application.streamingAssetsPath, settings.robotVideoPath)),
                $"00_Common.json의 robotVideoPath '{settings.robotVideoPath}' 파일이 없음");
            Assert.IsTrue(File.Exists(Path.Combine(Application.streamingAssetsPath, Constants.VideoPaths.RobotRelative)),
                $"기본 영상 '{Constants.VideoPaths.RobotRelative}' 파일이 없음");
        }

        /// <summary>
        /// 비밀번호만 있던 예전 Admin.json도 자동 닫기 시간·진입 클릭 수는 기본값(60초·10초·10회·3초)으로 읽힌다.
        /// </summary>
        [Test]
        public void 예전_Admin_json도_나머지_값은_기본값으로_읽힌다()
        {
            AdminSettings settings = JsonUtility.FromJson<AdminSettings>("{\"password\": \"1234\"}");

            Assert.AreEqual("1234", settings.password);
            Assert.AreEqual(Constants.Admin.DefaultIdleCloseSeconds, settings.idleCloseSeconds);
            Assert.AreEqual(Constants.Admin.DefaultPasswordIdleCloseSeconds, settings.passwordIdleCloseSeconds);
            Assert.AreEqual(Constants.Admin.DefaultEntryClickCount, settings.entryClickCount);
            Assert.AreEqual(Constants.Admin.DefaultEntryClickWindowSeconds, settings.entryClickWindowSeconds);
        }

        /// <summary>
        /// 관리자 시간·횟수가 1보다 작으면 기본값으로 바꾼다 — 창이 열리자마자 닫히거나 숨은 버튼 진입이 동작하지 않는 일을 막는다.
        /// </summary>
        [Test]
        public void 관리자_시간과_횟수가_1보다_작으면_기본값으로_바꾼다()
        {
            AdminSettings invalid = new AdminSettings
            {
                idleCloseSeconds = 0f, passwordIdleCloseSeconds = 0.5f, entryClickCount = 0, entryClickWindowSeconds = -1f
            };

            Assert.IsTrue(invalid.ClampToValid());
            Assert.AreEqual(Constants.Admin.DefaultIdleCloseSeconds, invalid.idleCloseSeconds);
            Assert.AreEqual(Constants.Admin.DefaultPasswordIdleCloseSeconds, invalid.passwordIdleCloseSeconds);
            Assert.AreEqual(Constants.Admin.DefaultEntryClickCount, invalid.entryClickCount);
            Assert.AreEqual(Constants.Admin.DefaultEntryClickWindowSeconds, invalid.entryClickWindowSeconds);

            AdminSettings valid = new AdminSettings { idleCloseSeconds = 120f, entryClickCount = 5 };
            Assert.IsFalse(valid.ClampToValid(), "1 이상인 값은 그대로 둔다");
            Assert.AreEqual(120f, valid.idleCloseSeconds);
            Assert.AreEqual(5, valid.entryClickCount);
        }

        /// <summary>
        /// 타이틀의 체험자 시작 안내 문구에 이름 자리({name})가 있다 — 빠지면 QR로 확인한 체험자 이름이 안내에 나오지 않는다.
        /// </summary>
        [Test]
        public void 타이틀_이름_안내_문구에_이름_자리가_있다()
        {
            TitleSceneSettings settings = JsonUtility.FromJson<TitleSceneSettings>(ReadSettingsJson(Constants.SettingsFiles.Title));

            StringAssert.Contains(App.VisitorInfoProvider.NamePlaceholder, settings.startGuideWithNameText);
            StringAssert.Contains(App.VisitorInfoProvider.NamePlaceholder, Constants.TitleMessages.StartGuideWithName);
        }

        /// <summary>
        /// StreamingAssets의 설정 JSON 본문을 읽는다 (fileName은 JsonLoader와 같이 확장자 없이).
        /// </summary>
        private static string ReadSettingsJson(string fileName)
        {
            string path = Path.Combine(Application.streamingAssetsPath, fileName + ".json");
            Assert.IsTrue(File.Exists(path), $"{path} 파일이 없음");
            return File.ReadAllText(path);
        }
    }
}
