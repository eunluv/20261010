using System;
using Detective.Core.Logic;
using Detective.Core.Model;
using NUnit.Framework;
using static Detective.Tests.Core.FestivalSpace;

namespace Detective.Tests.Core
{
    public class ConstraintTests
    {
        CaseSpace space;

        [SetUp]
        public void SetUp() => space = Create();

        [Test]
        public void IsNot_Holds()
        {
            var c = new IsNot(space, Culprit, Drama);
            Assert.IsTrue(c.Holds(P(Council, Backstage, CostumeRack)));
            Assert.IsFalse(c.Holds(P(Drama, Backstage, CostumeRack)));
            Assert.AreEqual("culprit ≠ drama", c.DebugText);
        }

        [Test]
        public void HasTag_Holds()
        {
            var c = new HasTag(space, Culprit, "trait.lefty");
            Assert.IsTrue(c.Holds(P(Broadcast, Storage, Cart)));
            Assert.IsFalse(c.Holds(P(Drama, Storage, Cart)));
        }

        [Test]
        public void LacksTag_Holds()
        {
            var c = new LacksTag(space, Place, "env.outdoor");
            Assert.IsTrue(c.Holds(P(Drama, Kitchen, Cart)));
            Assert.IsFalse(c.Holds(P(Drama, Rooftop, Cart)));
        }

        [Test]
        public void SameTag_Holds()
        {
            var club = new SameTag(space, Item, Place, "club");
            Assert.IsTrue(club.Holds(P(Cook, Kitchen, BigPot)));
            Assert.IsFalse(club.Holds(P(Cook, Kitchen, DocBox)));

            // 범인 drama(floor.2)와 장소 backstage(floor.1)는 club은 같지만 floor는 다르다.
            var floor = new SameTag(space, Culprit, Place, "floor");
            Assert.IsFalse(floor.Holds(P(Drama, Backstage, CostumeRack)));
            Assert.IsTrue(floor.Holds(P(Council, Backstage, CostumeRack)));
        }

        [Test]
        public void Implies_Holds()
        {
            var c = new Implies(space, Culprit, Cook, Place, Kitchen);
            Assert.IsTrue(c.Holds(P(Cook, Kitchen, BigPot)), "전제 참, 결론 참");
            Assert.IsFalse(c.Holds(P(Cook, Storage, BigPot)), "전제 참, 결론 거짓");
            Assert.IsTrue(c.Holds(P(Drama, Storage, BigPot)), "전제 거짓이면 항상 참");
        }

        [Test]
        public void OneOf_Holds()
        {
            var c = new OneOf(space, Culprit, Drama, Broadcast);
            Assert.IsTrue(c.Holds(P(Drama, Storage, Cart)));
            Assert.IsTrue(c.Holds(P(Broadcast, Storage, Cart)));
            Assert.IsFalse(c.Holds(P(Cook, Storage, Cart)));
        }

        [Test]
        public void NotTogether_Holds()
        {
            var c = new NotTogether(space, Place, Kitchen, Item, DocBox);
            Assert.IsFalse(c.Holds(P(Drama, Kitchen, DocBox)), "둘 다 해당하면 거짓");
            Assert.IsTrue(c.Holds(P(Drama, Kitchen, BigPot)));
            Assert.IsTrue(c.Holds(P(Drama, Storage, DocBox)));
        }

        [Test]
        public void Constructors_RejectInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new IsNot(space, Culprit, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IsNot(space, 3, 0));
            Assert.Throws<ArgumentException>(() => new HasTag(space, Culprit, "no.such.tag"));
            Assert.Throws<ArgumentException>(() => new SameTag(space, Place, Place, "club"));
            Assert.Throws<ArgumentException>(() => new SameTag(space, Item, Place, "no_category"));
            Assert.Throws<ArgumentException>(() => new Implies(space, Culprit, Drama, Culprit, Cook));
            Assert.Throws<ArgumentException>(() => new OneOf(space, Culprit, Drama, Drama));
            Assert.Throws<ArgumentException>(() => new NotTogether(space, Item, Cart, Item, DocBox));
        }

        [Test]
        public void CaseSpace_RejectsUnknownTagInTable()
        {
            var axes = new[] { new AxisDef("culprit", new[] { "a", "b" }) };
            var catalog = new TagCatalog().Add("t", "cat");
            var tags = new OptionTagTable().Add("culprit", "a", "missing");
            Assert.Throws<ArgumentException>(() => new CaseSpace(axes, catalog, tags));
        }
    }
}
