using System.Collections.Generic;
using System.Linq;
using Detective.Core.Logic;
using Detective.Data;
using NUnit.Framework;
using UnityEditor;

namespace Detective.Tests.Data
{
    // 실제 학원 축제 팩 에셋으로 하는 대량 테스트 (tool-design 8-3).
    // 팩이 아직 없으면 건너뛴다: 메뉴 Detective > Setup > Create School Festival Pack 으로 먼저 만든다.
    public class SchoolFestivalPackTests
    {
        const string Folder = "Assets/Packs/SchoolFestival";
        const int SeedCount = 300;

        WorldPack pack;

        [SetUp]
        public void SetUp()
        {
            pack = null;
            if (AssetDatabase.IsValidFolder(Folder))
            {
                var paths = AssetDatabase.FindAssets("t:WorldPack", new[] { Folder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(p => p, System.StringComparer.Ordinal);
                foreach (var path in paths)
                {
                    pack = AssetDatabase.LoadAssetAtPath<WorldPack>(path);
                    if (pack != null) break;
                }
            }

            if (pack == null)
                Assert.Ignore("학원 축제 팩 에셋이 없습니다. 메뉴 Detective > Setup > Create School Festival Pack 을 먼저 실행하세요.");
        }

        [Test]
        public void Pack_HasExpectedContent()
        {
            Assert.GreaterOrEqual(pack.entities.Count(e => e != null && e.kind == Detective.Core.Model.EntityKind.Suspect), 6);
            Assert.GreaterOrEqual(pack.entities.Count(e => e != null && e.kind == Detective.Core.Model.EntityKind.Place), 6);
            Assert.GreaterOrEqual(pack.entities.Count(e => e != null && e.kind == Detective.Core.Model.EntityKind.Item), 6);
            Assert.GreaterOrEqual(pack.suspectProfiles.Count, 6);
            Assert.GreaterOrEqual(pack.rules.Count, 8);
            Assert.GreaterOrEqual(pack.templates.Count, 3);

            foreach (var rule in pack.rules)
                Assert.GreaterOrEqual(rule.textVariants.Count, 3, $"규칙 '{rule.id}'의 문장 변형이 3개 미만입니다.");
        }

        [Test]
        public void Pack_ValidatesWithoutErrors()
        {
            var errors = PackValidator.Validate(pack).Where(p => p.Severity == ProblemSeverity.Error).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void EveryTemplate_300Seeds_AtLeast99PercentSucceed_NoneUnsolvable()
        {
            Assert.IsNotEmpty(pack.templates, "사건 템플릿이 없습니다.");
            var lines = new List<string>();

            foreach (var template in pack.templates)
            {
                Assert.IsNotNull(template, "팩에 끊긴 템플릿 연결이 있습니다.");
                var data = PackConverter.Convert(template, pack);
                var report = CaseBatch.Run(data, 0, SeedCount);

                lines.Add($"{template.id}: 성공 {report.Succeeded}/{report.Count}, 평균 시도 {report.AverageAttempts:0.00}, " +
                          $"단서 평균 {report.AverageClues:0.0} 최대 {report.MaxClues}, 평균 {report.AverageMs:0.000}ms 최대 {report.MaxMs:0.000}ms, " +
                          $"안 쓰인 규칙: {string.Join(",", report.RuleUsages.Where(u => u.RealUses == 0).Select(u => u.RuleId))}");

                Assert.AreEqual(0, report.Unsolvable,
                    $"'{template.id}': 풀 수 없는 사건이 나왔습니다. 시드: {string.Join(", ", report.UnsolvableSeeds)}");
                Assert.GreaterOrEqual(report.SuccessRate, 0.99,
                    $"'{template.id}': 성공률 {report.SuccessRate:P1}. 실패한 시드: {string.Join(", ", report.FailedSeeds)}");
            }

            TestContext.WriteLine(string.Join("\n", lines));
        }

        [Test]
        public void EveryTemplate_300Seeds_StoryConditionsHold()
        {
            Assert.IsNotNull(pack.testimonyText, "팩에 증언 문장(TestimonyText)이 없습니다. 팩 생성 메뉴를 다시 실행하세요.");

            foreach (var template in pack.templates)
            {
                var data = PackConverter.Convert(template, pack);
                for (int seed = 0; seed < SeedCount; seed++)
                {
                    var instance = CaseGenerator.Generate(data, seed);
                    Assert.IsNotNull(instance, $"'{template.id}' seed {seed}: 생성 실패");
                    Assert.IsNotNull(instance.Timeline, $"'{template.id}' seed {seed}: 타임라인이 없습니다.");

                    var violations = CaseVerifier.FindViolations(instance);
                    Assert.IsEmpty(violations, $"'{template.id}' seed {seed}:\n{string.Join("\n", violations)}\n\n{instance.ToDebugString()}");

                    foreach (var line in instance.Testimonies.SelectMany(t => t.Lines))
                    {
                        Assert.IsNotEmpty(line.Text, $"'{template.id}' seed {seed}: 문장 없는 증언 줄 {line.Id}");
                        Assert.IsFalse(line.Text.Contains("{"), $"'{template.id}' seed {seed}: 안 바뀐 변수 — {line.Text}");
                    }
                    foreach (var evidence in instance.Evidence)
                    {
                        Assert.IsNotEmpty(evidence.Text, $"'{template.id}' seed {seed}: 문장 없는 증거 {evidence.Id}");
                        Assert.IsFalse(evidence.Text.Contains("{"), $"'{template.id}' seed {seed}: 안 바뀐 변수 — {evidence.Text}");
                    }
                }
            }
        }

        [Test]
        public void EveryTemplate_ClueTexts_HaveNoUnfilledVariables()
        {
            foreach (var template in pack.templates)
            {
                var data = PackConverter.Convert(template, pack);
                for (int seed = 0; seed < 100; seed++)
                {
                    var instance = CaseGenerator.Generate(data, seed);
                    if (instance == null) continue;
                    foreach (var clue in instance.Clues)
                    {
                        Assert.IsNotEmpty(clue.Text, $"'{template.id}' seed {seed}: 규칙 '{clue.RuleId}'에 문장이 없습니다.");
                        Assert.IsFalse(clue.Text.Contains("{"), $"'{template.id}' seed {seed}: 안 바뀐 변수 — {clue.Text}");
                    }
                }
            }
        }
    }
}
