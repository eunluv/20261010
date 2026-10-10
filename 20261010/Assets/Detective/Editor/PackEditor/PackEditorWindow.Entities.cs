using Detective.Core.Model;
using Detective.Data;
using UnityEditor;
using UnityEngine;

namespace Detective.Editor
{
    // 엔티티 탭: 왼쪽 목록에서 고르고 오른쪽에서 고친다. 용의자는 프로필(말버릇·거짓말 성향)도 함께 다룬다.
    public partial class PackEditorWindow
    {
        static readonly string[] EntityFilterNames = { "전체", "용의자", "장소", "도구", "동기" };
        static readonly EntityKind[] EntityKinds = { EntityKind.Suspect, EntityKind.Place, EntityKind.Item, EntityKind.Motive };

        void DrawEntitiesTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(ListWidth)))
                {
                    int filter = GUILayout.Toolbar(entityFilter, EntityFilterNames);
                    if (filter != entityFilter) PackGui.Later(() => entityFilter = filter);

                    listScroll = EditorGUILayout.BeginScrollView(listScroll);
                    foreach (var entity in pack.entities)
                    {
                        if (entity == null) continue;
                        if (entityFilter > 0 && entity.kind != EntityKinds[entityFilter - 1]) continue;
                        var e = entity;
                        string text = entityFilter == 0
                            ? $"{PackAssetOps.KindLabel(e.kind)} · {PackGui.Name(e.displayName, e.id)}"
                            : PackGui.Name(e.displayName, e.id);
                        ListItem(e, selectedEntity == e, text, () => { selectedEntity = e; detailScroll = Vector2.zero; });
                    }
                    EditorGUILayout.EndScrollView();

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        foreach (var kind in EntityKinds)
                        {
                            var k = kind;
                            if (GUILayout.Button("+ " + PackAssetOps.KindLabel(k), GUILayout.Height(24)))
                                PackGui.Later(() =>
                                {
                                    selectedEntity = PackAssetOps.CreateEntity(pack, k);
                                    if (entityFilter > 0) entityFilter = System.Array.IndexOf(EntityKinds, k) + 1;
                                    needsSync = true;
                                });
                        }
                    }
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                    if (selectedEntity == null || !pack.entities.Contains(selectedEntity))
                        EditorGUILayout.HelpBox("왼쪽에서 엔티티를 고르거나, 아래 버튼으로 새로 만드세요.", MessageType.Info);
                    else
                        DrawEntityDetail(selectedEntity);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawEntityDetail(EntityDef e)
        {
            var profile = FindProfile(e);
            DrawProblemsFor(e, profile);

            Header("기본");
            PackGui.Text(e, PackGui.L("Id", "팩 안에서 겹치지 않는 고유 id. 예: drama_captain"), e.id, v =>
            {
                e.id = v.Trim();
                PackAssetOps.RenameToId(e, PackAssetOps.EntityPrefix, e.id);
                PackAssetOps.RenameToId(FindProfile(e), PackAssetOps.SuspectPrefix, e.id);
                needsSync = true;
            }, true);
            PackGui.Text(e, PackGui.L("표시 이름", "화면에 보일 이름. 예: 연극부 부장"), e.displayName, v => e.displayName = v);
            PackGui.EnumField(e, PackGui.L("종류", "어떤 축의 후보가 되는지 정합니다"), e.kind, v =>
            {
                e.kind = v;
                if (v == EntityKind.Suspect && FindProfile(e) == null) PackAssetOps.CreateProfile(pack, e);
                needsSync = true;
            });
            PackGui.TagList(e, PackGui.L("태그", "이 엔티티가 가진 태그. 누르면 체크해서 고를 수 있습니다"), e.tags, pack.tags);

            Header("표시");
            PackGui.ObjectField(e, PackGui.L("초상화", "비워 둬도 됩니다"), e.portrait, v => e.portrait = v);
            PackGui.Area(e, PackGui.L("설명", "수첩·프로필에 보일 설명"), e.description, v => e.description = v, 3);

            if (e.kind == EntityKind.Suspect)
            {
                Header("용의자 프로필");
                if (profile == null)
                {
                    if (GUILayout.Button("용의자 프로필 만들기", GUILayout.Width(180)))
                        PackGui.Later(() => { PackAssetOps.CreateProfile(pack, e); needsSync = true; });
                }
                else
                {
                    PackGui.Text(profile, PackGui.L("말버릇", "증언과 잡담에 섞여 나오는 말버릇"), profile.catchphrase, v => profile.catchphrase = v);
                    PackGui.EnumField(profile, PackGui.L("거짓말 성향",
                            "Liar: 사실 하나를 바꿔 말함\nOmitter: 하나를 빼고 추궁 시 공개\nExaggerator: 사실이지만 과장\nHonest: 그대로 말함\n(범인인지와는 상관없습니다)"),
                        profile.lieStyle, v => profile.lieStyle = v);
                    EditorGUILayout.LabelField(PackGui.L("잡담 대사", "잡담 행동에서 나오는 대사"));
                    PackGui.StringList(profile, profile.smallTalkLines, "+ 대사 추가", true);
                }
            }
            else if (profile != null)
            {
                Header("용의자 프로필");
                EditorGUILayout.HelpBox("종류가 용의자가 아닌데 용의자 프로필이 남아 있습니다.", MessageType.Warning);
                if (GUILayout.Button("프로필 삭제", GUILayout.Width(120)))
                    ConfirmLater("프로필 삭제", "남아 있는 용의자 프로필을 지웁니다. 파일은 휴지통으로 이동합니다.",
                        () => PackAssetOps.DeleteProfile(pack, profile));
            }

            EditorGUILayout.Space(16);
            if (GUILayout.Button("이 엔티티 삭제", GUILayout.Width(140)))
            {
                string message = $"'{PackGui.Name(e.displayName, e.id)}'를 지웁니다.";
                if (profile != null) message += "\n용의자 프로필도 함께 지웁니다.";
                int uses = RuleUseCount(e);
                if (uses > 0) message += $"\n단서 규칙 {uses}개에서 이 엔티티 연결이 빠집니다.";
                message += "\n파일은 휴지통으로 이동합니다.";
                ConfirmLater("엔티티 삭제", message, () =>
                {
                    PackAssetOps.DeleteEntity(pack, e);
                    selectedEntity = null;
                });
            }
        }

        SuspectProfile FindProfile(EntityDef entity)
        {
            foreach (var profile in pack.suspectProfiles)
                if (profile != null && profile.entity == entity) return profile;
            return null;
        }

        int RuleUseCount(EntityDef entity)
        {
            int count = 0;
            foreach (var rule in pack.rules)
                if (rule != null && (rule.optionX == entity || rule.optionY == entity)) count++;
            return count;
        }
    }
}
