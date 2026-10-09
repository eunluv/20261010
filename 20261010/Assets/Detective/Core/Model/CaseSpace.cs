using System;
using System.Collections.Generic;
using System.Text;

namespace Detective.Core.Model
{
    // 한 사건의 정답 공간: 축 목록, 축별 옵션 수, 옵션별 태그.
    // 정답 후보는 축마다 고른 옵션 번호 배열(int[] picks)로 표현한다. picks[a] = 축 a의 옵션 번호.
    // 만들어진 뒤에는 바뀌지 않는다.
    public sealed class CaseSpace
    {
        static readonly string[] NoTags = new string[0];

        readonly AxisDef[] axes;
        readonly int[] optionCounts;
        readonly string[][][] optionTags; // [축][옵션] → Ordinal 정렬된 태그 id
        readonly Dictionary<string, string> categoryByTag = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly HashSet<string> categories = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<AxisDef> Axes => axes;
        public int AxisCount => axes.Length;
        public int CombinationCount { get; }

        public CaseSpace(IEnumerable<AxisDef> axes, TagCatalog tagCatalog = null, OptionTagTable optionTags = null)
        {
            if (axes == null) throw new ArgumentNullException(nameof(axes));
            this.axes = new List<AxisDef>(axes).ToArray();
            if (this.axes.Length == 0) throw new ArgumentException("축이 하나도 없습니다.", nameof(axes));

            optionCounts = new int[this.axes.Length];
            var seenAxes = new HashSet<string>(StringComparer.Ordinal);
            int combinations = 1;
            for (int a = 0; a < this.axes.Length; a++)
            {
                var axis = this.axes[a];
                if (axis == null) throw new ArgumentException($"{a}번 축이 null입니다.", nameof(axes));
                if (!seenAxes.Add(axis.Id)) throw new ArgumentException($"축 '{axis.Id}'가 중복되었습니다.", nameof(axes));
                optionCounts[a] = axis.OptionCount;
                combinations = checked(combinations * axis.OptionCount);
            }
            CombinationCount = combinations;

            if (tagCatalog != null)
            {
                foreach (var entry in tagCatalog.Entries)
                {
                    categoryByTag.Add(entry.TagId, entry.CategoryId);
                    categories.Add(entry.CategoryId);
                }
            }

            var sets = new SortedSet<string>[this.axes.Length][];
            for (int a = 0; a < this.axes.Length; a++) sets[a] = new SortedSet<string>[optionCounts[a]];

            if (optionTags != null)
            {
                foreach (var entry in optionTags.Entries)
                {
                    int a = AxisIndex(entry.AxisId);
                    if (a < 0) throw new ArgumentException($"태그표에 없는 축 '{entry.AxisId}'가 있습니다.", nameof(optionTags));
                    int o = this.axes[a].IndexOf(entry.OptionId);
                    if (o < 0)
                        throw new ArgumentException($"태그표에 축 '{entry.AxisId}'에 없는 옵션 '{entry.OptionId}'가 있습니다.", nameof(optionTags));
                    if (!categoryByTag.ContainsKey(entry.TagId))
                        throw new ArgumentException($"태그 '{entry.TagId}'가 태그 사전에 없습니다.", nameof(optionTags));
                    if (sets[a][o] == null) sets[a][o] = new SortedSet<string>(StringComparer.Ordinal);
                    sets[a][o].Add(entry.TagId);
                }
            }

            this.optionTags = new string[this.axes.Length][][];
            for (int a = 0; a < this.axes.Length; a++)
            {
                this.optionTags[a] = new string[optionCounts[a]][];
                for (int o = 0; o < optionCounts[a]; o++)
                {
                    var set = sets[a][o];
                    if (set == null)
                    {
                        this.optionTags[a][o] = NoTags;
                        continue;
                    }
                    var arr = new string[set.Count];
                    set.CopyTo(arr);
                    this.optionTags[a][o] = arr;
                }
            }
        }

        public int OptionCount(int axis)
        {
            CheckAxis(axis, nameof(axis));
            return optionCounts[axis];
        }

        public int[] GetOptionCounts() => (int[])optionCounts.Clone();

        // 축 번호를 돌려준다. 없으면 -1.
        public int AxisIndex(string axisId)
        {
            for (int a = 0; a < axes.Length; a++)
                if (string.Equals(axes[a].Id, axisId, StringComparison.Ordinal)) return a;
            return -1;
        }

        public string AxisId(int axis)
        {
            CheckAxis(axis, nameof(axis));
            return axes[axis].Id;
        }

        public string OptionId(int axis, int option)
        {
            CheckOption(axis, option, nameof(option));
            return axes[axis].OptionIds[option];
        }

        public IReadOnlyList<string> TagsOf(int axis, int option)
        {
            CheckOption(axis, option, nameof(option));
            return optionTags[axis][option];
        }

        public bool HasTag(int axis, int option, string tagId)
        {
            CheckOption(axis, option, nameof(option));
            return Array.BinarySearch(optionTags[axis][option], tagId, StringComparer.Ordinal) >= 0;
        }

        public bool IsKnownTag(string tagId) => tagId != null && categoryByTag.ContainsKey(tagId);

        public bool IsKnownCategory(string categoryId) => categoryId != null && categories.Contains(categoryId);

        public string CategoryOf(string tagId)
        {
            if (tagId == null || !categoryByTag.TryGetValue(tagId, out var category))
                throw new ArgumentException($"태그 '{tagId}'가 태그 사전에 없습니다.", nameof(tagId));
            return category;
        }

        // 두 옵션이 지정 카테고리의 태그를 하나라도 함께 갖는가.
        public bool ShareCategoryTag(int axisA, int optionA, int axisB, int optionB, string categoryId)
        {
            CheckOption(axisA, optionA, nameof(optionA));
            CheckOption(axisB, optionB, nameof(optionB));
            foreach (var tag in optionTags[axisA][optionA])
            {
                if (!string.Equals(categoryByTag[tag], categoryId, StringComparison.Ordinal)) continue;
                if (Array.BinarySearch(optionTags[axisB][optionB], tag, StringComparer.Ordinal) >= 0) return true;
            }
            return false;
        }

        // 디버그용: "culprit=drama, place=storage, item=cart"
        public string Describe(int[] picks)
        {
            if (picks == null) throw new ArgumentNullException(nameof(picks));
            if (picks.Length != axes.Length)
                throw new ArgumentException($"picks 길이 {picks.Length}가 축 수 {axes.Length}와 다릅니다.", nameof(picks));
            var sb = new StringBuilder();
            for (int a = 0; a < axes.Length; a++)
            {
                if (a > 0) sb.Append(", ");
                sb.Append(axes[a].Id).Append('=').Append(OptionId(a, picks[a]));
            }
            return sb.ToString();
        }

        internal void CheckAxis(int axis, string paramName)
        {
            if (axis < 0 || axis >= axes.Length)
                throw new ArgumentOutOfRangeException(paramName, axis, $"축 번호는 0~{axes.Length - 1} 범위여야 합니다.");
        }

        internal void CheckOption(int axis, int option, string paramName)
        {
            CheckAxis(axis, nameof(axis));
            if (option < 0 || option >= optionCounts[axis])
                throw new ArgumentOutOfRangeException(paramName, option,
                    $"축 '{axes[axis].Id}'의 옵션 번호는 0~{optionCounts[axis] - 1} 범위여야 합니다.");
        }

        internal void RequireTag(string tagId, string paramName)
        {
            if (!IsKnownTag(tagId)) throw new ArgumentException($"태그 '{tagId}'가 태그 사전에 없습니다.", paramName);
        }

        internal void RequireCategory(string categoryId, string paramName)
        {
            if (!IsKnownCategory(categoryId))
                throw new ArgumentException($"카테고리 '{categoryId}'가 태그 사전에 없습니다.", paramName);
        }
    }
}
