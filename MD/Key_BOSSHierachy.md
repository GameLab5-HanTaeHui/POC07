# Key_BOSSHierarchy — 보스 기사 오브젝트 배치도

최신 버전 기준: v0.28 (Prefab 구성 완료 + 코드 수정)

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
├── [BossKnight]                         v1.1
│     (SO) BossKnightDataSO             * 보스 전용 수치 SO
│     _phaseManager                     BossPhaseManager 참조
│     _counterSystem                    BossCounterSystem 참조
│     _shockwave                        BossShockwave 참조
│     OnPhaseChanged 이벤트             Phase 전환 시 발행
│
├── [BossPhaseManager]                   v1.0
│     _currentPhase                     현재 Phase (1/2/3)
│     _phase1HpThreshold = 0.5f         Phase 1→2 전환 HP 임계값
│     _phase2HpThreshold = 0.0f         Phase 2→3 전환 HP 임계값
│     HP 0% 도달 시 → HP 100% 회복 + Phase 3 진입
│
├── [BossKnightAI]                       v1.0
│     Phase별 패턴 분기
│     쿨타임 관리
│     회피 기동 처리
│     _allPatternsCooldown 체크 → 회피 기동 발동
│
├── [BossCounterSystem]                  v1.1
│     _isCounterActive (bool)           반격 패턴 중복 방지 플래그
│     검 무식 / 대타 출동 통합 관리
│     봉인 투사체 감지 → 상태 판단 → 발동 결정
│
├── [BossShockwave]                      v1.0
│     데미지 없음 / 밀침 전용
│     Phase 전환 시 / 그로기 회복 시 / 딜타임 종료 시 발동
│     _shockwaveRadius                  밀침 범위 (DataSO)
│     _shockwavePower                   밀침 강도 (DataSO)
│
├── [BossExecutionHandler]               v1.0
│     A키 홀드 처형 처리
│     _holdDuration                     홀드 필요 시간 (DataSO)
│     그로기 회복 감지 → 처형 강제 중단 + 충격파
│
├── [BossCoreLock]                       v1.0
│     활성 조건: 왼팔 + 오른팔 동시 봉인
│     딜타임 진입 / 종료 처리
│     _dilTimeDuration = 7.0f           딜타임 지속 (DataSO)
│
├── [EnemyBase]                          v2.0
│     (SO) BossKnightDataSO
│     virtual TakeDamage / OnDead
│
├── [SealComponent]                      v1.3
│
├── [Rigidbody2D]
│     Gravity Scale = 1
│     Freeze Rotation Z = ON
│     Collision Detection = Continuous
│
├── [CapsuleCollider2D]                  물리 충돌 본체
│
├── [SpriteRenderer]                     보스 본체 스프라이트
│
│
│ ═══════════════════════════════════════
│ Phase 1 전용 오브젝트
│ ═══════════════════════════════════════
│
├── Shield_Phase1                        Layer: EnemyShield  (Phase 1)
│     localPosition = (+1.0, 0, 0)      기사 정면
│     [BossPartComponent]               방패 자물쇠 관리
│           _partType = Shield
│           _lockComponent              Lock의 LockComponent
│     [BoxCollider2D]                   isTrigger=OFF  물리 차단
│     [SpriteRenderer]                  방패 스프라이트
│     └── Lock_Shield                   Layer: EnemyLock
│               [LockComponent]         v2.1
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
│
│ ═══════════════════════════════════════
│ Phase 2 전용 오브젝트
│ ═══════════════════════════════════════
│
├── Sword_Phase2                         (Phase 2)
│     [BossPartComponent]               검 자물쇠 관리
│           _partType = Sword
│     [SpriteRenderer]                  검 스프라이트
│     └── Lock_Sword                    Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
│
│ ═══════════════════════════════════════
│ Phase 3 전용 오브젝트
│ ═══════════════════════════════════════
│
├── Sword_L_Phase3                       (Phase 3)
│     [BossPartComponent]               왼손 검 자물쇠
│           _partType = SwordL
│     [SpriteRenderer]
│     └── Lock_SwordL                   Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
├── Sword_R_Phase3                       (Phase 3)
│     [BossPartComponent]               오른손 검 자물쇠
│           _partType = SwordR
│     [SpriteRenderer]
│     └── Lock_SwordR                   Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
├── Hand2_L                              (Phase 3) 추가 생성 왼손
│     [BossPartComponent]               왼손2 자물쇠
│           _partType = Hand2L
│     [SpriteRenderer]
│     └── Lock_Hand2L                   Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
├── Hand2_R                              (Phase 3) 추가 생성 오른손
│     [BossPartComponent]               오른손2 자물쇠
│           _partType = Hand2R
│     [SpriteRenderer]
│     └── Lock_Hand2R                   Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
│
│ ═══════════════════════════════════════
│ 전 Phase 공통 오브젝트
│ ═══════════════════════════════════════
│
├── Arm_L                                왼팔 부위
│     [BossPartComponent]               왼팔 자물쇠 관리
│           _partType = ArmL
│           _affectedPatterns           왼팔 사용 패턴 목록
│     [SpriteRenderer]                  왼팔 스프라이트
│     └── Lock_ArmL                     Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
├── Arm_R                                오른팔 부위
│     [BossPartComponent]               오른팔 자물쇠 관리
│           _partType = ArmR
│           _affectedPatterns           오른팔 사용 패턴 목록
│     [SpriteRenderer]                  오른팔 스프라이트
│     └── Lock_ArmR                     Layer: EnemyLock
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
├── Core                                 코어 (기본 비활성)
│     Layer: Enemy
│     [BossCoreLock]                    활성 조건 감지 + 딜타임 관리
│     [SpriteRenderer]                  코어 스프라이트 (활성 시 표시)
│     [CircleCollider2D]                isTrigger=ON  활성 시만 ON
│     └── Lock_Core                     Layer: EnemyLock (활성 시만)
│               [LockComponent]
│               [BoxCollider2D]         isTrigger=ON
│               [SpriteRenderer]
│
│
│ ═══════════════════════════════════════
│ 공격 히트박스
│ ═══════════════════════════════════════
│
├── Hitbox_ShieldCharge                  Layer: EnemyAttackHit
│     [BoxCollider2D]                   isTrigger=ON  방패 돌진 판정
│
├── Hitbox_ShieldSlam                    Layer: EnemyAttackHit
│     [BoxCollider2D]                   isTrigger=ON  방패 밀치기 판정
│
├── Hitbox_PunchR                        Layer: EnemyAttackHit
│     [BoxCollider2D]                   isTrigger=ON  오른팔 주먹 판정
│
├── Hitbox_Sword                         Layer: EnemyAttackHit
│     [BoxCollider2D]                   isTrigger=ON  검 공격 판정
│
├── Hitbox_Hand2L                        Layer: EnemyAttackHit  (Phase 3)
│     [BoxCollider2D]                   isTrigger=ON  왼손2 주먹 판정
│
├── Hitbox_Hand2R                        Layer: EnemyAttackHit  (Phase 3)
│     [BoxCollider2D]                   isTrigger=ON  오른손2 주먹 판정
│
│
│ ═══════════════════════════════════════
│ 예상 범위 표시
│ ═══════════════════════════════════════
│
├── RangeIndicator_ShieldCharge          Layer: BossRange
│     [BossRangeIndicator]              Inspector on/off
│     [SpriteRenderer] or [LineRenderer]
│
├── RangeIndicator_PunchR                Layer: BossRange
│     [BossRangeIndicator]
│     [SpriteRenderer]
│
├── RangeIndicator_SwordSlash            Layer: BossRange
│     [BossRangeIndicator]
│     [SpriteRenderer]
│
├── RangeIndicator_Advance               Layer: BossRange  (Phase 2)
│     [BossRangeIndicator]
│     [LineRenderer]
│
├── RangeIndicator_DonutSlash            Layer: BossRange  (Phase 3)
│     [BossRangeIndicator]
│     [SpriteRenderer]
│
├── RangeIndicator_StraightThrust        Layer: BossRange  (Phase 3)
│     [BossRangeIndicator]
│     [LineRenderer]
│
│
│ ═══════════════════════════════════════
│ 비주얼 / 이펙트
│ ═══════════════════════════════════════
│
├── ShockwaveEffect                      충격파 이펙트
│     [ParticleSystem]
│
├── PhaseTransitionEffect                Phase 전환 이펙트
│     [ParticleSystem]
│
├── SealOverlay                          봉인 오버레이
│     [SpriteRenderer]
│     SealComponent._overlayRenderer 연결
│
└── ChargeWarningLine                    돌진 예고선
      [LineRenderer]
```

---

## BossPartComponent 상태 머신

```
[Locked]    자물쇠 있음. 봉인 상태. 피격 누적.
            팔 봉인 시 _affectedPatterns 에 _sealedSpeedMultiplier 적용.
[Unlocked]  자물쇠 해제. 약점 노출. 재잠금 가능.
            팔 해제 시 패턴 속도 배율 1.0 으로 복귀.
[Broken]    부위 파괴. (추후 확장)
```

## BossKnightAI 상태 다이어그램

```
Idle ──(패턴 선택)──────────→ Warning → Active → Recovery → Idle
Idle ──(전 패턴 쿨타임)───────→ Dodge → Idle
Warning ──(봉인 성공 가능)───→ Groggy
Active ──(봉인 성공)─────────→ Groggy
Groggy ──(groggyDuration)───→ Idle
DilTime ──(dilTimeDuration)──→ 충격파 → Idle
Counter ──(완료)─────────────→ 이전 상태 복귀
PhaseTransition ──(완료)─────→ Idle

봉인 투사체 감지 → BossCounterSystem.TryCounter()
  그로기 / 딜타임 / PhaseTransition → 반격 불가
  전투 대기 / Warning → 검 무식 (Phase 1/2)
  시전 중 / Warning Phase 3 검 패턴 → 대타 출동 우선
  시전 중 / Warning Phase 3 주먹 패턴 → 대타 출동만
```

## BossPatternBase 생애주기

```
Warning 단계
  ShowRangeIndicator(true)
  OnWarning() 추상 구현 실행
  WaitScaled(duration) — _speedMultiplier 적용
  ShowRangeIndicator(false)

Active 단계
  OnActive() 추상 구현 실행
  봉인 감지 → OnSealHit() → BossPatternSealResult 반환
  TriggerGroggy() → OnPatternGroggy 이벤트 → BossKnightAI.EnterGroggy()

Recovery 단계
  OnRecovery() 추상 구현 실행
  StartCooldown() → 쿨타임 시작
  OnPatternEnd 이벤트 발행

제어 API
  Pause()     : 검 무식 중 일시 정지
  Resume()    : 검 무식 완료 후 재개
  Interrupt() : 그로기/Phase 전환 시 강제 중단
```

---

## 컴포넌트 연결 체크리스트

### BossKnight (루트)

| 필드 | 값 | 비고 |
|---|---|---|
| `_bossData` | BossKnightDataSO.asset | ★ 유일한 DataSO 연결 지점 |
| `_allParts` | 전체 BossPartComponent 목록 | Phase 전환 시 전부 초기화 |
| `_phase1Patterns` | Phase 1 패턴 목록 | |
| `_phase2Patterns` | Phase 2 패턴 목록 | |
| `_phase3Patterns` | Phase 3 패턴 목록 | |
| `_phase1Objects` | Phase 1 전용 오브젝트 | Shield_Phase1 등 |
| `_phase2Objects` | Phase 2 전용 오브젝트 | Sword_Phase2 등 |
| `_phase3Objects` | Phase 3 전용 오브젝트 | Sword_L·R, Hand2_L·R 등 |

### BossCoreLock

| 필드 | 값 |
|---|---|
| `_coreObject` | Core GameObject |
| `_coreLockComponent` | Core의 LockComponent |
| `_coreCollider` | Core의 Collider2D |
| `_activateEffect` | 코어 활성 ParticleSystem |

### BossCounterSystem

| 필드 | 값 |
|---|---|
| `_interceptHands` | Hand2_L / Hand2_R의 BossPartComponent 목록 |
| `_interceptHandSeals` | Hand2_L / Hand2_R의 SealComponent 목록 (v1.1 추가) |
| `_interceptSealDuration` | 4.0초 (대타 출동 봉인 지속 시간) |
| `_parryEffect` | 검 무식 ParticleSystem |
| `_rearDetectThreshold` | -0.2 (후방 판단 dot product 임계값) |
| `_rearTurnDelay` | 0.2초 (후방 감지 시 회전 딜레이) |

### BossExecutionHandler

| 필드 | 값 |
|---|---|
| `_executionEffect` | 처형 완료 ParticleSystem |
| `_executionProgressEffect` | 처형 진행 루프 ParticleSystem |

### BossPartComponent (각 부위)

| 필드 | 값 |
|---|---|
| `_partType` | BossPartType enum 선택 |
| `_activePhases` | 활성화될 Phase 목록 |
| `_lockComponent` | 자식의 LockComponent (미연결 시 자동 탐색) |
| `_lockCollider` | 자물쇠 Collider2D |
| `_affectedPatterns` | 봉인 시 속도 영향받는 패턴 목록 |
| `_sealedSpeedMultiplier` | 1.5 (봉인 시 패턴 속도 배율) |
| `_executionRangeOverride` | 0 = DataSO 기본값 사용 |

### SealComponent

| 필드 | 값 |
|---|---|
| `_overlayRenderer` | SealOverlay의 SpriteRenderer |

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
| Core | ❌ | 조건부 | 조건부 |
| Hitbox_ShieldCharge | ✅ | ❌ | ❌ |
| Hitbox_PunchR | ✅ | ✅ | ✅ |
| Hitbox_Sword | ❌ | ✅ | ✅ |
| Hitbox_Hand2L | ❌ | ❌ | ✅ |
| Hitbox_Hand2R | ❌ | ❌ | ✅ |

---

## BossKnightDataSO 수치 항목

```
[기본 정보]
bossName              : 봉인된 기사

[Phase 1]
p1ShieldChargeCooldown
p1DefenseStanceDuration      (2.0~4.0초)
p1PunchCooldown
p1MoveSpeed

[Phase 2]
p2AdvanceCooldown
p2ChargeCooldown
p2SwordSlash7Cooldown
p2SwordSlash12Cooldown
p2CounterInitialCooldown     (10~15초)
p2CounterCooldown            (60초)
p2DilTimeDuration            (7초)
p2MoveSpeed

[Phase 3]
p3Slash4Cooldown
p3Slash0Cooldown
p3Slash1Cooldown
p3PunchDashCooldown
p3GrabCooldown
p3CounterCooldown            (30초)
p3CounterInitialCooldown     (10~15초)
p3DilTimeDuration            (7초)
p3MoveSpeed

[공통]
shockwaveRadius
shockwavePower
executionHoldDuration        (A키 홀드 시간)
dodgeCooldown                (회피 기동 쿨타임)
rangeIndicatorEnabled        (bool — Inspector on/off)
attackHitLayer               (Player 레이어)
groundLayer                  (Ground + Wall)
playerLayer                  (Player)
```

---

## Physics 2D Matrix 추가 설정

| | Player | PlayerAttackHit | Enemy | EnemyLock | EnemyShield | EnemyAttackHit | BossRange |
|---|---|---|---|---|---|---|---|
| **BossRange** | OFF | OFF | OFF | OFF | OFF | OFF | OFF |

BossRange 레이어는 시각화 전용. 모든 충돌 OFF.



---

## Prefab 실제 구성 확인 (v0.28 파싱 결과)

### 확인된 오브젝트 목록

| 오브젝트 | Layer | 컴포넌트 |
|---|---|---|
| BossKnight (루트) | 15 Enemy | BossKnight v1.1, BossPhaseManager, BossKnightAI, BossCounterSystem v1.1, BossShockwave, BossExecutionHandler, BossCoreLock, SealComponent, ObjectFlipController, 패턴 12개 |
| Phase1 | 0 | Transform 빈 그룹 |
| Phase2 | 0 | Transform 빈 그룹 |
| Phase3 | 0 | Transform 빈 그룹 |
| AllType | 0 | Transform 빈 그룹 |
| Shield_Phase1 | 18 EnemyShield | BossPartComponent(_partType=Shield, _activePhases=[Phase1]) |
| Sword_Phase2 | 0 | BossPartComponent(_partType=Sword, _activePhases=[Phase2]) |
| Sword_L_Phase3 | 0 | BossPartComponent(_partType=SwordL, _activePhases=[Phase3]) |
| Sword_R_Phase3 | 0 | BossPartComponent(_partType=SwordR, _activePhases=[Phase3]) |
| Hand2_L_Phase3 | 0 | BossPartComponent(_partType=Hand2L), SealComponent |
| Hand2_R_Phase3 | 0 | BossPartComponent(_partType=Hand2R), SealComponent |
| Arm_L | 0 | BossPartComponent(_partType=ArmL, _activePhases=[Phase1,2,3], affectedPatterns=7개) |
| Arm_R | 0 | BossPartComponent(_partType=ArmR, _activePhases=[Phase1,2,3], affectedPatterns=3개) |
| Core | 0 | BossPartComponent(_partType=Core, _activePhases=[Phase2,3]), BossCoreLock |
| HitBox_ShieldCharge | 16 EnemyAttackHit | BoxCollider2D |
| HitBox_ShieldSlam | 16 EnemyAttackHit | BoxCollider2D |
| HitBox_PunchR | 16 EnemyAttackHit | BoxCollider2D |
| HitBox_Sword | 16 EnemyAttackHit | BoxCollider2D |
| HitBox_Hand2L | 16 EnemyAttackHit | BoxCollider2D |
| HitBox_Hand2R | 16 EnemyAttackHit | BoxCollider2D |
| RangeIndicator_ShieldCharge | 20 BossRange | BossRangeIndicator(Line) |
| RangeIndicator_Punch | 20 BossRange | BossRangeIndicator(Line) |
| RangeIndicator_SwordSlash | 20 BossRange | BossRangeIndicator(Line) |
| RangeIndicator_Advance | 20 BossRange | BossRangeIndicator(Line) |
| RangeIndicator_DonutSlash | 20 BossRange | BossRangeIndicator(Donut) |
| RangeIndicator_Slash1 | 20 BossRange | BossRangeIndicator(Line) |
| SealOverlay | 19 BossRange | SpriteRenderer |
| HitBox | 0 | Transform 빈 그룹 |
| Range | 0 | Transform 빈 그룹 |
| public | 0 | Transform 빈 그룹 |

### 수정된 항목 (v0.28)

```
[수정됨]
  BossPattern_Slash4/0/1 → SwordSlash4/0/1 재연결
  Core._activePhases: Phase1 제거 (Phase2, Phase3만)
  SwordSlash7/12/4/0/1 _isSwordPattern = true
  Core 오브젝트의 중복 BossCoreLock 제거
  Hand2_L / Hand2_R에 SealComponent 추가
  BossCounterSystem._interceptHandSeals 연결
  ObjectFlipController._invertList = [false, false, false]

[확인됨]
  _settings (EnemyDataSO) = null (비워두면 됨)
  _bossData (BossKnightDataSO) = BossKnightData.asset 연결됨
```