# Key_DevSession_TestBoss — 테스트 보스 개발 세션 전체 기록

Unity 버전 6000.3.10f1 | 2D Universal | namespace : KEY  
경로: POC07/Assets/HTH/TestBoss/

---

## 세션 개요

TestBoss(테스트 미니보스)는 KEY 프로젝트의 핵심 플레이 루프를 검증하기 위한
간이 보스다. BossKnight의 시행착오를 반영하여 최소 구조로 구현했다.

---

## 핵심 플레이 루프 (확정)

```
팔(Arm_L/R) 해제 상태 시작 (붉은색)
  ↓ 패턴 시전으로 그로기 유도
그로기 진입
  ↓ A키 홀드 → Arm_L 처형 → 봉인 (파란색)
  ↓ A키 홀드 → Arm_R 처형 → 봉인 (파란색)
양팔 봉인 → 코어 활성 (노란색)
  ↓ A키 홀드 → 코어 처형 → 딜타임
딜타임 (7초) → 코어 집중 공격 (HP 감소)
  ↓ 딜타임 종료
양팔 강제 해제 + 충격파 → 루프 반복 / HP 0 → 처치
```

---

## 파일 목록 (최종 v1.4 기준)

| 파일 | 버전 | 역할 |
|---|---|---|
| `TestBossDataSO.cs` | v1.0 | 수치 ScriptableObject |
| `TestBossCore.cs` | v1.0 | 루트: HP/그로기/코어/딜타임 |
| `TestBossExecution.cs` | v1.1 | A키 홀드 처형 (공격/처형 분리, 쿨다운) |
| `TestBossArmPart.cs` | v1.1 | 팔 봉인/해제 + 투사체봉인 + RestoreArmColor |
| `TestBossArmSealReceiver.cs` | v1.0 | 팔 봉인 투사체 수신 |
| `TestBossArmHitbox.cs` | v1.0 | 팔 히트박스 피격 수신 (OnTriggerEnter2D 오브젝트 불일치 해결) |
| `TestBossAI.cs` | v1.0 | AI 상태관리/패턴실행/이동 + SealComponent 연동 |
| `TestBossPatternBase.cs` | v1.1 | 패턴 추상 베이스 + 봉인감지 + virtual Interrupt |
| `TestBossPattern_PunchDown.cs` | v1.1 | 주먹1: 수직 내리찍기 + DOTween 회전 + ArmHitbox 위임 |
| `TestBossPattern_PunchShot.cs` | v1.1 | 주먹2: 수평 날리기 + DOTween 회전 + ArmHitbox 위임 |
| `TestBossFeedback.cs` | v1.1 | 보스 본체 DOTween 피드백 (Charge/Stomp 연출 제거) |
| `TestBossGroggyTrigger.cs` | v1.0 | F키=그로기, G키=딜타임 강제진입 (테스트용) |
| `TestBossShockwave.cs` | v1.0 | TestBoss 전용 충격파 (BossKnightDataSO 의존 없음) |
| `TestBossCoreHitbox.cs` | v1.0 | 코어 히트박스 수신 |
| `PlayerHealth.cs` | v1.1 | 히트스탑 + 상방넉백 + InputManager.BlockMove 추가 |
| `Key_DevSession_TestBoss.md` | — | 전체 개발 세션 기록 (이 파일) |
| `Key_TestBossHierarchy.md` | v1.3 | Prefab 배치도 |

---

## v1.0 — TestBoss 기초 구조 + 핵심 루프

### 생성 파일
`TestBossDataSO`, `TestBossArmPart`, `TestBossCore`, `TestBossExecution v1.1`,
`TestBossGroggyTrigger`, `TestBossCoreHitbox`

### 팔 색상 피드백

| 상태 | 색상 | 의미 |
|---|---|---|
| 해제 (IsUnlocked) | 🔴 붉은색 | 처형 가능 |
| A키 처형 봉인 (IsLocked) | 🔵 파란색 | 코어 활성 조건 충족 |
| 투사체 봉인 (IsSealedByProjectile) | 🟢 초록색 | 패턴 기능 일시 정지 |
| 코어 활성 | 🟡 노란색 | 처형 → 딜타임 진입 |

### TestBossExecution v1.1 개선

- A키 단타(performed)와 홀드 처형 분리 — `_holdThreshold` 기준
- 처형 중 공격 이벤트 차단 — `BlockAttack()` / `UnblockAttack()`
- 처형 완료 후 재발동 방지 — `_executionCooldown` + `_mustReleaseKey`

---

## v1.1 — TestBossAI + 패턴 시스템

### 생성 파일
`TestBossPatternBase`, `TestBossAI`, `TestBossPattern_PunchDown`,
`TestBossPattern_PunchShot`

### BossKnightAI 시행착오 → TestBossAI 개선

| 항목 | BossKnightAI (기존) | TestBossAI (개선) |
|---|---|---|
| 상태 수 | 10개 (복잡) | 5개 (Idle/Chase/Warning/Active/Recovery) |
| Chase → Idle | 즉시 전환 (이동 없음) | Chase = 실제 이동 상태 유지 |
| DataSO 의존 | BossKnightDataSO 직접 의존 | TestBossDataSO 독립 |
| 패턴↔AI 통신 | `_ai` 직접 참조 (강결합) | 이벤트만 사용 (OnPatternGroggy) |
| 그로기/딜타임 관리 | AI 내부 코루틴 | TestBossCore 위임 (_isStopped 플래그) |
| Counter/Dodge/Phase 전환 | 포함 | 생략 (간이 테스트용) |

### AI 상태 다이어그램

```
Idle ──(플레이어 범위 밖)──→ Chase (이동)
Idle ──(패턴 선택)──────→ Warning → Active → Recovery → Idle
Recovery ──(OnPatternGroggy 발행)──→ TestBossCore.EnterGroggy()
                                     ↓
                             _isStopped = true → AI 정지
                             처형 루프 (TestBossExecution)
                             그로기 종료 → _isStopped = false → Idle
```

### 패턴 DOTween 연출

#### PunchDown (주먹1: 수직 내리찍기)

| 단계 | 이동 | 회전 | 색상 |
|---|---|---|---|
| Warning | 팔 위로 상승 | Z +45° 뒤로 젖힘 (OutBack) | 주황색 |
| Active | 팔 아래 내리찍기 (OutExpo) | Z -20° 앞으로 오버슈트 | — |
| Recovery | 원위치 (InOutSine) | 원회전 복귀 | 기본→봉인색 |

#### PunchShot (주먹2: 수평 날리기)

| 단계 | 이동 | 회전 | 색상 |
|---|---|---|---|
| Warning | 팔 뒤로 후퇴 (OutBack) | Z -90° 뒤로 젖힘 | 파란색 |
| Active | 수평 발사 (OutExpo) | Z +90° 앞 오버슈트 | 흰색 플래시 |
| Recovery | 원위치 (InOutSine) | 원회전 복귀 | 기본→봉인색 |
| 봉인 시 | 즉시 복귀 + 보스 후퇴 | 즉시 원회전 | 봉인색 |

---

## v1.2 — TestBossFeedback (DOTween 시각 피드백)

### 생성 파일
`TestBossFeedback v1.1`

### Charge/Stomp 연출 제거 이유
초기에 BossKnight 구조 차용으로 Charge/Stomp 연출이 포함되었으나
기획서에 없는 패턴 — 제거하고 보스 본체(Body)만 담당하도록 단순화.

### 본체 상태별 연출

| 상태 | 연출 |
|---|---|
| Warning | 연한 주황 Ping-Pong + Scale 미세 진동 |
| Active | 흰 플래시 |
| Recovery | 빨간 Shake + 페이드 아웃 |
| Groggy | 노란 Pulse 루프 + Y 축소 |
| DilTime | 주황 빠른 Pulse + 코어 깜빡임 |
| 피격 | 흰 플래시 + X 흔들림 |
| 사망 | Scale 0 Shrink + 회색 |

---

## v1.3 — Seal 봉인 시스템 탑재

### 생성 파일
`TestBossArmSealReceiver v1.0`

### 수정 파일

| 파일 | 변경 내용 |
|---|---|
| `TestBossArmPart` | 투사체 봉인 `_isSealedByProjectile` + `CanPatternExecute` + `RestoreArmColor()` 추가 |
| `TestBossPatternBase` | `OnPatternSealHit` 이벤트 + `WaitScaled` 봉인 감지 + `SetSealableArm()` + `Interrupt` → virtual |
| `TestBossPattern_PunchDown` | `IsArmAvailable` 프로퍼티 + `SetSealableArm()` 호출 + `Interrupt` → override |
| `TestBossPattern_PunchShot` | `IsArmAvailable` 프로퍼티 + `SetSealableArm()` 호출 + `Interrupt` → override |
| `TestBossAI` | `SealComponent` 연동 + `HandlePatternSealHit()` 구현 + 봉인된 팔 패턴 스킵 |

### 봉인 흐름

```
SealProjectile (PlayerHitbox 레이어)
  ↓ Arm_L/R BoxCollider2D 충돌
TestBossArmSealReceiver.OnTriggerEnter2D()
  ↓
TestBossArmPart.ApplySealByProjectile(_sealDuration)
  → _isSealedByProjectile = true (초록색)
  → CanPatternExecute = false
  ↓
TestBossPatternBase.WaitScaled() 매 프레임 체크
  → IsSealedByProjectile 감지 → HandleSealHit()
     1. Interrupt() 호출 ← ★ override 실행
        PunchDown: DOTween 팔 위치/회전 원복
        PunchShot: DOTween 팔 위치/회전 원복 + 보스 후퇴
     2. OnPatternSealHit 발행
  ↓
TestBossAI.HandlePatternSealHit()
  → sealedArm.ApplySealByProjectile(_armSealDuration)
  → TestBossCore.EnterGroggy()
  ↓
TestBossAI.HandleGroggyEnter()
  → _currentPattern.Interrupt() 재호출
  → ★ 이중 실행 가드: _isInterrupted 이미 true → 즉시 return
```

### 핵심 버그 수정

| 버그 | 원인 | 수정 |
|---|---|---|
| 봉인 후 팔이 원위치로 안 돌아옴 | `public new` 는 다형성 없음 — 베이스 Interrupt()만 호출됨 | `Interrupt()` → `virtual` + `override` 구조로 변경 |
| DOTween 이중 실행 | HandleSealHit → Interrupt 후, HandleGroggyEnter → Interrupt 재호출 | `if (_isInterrupted) return` 가드 추가 |

### Prefab 추가 항목

| 오브젝트 | 추가 컴포넌트 | 설정 |
|---|---|---|
| TestBoss 루트 | `SealComponent` | Layer = Enemy (15) |
| Arm_L | `TestBossArmSealReceiver` | _sealProjectileLayer = PlayerHitbox, _sealDuration = 4.0 |
| Arm_R | `TestBossArmSealReceiver` | _sealProjectileLayer = PlayerHitbox, _sealDuration = 4.0 |

---

## v1.4 — 충격파 + 플레이어 피격/넉백/히트스탑

### 생성 파일
`TestBossShockwave v1.0`, `TestBossArmHitbox v1.0`

### 수정 파일

| 파일 | 버전 | 변경 내용 |
|---|---|---|
| `PlayerHealth.cs` | v1.1 | 히트스탑 + 상방 넉백 + BlockMove 추가 |
| `TestBossCore.cs` | — | `_shockwave` 타입 BossShockwave → TestBossShockwave 교체 |
| `TestBossPattern_PunchDown.cs` | v1.1 | OnTriggerEnter2D 제거 → TestBossArmHitbox 위임 |
| `TestBossPattern_PunchShot.cs` | v1.1 | OnTriggerEnter2D 제거 → TestBossArmHitbox 위임 |

### TestBossShockwave 기능

| 기능 | 내용 |
|---|---|
| 밀침 방향 | 수평(보스→플레이어) + `_upwardBias(0.4)` 상방 혼합 → 대각선으로 날아가는 느낌 |
| 속도 적용 | `rb.linearVelocity = finalDir * _shockwavePower` 직접 설정 |
| 이동 차단 | `InputManager.BlockMove/Jump/Dash` (날아가는 동안) |
| 히트스탑 | `Time.timeScale` 일시 낮춤 → `WaitForSecondsRealtime` 복구 |
| 카메라 셰이크 | `DOTween.DOShakePosition` (선택 연결) |

### PlayerHealth v1.1 변경

| 기능 | 내용 |
|---|---|
| 히트스탑 | TakeDamage 시 `Time.timeScale = 0.02` → `0.07초(실시간)` 후 `1.0` 복구 |
| 상방 넉백 | `_knockbackUpward(0.3)` 비율 상방 혼합 → 위로 살짝 튀는 느낌 |
| 넉백 보장 | `KnockbackRoutine` 시작 시 `BlockMove/Dash` → 종료 시 `Unblock` |

### TestBossArmHitbox 생성 이유

Unity `OnTriggerEnter2D`는 **Collider가 붙은 오브젝트**에서만 수신된다.
`PunchDown/PunchShot` 스크립트는 루트 자식 오브젝트에 있고,
실제 Collider는 `Arm_L/R`에 있어 트리거가 패턴 스크립트에 전달되지 않는다.
→ `TestBossArmHitbox`를 `Arm_L/R`에 부착하여 수신 후 패턴에 위임.

### 핵심 버그 수정

| 버그 | 원인 | 수정 |
|---|---|---|
| 패턴이 플레이어를 피격 못 함 | OnTriggerEnter2D가 Collider 오브젝트(Arm_L)에서만 수신 — 스크립트(PunchDown)에 전달 안 됨 | `TestBossArmHitbox`를 Arm_L/R에 부착, OnPatternStart/End 이벤트로 활성화 관리 |
| 넉백이 즉시 무효화됨 | `PlayerMover.ApplyMovement()`가 매 FixedUpdate `velocity.x`를 덮어씀 | `KnockbackRoutine` 시작 시 `BlockMove` → 종료 시 `Unblock` |
| 충격파 밀침 안 됨 | `AddForce`가 PlayerMover에 즉시 덮어씌워짐 | `rb.linearVelocity = finalDir * power` 직접 설정 + `BlockPlayerMoveRoutine` |

### Prefab 추가 항목

| 오브젝트 | 추가 컴포넌트 | 설정 |
|---|---|---|
| TestBoss 루트 | `TestBossShockwave` | _playerLayer=Player, _upwardBias=0.4, _shockwavePower=20 |
| Arm_L | `TestBossArmHitbox` | _pattern=PunchDown컴포넌트, _damage=15, _playerLayer=Player |
| Arm_R | `TestBossArmHitbox` | _pattern=PunchShot컴포넌트, _damage=12, _playerLayer=Player |

---

## TestBoss Prefab 최종 구조 (v1.4 기준)

```
TestBoss                                    Layer: Enemy (15)
│  [TestBossCore]           DataSO, ArmL/R, Core, Shockwave 연결
│  [TestBossAI]             DataSO, 패턴목록, _armSealDuration=4.0
│  [TestBossExecution]
│  [TestBossFeedback]       bodyRenderer, coreRenderer 연결
│  [SealComponent]
│  [TestBossShockwave]      _playerLayer=Player, _upwardBias=0.4
│  [TestBossGroggyTrigger]  F=그로기, G=딜타임
│  [Rigidbody2D]            FreezeRotation
│  [SpriteRenderer]
│  [BoxCollider2D]          IsTrigger=true
│
├── Arm_L                                   Layer: EnemyAttackHit (16)
│     LocalPosition = (-2, 0, 0)
│     [SpriteRenderer]
│     [BoxCollider2D]       IsTrigger=true
│     [TestBossArmPart]     partType=ArmL
│     [TestBossArmSealReceiver]
│           _sealProjectileLayer = PlayerHitbox
│           _sealDuration = 4.0
│     [TestBossArmHitbox]
│           _pattern = PunchDown의 TestBossPattern_PunchDown
│           _damage  = 15
│           _playerLayer = Player
│
├── Arm_R                                   Layer: EnemyAttackHit (16)
│     LocalPosition = (+2, 0, 0)
│     [SpriteRenderer]
│     [BoxCollider2D]       IsTrigger=true
│     [TestBossArmPart]     partType=ArmR
│     [TestBossArmSealReceiver]
│           _sealProjectileLayer = PlayerHitbox
│           _sealDuration = 4.0
│     [TestBossArmHitbox]
│           _pattern = PunchShot의 TestBossPattern_PunchShot
│           _damage  = 12
│           _playerLayer = Player
│
├── Core                                    Layer: Enemy (15)
│     ★ SetActive = false (시작 시 비활성)
│     [SpriteRenderer]
│     [CircleCollider2D]    IsTrigger=true, Radius=0.5
│     [TestBossCoreHitbox]  _core = 루트 TestBossCore
│
├── PunchDown                               Layer: Enemy (15)
│     LocalPosition = (-2, -1, 0)
│     [TestBossPattern_PunchDown]
│           _armTransform        = Arm_L Transform
│           _armRenderer         = Arm_L SpriteRenderer
│           _armPart             = Arm_L TestBossArmPart
│           _hitbox              = Arm_L BoxCollider2D
│           _cooldown            = 5.0
│           _warningDuration     = 1.0
│           _recoveryDuration    = 0.8
│           _windupHeight        = 2.5
│           _slamDepth           = 2.5
│           _slamDuration        = 0.2
│           _hitboxDuration      = 0.2
│           _punchDamage         = 15
│           _windupRotate        = -45
│           _windupRotateDuration = 0.5
│           _slamOvershoot       = 80
│           _triggerGroggyOnRecovery = true
│
└── PunchShot                               Layer: Enemy (15)
      LocalPosition = (+2, -1, 0)
      [TestBossPattern_PunchShot]
            _armTransform         = Arm_R Transform
            _armRenderer          = Arm_R SpriteRenderer
            _armPart              = Arm_R TestBossArmPart
            _hitbox               = Arm_R BoxCollider2D
            _bossRigid2D          = 루트 Rigidbody2D
            _cooldown             = 5.0
            _warningDuration      = 1.0
            _recoveryDuration     = 0.8
            _windupPullback       = 1.0
            _shotDistance         = 6.0
            _shotDuration         = 0.2
            _hitboxDuration       = 0.18
            _punchDamage          = 12
            _windupRotate         = -90
            _windupRotateDuration = 0.4
            _shotOvershoot        = 90
            _retreatSpeed         = 5.0
            _retreatDuration      = 0.4
            _triggerGroggyOnRecovery = true
```

---

## 전체 버그 수정 이력

| 버그 | 원인 | 수정 | 버전 |
|---|---|---|---|
| Interrupt() 다형성 문제 | `public new`는 다형성 없음 — 베이스만 호출됨 | `virtual` + `override` 구조로 변경 | v1.3 |
| Interrupt() 이중 실행 | HandleSealHit → HandleGroggyEnter 순서로 두 번 호출 | `if (_isInterrupted) return` 가드 추가 | v1.3 |
| 패턴이 플레이어 피격 못 함 | OnTriggerEnter2D는 Collider 오브젝트(Arm_L)에서만 수신, 스크립트(PunchDown)에 전달 안 됨 | `TestBossArmHitbox` 신규 — Arm_L/R에 부착 | v1.4 |
| 넉백이 즉시 무효화됨 | `PlayerMover.ApplyMovement()`가 매 FixedUpdate velocity.x 덮어씀 | BlockMove → Unblock | v1.4 |
| 충격파 밀침 안 됨 | `AddForce`가 PlayerMover에 즉시 덮어씌워짐 | `linearVelocity` 직접 설정 + BlockMove | v1.4 |

---

## Physics2D Matrix 필수 설정

```
EnemyAttackHit (16) ↔ Player (레이어번호) : ON  ← 반드시 확인
```

---

## 미완료 항목 (Pending)

| 항목 | 상태 | 메모 |
|---|---|---|
| Physics2D Matrix EnemyAttackHit↔Player | 🔲 필요 | ON 확인 후 TestBossArmHitbox 동작 검증 |
| 플레이어 피격 동작 최종 검증 | 🔲 검증 필요 | TestBossArmHitbox 실제 동작 플레이 테스트 |
| 넉백 + 히트스탑 최종 검증 | 🔲 검증 필요 | BlockMove 연동 플레이 테스트 |
| 충격파 연출 최종 검증 | 🔲 검증 필요 | 대각선 날아가는 느낌 확인 |
| TestBossDataSO 수치 밸런싱 | 🔲 미착수 | 쿨타임 / 데미지 / 딜타임 전반 |
| 스프라이트 / 애니메이션 | 🔲 미착수 | 현재 기본 스프라이트 사용 중 |
| UI — 보스 HP 바 | 🔲 미착수 | 보스 HP 표시 없음 |

---

## BossKnight (별도 프로젝트) 미완료 항목

| 항목 | 상태 | 메모 |
|---|---|---|
| SealComponent ApplySealByType() 패치 | 🔲 패치 필요 | BossCounterSystem 대타 출동 연동 |
| BossKnightDataSO 수치 밸런싱 | 🔲 미착수 | |
| 보스 Animator Controller | 🔲 미착수 | |
| 보스 스프라이트/애니메이션 | 🔲 미착수 | |
| 보스 룸 씬 구성 | 🔲 미착수 | |
| Phase1 패턴 검증 | 🔲 미착수 | ShieldCharge/DefenseStance/PunchR |


# Key_TestBossHierarchy — 테스트 보스 오브젝트 배치도

Unity 버전 6000.3.10f1 | 2D Universal | namespace : KEY  
최신 버전: v1.4

---

## 표기 규칙

- `[컴포넌트]` : 해당 오브젝트에 부착된 컴포넌트
- `(SO)` : ScriptableObject 참조
- `*` : 필수 연결 항목
- `★` : 이번 작업에서 새로 추가된 항목
- 들여쓰기 = 부모-자식 관계

---

## Layer 설정

| Layer 이름 | 번호 | 용도 |
|---|---|---|
| Enemy | 15 | 보스 본체 / 패턴 오브젝트 / 코어 |
| EnemyAttackHit | 16 | 팔 히트박스 (Arm_L / Arm_R) |
| PlayerHitbox | 9 | SealProjectile 레이어 |

## Physics2D Matrix 필수

```
EnemyAttackHit (16) ↔ Player : ON  ← TestBossArmHitbox 동작 조건
```

---

## TestBoss Prefab 구조

```
TestBoss                                         Layer: Enemy (15)
│  [SpriteRenderer]
│  [BoxCollider2D]                               IsTrigger=true
│  [Rigidbody2D]                                 Constraints: FreezeRotation
│
│  [TestBossCore]
│        (SO) TestBossDataSO                    *
│        _armL   = Arm_L 의 TestBossArmPart    *
│        _armR   = Arm_R 의 TestBossArmPart    *
│        _coreObject     = Core GameObject      *
│        _coreSpriteRenderer = Core SpriteRenderer
│        _shockwave = TestBossShockwave         *
│
│  [TestBossAI]
│        (SO) TestBossDataSO                    *
│        _patterns[0] = PunchDown 의 TestBossPattern_PunchDown  *
│        _patterns[1] = PunchShot 의 TestBossPattern_PunchShot  *
│        _armSealDuration = 4.0
│        _moveSpeed    = 3.5
│        _patternRange = 6.0
│        _flipCooldown = 1.0
│
│  [TestBossExecution]
│
│  [TestBossFeedback]
│        _bodyRenderer = 루트 SpriteRenderer
│        _coreRenderer = Core 의 SpriteRenderer
│
│  [SealComponent] ★ v1.3
│        _overlayRenderer = (선택)
│        ※ 루트 Layer = Enemy → SealProjectile 자동 감지
│
│  [TestBossShockwave] ★ v1.4
│        _playerLayer     = Player 레이어          *
│        _shockwaveRadius = 8.0
│        _shockwavePower  = 20.0
│        _upwardBias      = 0.4
│        _hitStopDuration = 0.08
│        _hitStopTimeScale = 0.02
│        _cameraTransform  = (선택, 미연결 시 Camera.main 자동 탐색)
│
│  [TestBossGroggyTrigger]                       (테스트용)
│        _testBossCore = TestBossCore            *
│        F키 = 그로기 강제 진입
│        G키 = 딜타임 강제 진입
│
│  ─── 팔 부위 ───────────────────────────────────────────────
│
│  [!] 실제 Prefab 구조:
│      PunchDown, PunchShot 은 Arm 의 자식이 아닌 루트의 직속 자식.
│      패턴 스크립트가 팔 Transform 을 Inspector 에서 직접 참조.
│
├── Arm_L                                        Layer: EnemyAttackHit (16)
│     LocalPosition = (-2, 0, 0)
│
│     [SpriteRenderer]
│     [BoxCollider2D]                            IsTrigger=true
│
│     [TestBossArmPart]
│           _partType = ArmL (0)               *
│           _spriteRenderer = Arm_L SpriteRenderer *
│
│     [TestBossArmSealReceiver] ★ v1.3
│           _armPart              = Arm_L TestBossArmPart *
│           _sealProjectileLayer  = PlayerHitbox           *
│           _sealDuration         = 4.0
│
│     [TestBossArmHitbox] ★ v1.4
│           _pattern     = PunchDown 의 TestBossPattern_PunchDown *
│           _damage      = 15
│           _playerLayer = Player 레이어                          *
│
├── Arm_R                                        Layer: EnemyAttackHit (16)
│     LocalPosition = (+2, 0, 0)
│
│     [SpriteRenderer]
│     [BoxCollider2D]                            IsTrigger=true
│
│     [TestBossArmPart]
│           _partType = ArmR (1)               *
│           _spriteRenderer = Arm_R SpriteRenderer *
│
│     [TestBossArmSealReceiver] ★ v1.3
│           _armPart              = Arm_R TestBossArmPart *
│           _sealProjectileLayer  = PlayerHitbox           *
│           _sealDuration         = 4.0
│
│     [TestBossArmHitbox] ★ v1.4
│           _pattern     = PunchShot 의 TestBossPattern_PunchShot *
│           _damage      = 12
│           _playerLayer = Player 레이어                          *
│
│  ─── 코어 ──────────────────────────────────────────────────
│
├── Core                                         Layer: Enemy (15)
│     ★ SetActive = false (기본 비활성 — 양팔 봉인 시 TestBossCore 가 활성화)
│
│     [SpriteRenderer]
│     [CircleCollider2D]                         IsTrigger=true, Radius=0.5
│     [TestBossCoreHitbox]
│           _core = 루트 TestBossCore            *
│
│  ─── 패턴 오브젝트 ─────────────────────────────────────────
│
├── PunchDown                                    Layer: Enemy (15)
│     LocalPosition = (-2, -1, 0)
│
│     [TestBossPattern_PunchDown]
│           _armTransform         = Arm_L Transform              *
│           _armRenderer          = Arm_L SpriteRenderer         *
│           _armPart              = Arm_L TestBossArmPart        *
│           _hitbox               = Arm_L BoxCollider2D          *
│           _cooldown             = 5.0
│           _warningDuration      = 1.0
│           _recoveryDuration     = 0.8
│           _windupHeight         = 2.5
│           _slamDepth            = 2.5
│           _slamDuration         = 0.2
│           _hitboxDuration       = 0.2
│           _punchDamage          = 15
│           _windupRotate         = -45
│           _windupRotateDuration = 0.5
│           _slamOvershoot        = 80
│           _triggerGroggyOnRecovery = true
│
└── PunchShot                                    Layer: Enemy (15)
      LocalPosition = (+2, -1, 0)

      [TestBossPattern_PunchShot]
            _armTransform         = Arm_R Transform              *
            _armRenderer          = Arm_R SpriteRenderer         *
            _armPart              = Arm_R TestBossArmPart        *
            _hitbox               = Arm_R BoxCollider2D          *
            _bossRigid2D          = 루트 Rigidbody2D             *
            _cooldown             = 5.0
            _warningDuration      = 1.0
            _recoveryDuration     = 0.8
            _windupPullback       = 1.0
            _shotDistance         = 6.0
            _shotDuration         = 0.2
            _hitboxDuration       = 0.18
            _punchDamage          = 12
            _windupRotate         = -90
            _windupRotateDuration = 0.4
            _shotOvershoot        = 90
            _retreatSpeed         = 5.0
            _retreatDuration      = 0.4
            _triggerGroggyOnRecovery = true
```

---

## 컴포넌트 연결 체크리스트

### TestBoss 루트

| 필드 | 연결 대상 | 필수 |
|---|---|---|
| TestBossCore._data | TestBossDataSO.asset | ★ |
| TestBossCore._armL | Arm_L 의 TestBossArmPart | ★ |
| TestBossCore._armR | Arm_R 의 TestBossArmPart | ★ |
| TestBossCore._coreObject | Core GameObject | ★ |
| TestBossCore._coreSpriteRenderer | Core 의 SpriteRenderer | |
| TestBossCore._shockwave | TestBossShockwave 컴포넌트 | ★ |
| TestBossAI._data | TestBossDataSO.asset | ★ |
| TestBossAI._patterns[0] | PunchDown 의 TestBossPattern_PunchDown | ★ |
| TestBossAI._patterns[1] | PunchShot 의 TestBossPattern_PunchShot | ★ |
| TestBossFeedback._bodyRenderer | 루트 SpriteRenderer | |
| TestBossFeedback._coreRenderer | Core 의 SpriteRenderer | |
| TestBossShockwave._playerLayer | Player 레이어 | ★ |

### Arm_L

| 필드 | 연결 대상 | 필수 |
|---|---|---|
| TestBossArmPart._partType | ArmL (0) | ★ |
| TestBossArmPart._spriteRenderer | Arm_L SpriteRenderer | ★ |
| TestBossArmSealReceiver._armPart | Arm_L TestBossArmPart | ★ |
| TestBossArmSealReceiver._sealProjectileLayer | PlayerHitbox 레이어 | ★ |
| TestBossArmHitbox._pattern | PunchDown 의 TestBossPattern_PunchDown | ★ |
| TestBossArmHitbox._damage | 15 | |
| TestBossArmHitbox._playerLayer | Player 레이어 | ★ |

### Arm_R

| 필드 | 연결 대상 | 필수 |
|---|---|---|
| TestBossArmPart._partType | ArmR (1) | ★ |
| TestBossArmPart._spriteRenderer | Arm_R SpriteRenderer | ★ |
| TestBossArmSealReceiver._armPart | Arm_R TestBossArmPart | ★ |
| TestBossArmSealReceiver._sealProjectileLayer | PlayerHitbox 레이어 | ★ |
| TestBossArmHitbox._pattern | PunchShot 의 TestBossPattern_PunchShot | ★ |
| TestBossArmHitbox._damage | 12 | |
| TestBossArmHitbox._playerLayer | Player 레이어 | ★ |

### Core

| 필드 | 연결 대상 | 필수 |
|---|---|---|
| TestBossCoreHitbox._core | 루트 TestBossCore | ★ |
| SetActive | false (시작 비활성) | ★ |

### PunchDown

| 필드 | 연결 대상 | 필수 |
|---|---|---|
| _armTransform | Arm_L Transform | ★ |
| _armRenderer | Arm_L SpriteRenderer | ★ |
| _armPart | Arm_L TestBossArmPart | ★ |
| _hitbox | Arm_L BoxCollider2D | ★ |

### PunchShot

| 필드 | 연결 대상 | 필수 |
|---|---|---|
| _armTransform | Arm_R Transform | ★ |
| _armRenderer | Arm_R SpriteRenderer | ★ |
| _armPart | Arm_R TestBossArmPart | ★ |
| _hitbox | Arm_R BoxCollider2D | ★ |
| _bossRigid2D | 루트 Rigidbody2D | ★ |

---

## 봉인 흐름 요약

```
SealProjectile (PlayerHitbox 레이어)
  ↓ Arm_L / Arm_R BoxCollider2D 충돌
TestBossArmSealReceiver.OnTriggerEnter2D()
  ↓
TestBossArmPart.ApplySealByProjectile(_sealDuration)
  → 팔 색상 → 초록색
  → CanPatternExecute = false
  ↓
TestBossPatternBase.WaitScaled() 매 프레임 체크
  → IsSealedByProjectile 감지 → HandleSealHit()
     1. Interrupt() — override 실행
        PunchDown: DOTween 팔 원위치 복귀
        PunchShot: DOTween 팔 원위치 복귀 + 보스 후퇴
     2. OnPatternSealHit 발행
  ↓
TestBossAI.HandlePatternSealHit()
  → TestBossCore.EnterGroggy()
  ↓
TestBossAI.HandleGroggyEnter()
  → _currentPattern.Interrupt() 재호출
  → ★ 이중 실행 가드: _isInterrupted 이미 true → 즉시 return
```

## 피격 흐름 요약

```
PunchDown Active / PunchShot Active
  → OnPatternStart 이벤트 발행
  ↓
TestBossArmHitbox._isActive = true
  ↓
Arm_L/R 이동 → 플레이어와 충돌
TestBossArmHitbox.OnTriggerEnter2D()
  → PlayerLayer 체크 → IDamageable.TakeDamage()
  ↓
PlayerHealth.TakeDamage()
  → 히트스탑 (Time.timeScale = 0.02)
  → KnockbackRoutine (BlockMove → velocity 설정 → Unblock)
  → iFrame 시작
  → 피격 플래시
```

## 충격파 흐름 요약

```
딜타임 종료 → TestBossCore.ExitDilTime()
  → TestBossShockwave.Trigger(boosPosition)
    1. 파티클 재생 (선택)
    2. 카메라 셰이크 DOShakePosition (선택)
    3. 히트스탑 Time.timeScale 낮춤
    4. OverlapCircle → 플레이어 감지
       rb.linearVelocity = finalDir * power (직접 설정)
       BlockPlayerMoveRoutine 시작 (날아가는 동안 이동 차단)
```

---

## 색상 피드백 정리

| 팔 상태 | 색상 | 의미 |
|---|---|---|
| 해제 (IsUnlocked) | 🔴 붉은색 | 처형 가능 — A키 홀드 |
| A키 처형 봉인 (IsLocked) | 🔵 파란색 | 코어 활성 조건 충족 |
| 투사체 봉인 (IsSealedByProjectile) | 🟢 초록색 | 패턴 기능 일시 정지 |
| 코어 활성 | 🟡 노란색 | 처형 → 딜타임 진입 |

---

## 수정 이력

| 버전 | 변경 항목 |
|---|---|
| 초기 | TestBossCore / TestBossExecution / Arm_L / Arm_R / Core 기본 구조 |
| v1.1 | TestBossAI 추가, PunchDown / PunchShot 패턴 오브젝트 추가 |
| v1.2 | TestBossFeedback 추가 (DOTween 시각 연출) |
| v1.3 ★ | SealComponent (루트), TestBossArmSealReceiver (Arm_L/R) 추가 |
| v1.3 ★ | Interrupt() virtual/override 구조 수정 (봉인 팔 복귀 동작 보장) |
| v1.4 ★ | TestBossShockwave (루트), TestBossArmHitbox (Arm_L/R) 추가 |
| v1.4 ★ | PlayerHealth v1.1 — 히트스탑 + 상방넉백 + BlockMove |