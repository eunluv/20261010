# CLAUDE.md

랜덤 생성 추리 게임 프로토타입. 기획: [Docs/design.md](Docs/design.md), 구현 설계: [Docs/tool-design.md](Docs/tool-design.md).

## 설계 핵심 요약

- **3층 구조**: 코어(순수 C#: 축·조건·솔버·생성기·진행 상태) / 데이터(ScriptableObject 폼) / 표현(UI·연출·네트워크). 새 사건·팩 추가에 코드 수정이 없어야 한다.
- **축(Axis)**: 맞혀야 할 항목(culprit, place, item, 긴 사건은 motive, accomplice). 정답 후보 = 축마다 옵션 하나씩 고른 조합 `int[] picks`.
- **조건(IConstraint)**: 후보 조합 하나에 참/거짓을 돌려주는 규칙. 단서 하나 = 조건 하나. 코드에는 `IsNot`, `HasTag`, `LacksTag`, `SameTag`, `Implies`, `OneOf`, `NotTogether` 7종만 둔다. `ClueRule`은 파라미터를 비워 둔 틀이고, 생성 시 진짜 정답에 참인 값만 채운다.
- **솔버**: 모든 후보 조합을 나열하고 조건별 생존 비트마스크를 미리 계산해 AND로 남은 후보 수를 센다. 1개 = 풀 수 있음, 0개 = 버그. 힌트·복기에도 재사용.
- **생성기**: 시드 → 축 옵션 뽑기 → 진실 → 규칙 확장(정답에 참인 것만) → 후보가 1개가 될 때까지 단서 선택 → 군더더기 제거 → 가짜 단서 추가. 막히면 결정적으로 파생된 시드로 최대 50회 재시도.
- **타임라인·증언**: 사실(Presence/Saw/Handled) 기록에서 단서 문장·증언·반박 증거가 나온다. 거짓 줄마다 반박 증거를 만들고, 반박 성공 시 잠긴 핵심 단서가 열린다.
- **페이즈 러너**: `CaseFlow` 에셋의 페이즈 목록(Dialogue/Investigate/Testimony/Event/Accuse/Result)을 순서대로 실행. 캐릭터 대신 역할 슬롯을 쓰고, `CaseSession`이 상태를 갖고 UI는 이벤트만 구독한다.

## 엔진

- Unity 6.6 (6000.6.0f1), Universal 2D 템플릿.
- C# 언어 버전은 이 Unity 버전이 지원하는 범위(C# 9.0)만 사용. file-scoped namespace, global using, record struct 등 C# 10 이상 문법 금지.

## 폴더와 어셈블리

- `Assets/Detective/{Core, Data, Runtime, Editor, Tests}`, `Assets/Packs/<팩이름>`. Detective 하위 폴더는 각각 asmdef로 분리.
  - `Detective.Core`: UnityEngine 참조 금지(`noEngineReferences: true`). 순수 C#.
  - `Detective.Data`: Core 참조. ScriptableObject 정의와 Core 변환기.
  - `Detective.Runtime`: Core, Data 참조.
  - `Detective.Editor`: Editor 플랫폼 전용. Core, Data, Runtime 참조.
  - `Detective.Tests`: EditMode 테스트. Core, Data, Runtime 참조(Runtime은 `CaseSession`·`FlowRunner` 자동 플레이 테스트용), Unity Test Framework 사용.
- 팩은 데이터 에셋만 담는다. 새 팩 = `Assets/Packs/` 아래 폴더 하나 추가.

## 결정성

- 랜덤은 시드를 받은 `System.Random`만 사용. `UnityEngine.Random` 금지.
- `Dictionary`/`HashSet` 순회 순서에 의존하지 않는다. 목록은 id 순으로 정렬해 둔다.

## 콘텐츠

- 엔진 코드는 특정 캐릭터·장소·사건을 하드코딩하지 않는다. 콘텐츠는 전부 ScriptableObject 데이터.
- `.meta`, `.unity`, `.asset`, `.prefab` 파일을 직접 편집하지 않는다. 에셋이 필요하면 `AssetDatabase`를 쓰는 에디터 메뉴 스크립트를 만들어 사람이 실행하게 한다.

## 코딩 스타일

- 식별자는 영어. 주석과 사용자에게 보이는 문자열은 한국어 가능.

## 테스트

Unity 에디터가 닫혀 있어야 한다.

```
"<UNITY_PATH>" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -
```

- `<UNITY_PATH>` 예: `C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe`
- 실행 후 `TestResults.xml`에서 실패 항목(`result="Failed"`)을 확인한다. `TestResults.xml`은 `.gitignore`에 있다.

## 작업 방식

- 한 번에 한 단계만 한다.
- 끝나면 무엇을 만들었는지, 사람이 Unity에서 확인할 것이 무엇인지 짧게 정리한다.
