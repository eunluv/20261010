using System;
using System.Collections.Generic;

namespace Detective.Data
{
    // 변환을 할 수 없는 구조적 오류(빈 id, 중복 id, 끊긴 참조 등). 발견한 오류를 모두 담는다.
    public class PackConversionException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public PackConversionException(IReadOnlyList<string> errors)
            : base($"팩 변환 실패 ({errors.Count}건):\n- " + string.Join("\n- ", errors))
        {
            Errors = errors;
        }
    }
}
