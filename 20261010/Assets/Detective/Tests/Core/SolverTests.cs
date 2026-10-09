using System;
using System.Collections.Generic;
using System.Diagnostics;
using Detective.Core.Logic;
using Detective.Core.Model;
using NUnit.Framework;
using static Detective.Tests.Core.FestivalSpace;

namespace Detective.Tests.Core
{
    public class SolverTests
    {
        CaseSpace space;
        Solver solver;

        [SetUp]
        public void SetUp()
        {
            space = Create();
            solver = new Solver(space);
        }

        // ---- 기본 동작 ----

        [Test]
        public void Enumerates_AllCombos_LastAxisFastest()
        {
            Assert.AreEqual(64, solver.ComboCount);
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, solver.GetCombo(0));
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, solver.GetCombo(1));
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, solver.GetCombo(4));
            CollectionAssert.AreEqual(new[] { 3, 3, 3 }, solver.GetCombo(63));
            Assert.AreEqual(64, solver.CountAlive(solver.AllAlive()));
        }

        [Test]
        public void CountEliminated_CountsOnlyAliveCombos()
        {
            var alive = solver.AllAlive();
            var notDrama = new IsNot(space, Culprit, Drama);
            Assert.AreEqual(16, solver.CountEliminated(alive, notDrama));

            alive = solver.Apply(alive, notDrama);
            Assert.AreEqual(0, solver.CountEliminated(alive, notDrama), "이미 지워진 후보는 다시 세지 않는다");
            Assert.AreEqual(12, solver.CountEliminated(alive, new IsNot(space, Place, Kitchen)));
        }

        [Test]
        public void Apply_DoesNotMutateInput()
        {
            var alive = solver.AllAlive();
            solver.Apply(alive, new IsNot(space, Culprit, Drama));
            Assert.AreEqual(64, solver.CountAlive(alive));
        }

        // ---- 손으로 만든 사건 5개 ----

        [Test]
        public void HandCase1_DirectExclusions()
        {
            AssertUniqueSolution(P(Drama, Backstage, CostumeRack),
                new IsNot(space, Culprit, Council),
                new IsNot(space, Culprit, Cook),
                new IsNot(space, Culprit, Broadcast),
                new IsNot(space, Place, Storage),
                new IsNot(space, Place, Kitchen),
                new IsNot(space, Place, Rooftop),
                new SameTag(space, Item, Place, "club"));
        }

        [Test]
        public void HandCase2_TagsAndImplies()
        {
            AssertUniqueSolution(P(Broadcast, Rooftop, Cart),
                new HasTag(space, Culprit, "trait.lefty"),
                new LacksTag(space, Place, "env.indoor"),
                new Implies(space, Culprit, Broadcast, Item, Cart));
        }

        [Test]
        public void HandCase3_OneOfAndSameTagAcrossCulpritItem()
        {
            AssertUniqueSolution(P(Council, Storage, DocBox),
                new HasTag(space, Culprit, "floor.1"),
                new OneOf(space, Culprit, Drama, Council),
                new HasTag(space, Place, "club.council"),
                new SameTag(space, Culprit, Item, "club"));
        }

        [Test]
        public void HandCase4_ImpliesContrapositive()
        {
            // culprit=council → place=backstage 인데 place≠backstage 이므로 council은 범인이 아니다.
            AssertUniqueSolution(P(Cook, Kitchen, BigPot),
                new LacksTag(space, Culprit, "trait.tall"),
                new LacksTag(space, Culprit, "trait.lefty"),
                new Implies(space, Culprit, Council, Place, Backstage),
                new HasTag(space, Place, "floor.1"),
                new IsNot(space, Place, Backstage),
                new IsNot(space, Place, Storage),
                new SameTag(space, Place, Item, "club"));
        }

        [Test]
        public void HandCase5_NotTogetherChain()
        {
            AssertUniqueSolution(P(Drama, Kitchen, Cart),
                new OneOf(space, Culprit, Drama, Broadcast),
                new HasTag(space, Culprit, "trait.tall"),
                new HasTag(space, Place, "env.indoor"),
                new LacksTag(space, Place, "club.drama"),
                new NotTogether(space, Culprit, Drama, Place, Storage),
                new OneOf(space, Item, Cart, DocBox),
                new NotTogether(space, Place, Kitchen, Item, DocBox));
        }

        void AssertUniqueSolution(int[] expected, params IConstraint[] clues)
        {
            foreach (var clue in clues)
                Assert.IsTrue(clue.Holds(expected), $"단서 '{clue.DebugText}'가 의도한 정답에서 거짓입니다.");

            var alive = solver.Apply(solver.AllAlive(), clues);
            var remaining = solver.AliveCombos(alive);

            Assert.AreEqual(1, remaining.Count,
                "남은 후보: " + string.Join(" | ", remaining.ConvertAll(space.Describe)));
            CollectionAssert.AreEqual(expected, remaining[0], space.Describe(remaining[0]));
        }

        // ---- 모순 ----

        [Test]
        public void Contradiction_AllSuspectsExcluded_LeavesZero()
        {
            var alive = solver.Apply(solver.AllAlive(), new IConstraint[]
            {
                new IsNot(space, Culprit, Drama),
                new IsNot(space, Culprit, Council),
                new IsNot(space, Culprit, Cook),
                new IsNot(space, Culprit, Broadcast),
            });
            Assert.AreEqual(0, solver.CountAlive(alive));
        }

        [Test]
        public void Contradiction_NoSuspectHasBothTags_LeavesZero()
        {
            var alive = solver.Apply(solver.AllAlive(), new IConstraint[]
            {
                new HasTag(space, Culprit, "trait.tall"),
                new HasTag(space, Culprit, "trait.lefty"),
            });
            Assert.AreEqual(0, solver.CountAlive(alive));
        }

        // ---- 성능 ----

        [Test]
        public void Performance_FiveAxes_TwentyConstraints_Under100ms()
        {
            int[] counts = { 8, 6, 6, 4, 2 };
            var big = CreateBigSpace(counts);
            var constraints = CreateRandomConstraints(big, counts, 20, seed: 12345);

            var sw = Stopwatch.StartNew();
            var bigSolver = new Solver(big);
            var alive = bigSolver.AllAlive();
            foreach (var c in constraints) alive = bigSolver.Apply(alive, c);
            int remaining = bigSolver.CountAlive(alive);
            sw.Stop();

            Assert.AreEqual(8 * 6 * 6 * 4 * 2, bigSolver.ComboCount);
            Assert.Less(sw.ElapsedMilliseconds, 100,
                $"솔버 생성 + 조건 20개 적용에 {sw.ElapsedMilliseconds}ms 걸렸습니다 (남은 후보 {remaining}).");
        }

        static CaseSpace CreateBigSpace(int[] counts)
        {
            var catalog = new TagCatalog()
                .Add("color.red", "color").Add("color.green", "color").Add("color.blue", "color")
                .Add("size.small", "size").Add("size.large", "size");
            string[] colors = { "color.red", "color.green", "color.blue" };
            string[] sizes = { "size.small", "size.large" };

            var axes = new List<AxisDef>();
            var tags = new OptionTagTable();
            for (int a = 0; a < counts.Length; a++)
            {
                var options = new List<string>();
                for (int o = 0; o < counts[a]; o++)
                {
                    string optionId = $"o{o}";
                    options.Add(optionId);
                    tags.Add($"axis{a}", optionId, colors[(o + a) % colors.Length], sizes[o % sizes.Length]);
                }
                axes.Add(new AxisDef($"axis{a}", options));
            }
            return new CaseSpace(axes, catalog, tags);
        }

        static List<IConstraint> CreateRandomConstraints(CaseSpace s, int[] counts, int n, int seed)
        {
            var rng = new Random(seed);
            string[] tagIds = { "color.red", "color.green", "color.blue", "size.small", "size.large" };
            string[] categories = { "color", "size" };
            var result = new List<IConstraint>();

            for (int i = 0; i < n; i++)
            {
                int a = rng.Next(counts.Length);
                int b = (a + 1 + rng.Next(counts.Length - 1)) % counts.Length;
                int x = rng.Next(counts[a]);
                int x2 = (x + 1 + rng.Next(counts[a] - 1)) % counts[a];
                int y = rng.Next(counts[b]);

                switch (i % 7)
                {
                    case 0: result.Add(new IsNot(s, a, x)); break;
                    case 1: result.Add(new HasTag(s, a, tagIds[rng.Next(tagIds.Length)])); break;
                    case 2: result.Add(new LacksTag(s, a, tagIds[rng.Next(tagIds.Length)])); break;
                    case 3: result.Add(new SameTag(s, a, b, categories[rng.Next(categories.Length)])); break;
                    case 4: result.Add(new Implies(s, a, x, b, y)); break;
                    case 5: result.Add(new OneOf(s, a, x, x2)); break;
                    default: result.Add(new NotTogether(s, a, x, b, y)); break;
                }
            }
            return result;
        }
    }
}
