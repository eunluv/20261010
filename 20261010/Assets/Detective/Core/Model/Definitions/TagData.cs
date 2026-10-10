namespace Detective.Core.Model
{
    // TagDef 에셋의 순수 데이터판.
    public sealed class TagData
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Category { get; }

        public TagData(string id, string displayName, string category)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
        }

        public override string ToString() => $"{Id} ({Category})";
    }
}
