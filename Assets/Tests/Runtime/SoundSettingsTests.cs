#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HuliacDev.Data;
using NUnit.Framework;
using UnityEngine;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 코드가 재생하는 효과음 키(Constants.Sounds)가 Settings.json에 등록돼 있고 파일도 있는지 검증한다.
    /// 키나 파일 이름이 어긋나면 SoundManager가 경고 로그만 남기고 소리 없이 넘어가 놓치기 쉽다.
    /// </summary>
    public class SoundSettingsTests
    {
        private const string SettingsFileName = "Settings.json";

        /// <summary>
        /// Constants.Sounds의 모든 키가 Settings.json의 sounds에 있고, 그 파일이 StreamingAssets에 있는지 확인한다.
        /// </summary>
        [Test]
        public void 모든_효과음_키가_설정에_등록되어_있고_파일도_있다()
        {
            string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, SettingsFileName));
            Settings settings = JsonUtility.FromJson<Settings>(json);
            Assert.IsNotNull(settings.sounds, "Settings.json에 sounds 항목이 없음");

            Dictionary<string, SoundSetting> byKey = new Dictionary<string, SoundSetting>();
            foreach (SoundSetting s in settings.sounds) byKey[s.key] = s;

            foreach (FieldInfo field in typeof(Constants.Sounds).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                string key = (string)field.GetValue(null);
                Assert.IsTrue(byKey.TryGetValue(key, out SoundSetting setting), $"Settings.json에 '{key}' 키가 없음");
                Assert.IsTrue(File.Exists(Path.Combine(Application.streamingAssetsPath, setting.clipPath)),
                    $"'{key}'의 파일이 없음: {setting.clipPath}");
            }
        }
    }
}
#endif
