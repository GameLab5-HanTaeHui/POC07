# Key_Hierarchy — Scene 오브젝트 배치도

Unity 버전 6000.3.10f1 | 2D Universal | namespace : KEY  
최신 버전 기준: v0.14

---

## 표기 규칙

- `[컴포넌트]` : 해당 오브젝트에 부착된 컴포넌트
- `(SO)` : ScriptableObject 참조
- `*` : 필수 연결 항목
- 들여쓰기 = 부모-자식 관계

---

## Layer 전체 목록

| Layer 이름 | 용도 | 사용 위치 |
|---|---|---|
| `Default` | Unity 기본값 | 미분류 오브젝트 |
| `Player` | 플레이어 본체 | Player 오브젝트 / EnemyAI.playerLayer / EnemySensor 감지 대상 |
| `PlayerHitbox` | 플레이어 공격 판정 | Hitbox_Combo1~3 / Hitbox_AirAttack / SealProjectile / ChargeProjectile |
| `Enemy` | 적 본체 | Enemy_* 오브젝트 / PlayerWeaponHitboxManager._hitLayer / ChargeProjectile._enemyLayer |
| `EnemyHitbox` | 적 공격 판정 | AttackHitbox (EnemyKnight 공격 히트박스) |
| `Lock` | 자물쇠 콜라이더 | Lock_Back / Lock (DummyLocked) — PlayerHitbox 레이어의 공격에 반응 |
| `Ground` | 지형 바닥 | TileMap Ground / EnemySensor.groundLayer / PlayerMover.GroundLayer |
| `Wall` | 지형 벽 | TileMap Wall / PlayerMover.DashWallLayer / ChargeProjectile._terrainLayer |
| `UI` | UI 캔버스 | Canvas 및 하위 UI 오브젝트 |

### Layer 충돌 매트릭스 요약

```
Player       ↔ Ground / Wall     : 물리 충돌 (이동/착지)
Player       ↔ EnemyHitbox       : 피격 판정 (적 공격이 플레이어에 닿음)
PlayerHitbox ↔ Enemy             : 플레이어 무기 명중 판정
PlayerHitbox ↔ Lock              : 자물쇠 피격 판정
Enemy        ↔ Ground / Wall     : 물리 충돌 (적 이동/착지)
ChargeProjectile(PlayerHitbox) ↔ Enemy    : 차징 투사체 명중
ChargeProjectile(PlayerHitbox) ↔ Ground/Wall : 지형 충돌 → 즉시 소멸
SealProjectile(PlayerHitbox) ↔ Enemy      : 봉인 투사체 명중
```

---

## Player

```
Player                                     Layer: Player
│
│  ─── 입력 / 이동 ──────────────────────────────────────────────
├── [InputManager]               v2.2  싱글턴 / 모든 키 입력 통합
│     InGame  : ← → Space LShift A
│     KeySwap : LCtrl + 1234/QWER/ASDF/ZXCV (16슬롯)
│     Charge  : S(차징시작) ↑↓(조준각도) S뗌(발사)
│
├── [PlayerMover]                v1.5  이동 / 점프 / 대쉬 물리
│     └── (SO) MovementSettings  *
│     이벤트 발행: OnJumped / OnDoubleJumped / OnDashStarted / OnFlipped
│
├── [MovementAnimator]           v2.1  모든 Animator 파라미터 단독 관리
│     매 프레임: Speed / VelocityY / IsGrounded / IsFiring
│     이벤트 구독: OnJumped → Jump / OnDoubleJumped → DoubleJump / OnDashStarted → Dash
│     이벤트 구독: OnCombo1/2/3Started / OnAirAttackStarted → AttackCombo1/2/3 / AirAttack
│
├── [PlayerMovementFacade]             외부 단일 진입점 (싱글턴)
│
│  ─── 차징 공격 ──────────────────────────────────────────────
├── [PlayerChargeAttack]         v1.1  차징 상태 관리 + 각도 조절 + 발사
│     이벤트 구독: OnChargeStart / OnChargeRelease / OnAimAdjust
│     _weaponController = GetComponentInChildren 으로 자동 탐색
│
│  ─── 렌더링 / 물리 ──────────────────────────────────────────
├── [Animator]                   *     Player.controller 연결
├── [Rigidbody2D]                *     Collision Detection = Continuous
│                                      Gravity Scale = MovementSettings.GravityScale
├── [SpriteRenderer]             *     플레이어 스프라이트
├── [CapsuleCollider2D]          *     물리 충돌 (Ground / Wall 레이어와 충돌)
│
│  ─── 자식 오브젝트 ──────────────────────────────────────────
├── GroundCheck                        지면 감지 기준점 (Transform 만)
│
├── AimLine                            차징 조준선
│     ├── [ChargeAimLine]        v1.0  LineRenderer 제어 + DOTween 피드백
│     │     └── _playerTransform = Player Transform  *
│     └── [LineRenderer]               조준선 렌더링
│
└── Weapon                             무기 시스템 루트
      │
      │  ─── 컨트롤러 ──────────────────────────────────────────
      ├── [PlayerWeaponController] v1.4  열쇠 교체 핵심 컨트롤러
      │     ├── (SO) KeyInventoryDataSO        *
      │     ├── _weaponEntries[0]  keyType=Rusty / weapon=RustyKeyWeapon
      │     ├── _weaponEntries[1]  keyType=Seal  / weapon=SealKeyWeapon / sealData=SealData_Dash.asset
      │     ├── _movementAnimator  MovementAnimator
      │     ├── _weaponAnimator    PlayerWeaponAnimator
      │     └── _weaponMover       PlayerWeaponMover
      │
      │  ─── 무기 구현체 (비활성 대기) ──────────────────────────
      ├── [RustyKeyWeapon]         v1.4  3단 콤보 + 공중 공격
      │     └── _hitboxManager = PlayerWeaponHitboxManager  *
      │
      ├── [SealKeyWeapon]          v1.0  단발 투사체 봉인 무기
      │     ├── _projectilePrefab = SealProjectile.prefab  *
      │     └── _firePoint = FirePoint Transform (선택)
      │
      │  ─── 무기 이동 / 애니메이션 ──────────────────────────────
      ├── [PlayerWeaponAnimator]   v1.2  무기 이벤트 구독 → PlayerWeaponMover 호출
      │     OnAirAttackSide / OnAirAttackDown / OnAirAttackUp 구독 추가
      │     └── _weaponMover (자동 탐색)
      │
      ├── [PlayerWeaponMover]      v1.1  DOTween 스윙 이동 전담
      │     OnFlipped 구독: _originLocalPosition.x 반전 + SpriteRenderer.flipX
      │
      ├── [PlayerWeaponHitboxManager] v1.1  히트박스 관리
      │     OnFlipped 구독: FlipHitboxes() → 각 Hitbox localPosition.x 반전
      │     └── _hitLayer = Enemy 레이어  *
      │
      │  ─── 히트박스                          Layer: PlayerHitbox
      ├── Hitbox_Combo1       [BoxCollider2D]   isTrigger=ON
      ├── Hitbox_Combo2       [BoxCollider2D]   isTrigger=ON
      ├── Hitbox_Combo3       [BoxCollider2D]   isTrigger=ON
      └── Hitbox_AirAttack    [BoxCollider2D]   isTrigger=ON
```

### Player Inspector 연결 체크리스트

**Player 루트**

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| PlayerMover | _settings | MovementSettings.asset * |
| PlayerMover | _groundCheck | GroundCheck Transform * |
| PlayerMover | _trailRenderer | TrailRenderer (선택) |
| PlayerChargeAttack | _aimLine | AimLine/ChargeAimLine (자동 탐색) |
| PlayerChargeAttack | _firePoint | FirePoint Transform (선택) |
| ChargeAimLine | _playerTransform | Player Transform * |
| Animator | Controller | Player.controller * |

**Weapon 오브젝트**

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| PlayerWeaponController | _inventory | KeyInventory.asset * |
| PlayerWeaponController | _weaponEntries[0] | keyType=Rusty / weapon=RustyKeyWeapon |
| PlayerWeaponController | _weaponEntries[1] | keyType=Seal / weapon=SealKeyWeapon / sealData=SealData_Dash.asset |
| PlayerWeaponController | _movementAnimator | MovementAnimator |
| PlayerWeaponController | _weaponAnimator | PlayerWeaponAnimator |
| PlayerWeaponController | _weaponMover | PlayerWeaponMover |
| RustyKeyWeapon | _hitboxManager | PlayerWeaponHitboxManager |
| SealKeyWeapon | _projectilePrefab | SealProjectile.prefab * |
| PlayerWeaponHitboxManager | _hitboxes[0~3] | 각 Hitbox BoxCollider2D * |
| PlayerWeaponHitboxManager | _hitLayer | Enemy 레이어 * |

---

## Animator Controller — Player.controller

```
파라미터 전체 목록

  Float   : Speed        Mathf.Abs(MoveInput)
            VelocityY    Rigidbody2D.velocity.y (Fall 전환 조건)

  Bool    : IsGrounded   PlayerMover.IsGrounded
            IsFiring     외부 SetFiring()

  Trigger : Jump         PlayerMover.OnJumped
            DoubleJump   PlayerMover.OnDoubleJumped
            Dash         PlayerMover.OnDashStarted
            AttackCombo1 RustyKeyWeapon.OnCombo1Started
            AttackCombo2 RustyKeyWeapon.OnCombo2Started
            AttackCombo3 RustyKeyWeapon.OnCombo3Started
            AirAttack    RustyKeyWeapon.OnAirAttackStarted

스테이트 목록

  Base Layer
    PlayerIdle / PlayerMove / PlayerJump / PlayerFall
    PlayerDash / PlayerDoubleJump

  Attack Layer
    PlayerAttack01 / PlayerAttack02 / PlayerAttack03
    PlayerAirAttack

전환 조건

  Idle/Move → PlayerJump      : Jump(Trigger)
  PlayerJump → PlayerFall     : VelocityY < -0.1
  AnyState → PlayerAttack01   : AttackCombo1 + IsGrounded=true
  Attack01 → Attack02         : AttackCombo2 + ExitTime 0.5
  Attack02 → Attack03         : AttackCombo3 + ExitTime 0.5
  Attack01/02/03 → PlayerIdle : ExitTime 1.0
  AnyState → PlayerAirAttack  : AirAttack + IsGrounded=false
  PlayerAirAttack → PlayerFall: ExitTime 1.0

주의사항

  Attack 클립 Loop Time = OFF 필수
  AnimatorOverrideController : 스프라이트 완성 후 연결
```

---

## ScriptableObject 목록

```
Assets/KEY/DataSO/
  MovementSettings.asset        이동 수치 SO

Assets/KEY/DataSO/Keys/
  RustyKeyData.asset            녹슨 열쇠 (KeyDataSO v1.3)
  HookKeyData.asset             갈고리 열쇠 ← 추후
  SpringKeyData.asset           태엽 열쇠   ← 추후

Assets/KEY/DataSO/Seals/
  SealData_Dash.asset           돌진 봉인 (SealDataSO / sealType=Dash)
  SealData_Guard.asset          방어 봉인 (SealDataSO / sealType=Guard)
  SealData_Move.asset           이동 봉인 (SealDataSO / sealType=Move)
  SealData_Attack.asset         공격 봉인 (SealDataSO / sealType=Attack)

Assets/KEY/DataSO/Inventory/
  KeyInventory.asset            보유 열쇠 목록 SO (KeyInventoryDataSO)
    └── _defaultKeys[0] = RustyKeyData.asset

Assets/KEY/DataSO/Enemy/
  DummyData.asset               더미 적 수치 (EnemyDataSO / enemyType=Dummy)
  DummyLockedData.asset         자물쇠 더미 수치 (enemyType=DummyLocked)
  KnightData.asset              기사형 수치 (EnemyDataSO / enemyType=Knight)
```

### RustyKeyData.asset 기본값

```
keyName               : 녹슨 열쇠
keyType               : KeyType.Rusty
baseDamage            : 10
comboCount            : 3
hitboxDuration        : 0.15
attackStateDuration   : 1.0
comboWindowStartRatio : 0.5
hitboxStartRatio      : 0.1
hitboxEndRatio        : 0.45
comboMultipliers      : [1.0, 1.2, 1.5]
airAttackMultiplier   : 1.3
swingDistance         : 0.5
swingDuration         : 0.08
returnDuration        : 0.15
airSwingDistance      : 0.4
minChargeTime         : 0.3
maxChargeTime         : 1.5
chargeAimAngleStep    : 15
chargeAimAngleRange   : 60
chargeProjectilePrefab: ChargeProjectile.prefab (연결 필수)
keySprite             : (추후)
overrideController    : (추후)
```

---

## MovementSettings SO 기본값

```
MoveSpeed            : 5
JumpForce            : 14
MaxJumpCount         : 2
DoubleJumpMultiplier : 0.85
CoyoteTime           : 0.1
JumpBufferTime       : 0.15
GravityScale         : 3
DashDistance         : 5
DashDuration         : 0.2
DashCooldown         : 2.3
DashGravityScale     : 0
DashBodyWidth        : 0.25
GroundLayer          : Ground 레이어 *
GroundCheckRadius    : 0.1
DashWallLayer        : Ground + Wall 레이어 *
```

---

## Prefab 목록

```
Assets/KEY/Prefabs/
  SealProjectile.prefab         봉인 투사체
  ChargeProjectile.prefab       차징 투사체

Assets/KEY/Prefabs/UI/
  WeaponSlot.prefab             무기 슬롯 UI
```

### SealProjectile Prefab

```
SealProjectile                             Layer: PlayerHitbox
├── [SealProjectile]
│     └── _sealLayer = Enemy 레이어  *
├── [Rigidbody2D]    GravityScale=0 / Continuous
├── [CircleCollider2D] isTrigger=ON / radius=0.15
└── [SpriteRenderer]  (스프라이트: SealDataSO.projectileSprite 런타임 적용)
```

### ChargeProjectile Prefab

```
ChargeProjectile                           Layer: PlayerHitbox
├── [ChargeProjectile]
│     ├── _enemyLayer   = Enemy 레이어  *
│     └── _terrainLayer = Ground + Wall 레이어  *
├── [Rigidbody2D]    GravityScale=0 / Continuous
├── [CircleCollider2D] isTrigger=ON
└── [SpriteRenderer]  (추후 연결)
```

---

## Enemy_Dummy                             Layer: Enemy

```
Enemy_Dummy
├── [EnemyDummy]           완전 정지 더미 (EnemyBase 상속)
│     └── (SO) EnemyDataSO  * enemyType=Dummy
├── [Rigidbody2D]          gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]    물리 충돌 (Ground 레이어와 충돌)
└── [SpriteRenderer]
```

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| EnemyDummy | _settings | DummyData.asset |

---

## Enemy_DummyLocked                       Layer: Enemy

```
Enemy_DummyLocked
├── [EnemyDummyLocked]     자물쇠 있는 정지 더미 (EnemyBase 상속)
│     └── (SO) EnemyDataSO  * enemyType=DummyLocked
├── [Rigidbody2D]          gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]    물리 충돌
├── [SpriteRenderer]
└── Lock                                   Layer: Lock
      ├── [LockComponent]  피격 횟수 누적 / 해제 이벤트
      │     └── _requiredHitCount : 3 (기본값)
      ├── [SpriteRenderer]
      └── [BoxCollider2D]  isTrigger=ON
```

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| EnemyDummyLocked | _settings | DummyLockedData.asset |
| EnemyDummyLocked | _lockComponent | Lock 오브젝트의 LockComponent |

---

## Enemy_Knight                        Layer: Enemy
```
│
├── [EnemyKnight]                   v2.0
│     _settings    = KnightData.asset
│     _locks       = [Lock의 LockComponent]  ← 리스트
│     _shieldCollider = ShieldCollider의 Collider2D
│
├── [EnemyAI]                       v5.0
│     (DataSO Inspector 연결 없음 — EnemyBase에서 자동 취득)
│
├── [EnemyKnightChargeAttack]       v2.0
│     _chargeHitbox   = ChargeHitbox의 Collider2D (선택)
│     _lineRenderer   = ChargeWarningLine의 LineRenderer
│     _countdownText  = (선택) TMP
│
├── [EnemySensor]                   v2.0
│     (DataSO Inspector 연결 없음 — EnemyAI.Start()에서 SetData 주입)
│
├── [EnemySealComponent]            v1.0
│     _overlayRenderer = SealOverlay/SpriteRenderer
│
├── [Rigidbody2D]
│     Gravity Scale = 1
│     Freeze Rotation Z = ON
│     Collision Detection = Continuous
│
├── [CapsuleCollider2D]             물리 충돌 본체
│
├── [SpriteRenderer]
│
│
├── ShieldCollider                  Layer: EnemyShield  ← 신규 생성
│     localPosition = (+0.5, 0, 0)  기사 정면(오른쪽) 기준
│     [BoxCollider2D]
│           isTrigger = OFF         ← 물리 충돌로 플레이어 통과 차단
│           size = (0.3, 1.2)
│
├── Lock                            Layer: EnemyLock
│     localPosition = (-1.7, 0, 0)  기사 후방(왼쪽) ← +1.7 → -1.7 수정
│     [LockComponent]               v2.0
│     [SpriteRenderer]
│     [BoxCollider2D]
│           isTrigger = ON
│           size = (0.5, 0.5)
│
├── EnemyChargeAttackHitBox         Layer: EnemyAttackHit  (선택)
│     [BoxCollider2D]
│           isTrigger = ON
│
├── ChargeWarningLine
│     [LineRenderer]
│           positionCount = 2
│           Width = 0.05
│
└── SealOverlay
      [SpriteRenderer]              EnemySealComponent._overlayRenderer 연결
```

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| EnemyKnight | _settings | KnightData.asset |
| EnemyKnight | _backLock | Lock_Back의 LockComponent |
| EnemyAI | _settings | KnightData.asset |
| EnemyKnightAttack | _hitbox | AttackHitbox의 BoxCollider2D |
| EnemySealComponent | _overlayRenderer | SealOverlay/SpriteRenderer |

### KnightData.asset 기본값

```
enemyName          : 기사
enemyType          : EnemyType.Knight
maxHp              : 150
knockbackForce     : 4
knockbackDecay     : 0.8
iFrameDuration     : 0.3
hitFlashInterval   : 0.07
patrolSpeed        : 2
chaseSpeed         : 3.5
idleTimeMin        : 1.0
idleTimeMax        : 3.0
idleChance         : 0.4
patrolSightRange   : 6
chaseSightRadius   : 10
attackRange        : 1.5
wallCheckDistance  : 0.6
cliffCheckDistance : 1.0
cliffCheckOffset   : 0.4
attackDamage       : 15
attackCooldown     : 2.0
attackDuration     : 0.3
playerLayer        : Player *
groundLayer        : Ground *
```

### EnemySensor Gizmos 색상 범례

```
노란선  : 순찰 직선 감지 Ray (patrolSightRange)
빨간선  : 벽 감지 Ray (wallCheckDistance)
보라선  : 낭떠러지 하향 Ray (cliffCheckDistance)
주황원  : 추격 OverlapCircle (chaseSightRadius)
빨간원  : 공격 사정거리 (attackRange)
```

### EnemyAI 상태 전환

```
Patrol ──(직선 감지)──→ Chase
Patrol ──(벽/낭떠러지)─→ 방향 반전 → (idleChance 확률) → Idle
Idle   ──(대기 완료)──→ Patrol
Idle   ──(직선 감지)──→ Chase
Chase  ──(사정거리)───→ Attack
Chase  ──(범위 이탈)──→ Patrol
Attack ──(완료)───────→ Chase

봉인 체크 (EnemySealComponent)
  OnPatrolMove()  : IsSealed(Move/Dash) → StopHorizontal()
  OnChaseMove()   : IsSealed(Move)      → StopHorizontal()
  OnEnterAttack() : IsSealed(Attack)    → ChangeState(Chase)
```

---

## UI — WeaponHUD                          Layer: UI

```
Canvas
└── WeaponHUD
      ├── [WeaponHUDController]  v1.2  HUD 전체 관리
      │     ├── (SO) KeyInventoryDataSO  *
      │     ├── _slotPrefab      WeaponSlot.prefab  *
      │     ├── _slotContainer   SlotContainer Transform  *
      │     ├── _panelRoot       KeySwapPanel GameObject  *
      │     ├── _equippedIcon    EquippedIcon Image
      │     └── _equippedName    EquippedName TMP
      │
      ├── EquippedWeaponDisplay        항상 표시
      │     ├── EquippedIcon [Image]   keySprite 표시
      │     └── EquippedName [TMP]     keyName 표시
      │
      └── KeySwapPanel                 LCtrl 누름 시 활성 / 뗌 or 교체 시 비활성
            └── SlotContainer          슬롯 동적 생성 부모 (HorizontalLayoutGroup 권장)
                  └── WeaponSlot × N   런타임 동적 생성
```

### WeaponSlot Prefab 구조

```
WeaponSlot (Assets/KEY/Prefabs/UI/WeaponSlot.prefab)
├── [WeaponSlotUI]
├── [Image]                슬롯 배경 (장착=노랑 / 미장착=어두운 회색)
├── Icon [Image]           keyData.keySprite
├── KeyName [TMP]          keyData.keyName
├── KeyBinding [TMP]       슬롯 키 이름 (예: "1", "Q", "A", "Z")
└── EquippedIndicator      장착 중 강조 오브젝트 (테두리 등)
```

---

## 키 바인딩 전체 요약

| 동작 | 키 | 모드 |
|---|---|---|
| 이동 왼쪽/오른쪽 | ← → (방향키) | 항상 |
| 점프 | Space | 항상 |
| 대쉬 | Left Shift | 항상 |
| 공격 | A | InGame |
| KeySwap 모드 | Left Ctrl (누름 유지) | 항상 |
| 무기 교체 슬롯 0~3 | 1 2 3 4 | KeySwap 모드 중 |
| 무기 교체 슬롯 4~7 | Q W E R | KeySwap 모드 중 |
| 무기 교체 슬롯 8~11 | A S D F | KeySwap 모드 중 |
| 무기 교체 슬롯 12~15 | Z X C V | KeySwap 모드 중 |
| 차징 시작 / 발사 | S (누름→차징 / 뗌→발사) | InGame |
| 조준 위 / 아래 | ↑ / ↓ | 차징 중 |

---

## Scene 공통 (추후)

```
GameManager        씬 전역 관리
CinemachineCamera  플레이어 추적
SpawnPoint         플레이어 시작 위치
```