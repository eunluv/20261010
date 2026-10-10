namespace Detective.Runtime
{
    // 에디터의 "이 시드로 플레이"가 플레이 모드로 넘기는 값의 EditorPrefs 키.
    public static class PrototypeLaunch
    {
        public const string ScenePath = "Assets/Scenes/Prototype.unity";

        public const string PendingKey = "Detective.Prototype.Pending";       // bool: 넘길 값이 있다 (한 번 읽으면 지운다)
        public const string SeedKey = "Detective.Prototype.Seed";             // int
        public const string PackGuidKey = "Detective.Prototype.PackGuid";     // string: WorldPack 에셋 GUID
        public const string TemplateGuidKey = "Detective.Prototype.TemplateGuid"; // string: CaseTemplate 에셋 GUID
        public const string DifficultyKey = "Detective.Prototype.Difficulty"; // int: 0 = 템플릿 값, 1~3 = Easy/Normal/Hard
    }
}
