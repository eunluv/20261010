# 재사용 사건 툴 설계 (Unity)

## 1. 핵심 개념

**엔진 코드는 특정 사건·캐릭터·장소를 절대 직접 알지 못한다.** 모든 콘텐츠는 Unity의 ScriptableObject 에셋(인스펙터에서 채우는 폼)으로 만들고, 엔진은 그 폼을 읽어서 사건을 배정하고 진행시킨다. 새 사건이나 새 세계관 팩을 추가할 때 코드를 고치지 않는 것이 목표다.

세 층으로 나눈다.

| 층 | 내용 | 누가 다루나 | 바뀌는 빈도 |
| --- | --- | --- | --- |
| 코어 (순수 C#) | 정답 축, 조건, 솔버, 생성기, 진행 상태 | 프로그래머 | 거의 안 바뀜 |
| 데이터 (ScriptableObject) | 캐릭터, 장소, 단서 규칙, 사건 템플릿, 진행 흐름, 대사 | 기획·시나리오 | 계속 늘어남 |
| 표현 (Unity 런타임) | UI, 연출, 사운드, 네트워크 | 프로그래머·아티스트 | 가끔 |

핵심 설계 결정 세 가지:

1. **사건 논리를 "정답 축 + 조건"으로 일반화한다.** 범인·장소·도구를 하드코딩하지 않고 "축"이라는 개념으로 다뤄서, 긴 사건에 동기·공범 축을 추가해도 코드가 그대로다 (4장).
2. **조건 종류는 코드에 소수만 두고, 조합은 데이터로 한다.** "X가 아니다", "태그 P를 가진다", "A면 B다" 같은 6\~7개 조건 타입만 코드로 만들고, 어떤 단서가 어떤 조건을 쓰는지는 폼에서 고른다.
3. **진행 흐름도 데이터다.** 인트로 → 조사 → 증언 → 지목 같은 순서를 "페이즈 목록" 에셋으로 정의해서, 짧은 사건과 긴 사건은 다른 흐름 에셋을 쓸 뿐이다 (7장).

## 2. 제작 흐름

새 사건을 추가하는 사람은 코드를 열지 않고 아래 순서만 따른다.

1. **엔티티 폼 채우기**: 용의자, 장소, 도구 에셋을 만들고 이름·태그·일러스트를 넣는다.
2. **단서 규칙 고르기**: 단서 규칙 에셋에서 조건 타입을 고르고 문장 변형을 쓴다. 기존 규칙은 다른 팩에서도 재사용한다.
3. **사건 템플릿 만들기**: 어떤 축을 쓸지, 각 축의 후보 풀(태그로 필터), 난이도 값, 진행 흐름 에셋을 지정한다.
4. **검증 버튼 누르기**: 빠진 문장 변수, 쓸 수 없는 규칙, 후보가 부족한 축 같은 오류를 에디터가 알려준다.
5. **미리보기 창에서 시드 돌리기**: 시드를 넣으면 진실, 단서 목록, 후보가 줄어드는 과정, 증언과 거짓말이 바로 보인다.
6. **대량 테스트**: 시드 1,000개를 돌려 생성 성공률, 평균 단서 수, 생성 시간을 확인한다.
7. **플레이 버튼**: 같은 시드로 실제 게임 화면에서 바로 플레이해 본다.

## 3. 데이터 에셋 (채워 넣을 폼)

폼은 7종이면 충분하다. 모두 `[CreateAssetMenu]`로 만들어 프로젝트 창 우클릭으로 생성한다.

| 에셋 | 역할 | 주요 필드 |
| --- | --- | --- |
| `EntityDef` | 용의자·장소·도구 등 모든 "후보"의 공통 폼 | id, 표시 이름, 종류(Suspect/Place/Item/Motive), 태그 목록, 일러스트·표정, 설명 |
| `SuspectProfile` | 용의자에만 붙는 추가 정보 | 대상 EntityDef, 말버릇, 거짓말 성향(Liar/Omitter/Exaggerator/Honest), 잡담 대사 |
| `TagDef` | 태그 사전 | id, 표시 이름, 카테고리(동아리, 층, 손잡이 등) |
| `ClueRule` | 단서 한 종류의 논리 + 문장 | 조건 타입, 대상 축, 파라미터, 문장 변형 목록, 획득 경로(조사/탐문/잡담), 난이도 가중치 |
| `CaseTemplate` | 사건 하나의 틀 | 제목·의뢰 문장, 축 목록(축 이름, 후보 종류, 필수 태그, 뽑을 수), 사용 단서 규칙, 시간대 목록, 행동력, 거짓 증언 수, 가짜 단서 수, 진행 흐름 |
| `CaseFlow` | 진행 순서 | 페이즈 목록 (7장) |
| `WorldPack` | 팩 묶음 | 위 에셋들의 목록, 고유 규칙 모듈, 대사 테이블 |

**설계 포인트**

- 용의자·장소·도구를 별도 클래스로 나누지 않고 `EntityDef` 하나 + 종류 값으로 둔다. 그래야 솔버가 "축의 후보"로 똑같이 다룰 수 있다.
- 태그는 문자열 대신 `TagDef` 에셋 참조로 둔다. 오타가 나면 연결이 끊긴 게 바로 보인다.
- 문장은 `{suspect}`, `{place}`, `{time}` 같은 변수를 쓴다. 검증 버튼이 템플릿이 쓰는 변수와 규칙이 채워줄 수 있는 변수를 대조한다.
- 다국어를 고려해 문장 필드는 나중에 Unity Localization 패키지의 문자열 키로 바꿀 수 있게, 처음부터 `LocalizedText` 같은 래퍼 타입으로 감싸 둔다.

## 4. 정답 축과 단서 조건

사건 논리는 **축(Axis)**, **정답 후보(Candidate)**, **조건(Constraint)** 세 개념으로 표현한다.

- **축**: 플레이어가 맞혀야 할 항목. 짧은 사건은 `culprit`, `place`, `item` 3축, 긴 사건은 `motive`, `accomplice`를 추가한다.
- **정답 후보**: 축마다 후보 하나씩 고른 조합. 5×5×5 사건이면 125개.
- **조건**: 정답 후보 하나를 받아 참/거짓을 돌려주는 규칙. 단서 하나 = 조건 하나.

### 4-1. 조건 타입 (코드에 고정)

이 목록만 코드로 구현하고, 나머지는 전부 `ClueRule` 폼의 선택값으로 해결한다.

| 조건 타입 | 의미 | 파라미터 | 단서 예시 |
| --- | --- | --- | --- |
| `IsNot` | 축 A의 정답은 X가 아니다 | 축, 엔티티 | 연극부 부장은 그 시간에 무대 위에 있었다 |
| `HasTag` | 축 A의 정답은 태그 T를 가진다 | 축, 태그 | 범인은 2층 동아리 소속이다 |
| `LacksTag` | 축 A의 정답은 태그 T가 없다 | 축, 태그 | 숨긴 곳은 실외가 아니다 |
| `SameTag` | 축 A와 축 B의 정답이 같은 카테고리 태그를 공유 | 축 A, 축 B, 태그 카테고리 | 사라진 도구는 숨긴 장소의 비품이었다 |
| `Implies` | A가 X면 B는 Y다 | 축 A, X, 축 B, Y | 방송부원이 옥상에 가는 걸 누가 봤대 |
| `OneOf` | 축 A의 정답은 X 또는 Y | 축, 엔티티 2개 | 범인은 키가 큰 두 사람 중 하나다 |
| `NotTogether` | A가 X면 B는 Y가 아니다 | 축 A, X, 축 B, Y | 조리실에서 앰프 케이스를 옮기는 건 불가능하다 |

### 4-2. 단서 규칙이 실제 단서가 되는 과정

`ClueRule`은 파라미터 일부를 **비워 두는 틀**이다. 예를 들어 `IsNot(culprit, ?)` 규칙은 "범인이 아닌 누군가"를 생성 시점에 채운다.

1. 생성기가 규칙의 빈 파라미터에 들어갈 수 있는 모든 값을 나열한다.
2. 그중 **진짜 정답에 대해 참인 것만** 남긴다. (범인이 아닌 사람만 `IsNot`의 대상이 될 수 있다.)
3. 남은 것이 이 사건의 "단서 후보"가 된다.

이렇게 하면 기획자는 "알리바이형 단서" 규칙 하나만 만들어도, 사건마다 다른 사람의 알리바이가 자동으로 생긴다. 거짓 단서는 이 단계에서 원천적으로 생길 수 없다.

## 5. 생성기와 솔버 구현

코어는 `UnityEngine`을 참조하지 않는 순수 C#으로 만든다. 그래야 에디터 창, 런타임, 유닛 테스트, 나중의 방장 서버 로직이 같은 코드를 쓴다. ScriptableObject는 런타임 시작 시 코어용 데이터로 한 번 변환한다.

### 5-1. 조건 인터페이스

정답 후보는 축별로 고른 옵션 번호 배열(`int[] picks`)로 표현한다. `picks[0]`이 범인 번호, `picks[1]`이 장소 번호인 식이다.

```csharp
public interface IConstraint
{
    bool Holds(int[] picks);          // 이 후보 조합에서 단서가 참인가
    ClueText Render(CaseContext ctx); // 표시용 문장
}

public sealed class IsNot : IConstraint
{
    readonly int axis, option;
    public IsNot(int axis, int option) { this.axis = axis; this.option = option; }
    public bool Holds(int[] p) => p[axis] != option;
    // Render 생략
}

public sealed class HasTag : IConstraint
{
    readonly int axis; readonly bool[] optionHasTag; // 옵션별 태그 보유 여부 미리 계산
    public bool Holds(int[] p) => optionHasTag[p[axis]];
}

public sealed class Implies : IConstraint
{
    readonly int a, x, b, y;
    public bool Holds(int[] p) => p[a] != x || p[b] == y;
}
```

### 5-2. 솔버

후보 조합을 사건 생성 시 한 번 전부 나열하고, 단서마다 "살아남는 조합" 비트마스크를 미리 계산한다. 남은 후보 계산은 마스크 AND 한 번이다. 긴 사건(약 1,000\~2,000개 조합)에서도 충분히 빠르다.

```csharp
public sealed class Solver
{
    readonly List<int[]> combos;              // 모든 정답 후보 조합
    public Solver(int[] optionCounts) { combos = Enumerate(optionCounts); }

    public BitArray AllAlive() => new BitArray(combos.Count, true);

    public BitArray MaskOf(IConstraint c)
    {
        var m = new BitArray(combos.Count);
        for (int i = 0; i < combos.Count; i++) m[i] = c.Holds(combos[i]);
        return m;
    }

    public int CountAlive(BitArray alive) { /* true 개수 */ }
}
```

같은 솔버로 힌트 기능("지금 단서로 지울 수 있는 후보가 있다")과 복기 화면("이 단서가 후보를 몇 개 지웠다")도 만든다.

### 5-3. 생성기

```csharp
public CaseInstance TryGenerate(CaseTemplateData t, int seed)
{
    var rng   = new System.Random(seed);
    var axes  = PickOptions(t, rng);                  // 축마다 후보 N개 뽑기 (태그 필터)
    var truth = axes.Select(a => rng.Next(a.Count)).ToArray();

    var pool = ExpandRules(t.Rules, axes)             // 빈 파라미터 채워 단서 후보 생성
                 .Where(c => c.Holds(truth))           // 정답에 참인 것만
                 .ToList();

    var solver = new Solver(axes.Select(a => a.Count).ToArray());
    var alive  = solver.AllAlive();
    var chosen = new List<IConstraint>();

    while (solver.CountAlive(alive) > 1)
    {
        var useful = pool.Where(c => Eliminates(alive, solver.MaskOf(c)) > 0).ToList();
        if (useful.Count == 0) return null;           // 막힘 → 호출부에서 다음 시드
        var pick = PickByDifficulty(useful, alive, t.Difficulty, rng);
        chosen.Add(pick); pool.Remove(pick);
        alive = alive.And(solver.MaskOf(pick));
    }

    TrimRedundant(chosen, solver);                    // 빼도 답이 하나면 제거
    AddRedHerrings(chosen, pool, t.RedHerringCount, rng); // 참이지만 결정적이지 않은 단서
    return new CaseInstance(seed, axes, truth, chosen);
}
```

- **실패 처리**: `null`이면 `seed * 31 + 시도횟수`처럼 결정적으로 파생된 시드로 최대 50번 재시도한다. 같은 원래 시드는 항상 같은 사건이 나온다.
- **난이도 선택 (`PickByDifficulty`)**: 쉬움은 후보를 많이 지우는 단서와 `IsNot` 위주로, 어려움은 조금씩 지우는 단서와 `Implies`·`SameTag` 위주로 가중치를 준다. 가중치는 `ClueRule` 폼의 값이다.
- **군더더기 제거 (`TrimRedundant`)**: 단서를 하나씩 빼 보고, 빼도 답이 하나로 정해지면 제거한다. 어려움 난이도에서만 강하게 적용한다.
- **결정성 규칙**: `UnityEngine.Random` 대신 시드를 받은 `System.Random`을 쓰고, `Dictionary`나 `HashSet`을 순회해서 순서에 의존하지 않는다. 목록은 항상 id 순으로 정렬해 둔다. 멀티플레이와 버그 재현이 여기에 달려 있다.

## 6. 타임라인, 증언, 거짓말

논리 단서(5장) 위에 **타임라인**이라는 "사건의 사실 기록"을 한 층 더 둔다. 단서 문장, 증언, 반박 증거는 모두 이 타임라인에서 나오기 때문에 서로 어긋나지 않는다.

### 6-1. 사실(Fact) 모델

```csharp
public enum FactKind { Presence, Saw, Handled }

public readonly struct Fact
{
    public readonly FactKind Kind;
    public readonly int Who;      // 용의자 옵션 번호
    public readonly int Where;    // 장소
    public readonly int When;     // 시간대 번호
    public readonly int Target;   // Saw: 본 사람, Handled: 만진 물건
}
```

### 6-2. 타임라인 생성

1. 사건 템플릿의 시간대 목록을 쓴다 (예: 13시, 14시, 15시=사건, 16시).
2. 범인은 사건 시간대에 사건 장소에 배치하고, 사건 도구를 `Handled`로 기록한다.
3. 나머지 용의자는 사건 시간대에 **사건 장소가 아닌 곳**에 배치한다. 다른 시간대는 자유롭게 배치한다.
4. 같은 장소·시간에 있는 사람끼리는 `Saw` 사실을 자동으로 만든다. 알리바이와 목격 증언의 원천이다.

논리 단서의 문장은 타임라인을 참조해 만든다. 예를 들어 `IsNot(culprit, 방송부원)` 단서는 "15시에 방송부원은 방송실에 있었다" 같은 `Presence` 사실로 렌더링된다.

### 6-3. 증언과 거짓말

각 용의자의 증언은 자기 관련 사실 3\~5개를 고른 것이다. 거짓말 성향에 따라 처리가 달라진다.

| 거짓말 성향 | 증언 처리 | 플레이어 대응 |
| --- | --- | --- |
| Liar | 사실 하나의 장소·시간·물건 중 하나를 바꾼다 | 반박 (진짜 사실을 보여주는 증거 제시) |
| Omitter | 사실 하나를 빼고 "추궁 시 공개" 표시 | 추궁 → 숨긴 줄이 추가됨 |
| Exaggerator | 사실은 맞지만 과장 문장으로 렌더링 | 반박하면 "과장일 뿐 거짓은 아님" 개그 반응 (신뢰도 감소 없음) |
| Honest | 그대로 | 없음 |

범인은 성향과 상관없이 **사건 시간대의 자기 위치를 반드시 거짓으로** 말한다.

### 6-4. 거짓말과 반박 증거 연결

- 거짓 줄을 만들 때마다 그 줄의 **원래 사실을 보여주는 증거 아이템**을 함께 만든다 (목격 증언, 출입 기록, 떨어진 물건 등). 증거 아이템 폼(`EvidenceStyle`)에서 사실 종류별 문장을 고른다.
- 판정은 단순하다: `증거.반박대상 == 증언줄.id` 이면 성공.
- 반박에 성공하면 **잠겨 있던 논리 단서가 열린다.** 생성기가 고른 핵심 단서 중 1\~2개를 "반박 보상"으로 지정해서, 증언 반박을 하지 않으면 사건을 풀 수 없게 만든다. 이렇게 해야 반박이 장식이 아니라 추리의 일부가 된다.

### 6-5. 배치와 도달 가능성

- 모든 단서·증거를 획득 경로에 배치한다: 장소 조사, 용의자 탐문, 잡담, 반박 보상.
- 행동 1회 = 아이템 1개 획득으로 단순화한다.
- 검사: (해결에 필요한 단서 수 + 필요한 반박 증거 수) ≤ 행동력 − 여유값. 통과하지 못하면 다음 시드로 재생성한다.

## 7. 진행 흐름도 데이터로: 페이즈 러너

"사건이 배정되고 약간 진행되는" 부분을 담당한다. 진행 순서를 `CaseFlow` 에셋에 페이즈 목록으로 적어 두면, 러너가 위에서부터 하나씩 실행한다.

### 7-1. 페이즈 종류 (코드에 고정)

| 페이즈 | 하는 일 | 폼에서 정하는 값 |
| --- | --- | --- |
| `Dialogue` | 대사 장면 재생 | 대사 스크립트 키, 등장 역할 |
| `Investigate` | 조사 단계 | 행동력, 허용 행동 종류, 열려 있는 장소 |
| `Testimony` | 증언 + 추궁·반박 | 증언할 역할, 반박 필수 여부 |
| `Event` | 반전 이벤트 | 장소 개방, 새 용의자 등장, 단서 공개 |
| `Accuse` | 지목 | 맞혀야 할 축 목록, 실패 시 신뢰도 감소량 |
| `Result` | 결말·복기·보상 | 등급 기준 |

### 7-2. 역할 슬롯

흐름 에셋은 구체적인 캐릭터를 쓰지 않고 **역할 슬롯**을 쓴다: `범인`, `거짓말한 용의자 중 1명`, `목격자`, `의뢰인`. 사건이 생성될 때 슬롯이 실제 캐릭터로 채워진다. 그래서 같은 흐름 에셋을 모든 사건에 재사용할 수 있다.

짧은 사건 흐름 예시:

1. `Dialogue` 의뢰 도입 (의뢰인)
2. `Investigate` 행동력 6
3. `Testimony` 거짓말한 용의자 1명
4. `Accuse` 범인·장소·도구
5. `Result`

긴 사건은 `Investigate → Testimony → Event`를 3번 반복하고 마지막에 `Testimony(범인)`을 넣은 흐름 에셋을 따로 만든다.

### 7-3. 구현

```csharp
[CreateAssetMenu(menuName = "Detective/Case Flow")]
public class CaseFlow : ScriptableObject
{
    [SerializeReference] public List<PhaseDef> phases = new(); // 종류가 다른 페이즈를 한 목록에
}

[Serializable] public abstract class PhaseDef { }
[Serializable] public class InvestigateDef : PhaseDef { public int actionPoints = 6; }
[Serializable] public class TestimonyDef  : PhaseDef { public RoleSlot speaker; public bool mustRebut; }

public interface IPhase
{
    void Enter(CaseSession s);
    bool IsDone { get; }
    void Exit(CaseSession s);
}

public class FlowRunner
{
    public IEnumerator Run(CaseFlow flow, CaseSession s)
    {
        foreach (var def in flow.phases)
        {
            var phase = PhaseFactory.Create(def);  // PhaseDef → 실행용 IPhase
            phase.Enter(s);
            while (!phase.IsDone) yield return null;
            phase.Exit(s);
            if (s.Trust <= 0) { /* 실패 결말로 점프 */ break; }
        }
    }
}
```

- `[SerializeReference]` 목록은 기본 인스펙터에서 하위 타입을 고르는 메뉴가 없으므로, 작은 커스텀 에디터로 "페이즈 추가 ▾" 버튼을 만든다.
- `CaseSession`이 진행 상태(획득 단서, 남은 행동력, 신뢰도, 반박 기록)를 전부 갖는다. UI는 세션의 이벤트(`OnClueGained`, `OnTrustChanged` 등)만 구독해서 화면을 그린다. 나중에 멀티에서는 방장의 세션만 상태를 바꾸고 이벤트를 참가자에게 전파한다.

## 8. 에디터 툴

"폼만 채우면 된다"를 실제로 가능하게 만드는 건 에디터 툴이다. 프로토타입 단계에서도 아래 세 개는 먼저 만든다. 게임 화면보다 이게 먼저다.

### 8-1. 검증 버튼 (`WorldPack` 인스펙터)

| 검사 항목 | 예시 오류 메시지 |
| --- | --- |
| 문장 변수 불일치 | "알리바이 단서 문장에 {item}이 있지만 이 규칙은 item을 채우지 않습니다" |
| 후보 부족 | "축 place: 필수 태그 '실내'를 가진 장소가 3개뿐인데 4개를 뽑으려 합니다" |
| 불가능한 규칙 | "규칙 '왼손잡이 범인': 왼손잡이 태그를 가진 용의자가 없습니다" |
| 비어 있는 필드 | "용의자 '방송부원': 거짓말 성향이 비어 있습니다" |
| 대사 누락 | "'중2병' 아키타입의 오답 핀잔 대사가 0개입니다" |

### 8-2. 사건 미리보기 창 (`EditorWindow`)

- 입력: 사건 템플릿, 시드, 난이도.
- 출력: 진실, 타임라인 표(시간 × 용의자), 선택된 단서와 단서별로 지운 후보 수, 증언과 거짓 줄 표시, 반박 증거 연결, 배치 결과.
- 버튼: `다음 시드`, `이 시드로 플레이`(플레이 모드 진입 후 해당 사건 로드), `JSON으로 내보내기`(버그 보고용).

### 8-3. 대량 테스트

- 시드 1,000개를 돌려 결과를 요약한다: 생성 성공률, 평균 재시도 수, 평균 단서 수, 평균 생성 시간, 단서 규칙별 사용 빈도.
- "한 번도 안 쓰인 규칙"과 "너무 자주 쓰인 규칙"을 표시해서 콘텐츠 균형을 잡는다.
- 같은 검사를 Unity Test Framework의 에디트 모드 테스트로도 만들어, 코드를 바꿀 때마다 "풀 수 없는 사건이 0개"인지 자동으로 확인한다.

## 9. 학원 축제 팩 예시 데이터

프로토타입용 최소 분량이다. 이름은 전부 가칭이다.

**태그 카테고리**: 동아리(연극부, 요리부, 방송부, 경음부, 학생회, 미술부) · 층(1층, 2층, 옥상) · 실내/실외 · 손잡이(왼손잡이) · 체격(키 큼)

### 용의자 6명

| 용의자 | 태그 | 아키타입 | 거짓말 성향 |
| --- | --- | --- | --- |
| 연극부 부장 | 연극부, 2층, 키 큼 | 허세 | Liar |
| 학생회 회계 | 학생회, 1층 | 완벽주의 | Liar |
| 요리부 1학년 | 요리부, 1층 | 소심한 목격자 | Omitter |
| 방송부원 | 방송부, 2층, 왼손잡이 | 중2병 | Exaggerator |
| 경음부 기타리스트 | 경음부, 2층, 키 큼, 왼손잡이 | 무기력 | Honest |
| 미술부 부장 | 미술부, 옥상 | 라이벌 탐정 기질 | Liar |

### 장소 6곳 / 도구 6개

| 장소 | 태그 | 도구 | 태그 |
| --- | --- | --- | --- |
| 강당 무대 뒤 | 연극부, 1층, 실내 | 의상 행거 | 연극부 |
| 창고 | 학생회, 1층, 실내 | 대형 냄비 | 요리부 |
| 조리실 | 요리부, 1층, 실내 | 방송 장비 카트 | 방송부 |
| 방송실 | 방송부, 2층, 실내 | 앰프 케이스 | 경음부 |
| 음악실 | 경음부, 2층, 실내 | 이젤 가방 | 미술부 |
| 옥상 | 미술부, 옥상, 실외 | 서류 상자 | 학생회 |

### 사건 템플릿 3개

| 템플릿 | 축 | 사건 문장 |
| --- | --- | --- |
| 마스코트 머리 실종 | 범인, 숨긴 장소, 운반 도구 | 축제 마스코트 인형의 머리가 사라졌어요! |
| 시식용 푸딩 도난 | 범인, 먹은 장소, 숨긴 도구 | 요리부의 한정 푸딩 20개가 감쪽같이… |
| 무대 조명 장난 | 범인, 조작한 장소, 사용한 도구 | 연극 리허설 중에 조명이 전부 꺼졌어요 |

### 단서 규칙 예시 8개

| 규칙 | 조건 타입 | 문장 변형 예시 |
| --- | --- | --- |
| 알리바이 | `IsNot(범인, ?)` | {suspect}는 {time}에 {place2}에서 친구들과 사진을 찍고 있었다 |
| 잠긴 방 | `IsNot(장소, ?)` | {place}은 오후 내내 잠겨 있었다고 한다 |
| 동아리 비품 | `SameTag(도구, 장소, 동아리)` | 없어진 물건을 옮긴 도구는 그 장소 동아리의 비품이다 |
| 층 목격 | `HasTag(범인, ?층)` | {floor} 복도에서 수상한 그림자를 봤어요 |
| 왼손 흔적 | `HasTag(범인, 왼손잡이)` | 문고리에 왼손으로 잡은 자국이 남아 있다 |
| 실외 아님 | `LacksTag(장소, 실외)` | 물건에는 물기 하나 없었다. 오늘은 비가 왔는데 |
| 목격 연결 | `Implies(범인=?, 장소=?)` | {suspect}가 {place} 쪽으로 가는 걸 누가 봤대 |
| 둘 중 하나 | `OneOf(범인, ?, ?)` | 범인은 키가 큰 두 사람 중 하나라는 증언 |

이 정도 분량이면 템플릿 3 × 용의자·장소·도구 조합으로 사건 수천 개가 나온다. 반복감은 단서 규칙과 문장 변형을 늘려서 줄인다.

## 10. 프로토타입 작업 순서와 폴더 구조

순서의 원칙은 **화면보다 툴 먼저**다. 미리보기 창에서 사건이 잘 나오는 걸 확인한 뒤에 게임 화면을 붙인다.

- [ ] **1. 코어 모델과 솔버**: 축, 조건 7종, 솔버. 에디트 모드 테스트로 "손으로 만든 사건 5개가 정확히 풀리는지" 확인.
- [ ] **2. 데이터 에셋 + 변환기**: `EntityDef`, `TagDef`, `ClueRule`, `CaseTemplate`, `WorldPack`과 SO → 코어 데이터 변환.
- [ ] **3. 생성기 + 미리보기 창**: 시드를 넣으면 진실과 단서 목록이 나오는 데까지. 여기까지가 첫 번째 마일스톤.
- [ ] **4. 검증 버튼 + 대량 테스트**: 학원 축제 팩 데이터를 넣고 1,000 시드 성공률 확인.
- [ ] **5. 타임라인·증언·거짓말**: 6장 구현, 미리보기 창에 증언 표시 추가.
- [ ] **6. 페이즈 러너 + 임시 UI**: 버튼과 텍스트만으로 조사 → 증언 반박 → 지목 → 결과까지 한 판.
- [ ] **7. 플레이 테스트**: 직접 20판, 지인 5명. "한 판 더" 반응이 나오는지 확인.
- [ ] **8. 그 다음**: 수첩·추리 보드 UI, 연출, 멀티플레이.

### 폴더와 어셈블리 구조

```text
Assets/
  Detective/
    Core/            # 순수 C#, UnityEngine 참조 없음 (Detective.Core.asmdef)
      Model/         # Axis, Fact, CaseInstance
      Logic/         # IConstraint 구현, Solver, CaseGenerator, TimelineBuilder, TestimonyBuilder
    Data/            # ScriptableObject 정의 + 코어 변환기 (Detective.Data.asmdef)
    Runtime/         # CaseSession, FlowRunner, 페이즈, UI 바인딩 (Detective.Runtime.asmdef)
    Editor/          # 검증, 미리보기 창, 대량 테스트, 커스텀 인스펙터 (Editor 전용)
    Tests/           # 에디트 모드 테스트
  Packs/
    SchoolFestival/  # 학원 축제 팩 에셋 (엔티티, 규칙, 템플릿, 흐름, 대사)
```

어셈블리 정의(`.asmdef`)로 나누면 Core가 실수로 Unity 코드나 특정 팩을 참조하는 것을 컴파일 단계에서 막을 수 있다. 새 세계관 팩은 `Packs/` 아래에 폴더 하나를 추가하는 것으로 끝난다.
