# Key_Hierarchy — Scene 오브젝트 배치도

Unity 버전 6000.3.10f1 | 2D Universal | namespace : KEY  
최신 버전 기준: v0.20

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
| `EnemyAttackHit` | 적 공격 판정 | ChargeHitbox (EnemyKnight 돌진 히트박스) |
| `EnemyShield` | 적 방패 콜라이더 | ShieldCollider — 물리 충돌 차단 (isTrigger=OFF) |
| `EnemyLock` | 자물쇠 콜라이더 | Lock — PlayerAttackHit 에 반응 (isTrigger=ON) |
| `Ground` | 지형 바닥 | TileMap Ground / EnemySensor.groundLayer / PlayerMover.GroundLayer |
| `Wall` | 지형 벽 | TileMap Wall / PlayerMover.DashWallLayer / ChargeProjectile._terrainLayer |
| `UI` | UI 캔버스 | Canvas 및 하위 UI 오브젝트 |

### Layer 충돌 매트릭스 요약

```
Player          ↔ Ground / Wall      : 물리 충돌 (이동/착지)
Player          ↔ EnemyAttackHit     : 플레이어 피격 (적 공격)
Player          ↔ EnemyShield        : 물리 충돌 (방패 통과 차단)
PlayerAttackHit ↔ Enemy              : 플레이어 무기 명중 판정
PlayerAttackHit ↔ EnemyLock          : 자물쇠 피격 판정
PlayerAttackHit ↔ EnemyShield        : OFF (방패는 코드로 무시)
SealProjectile(PlayerAttackHit) ↔ Enemy       : 봉인 투사체 명중
SealProjectile(PlayerAttackHit) ↔ EnemyShield : 방패 막힘 → 소멸
SealProjectile(PlayerAttackHit) ↔ Ground/Wall : 지형 충돌 → 소멸
Enemy           ↔ Ground / Wall      : 물리 충돌 (적 이동/착지)
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
├── [PlayerChargeAttack]         v1.3  차징 상태 관리 + 각도 조절 + 발사
│     이벤트 구독: OnChargeStart / OnChargeRelease / OnAimAdjust
│     Fire() → SealProjectile.Launch(KeyDataSO, facingDir, chargePower)
│
├── [PlayerHealth]               v1.0  플레이어 체력 / iFrame / 넉백
│     _maxHp=5 / _iFrameDuration=0.6 / _hitFlashInterval=0.08
│
├── [ObjectFlipController]       v1.2  자식 오브젝트 Flip 일괄 관리
│     _flipSourceType = PlayerMover
│     _flipTargets: Weapon / HitBox01~04 / FirePoint (6개)
│     _spriteRenderers: Weapon SpriteRenderer
│     SyncOrigin(dir) → PlayerWeaponMover._originLocalPosition 동기화
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
      ├── [PlayerWeaponController] v1.5  열쇠 교체 핵심 컨트롤러
      │     ├── (SO) KeyInventoryDataSO        *
      │     ├── _weaponEntries[0]  keyType=Rusty / weapon=RustyKeyWeapon
      │     ├── _movementAnimator  MovementAnimator
      │     ├── _weaponAnimator    PlayerWeaponAnimator
      │     └── _weaponMover       PlayerWeaponMover
      │     ※ SealKeyWeapon / SealDataSO 분기 제거됨 (v1.5)
      │
      │  ─── 무기 구현체 (비활성 대기) ──────────────────────────
      ├── [RustyKeyWeapon]         v1.4  3단 콤보 + 공중 공격
      │     └── _hitboxManager = PlayerWeaponHitboxManager  *
      │
      │  ─── 무기 이동 / 애니메이션 ──────────────────────────────
      ├── [PlayerWeaponAnimator]   v1.1  무기 이벤트 구독 → PlayerWeaponMover 호출
      │
      ├── [PlayerWeaponMover]      v1.2  DOTween 스윙 이동 전담
      │     SyncOrigin(dir) 추가 — ObjectFlipController 에서 호출
      │     ※ HandleFlipped() 제거 — ObjectFlipController 가 담당
      │
      ├── [PlayerWeaponHitboxManager] v1.3  히트박스 관리
      │     _enemyLayer  = Enemy 레이어  *
      │     _lockLayer   = EnemyLock 레이어  *
      │     _shieldLayer = EnemyShield 레이어  *
      │     ※ FlipHitboxes() 제거 — ObjectFlipController 가 담당
      │
      │  ─── 히트박스                       Layer: PlayerAttackHit
      ├── HitBox01  [BoxCollider2D]  isTrigger=ON  localPos=(0.7, 0, 0)
      ├── HitBox02  [BoxCollider2D]  isTrigger=ON
      ├── HitBox03  [BoxCollider2D]  isTrigger=ON
      ├── HitBox04  [BoxCollider2D]  isTrigger=ON
      └── FirePoint
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
| PlayerWeaponController | _movementAnimator | MovementAnimator |
| PlayerWeaponController | _weaponAnimator | PlayerWeaponAnimator |
| PlayerWeaponController | _weaponMover | PlayerWeaponMover |
| RustyKeyWeapon | _hitboxManager | PlayerWeaponHitboxManager |
| PlayerWeaponHitboxManager | _hitboxes[0~3] | HitBox01~04 BoxCollider2D * |
| PlayerWeaponHitboxManager | _enemyLayer | Enemy 레이어 * |
| PlayerWeaponHitboxManager | _lockLayer | EnemyLock 레이어 * |
| PlayerWeaponHitboxManager | _shieldLayer | EnemyShield 레이어 * |
| ObjectFlipController | _flipSourceType | PlayerMover |
| ObjectFlipController | _flipTargets | Weapon / HitBox01~04 / FirePoint (6개) |
| ObjectFlipController | _spriteRenderers | Weapon SpriteRenderer |

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
chargeProjectilePrefab: SealProjectile.prefab (연결 필수)
sealType              : Dash (기본값)
sealDuration          : 3.0
maxSealCount          : 2
sealProjectileSpeed   : 12
sealProjectileLifetime: 2
sealProjectileScale   : 1
sealFlashInterval     : 0.4
sealColor             : (0.3, 0.5, 1.0)
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
  SealProjectile.prefab   봉인 투사체 (모든 열쇠 공통 S키 투사체)

Assets/KEY/Prefabs/UI/
  WeaponSlot.prefab       무기 슬롯 UI
```

### SealProjectile Prefab

```
SealProjectile                             Layer: PlayerAttackHit
├── [SealProjectile]    v2.0
│     _sealLayer   = Enemy 레이어  *
│     _shieldLayer = EnemyShield 레이어  *
│     _terrainLayer = Ground + Wall 레이어  *
├── [Rigidbody2D]    GravityScale=0 / Continuous
├── [CircleCollider2D] isTrigger=ON / radius=0.15
└── [SpriteRenderer]
```

**충돌 분기**
```
EnemyShield 레이어 → HitFeedback.PlayerAttackBlocked() + 소멸
Enemy 레이어       → SealComponent.ApplySeal(KeyDataSO) + 소멸
지형 레이어        → 소멸
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
Enemy_Knight
│
├── [EnemyKnight]                   v2.1
│     _settings      = KnightData.asset
│     _locks         = [Lock의 LockComponent]  ← List<LockComponent>
│     _shieldCollider = ShieldCollider의 Collider2D
│     ※ EnemySealComponent → SealComponent 로 교체됨
│
├── [EnemyAI]                       v5.1
│     DataSO Inspector 연결 없음 — EnemyBase 에서 자동 취득
│     _sealComponent = SealComponent (EnemySealComponent 제거)
│
├── [EnemyKnightChargeAttack]       v2.1
│     _chargeHitbox     = ChargeHitbox의 BoxCollider2D
│     _lineRenderer     = ChargeWarningLine의 LineRenderer
│     _countdownDuration = 5
│     ※ FlipHitbox() 제거 — ObjectFlipController 가 담당
│
├── [EnemySensor]                   v2.0
│     DataSO Inspector 연결 없음 — EnemyAI.Start() 에서 SetData 주입
│
├── [SealComponent]                 v1.1  ← EnemySealComponent 대체
│     _overlayRenderer = SealOverlay/SpriteRenderer
│     ApplySeal(KeyDataSO) — SealProjectile 명중 시 호출
│
├── [ObjectFlipController]          v1.2
│     _flipSourceType = EnemyAI
│     _flipTargets[0] = ShieldCollider  _invertList[0]=false (정면)
│     _flipTargets[1] = Lock            _invertList[1]=true  (후방)
│     _flipTargets[2] = ChargeHitbox    _invertList[2]=false (정면)
│     _spriteRenderers[0] = SpriteRenderer
│
├── [HitFeedback 사용 컴포넌트]
│     EnemyBase.TakeDamage()  → HitFeedback.PlayerHitEnemy()
│     EnemyKnight.TakeDamage() → 방패 막힘 시 HitFeedback.PlayerAttackBlocked()
│     LockComponent.TakeDamage() → HitFeedback.PlayerHitLock()
│
├── [Rigidbody2D]           GravityScale=1 / FreezeRotation Z / Continuous
├── [CapsuleCollider2D]     물리 충돌 본체
├── [SpriteRenderer]
│
├── ShieldCollider                  Layer: EnemyShield (18)
│     localPosition = (+0.5, 0, 0)  기사 정면(오른쪽) 기준
│     [BoxCollider2D]  isTrigger=OFF  size=(0.3, 1.2)
│     ※ ObjectFlipController 가 방향 전환 시 localPosition.x 반전
│
├── Lock                            Layer: EnemyLock (17)
│     localPosition = (-1.7, 0, 0)  기사 후방(왼쪽)
│     [LockComponent]   v2.1  ※ FlipPosition() 제거
│     [SpriteRenderer]
│     [BoxCollider2D]  isTrigger=ON  size=(0.5, 0.5)
│     ※ ObjectFlipController 가 방향 전환 시 localPosition.x 반전 (invert=true)
│
├── ChargeHitbox                    Layer: EnemyAttackHit
│     [BoxCollider2D]  isTrigger=ON
│     ※ ObjectFlipController 가 방향 전환 시 localPosition.x 반전
│
├── ChargeWarningLine
│     [LineRenderer]  positionCount=2 / Width=0.05
│
└── SealOverlay
      [SpriteRenderer]  SealComponent._overlayRenderer 연결
```

| 컴포넌트 | 필드 | 값 |
|---|---|---|
| EnemyKnight | _settings | KnightData.asset |
| EnemyKnight | _locks | Lock의 LockComponent (List) |
| EnemyKnight | _shieldCollider | ShieldCollider의 Collider2D |
| EnemyAI | — | EnemyBase.Settings 자동 취득 |
| EnemyKnightChargeAttack | _chargeHitbox | ChargeHitbox의 BoxCollider2D |
| EnemyKnightChargeAttack | _lineRenderer | ChargeWarningLine의 LineRenderer |
| SealComponent | _overlayRenderer | SealOverlay/SpriteRenderer |
| ObjectFlipController | _flipSourceType | EnemyAI (1) |

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

봉인 체크 (SealComponent)
  OnPatrolMove()  : IsSealed(Move/Dash) → StopHorizontal()
  OnChaseMove()   : IsSealed(Move)      → StopHorizontal()
  OnEnterAttack() : IsSealed(Attack)    → ChangeState(Chase)
  EnemyKnight.TakeDamage() : IsSealed(Guard) → 방패 무시 피격 허용
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