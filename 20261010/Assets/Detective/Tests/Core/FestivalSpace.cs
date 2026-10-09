using Detective.Core.Model;

namespace Detective.Tests.Core
{
    // 테스트용 3축 4×4×4 사건 공간 (학원 축제 팩을 줄인 것).
    //
    // 범인(culprit)  : drama[club.drama, floor.2, trait.tall]  council[club.council, floor.1]
    //                  cook[club.cooking, floor.1]               broadcast[club.broadcast, floor.2, trait.lefty]
    // 장소(place)    : backstage[club.drama, floor.1, env.indoor] storage[club.council, floor.1, env.indoor]
    //                  kitchen[club.cooking, floor.1, env.indoor] rooftop[club.broadcast, floor.roof, env.outdoor]
    // 도구(item)     : costume_rack[club.drama] big_pot[club.cooking] cart[club.broadcast] doc_box[club.council]
    internal static class FestivalSpace
    {
        public const int Culprit = 0, Place = 1, Item = 2;

        public const int Drama = 0, Council = 1, Cook = 2, Broadcast = 3;
        public const int Backstage = 0, Storage = 1, Kitchen = 2, Rooftop = 3;
        public const int CostumeRack = 0, BigPot = 1, Cart = 2, DocBox = 3;

        public static CaseSpace Create()
        {
            var axes = new[]
            {
                new AxisDef("culprit", new[] { "drama", "council", "cook", "broadcast" }),
                new AxisDef("place", new[] { "backstage", "storage", "kitchen", "rooftop" }),
                new AxisDef("item", new[] { "costume_rack", "big_pot", "cart", "doc_box" }),
            };

            var catalog = new TagCatalog()
                .Add("club.drama", "club")
                .Add("club.council", "club")
                .Add("club.cooking", "club")
                .Add("club.broadcast", "club")
                .Add("floor.1", "floor")
                .Add("floor.2", "floor")
                .Add("floor.roof", "floor")
                .Add("trait.tall", "trait")
                .Add("trait.lefty", "trait")
                .Add("env.indoor", "env")
                .Add("env.outdoor", "env");

            var tags = new OptionTagTable()
                .Add("culprit", "drama", "club.drama", "floor.2", "trait.tall")
                .Add("culprit", "council", "club.council", "floor.1")
                .Add("culprit", "cook", "club.cooking", "floor.1")
                .Add("culprit", "broadcast", "club.broadcast", "floor.2", "trait.lefty")
                .Add("place", "backstage", "club.drama", "floor.1", "env.indoor")
                .Add("place", "storage", "club.council", "floor.1", "env.indoor")
                .Add("place", "kitchen", "club.cooking", "floor.1", "env.indoor")
                .Add("place", "rooftop", "club.broadcast", "floor.roof", "env.outdoor")
                .Add("item", "costume_rack", "club.drama")
                .Add("item", "big_pot", "club.cooking")
                .Add("item", "cart", "club.broadcast")
                .Add("item", "doc_box", "club.council");

            return new CaseSpace(axes, catalog, tags);
        }

        public static int[] P(int culprit, int place, int item) => new[] { culprit, place, item };
    }
}
