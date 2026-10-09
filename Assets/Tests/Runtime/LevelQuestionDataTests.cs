#if UNITY_EDITOR
using System.Collections.Generic;
using Data;
using NUnit.Framework;
using UnityEditor;

namespace DG.Zone1.Tests
{
    /// <summary>
    /// 레벨 에셋(LevelData)의 문제 데이터가 그 레벨의 블록과 맞는지 검증한다.
    /// 문제 값·정답은 블록 라벨과 글자까지 같아야 채점된다 — 어긋나면 정답을 놓아도 감점되는데 에디터는 알려 주지 않는다.
    /// </summary>
    public class LevelQuestionDataTests
    {
        private const string LevelDataFolder = "Assets/Data";

        /// <summary>
        /// 프로젝트의 모든 LevelData 에셋을 불러온다.
        /// </summary>
        private static List<LevelData> LoadLevels()
        {
            List<LevelData> levels = new List<LevelData>();
            foreach (string guid in AssetDatabase.FindAssets("t:LevelData", new[] { LevelDataFolder }))
            {
                LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(guid));
                if (level) levels.Add(level);
            }

            Assert.IsNotEmpty(levels, "LevelData 에셋을 찾지 못함");
            return levels;
        }

        /// <summary>
        /// 블록 레이아웃의 모든 블록 라벨과 값 라벨을 하위 블록까지 모은다.
        /// </summary>
        private static HashSet<string> CollectLabels(BlockLayoutData layout)
        {
            HashSet<string> labels = new HashSet<string>();
            if (!layout) return labels;

            AddLabels(layout.inventoryBlocks, labels);
            AddLabels(layout.codingBlocks, labels);
            return labels;
        }

        /// <summary>
        /// 블록 목록의 라벨을 내부·아니면·가로 체인 블록까지 재귀로 모은다.
        /// </summary>
        private static void AddLabels(BlockEntry[] entries, HashSet<string> labels)
        {
            if (entries is null) return;

            foreach (BlockEntry entry in entries)
            {
                if (entry is null) continue;
                if (!string.IsNullOrEmpty(entry.label)) labels.Add(entry.label);
                if (!string.IsNullOrEmpty(entry.valueLabel)) labels.Add(entry.valueLabel);
                AddLabels(entry.innerBlocks, labels);
                AddLabels(entry.elseBlocks, labels);
                AddLabels(entry.chainBlocks, labels);
            }
        }

        /// <summary>
        /// "5m 이상" / "5m"처럼 앞자리 숫자를 읽는다 (숫자로 시작하지 않으면 -1) — 채점(BlockScorer)과 같은 기준이다.
        /// </summary>
        private static int LeadingNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;

            int length = 0;
            while (length < text.Length && char.IsDigit(text[length])) length++;
            return length > 0 && int.TryParse(text.Substring(0, length), out int number) ? number : -1;
        }

        /// <summary>
        /// 모든 레벨에 문제 문구와 문제 값 후보가 있고, 문구에 문제 값을 넣어도 형식 오류가 나지 않는다.
        /// </summary>
        [Test]
        public void 레벨마다_문제_문구와_문제_값_후보가_있다()
        {
            foreach (LevelData level in LoadLevels())
            {
                Assert.IsFalse(string.IsNullOrEmpty(level.questionFormat), $"{level.name}: 문제 문구가 비어 있음");
                Assert.IsNotNull(level.questionOptions, $"{level.name}: 문제 값 후보가 없음");
                Assert.IsNotEmpty(level.questionOptions, $"{level.name}: 문제 값 후보가 없음");

                HashSet<string> values = new HashSet<string>();
                foreach (QuestionOption option in level.questionOptions)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(option.value), $"{level.name}: 빈 문제 값이 있음");
                    Assert.IsTrue(values.Add(option.value), $"{level.name}: 문제 값 '{option.value}'이 중복됨");
                    Assert.DoesNotThrow(() => string.Format(level.questionFormat, option.value),
                        $"{level.name}: 문제 문구 형식 오류({option.value})");

                    if (!string.IsNullOrEmpty(level.hintRuleFormat))
                        Assert.DoesNotThrow(() => string.Format(level.hintRuleFormat, option.value),
                            $"{level.name}: 힌트 규칙 문구 형식 오류({option.value})");
                }
            }
        }

        /// <summary>
        /// 정답 방향은 그 레벨 블록 라벨에 그대로 있다 — 없으면 정답 블록을 놓아도 오답으로 채점된다.
        /// </summary>
        [Test]
        public void 정답은_그_레벨의_블록_라벨에_있다()
        {
            foreach (LevelData level in LoadLevels())
            {
                HashSet<string> labels = CollectLabels(level.blockLayout);
                foreach (QuestionOption option in level.questionOptions)
                {
                    if (string.IsNullOrEmpty(option.correctAnswer)) continue;
                    Assert.IsTrue(labels.Contains(option.correctAnswer),
                        $"{level.name}: 문제 값 '{option.value}'의 정답 '{option.correctAnswer}'이 블록 라벨에 없음");
                }
            }
        }

        /// <summary>
        /// 채점이 블록 이름으로 찾는 라벨(Constants.BlockLabels)이 그 레벨의 블록 레이아웃에 그대로 있다 —
        /// 에셋 라벨만 바꾸면 컴파일 오류 없이 해당 채점 항목이 0점이 되기 때문이다.
        /// </summary>
        [Test]
        public void 채점에_쓰는_블록_라벨이_레벨_블록에_있다()
        {
            Dictionary<LevelKind, IEnumerable<string>> required = new Dictionary<LevelKind, IEnumerable<string>>
            {
                [LevelKind.Hydro] = new[] { Constants.BlockLabels.HydroOpen, Constants.BlockLabels.HydroClose },
                [LevelKind.PowerPlant] = new[]
                {
                    Constants.BlockLabels.AmusementOff, Constants.BlockLabels.AmusementOn,
                    Constants.BlockLabels.HospitalOn, Constants.BlockLabels.HospitalOff,
                    Constants.BlockLabels.DayCondition, Constants.BlockLabels.SpareCondition, Constants.BlockLabels.And
                },
                [LevelKind.FutureEnergy] = Constants.BlockLabels.FutureEnergies,
            };

            HashSet<LevelKind> checkedKinds = new HashSet<LevelKind>();
            foreach (LevelData level in LoadLevels())
            {
                if (!required.TryGetValue(level.kind, out IEnumerable<string> labels)) continue;

                HashSet<string> present = CollectLabels(level.blockLayout);
                foreach (string label in labels)
                    Assert.IsTrue(present.Contains(label), $"{level.name}의 블록에 채점 라벨 '{label}'이 없음");
                checkedKinds.Add(level.kind);
            }

            CollectionAssert.AreEquivalent(required.Keys, checkedKinds, "채점 라벨을 쓰는 레벨 데이터를 모두 찾지 못함");
        }

        /// <summary>
        /// 수력 문제 높이마다 같은 높이의 조건 블록이 있다 — 없으면 정확한 높이를 고를 수 없어 만점이 나오지 않는다.
        /// </summary>
        [Test]
        public void 수력_문제_높이마다_같은_높이의_조건_블록이_있다()
        {
            foreach (LevelData level in LoadLevels())
            {
                if (level.kind != LevelKind.Hydro) continue;

                HashSet<int> blockHeights = new HashSet<int>();
                foreach (string label in CollectLabels(level.blockLayout))
                {
                    int height = LeadingNumber(label);
                    if (height >= 0) blockHeights.Add(height);
                }

                foreach (QuestionOption option in level.questionOptions)
                {
                    int height = LeadingNumber(option.value);
                    Assert.GreaterOrEqual(height, 0, $"{level.name}: 문제 값 '{option.value}'이 숫자로 시작하지 않음");
                    Assert.IsTrue(blockHeights.Contains(height),
                        $"{level.name}: 문제 높이 '{option.value}'과 같은 높이의 조건 블록이 없음");
                }
            }
        }
    }
}
#endif
