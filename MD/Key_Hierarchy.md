# Key_Hierarchy — Scene 오브젝트 배치도

Unity 버전 6000.3.10f1 | 2D Universal | namespace : KEY
최신 버전 기준: 리모델링 완료 (v0.18)

---

## 규칙

- `[컴포넌트]` : 해당 오브젝트에 부착된 컴포넌트
- `(SO)` : ScriptableObject 참조
- `*` : 필수 연결 항목
- 들여쓰기 = 부모-자식 관계

---

## Layer 설정

| Layer 번호 | Layer 이름 | 용도 |
|---|---|---|
| 8 | Player | 플레이어 본체 |
| 11 | PlayerAttackHit | 플레이어 무기 히트박스 |
| 15 | Enemy | 적 본체 |
| 16 | EnemyAttackHit | 적 공격 히트박스 |
| 17 | EnemyLock | 자물쇠 콜라이더 |
| 18 | EnemyShield | 방패 콜라이더 ← 신규 |
| - | Ground | 지형 |
| - | Wall | 벽 |

---

## Physics 2D Matrix (필수 설정)

| | Player | PlayerAttackHit | Enemy | EnemyLock | EnemyShield | EnemyAttackHit |
|---|---|---|---|---|---|---|
| **Player** | | | | | **ON** | **ON** |
| **PlayerAttackHit** | | | **ON** | **ON** | OFF | |
| **Enemy** | | ON | | | | |
| **EnemyLock** | | ON | | | | |
| **EnemyShield** | **ON** | OFF | | | | |
| **EnemyAttackHit** | **ON** | | | | | |

**핵심:**
- `Player ↔ EnemyShield = ON` → 플레이어가 방패에 물리적으로 막힘
- `PlayerAttackHit ↔ EnemyShield = OFF` → 플레이어 무기는 방패 물리 충돌 없음
- `PlayerAttackHit ↔ Enemy = ON` → 적 본체 감지
- `PlayerAttackHit ↔ EnemyLock = ON` → 자물쇠 감지
- `Player ↔ EnemyAttackHit = ON` → 적 공격이 플레이어에게 닿음

---

## Player

```
Player                               Layer: Player
├── [InputManager]               * 모든 키 입력 통합 (이동 + 무기)
├── [PlayerMover]                * 이동 / 점프 / 대쉬 물리 (v1.6)
│     └── (SO) MovementSettings  * 이동 수치 설정
├── [MovementAnimator]             Animator 파라미터 동기화 (v2.1)
├── [PlayerMovementFacade]         외부 단일 진입점 (싱글턴)
├── [PlayerHealth]               * IDamageable 구현 — 체력/iFrame/넉백/사망 (v1.0)
│     OnDamaged → UI 체력 갱신
│     OnDead    → GameManager 리스폰 처리 (추후)
├── [Animator]                   * Player.controller 연결
├── [Rigidbody2D]                * Collision Detection = Continuous
├── [SpriteRenderer]             * 플레이어 스프라이트
├── [CapsuleCollider2D]          * 물리 충돌 콜라이더
│
├── GroundCheck                    발 아래 빈 오브젝트 (지면 감지 기준점)
│
└── Weapon
      ├── [PlayerWeaponController]  * 열쇠 교체 핵심 컨트롤러 (v1.4)
      │     ├── (SO) KeyInventoryDataSO  * 보유 열쇠 목록
      │     ├── _weaponEntries[0]  keyType=Rusty / weapon=RustyKeyWeapon
      │     └── _weaponEntries[1]  keyType=Seal  / weapon=SealKeyWeapon / sealData=SealData_Dash.asset
      │
      ├── [RustyKeyWeapon]         비활성 대기 (KeyType.Rusty)
      ├── [SealKeyWeapon]          비활성 대기 (KeyType.Seal)
      │     └── _projectilePrefab = SealProjectile.prefab
      │
      ├── [PlayerWeaponAnimator]        무기 이벤트 구독 → PlayerWeaponMover 호출
      ├── [PlayerWeaponMover]           DOTween 스윙 이동 전담 (v1.1)
      │     └── OnFlipped 구독          _originLocalPosition.x 반전 + SpriteRenderer.flipX
      ├── [PlayerWeaponHitboxManager] * 히트박스 관리 (v1.3)
      │     └── OnFlipped 구독          FlipHitboxes() → 각 Hitbox localPosition.x 반전
      │     _enemyLayer  = Enemy (Layer 15)
      │     _lockLayer   = EnemyLock (Layer 17)
      │     _shieldLayer = EnemyShield (Layer 18) ← v1.3 신규 (감지 시 무시)
      │
      ├── Hitbox_Combo1    [BoxCollider2D] isTrigger=ON  Layer: PlayerAttackHit
      ├── Hitbox_Combo2    [BoxCollider2D] isTrigger=ON  Layer: PlayerAttackHit
      ├── Hitbox_Combo3    [BoxCollider2D] isTrigger=ON  Layer: PlayerAttackHit
      └── Hitbox_AirAttack [BoxCollider2D] isTrigger=ON  Layer: PlayerAttackHit
```

### Player 컴포넌트 연결 체크리스트

| 컴포넌트 | 연결 항목 | 값 |
|---|---|---|
| PlayerMover | _settings | MovementSettings SO |
| PlayerMover | _groundCheck | GroundCheck Transform |
| PlayerWeaponController | _inventory | KeyInventoryDataSO |
| PlayerWeaponController | _weaponEntries[0] | keyType=Rusty / weapon=RustyKeyWeapon |
| PlayerWeaponController | _weaponEntries[1] | keyType=Seal / weapon=SealKeyWeapon / sealData=SealData_Dash.asset |
| PlayerWeaponController | _movementAnimator | MovementAnimator |
| PlayerWeaponController | _weaponAnimator | PlayerWeaponAnimator |
| PlayerWeaponController | _weaponMover | PlayerWeaponMover |
| RustyKeyWeapon | _hitboxManager | PlayerWeaponHitboxManager |
| SealKeyWeapon | _projectilePrefab | SealProjectile.prefab |
| PlayerWeaponHitboxManager | _hitboxes[0~3] | 각 Hitbox BoxCollider2D |
| PlayerWeaponHitboxManager | _enemyLayer | Enemy 레이어 |
| PlayerWeaponHitboxManager | _lockLayer | EnemyLock 레이어 |
| PlayerWeaponHitboxManager | _shieldLayer | EnemyShield 레이어 |
| Animator | Controller | Player.controller |

### OnFlipped 이벤트 구독자 (Player)

| 구독자 | 처리 내용 | 버전 |
|---|---|---|
| `PlayerWeaponMover.HandleFlipped` | `_originLocalPosition.x` 반전 + `SpriteRenderer.flipX` | v1.1 |
| `PlayerWeaponHitboxManager.FlipHitboxes` | 각 Hitbox `transform.localPosition.x` 반전 | v1.1 |

---

## SealProjectile (Prefab)

```
SealProjectile (Prefab)              Layer: PlayerAttackHit
├── [SealProjectile]
│     └── _sealLayer = Enemy 레이어
├── [Rigidbody2D]            GravityScale=0 / Continuous
├── [CircleCollider2D]       isTrigger=true / radius=0.15
└── [SpriteRenderer]
```

**저장 경로**: `Assets/KEY/Prefabs/SealProjectile.prefab`

---

## ScriptableObject 목록

```
Assets/KEY/DataSO/
  MovementSettings.asset

Assets/KEY/DataSO/Keys/
  RustyKeyData.asset          녹슨 열쇠 (KeyDataSO)

Assets/KEY/DataSO/Seals/
  SealData_Dash.asset         (SealDataSO / sealType=Dash)
  SealData_Guard.asset        (SealDataSO / sealType=Guard)
  SealData_Move.asset         (SealDataSO / sealType=Move)
  SealData_Attack.asset       (SealDataSO / sealType=Attack)

Assets/KEY/DataSO/Inventory/
  KeyInventory.asset          (KeyInventoryDataSO)

Assets/KEY/DataSO/Enemy/
  DummyData.asset             (EnemyDataSO v3.0 / enemyType=Dummy)
  DummyLockedData.asset       (EnemyDataSO v3.0 / enemyType=DummyLocked)
  KnightData.asset            (EnemyDataSO v3.0 / enemyType=Knight)
```

---

## Animator Controller — Player.controller

```
파라미터
  Speed        Float    Mathf.Abs(MoveInput)
  VelocityY    Float    Rigidbody2D.velocity.y
  IsGrounded   Bool     PlayerMover.IsGrounded
  IsFiring     Bool     PlayerMovementFacade.SetFiring()
  Jump         Trigger  PlayerMover.OnJumped
  DoubleJump   Trigger  PlayerMover.OnDoubleJumped
  Dash         Trigger  PlayerMover.OnDashStarted
  AttackCombo1 Trigger  RustyKeyWeapon.OnCombo1Started
  AttackCombo2 Trigger  RustyKeyWeapon.OnCombo2Started
  AttackCombo3 Trigger  RustyKeyWeapon.OnCombo3Started
  AirAttack    Trigger  RustyKeyWeapon.OnAirAttackStarted

전환 규칙
  AnyState → PlayerAttack01   : AttackCombo1 + IsGrounded=true
  Attack01 → Attack02         : AttackCombo2 + ExitTime=0.5
  Attack02 → Attack03         : AttackCombo3 + ExitTime=0.5
  Attack01/02/03 → PlayerIdle : ExitTime=1.0  ★ Loop Time=OFF 필수
  AnyState → PlayerAirAttack  : AirAttack + IsGrounded=false
```

---

## Enemy_Dummy

```
Enemy_Dummy                          Layer: Enemy
├── [EnemyDummy]           (EnemyBase 상속 / enemyType=Dummy)
│     └── (SO) EnemyDataSO  * DummyData.asset
├── [Rigidbody2D]          gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]
└── [SpriteRenderer]
```

---

## Enemy_DummyLocked

```
Enemy_DummyLocked                    Layer: Enemy
├── [EnemyDummyLocked]     (EnemyBase 상속 / enemyType=DummyLocked)
│     └── (SO) EnemyDataSO  * DummyLockedData.asset
├── [Rigidbody2D]          gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]
├── [SpriteRenderer]
└── Lock                             Layer: EnemyLock
      ├── [LockComponent]  v2.0 — OnFlipped 구독, localPosition.x 자동 반전
      ├── [SpriteRenderer]
      └── [BoxCollider2D]  isTrigger=ON
```

---

## Enemy_Knight (기사형) — 리모델링 완료

```
Enemy_Knight                         Layer: Enemy
│
├── [EnemyKnight]                    EnemyBase 상속 (v2.0)
│     _settings      = KnightData.asset  ← 유일한 DataSO 연결 지점
│     _locks         = [Lock의 LockComponent]  ← 리스트 (여러 개 가능)
│     _shieldCollider = ShieldCollider의 BoxCollider2D
│     OnFlipped 구독  FlipShield() → ShieldCollider localPosition.x = +originalX * dir
│
├── [EnemyAI]                        v5.0 (차징 전용)
│     DataSO 자동 취득 (Inspector 연결 없음)
│     OnFlipped 이벤트 발행 → 각 구독자가 자체 처리
│     AIState: Patrol / Idle / Chase / Attack / Groggy
│
├── [EnemyKnightChargeAttack]        EnemyAttackBase 상속 (v2.0)
│     _chargeHitbox   = ChargeHitbox의 BoxCollider2D (선택)
│     _lineRenderer   = ChargeWarningLine의 LineRenderer
│     OnFlipped 구독  FlipHitbox() → ChargeHitbox localPosition.x 반전
│
├── [EnemySensor]                    v2.0
│     (DataSO EnemyAI.Start() 에서 SetData 주입)
│
├── [EnemySealComponent]             v1.0
│     _overlayRenderer = SealOverlay의 SpriteRenderer
│
├── [Rigidbody2D]                    gravityScale=1 / FreezeRotation Z / Continuous
├── [CapsuleCollider2D]              물리 충돌 본체
├── [SpriteRenderer]
│
├── ShieldCollider                   Layer: EnemyShield ← 신규
│     localPosition = (+0.5, 0, 0)  기사 초기 정면 오른쪽
│     [BoxCollider2D]
│           isTrigger = OFF          ← 물리 충돌로 플레이어 통과 차단
│           size = (0.3, 1.2)
│
├── Lock                             Layer: EnemyLock
│     localPosition = (-1.7, 0, 0)  기사 초기 후방 왼쪽  ★ 기존 +1.7 → -1.7 수정
│     [LockComponent]               v2.0 — OnFlipped 구독, localPosition.x 자동 반전
│     [SpriteRenderer]
│     [BoxCollider2D]               isTrigger=ON / size=(0.5, 0.5)
│
├── EnemyChargeAttackHitBox (선택)  Layer: EnemyAttackHit
│     [BoxCollider2D]               isTrigger=ON
│     EnemyKnightChargeAttack._chargeHitbox 에 연결
│
├── ChargeWarningLine
│     [LineRenderer]                positionCount=2 / Width=0.05
│     EnemyKnightChargeAttack._lineRenderer 에 연결
│
└── SealOverlay
      [SpriteRenderer]              EnemySealComponent._overlayRenderer 에 연결
```

### Enemy_Knight 컴포넌트 연결 체크리스트

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| EnemyKnight | _settings | KnightData.asset ← 유일한 연결 지점 |
| EnemyKnight | _locks | Lock의 LockComponent (리스트) |
| EnemyKnight | _shieldCollider | ShieldCollider의 BoxCollider2D |
| EnemyAI | _settings | (Inspector 연결 없음) |
| EnemyKnightChargeAttack | _chargeHitbox | EnemyChargeAttackHitBox의 BoxCollider2D (선택) |
| EnemyKnightChargeAttack | _lineRenderer | ChargeWarningLine의 LineRenderer |
| EnemySealComponent | _overlayRenderer | SealOverlay의 SpriteRenderer |

### OnFlipped 이벤트 구독자 (Enemy_Knight)

| 구독자 | 처리 | 결과 |
|---|---|---|
| `EnemyKnight.FlipShield` | `ShieldCollider.localPosition.x = +originalX * dir` | 방패 항상 정면 |
| `LockComponent.FlipPosition` | `Lock.localPosition.x = -originalX * dir` | 자물쇠 항상 후방 |
| `EnemyKnightChargeAttack.FlipHitbox` | `ChargeHitbox.localPosition.x = originalX * dir` | 히트박스 방향 반전 |

### EnemyAI 상태 전환 (v5.0)

```
Patrol ──(전방 감지)──────→ Chase
Patrol ──(벽/낭떠러지)────→ Flip → TryIdle
Idle   ──(대기 완료)──────→ Patrol
Idle   ──(플레이어 감지)──→ Chase
Chase  ──(범위 이탈)──────→ Patrol
Chase  ──(차징 범위 진입)──→ Attack
Attack ──(완료/정상 도달)──→ Groggy → Chase
Attack ──(벽 충돌)────────→ Groggy → Chase  (ChargeAttack 직접 호출)
Attack ──(봉인 취소)──────→ Groggy → Chase  (ChargeAttack 직접 호출)
Groggy ──(groggyDuration)→ TurnTowardPlayer → Chase
```

### EnemyKnight TakeDamage 분기 (v2.0)

```
IDamageable.TakeDamage(info) 호출 시
  (Enemy 레이어 감지 → PlayerWeaponHitboxManager → EnemyKnight.TakeDamage)

  모든 Lock 해제 완료    → base.TakeDamage() (체력 감소 + 사망 가능)
  Guard 봉인 활성       → base.TakeDamage() (방패 무시)
  그 외                 → 무시
    (ShieldCollider 가 정상 동작하면 Enemy 레이어 직접 피격 거의 없음)

  LockComponent 직접 피격 (EnemyLock 레이어 감지 시):
    PlayerWeaponHitboxManager → LockComponent.TakeDamage() 직접 호출
    횟수 누적 → 해제 → EnemyKnight.HandleLockUnlocked()
    모든 Lock 해제 → _isAllLocksUnlocked = true → 약점 노출
```

### EnemySensor Gizmos 색상 범례 (v2.0)

```
노란선   : 순찰 직선 감지 (patrolSightRange)
빨간선   : 벽 감지 (wallCheckDistance)
보라선   : 낭떠러지 하향 (cliffCheckDistance)
주황원   : 추격 유지 범위 (chaseSightRadius)
주황실선 : 차징 발동 범위 (chargeDetectRange)
```

### KnightData.asset 기본값 (EnemyDataSO v3.0)

```
enemyName          : 기사
enemyType          : EnemyType.Knight

maxHp              : 150

knockbackForce     : 5
knockbackDecay     : 0.8
iFrameDuration     : 0.3
hitFlashInterval   : 0.07

patrolSpeed        : 2
chaseSpeed         : 3.5
idleTimeMin        : 1.0
idleTimeMax        : 3.0
idleChance         : 0.3

patrolSightRange   : 6
chaseSightRadius   : 10
chargeDetectRange  : 5      ← 차징 발동 범위
wallCheckDistance  : 0.6
cliffCheckDistance : 1.0
cliffCheckOffset   : 0.4

chargeSpeed        : 14
chargeDuration     : 0.8
chargeDamage       : 20
chargeCooldown     : 5

groggyDuration     : 2.5

playerLayer        : Player 레이어
groundLayer        : Ground + Wall 레이어
attackHitLayer     : Player 레이어
```

---

## Scene 공통 (추후)

```
GameManager        씬 전역 관리
CinemachineCamera  플레이어 추적
SpawnPoint         플레이어 시작 위치
```