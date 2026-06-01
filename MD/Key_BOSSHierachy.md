# Key_BOSSHierarchy — 보스 기사 오브젝트 배치도

최신 버전 기준: v0.29

Unity 버전 6000.3.10f1 | 2D Universal | namespace : KEY

---

## 규칙

- `[컴포넌트]` : 해당 오브젝트에 부착된 컴포넌트
- `(SO)` : ScriptableObject 참조
- `*` : 필수 연결 항목
- 들여쓰기 = 부모-자식 관계
- `Phase X` : 해당 페이즈에서만 활성화

---

## Layer 설정

| Layer 이름 | 번호 | 용도 |
|---|---|---|
| Enemy | 15 | 보스 본체 / 코어 |
| EnemyLock | 17 | 자물쇠 콜라이더 |
| EnemyShield | 18 | 방패 콜라이더 |
| EnemyAttackHit | 16 | 보스 공격 히트박스 |
| BossRange | 19 | 예상 범위 시각화 전용 (충돌 없음) |

---

## Boss_Knight Prefab 구조

```
Boss_Knight                              Layer: Enemy
│
├── [BossKnight]                         v1.2
│     (SO) BossKnightDataSO             * 보스 전용 수치 SO
│     _allParts = 9개                   전체 BossPartComponent 목록
│     _phase1Patterns = 3개
│     _phase2Patterns = 4개
│     _phase3Patterns = 5개
│     _phase1Objects = [Shield_Phase1]
│     _phase2Objects = [Sword_Phase2]
│     _phase3Objects = [Sword_L, Sword_R, Hand2_L, Hand2_R]
│
├── [EnemyBossBase]                      v1.0  ← v0.29 EnemyBase에서 분리
│     abstract BossMaxHp / BossKnockbackForce
│     abstract BossKnockbackDecay / BossIFrameDuration
│     _isPhaseInvincible (Phase 전환 중 무적)
│
├── [BossPhaseManager]                   v1.0
│     _phase1HpThreshold = 0.5f
│     _phase2HpThreshold = 0.0f
│
├── [BossKnightAI]                       v1.0
│     10상태 (Idle/Chase/Warning/Active/Recovery/
│             Groggy/DilTime/Counter/Dodge/PhaseTransition)
│
├── [BossCounterSystem]                  v1.1
│     _interceptHands = [Hand2_L, Hand2_R BossPartComponent]
│     _interceptHandSeals = [Hand2_L, Hand2_R SealComponent]
│
├── [BossShockwave]                      v1.0
│
├── [BossExecutionHandler]               v1.1  ← v0.29 수정
│     처형 흐름 재설계 (A키 홀드 즉시 이동)
│     코어 처형 완료 시 EnterDilTime() 연결
│     Rigidbody2D.MovePosition 방식
│
├── [BossCoreLock]                       v1.2  ← v0.29 수정
│     활성 조건: 왼팔(IsLocked) AND 오른팔(IsLocked)
│     IsGroggy 조건 제거 — 양팔 봉인 즉시 활성
│     딜타임 종료 시: 코어봉인 → 양팔ForceUnlock → 충격파
│     _coreObject    = Core GameObject  (기본 비활성)
│     _coreLockComponent = Lock_Core
│     _coreCollider  = Core CircleCollider2D
│
├── [SealComponent]
│
└── [ObjectFlipController]
      _flipTargets = [Shield_Phase1, Arm_L, Arm_R]
      _invertList  = [false, false, false]
│
├── Phase1 (빈 그룹)
├── Phase2 (빈 그룹)
├── Phase3 (빈 그룹)
├── AllType (빈 그룹)
│
│ ═══════════════════════════════════════
│ Phase 1 전용
│ ═══════════════════════════════════════
│
├── Shield_Phase1                        Layer: EnemyShield
│     [BossPartComponent]
│           _partType = Shield
│           _activePhases = [Phase1]
│           초기 상태: 잠금
│     [SpriteRenderer]
│     └── Lock_Shield                    Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]          isTrigger=ON
│               [SpriteRenderer]
│
│ ═══════════════════════════════════════
│ Phase 2 전용
│ ═══════════════════════════════════════
│
├── Sword_Phase2
│     [BossPartComponent]
│           _partType = Sword
│           _activePhases = [Phase2]
│
│ ═══════════════════════════════════════
│ Phase 3 전용
│ ═══════════════════════════════════════
│
├── Sword_L_Phase3
│     [BossPartComponent]  _partType = SwordL  _activePhases = [Phase3]
│
├── Sword_R_Phase3
│     [BossPartComponent]  _partType = SwordR  _activePhases = [Phase3]
│
├── Hand2_L                              (Phase 3) 추가 생성 왼손
│     [BossPartComponent]               _partType = Hand2L
│     [SealComponent]
│     [SpriteRenderer]
│     └── Lock_Hand2L                   Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
├── Hand2_R                              (Phase 3) 추가 생성 오른손
│     [BossPartComponent]               _partType = Hand2R
│     [SealComponent]
│     [SpriteRenderer]
│     └── Lock_Hand2R                   Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
│ ═══════════════════════════════════════
│ 전 Phase 공통 오브젝트
│ ═══════════════════════════════════════
│
├── Arm_L                                왼팔 부위
│     [BossPartComponent]
│           _partType = ArmL
│           _activePhases = [Phase1, Phase2, Phase3]
│           _affectedPatterns = 왼팔 사용 패턴 목록
│           초기 상태: 해제(IsUnlocked = true) ← v0.29
│     [SpriteRenderer]
│     ★ Lock_ArmL 자식의 LockComponent 제거 완료 ← v0.29
│       (일반 무기 피격으로 팔 해제되는 문제 차단)
│
├── Arm_R                                오른팔 부위
│     [BossPartComponent]
│           _partType = ArmR
│           _activePhases = [Phase1, Phase2, Phase3]
│           _affectedPatterns = 오른팔 사용 패턴 목록
│           초기 상태: 해제(IsUnlocked = true) ← v0.29
│     [SpriteRenderer]
│     ★ Lock_ArmR 자식의 LockComponent 제거 완료 ← v0.29
│
├── Core                                 코어
│     Layer: Enemy
│     ★ 기본 SetActive = false ← v0.29 수정
│     [BossPartComponent]
│           _partType = Core
│           _activePhases = [Phase1, Phase2, Phase3] ← v0.29 Phase1 추가
│     [SpriteRenderer]
│     [CircleCollider2D]                 isTrigger=ON  활성 시만 ON
│     └── Lock_Core                     Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
│ ═══════════════════════════════════════
│ 공격 히트박스
│ ═══════════════════════════════════════
│
├── HitBox (빈 그룹)
│     ├── HitBox_ShieldCharge           Layer: EnemyAttackHit / BoxCollider2D
│     ├── HitBox_ShieldSlam             Layer: EnemyAttackHit / BoxCollider2D
│     ├── HitBox_PunchR                 Layer: EnemyAttackHit / BoxCollider2D
│     ├── HitBox_Sword                  Layer: EnemyAttackHit / BoxCollider2D
│     ├── HitBox_Hand2L                 Layer: EnemyAttackHit / BoxCollider2D
│     └── HitBox_Hand2R                 Layer: EnemyAttackHit / BoxCollider2D
│
│ ═══════════════════════════════════════
│ 예상 범위 시각화
│ ═══════════════════════════════════════
│
├── Range (빈 그룹)
│     ├── RangeIndicator_ShieldCharge   Layer: BossRange / BossRangeIndicator(Line)
│     ├── RangeIndicator_Punch          Layer: BossRange / BossRangeIndicator(Line)
│     ├── RangeIndicator_SwordSlash     Layer: BossRange / BossRangeIndicator(Line)
│     ├── RangeIndicator_Advance        Layer: BossRange / BossRangeIndicator(Line)
│     ├── RangeIndicator_DonutSlash     Layer: BossRange / BossRangeIndicator(Donut)
│     └── RangeIndicator_Slash1         Layer: BossRange / BossRangeIndicator(Line)
│
└── SealOverlay                          Layer: BossRange / SpriteRenderer
```

---

## 컴포넌트 연결 체크리스트 (v0.29 최신)

### BossKnight (루트)

| 필드 | 값 | 비고 |
|---|---|---|
| `_bossData` | BossKnightDataSO.asset | ★ 유일한 DataSO 연결 지점 |
| `_allParts` | 전체 BossPartComponent 9개 | Shield/ArmL/ArmR/Core/Sword/SwordL/SwordR/Hand2L/Hand2R |
| `_phase1Patterns` | ShieldCharge/DefenseStance/PunchR | 3개 |
| `_phase2Patterns` | Advance/Charge/SwordSlash7/SwordSlash12 | 4개 |
| `_phase3Patterns` | SwordSlash4/0/1/PunchDash/Grab | 5개 |
| `_phase1Objects` | [Shield_Phase1] | |
| `_phase2Objects` | [Sword_Phase2] | |
| `_phase3Objects` | [Sword_L, Sword_R, Hand2_L, Hand2_R] | |

### BossCoreLock (루트 컴포넌트)

| 필드 | 값 |
|---|---|
| `_coreObject` | Core GameObject |
| `_coreLockComponent` | Lock_Core의 LockComponent |
| `_coreCollider` | Core의 CircleCollider2D |

### BossCounterSystem

| 필드 | 값 |
|---|---|
| `_interceptHands` | Hand2_L/Hand2_R BossPartComponent |
| `_interceptHandSeals` | Hand2_L/Hand2_R SealComponent |

### BossPartComponent (각 부위 — v0.29 기준)

| 필드 | 값 | 비고 |
|---|---|---|
| `_partType` | BossPartType enum | |
| `_activePhases` | Phase 목록 | Core: [Phase1,2,3] ← v0.29 |
| `_lockComponent` | 자식 LockComponent | 자동 탐색 |
| `_lockCollider` | 자물쇠 Collider2D | |
| `_affectedPatterns` | 봉인 시 속도 영향 패턴 | |
| `_sealedSpeedMultiplier` | 1.5 | 봉인 = 느림 |
| `_partSpriteRenderer` | 부위 SpriteRenderer | 색상 피드백용 |
| `_lockedColor` | 파란색 (0.3, 0.5, 1.0) | ← v0.29 추가 |
| `_unlockedColor` | 붉은색 (1.0, 0.3, 0.3) | ← v0.29 추가 |

---

## Prefab 수정 이력

### v0.28 수정 (완료)

| 항목 | 수정 내용 |
|---|---|
| BossPattern_Slash4/0/1 | SwordSlash4/0/1 재연결 |
| Core._activePhases | Phase1 제거 → Phase2, Phase3만 (당시) |
| SwordSlash 패턴 | _isSwordPattern = true 전체 적용 |
| Core 오브젝트 | BossCoreLock 중복 제거 |
| Hand2_L / Hand2_R | SealComponent 추가 |
| BossCounterSystem | _interceptHandSeals 연결 |
| ObjectFlipController | _invertList = [false, false, false] |

### v0.29 수정 (완료)

| 항목 | 수정 내용 | 이유 |
|---|---|---|
| Arm_L Lock_ArmL | LockComponent 제거 | 일반 무기 피격으로 팔 자동 해제되는 문제 |
| Arm_R Lock_ArmR | LockComponent 제거 | 동일 |
| Core GameObject | SetActive = false | 시작부터 코어 노출되는 문제 |
| Core._activePhases | Phase1 추가 → [Phase1, Phase2, Phase3] | Phase1 딜타임 진입 불가 문제 |
| Arm_L/Arm_R BossPartComponent | Initialize() 팔 해제 상태 시작 | Phase1 기획 구조 변경 |

---

## Phase별 활성화 오브젝트 정리

| 오브젝트 | Phase 1 | Phase 2 | Phase 3 |
|---|---|---|---|
| Shield_Phase1 | ✅ | ❌ | ❌ |
| Sword_Phase2 | ❌ | ✅ | ❌ |
| Sword_L_Phase3 | ❌ | ❌ | ✅ |
| Sword_R_Phase3 | ❌ | ❌ | ✅ |
| Hand2_L | ❌ | ❌ | ✅ |
| Hand2_R | ❌ | ❌ | ✅ |
| Arm_L | ✅ | ✅ | ✅ |
| Arm_R | ✅ | ✅ | ✅ |
| Core | 조건부 | 조건부 | 조건부 |

Core 조건: 양팔(ArmL + ArmR) 동시 봉인 상태 → BossCoreLock.ActivateCore()

---

## BossKnightAI 상태 다이어그램

```
Idle      ──(패턴 선택)──────────→ Warning → Active → Recovery → Idle
Idle      ──(전 패턴 쿨타임)──────→ Dodge → Idle
Warning   ──(봉인 성공 가능)───────→ Groggy
Active    ──(봉인 성공)──────────→ Groggy
Groggy    ──(groggyDuration)──────→ Idle
DilTime   ──(dilTimeDuration)─────→ 충격파 + 양팔해제 → Idle
Counter   ──(완료)───────────────→ 이전 상태 복귀
PhaseTransition ──(완료)──────────→ Idle
```

---

## Phase1 딜타임 흐름 (v0.29 기준)

```
시작
  Arm_L: IsUnlocked = true (붉은색)
  Arm_R: IsUnlocked = true (붉은색)
  Core: SetActive = false

플레이어 처형 → Arm_L ReLock()
  BossPartComponent.OnPartReLocked 발행
  BossCoreLock.CheckCoreActivation()
    → Arm_R 아직 해제 → 조건 미충족

플레이어 처형 → Arm_R ReLock()
  BossPartComponent.OnPartReLocked 발행
  BossCoreLock.CheckCoreActivation()
    → 양팔 IsLocked == true → ActivateCore()
    → Core.SetActive = true

플레이어 처형 → Core ForceUnlock()
  BossExecutionHandler: part.PartType == Core → _coreLock.EnterDilTime()
  딜타임 진입

딜타임 종료
  1. DeactivateCore() → Core.SetActive = false
  2. Arm_L.ForceUnlock() → 붉은색
  3. Arm_R.ForceUnlock() → 붉은색
  4. TriggerShockwave() → 플레이어 밀침

→ 처음으로 돌아가 반복
```

---

## BossKnightDataSO 수치 항목

```
[기본 정보]
bossName, maxHp

[피격 반응]
knockbackForce, knockbackDecay, iFrameDuration, hitFlashInterval

[Phase 전환]
phase1To2HpRatio = 0.5f
phase2To3HpRatio = 0.0f

[Phase 1]
p1ShieldChargeCooldown
p1DefenseStanceDuration (2.0~4.0초)
p1PunchCooldown
p1MoveSpeed
dilTimeDuration (7초)

[Phase 2]
p2AdvanceCooldown / p2ChargeCooldown
p2SwordSlash7Cooldown / p2SwordSlash12Cooldown
p2CounterInitialCooldown (10~15초) / p2CounterCooldown (60초)
p2DilTimeDuration / p2MoveSpeed

[Phase 3]
p3Slash4/0/1Cooldown
p3PunchDashCooldown / p3GrabCooldown
p3CounterCooldown (30초) / p3CounterInitialCooldown
p3DilTimeDuration / p3MoveSpeed

[공통]
shockwaveRadius / shockwavePower
executionHoldDuration
dodgeCooldown
executionRange (처형 가능 범위)
attackHitLayer / groundLayer / playerLayer
```

---

## Physics 2D Matrix 추가 설정

| | Player | PlayerAttackHit | Enemy | EnemyLock | EnemyShield | EnemyAttackHit | BossRange |
|---|---|---|---|---|---|---|---|
| **BossRange** | OFF | OFF | OFF | OFF | OFF | OFF | OFF |

BossRange 레이어는 시각화 전용. 모든 충돌 OFF.