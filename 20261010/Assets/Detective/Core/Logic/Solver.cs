using System;
using System.Collections;
using System.Collections.Generic;
using Detective.Core.Model;

namespace Detective.Core.Logic
{
    // 모든 정답 후보 조합을 한 번 나열해 두고, 조건마다 "살아남는 조합" 비트마스크로 남은 후보를 계산한다.
    // 조합 순서는 마지막 축이 가장 빠르게 바뀌는 사전식 순서로 고정된다.
    public sealed class Solver
    {
        readonly int[][] combos;

        public int AxisCount { get; }
        public int ComboCount => combos.Length;

        public Solver(CaseSpace space)
            : this((space ?? throw new ArgumentNullException(nameof(space))).GetOptionCounts())
        {
        }

        public Solver(IReadOnlyList<int> optionCounts)
        {
            if (optionCounts == null) throw new ArgumentNullException(nameof(optionCounts));
            if (optionCounts.Count == 0) throw new ArgumentException("축이 하나도 없습니다.", nameof(optionCounts));

            var counts = new int[optionCounts.Count];
            int total = 1;
            for (int a = 0; a < counts.Length; a++)
            {
                if (optionCounts[a] <= 0)
                    throw new ArgumentException($"{a}번 축의 옵션 수는 1 이상이어야 합니다.", nameof(optionCounts));
                counts[a] = optionCounts[a];
                total = checked(total * counts[a]);
            }

            AxisCount = counts.Length;
            combos = Enumerate(counts, total);
        }

        static int[][] Enumerate(int[] counts, int total)
        {
            var result = new int[total][];
            var current = new int[counts.Length];
            for (int i = 0; i < total; i++)
            {
                result[i] = (int[])current.Clone();
                // 마지막 축부터 1씩 올리고 넘치면 앞 축으로 올림.
                for (int a = counts.Length - 1; a >= 0; a--)
                {
                    if (++current[a] < counts[a]) break;
                    current[a] = 0;
                }
            }
            return result;
        }

        // index번째 조합의 복사본.
        public int[] GetCombo(int index)
        {
            if (index < 0 || index >= combos.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"조합 번호는 0~{combos.Length - 1} 범위여야 합니다.");
            return (int[])combos[index].Clone();
        }

        public BitArray AllAlive() => new BitArray(combos.Length, true);

        // 조건이 참인 조합만 true인 마스크.
        public BitArray MaskOf(IConstraint constraint)
        {
            if (constraint == null) throw new ArgumentNullException(nameof(constraint));
            var mask = new BitArray(combos.Length);
            for (int i = 0; i < combos.Length; i++) mask[i] = constraint.Holds(combos[i]);
            return mask;
        }

        // alive에 조건을 적용한 새 마스크. alive는 바뀌지 않는다.
        public BitArray Apply(BitArray alive, IConstraint constraint)
        {
            CheckMask(alive, nameof(alive));
            return new BitArray(alive).And(MaskOf(constraint));
        }

        // alive에 조건들을 차례로 적용한 새 마스크. alive는 바뀌지 않는다.
        public BitArray Apply(BitArray alive, IEnumerable<IConstraint> constraints)
        {
            CheckMask(alive, nameof(alive));
            if (constraints == null) throw new ArgumentNullException(nameof(constraints));
            var result = new BitArray(alive);
            foreach (var constraint in constraints) result.And(MaskOf(constraint));
            return result;
        }

        public int CountAlive(BitArray alive)
        {
            CheckMask(alive, nameof(alive));
            int count = 0;
            for (int i = 0; i < alive.Length; i++)
                if (alive[i]) count++;
            return count;
        }

        // 살아 있는 조합의 복사본 목록 (조합 번호 순).
        public List<int[]> AliveCombos(BitArray alive)
        {
            CheckMask(alive, nameof(alive));
            var result = new List<int[]>();
            for (int i = 0; i < alive.Length; i++)
                if (alive[i]) result.Add((int[])combos[i].Clone());
            return result;
        }

        // alive 중 mask가 지우는(false인) 조합 수.
        public int CountEliminated(BitArray alive, BitArray mask)
        {
            CheckMask(alive, nameof(alive));
            CheckMask(mask, nameof(mask));
            int count = 0;
            for (int i = 0; i < alive.Length; i++)
                if (alive[i] && !mask[i]) count++;
            return count;
        }

        // alive 중 조건이 지우는 조합 수.
        public int CountEliminated(BitArray alive, IConstraint constraint) => CountEliminated(alive, MaskOf(constraint));

        void CheckMask(BitArray mask, string paramName)
        {
            if (mask == null) throw new ArgumentNullException(paramName);
            if (mask.Length != combos.Length)
                throw new ArgumentException($"마스크 길이 {mask.Length}가 조합 수 {combos.Length}와 다릅니다.", paramName);
        }
    }
}
