# Key_Hierarchy — Scene 오브젝트 배치도

Unity 버전 6000.3.10f1 | 2D Universal | namespace : KEY

---

## 규칙

- `[컴포넌트]` : 해당 오브젝트에 부착된 컴포넌트
- `(SO)` : ScriptableObject 참조
- `*` : 필수 연결 항목
- 들여쓰기 = 부모-자식 관계

---

## Player

```
Player
├── [InputManager]               * 모든 키 입력 통합 (이동 + 무기)
├── [PlayerMover]                * 이동 / 점프 / 대쉬 물리
│     └── (SO) MovementSettings  * 이동 수치 설정
├── [MovementAnimator]             Animator 파라미터 동기화 (v2.1)
├── [PlayerMovementFacade]         외부 단일 진입점 (싱글턴)
├── [Animator]                   * Player.controller 연결
├── [Rigidbody2D]                * Collision Detection = Continuous 권장
│                                  Gravity Scale = MovementSettings.GravityScale
├── [SpriteRenderer]             * 플레이어 스프라이트
├── [CapsuleCollider2D]          * 물리 충돌 콜라이더
│
├── GroundCheck                    발 아래 빈 오브젝트 (지면 감지 기준점)
│
└── Weapon
      ├── [PlayerWeaponController]  * 열쇠 교체 핵심 컨트롤러 (v1.4)
      │     ├── (SO) KeyInventoryDataSO  * 보유 열쇠 목록
      │     ├── _weaponEntries[0]  keyType=Rusty / weapon=RustyKeyWeapon / sealData=(비움)
      │     └── _weaponEntries[1]  keyType=Seal  / weapon=SealKeyWeapon  / sealData=SealData_Dash.asset
      │
      ├── [RustyKeyWeapon]         비활성 대기 (KeyType.Rusty)
      ├── [SealKeyWeapon]          비활성 대기 (KeyType.Seal)
      │     └── _projectilePrefab = SealProjectile.prefab
      │
      ├── [PlayerWeaponAnimator]        무기 이벤트 구독 → PlayerWeaponMover 호출
      ├── [PlayerWeaponMover]           DOTween 스윙 이동 전담 (v1.1)
      └── [PlayerWeaponHitboxManager] * 히트박스 관리
            ├── Hitbox_Combo1    [BoxCollider2D] isTrigger=ON
            ├── Hitbox_Combo2    [BoxCollider2D] isTrigger=ON
            ├── Hitbox_Combo3    [BoxCollider2D] isTrigger=ON
            └── Hitbox_AirAttack [BoxCollider2D] isTrigger=ON
```

### Player 컴포넌트 연결 체크리스트

| 컴포넌트 | 연결 항목 | 값 |
|---|---|---|
| PlayerMover | _settings | MovementSettings SO |
| PlayerMover | _groundCheck | GroundCheck Transform |
| PlayerMover | _trailRenderer | (선택) TrailRenderer |
| PlayerWeaponController | _inventory | KeyInventoryDataSO |
| PlayerWeaponController | _weaponEntries[0] | keyType=Rusty / weapon=RustyKeyWeapon |
| PlayerWeaponController | _weaponEntries[1] | keyType=Seal / weapon=SealKeyWeapon / sealData=SealData_Dash.asset |
| PlayerWeaponController | _movementAnimator | MovementAnimator |
| PlayerWeaponController | _weaponAnimator | PlayerWeaponAnimator |
| PlayerWeaponController | _weaponMover | PlayerWeaponMover |
| RustyKeyWeapon | _hitboxManager | PlayerWeaponHitboxManager |
| SealKeyWeapon | _projectilePrefab | SealProjectile.prefab |
| SealKeyWeapon | _firePoint | (선택) FirePoint Transform |
| PlayerWeaponAnimator | _weaponMover | PlayerWeaponMover (자동 탐색) |
| PlayerWeaponHitboxManager | _hitboxes[0~3] | 각 Hitbox BoxCollider2D |
| PlayerWeaponHitboxManager | _hitLayer | Enemy 레이어 |
| Animator | Controller | Player.controller |

---

## SealProjectile (Prefab)

```
SealProjectile (Prefab)
├── [SealProjectile]         봉인 투사체 컴포넌트
│     └── _sealLayer = Enemy 레이어
├── [Rigidbody2D]            GravityScale=0 / Collision Detection=Continuous
├── [CircleCollider2D]       isTrigger=true / radius=0.15
└── [SpriteRenderer]         (스프라이트는 SealDataSO.projectileSprite 런타임 적용)
```

**저장 경로**: `Assets/KEY/Prefabs/SealProjectile.prefab`
**Layer**: `PlayerHitbox` (기존 플레이어 무기 레이어와 동일)

---

## ScriptableObject 목록

```
Assets/KEY/DataSO/
  MovementSettings.asset      이동 수치 SO

Assets/KEY/DataSO/Keys/
  RustyKeyData.asset          녹슨 열쇠 (KeyDataSO)

Assets/KEY/DataSO/Seals/
  SealData_Dash.asset         돌진 봉인 (SealDataSO / sealType=Dash)
  SealData_Guard.asset        방어 봉인 (SealDataSO / sealType=Guard)
  SealData_Move.asset         이동 봉인 (SealDataSO / sealType=Move)
  SealData_Attack.asset       공격 봉인 (SealDataSO / sealType=Attack)

Assets/KEY/DataSO/Inventory/
  KeyInventory.asset          보유 열쇠 목록 SO (KeyInventoryDataSO)
    └── _defaultKeys[0] = RustyKeyData.asset

Assets/KEY/DataSO/Enemy/
  DummyData.asset             더미 적 수치 (EnemyDataSO / enemyType=Dummy)
  DummyLockedData.asset       자물쇠 더미 수치 (enemyType=DummyLocked)
  KnightData.asset            기사형 수치 (EnemyDataSO / enemyType=Knight)
```

### SealData_Dash.asset 기본값

```
sealKeyName       : 봉인 열쇠 (돌진)
sealType          : SealType.Dash
sealDuration      : 4.0
maxSealCount      : 2
projectileSpeed   : 12.0
projectileLifetime: 2.0
projectileScale   : 1.0
cooldown          : 1.5
sealFlashInterval : 0.4
sealColor         : (0.3, 0.5, 1.0, 1.0)  ← 파란색
```

---

## Animator Controller — Player.controller

```
Base Layer (이동 + 공격 통합)
  파라미터
    Speed       (Float)   Mathf.Abs(MoveInput)
    VelocityY   (Float)   Rigidbody2D.velocity.y
    IsGrounded  (Bool)    PlayerMover.IsGrounded
    IsFiring    (Bool)    PlayerMovementFacade.SetFiring()
    Jump        (Trigger) PlayerMover.OnJumped
    DoubleJump  (Trigger) PlayerMover.OnDoubleJumped
    Dash        (Trigger) PlayerMover.OnDashStarted
    AttackCombo1 (Trigger) RustyKeyWeapon.OnCombo1Started
    AttackCombo2 (Trigger) RustyKeyWeapon.OnCombo2Started
    AttackCombo3 (Trigger) RustyKeyWeapon.OnCombo3Started
    AirAttack   (Trigger) RustyKeyWeapon.OnAirAttackStarted

  스테이트
    PlayerIdle / PlayerMove / PlayerJump / PlayerFall
    PlayerDash / PlayerDoubleJump
    PlayerAttack01 / PlayerAttack02 / PlayerAttack03
    PlayerAirAttack

  전환 규칙
    Idle/Move → PlayerJump      : Jump Trigger
    PlayerJump → PlayerFall     : VelocityY < -0.1
    AnyState → PlayerAttack01   : AttackCombo1 + IsGrounded=true
    Attack01 → Attack02         : AttackCombo2 + ExitTime=0.5
    Attack02 → Attack03         : AttackCombo3 + ExitTime=0.5
    Attack01/02/03 → PlayerIdle : ExitTime=1.0 (Loop Time=OFF 필수)
    AnyState → PlayerAirAttack  : AirAttack + IsGrounded=false
    PlayerAirAttack → PlayerFall: ExitTime=1.0
```

---

## Layer 설정

| 오브젝트 | Layer |
|---|---|
| Player | Player |
| Hitbox_* (플레이어 무기) | PlayerHitbox |
| SealProjectile | PlayerHitbox |
| Enemy_* | Enemy |
| Lock_* (자물쇠) | PlayerHitbox 감지 대상 |
| AttackHitbox (적 공격) | Player 감지 대상 |

---

## Enemy_Dummy (자물쇠 없는 더미)

```
Enemy_Dummy
├── [EnemyDummy]           완전 정지 더미 (EnemyBase 상속)
│     └── (SO) EnemyDataSO  * enemyType=Dummy
├── [Rigidbody2D]          gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]    물리 충돌
└── [SpriteRenderer]
```

---

## Enemy_DummyLocked (자물쇠 있는 더미)

```
Enemy_DummyLocked
├── [EnemyDummyLocked]     자물쇠 있는 정지 더미 (EnemyBase 상속)
│     └── (SO) EnemyDataSO  * enemyType=DummyLocked
├── [Rigidbody2D]          gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]    물리 충돌
├── [SpriteRenderer]
└── Lock
      ├── [LockComponent]  피격 횟수 누적 / 해제 이벤트
      ├── [SpriteRenderer]
      └── [BoxCollider2D]  isTrigger=ON
```

---

## Enemy_Knight (기사형)

```
Enemy_Knight
├── [EnemyKnight]                EnemyBase 상속 — 방패/자물쇠 피격 판단 (v1.2)
│     └── (SO) EnemyDataSO       * KnightData.asset
├── [EnemyAI]                    공용 AI 상태머신 — enemyType=Knight (v3.0)
│     └── (SO) EnemyDataSO       * KnightData.asset
├── [EnemyKnightAttack]          EnemyAttackBase 상속 — 근접 내려치기
├── [EnemySensor]                공용 감지 컴포넌트
├── [EnemySealComponent]         봉인 상태 관리 (v1.0)  ← v0.10 신규
│     └── _overlayRenderer = SealOverlay/SpriteRenderer
├── [Rigidbody2D]                gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]
├── [SpriteRenderer]
│
├── Lock_Back                    등 뒤 자물쇠
│     ├── [LockComponent]
│     ├── [SpriteRenderer]
│     └── [BoxCollider2D]        isTrigger=ON
│
├── AttackHitbox                 기사 공격 히트박스
│     └── [BoxCollider2D]        isTrigger=ON
│
└── SealOverlay                  봉인 오버레이 (v0.10 신규)
      └── [SpriteRenderer]       EnemySealComponent._overlayRenderer 에 연결
```

### Enemy_Knight 컴포넌트 연결

| 컴포넌트 | 연결 항목 | 값 |
|---|---|---|
| EnemyKnight | _settings | KnightData.asset |
| EnemyKnight | _backLock | Lock_Back의 LockComponent |
| EnemyAI | _settings | KnightData.asset |
| EnemyKnightAttack | _hitbox | AttackHitbox의 BoxCollider2D |
| EnemySealComponent | _overlayRenderer | SealOverlay/SpriteRenderer |

### EnemyAI 봉인 체크 (v3.0)

```
OnPatrolMove()  : IsSealed(Move) || IsSealed(Dash) → StopHorizontal()
OnChaseMove()   : IsSealed(Move)                   → StopHorizontal()
OnEnterAttack() : IsSealed(Attack)                 → ChangeState(Chase)
```

### EnemyKnight Guard 봉인 체크 (v1.2)

```
TakeDamage(info)
  자물쇠 해제됨?       → EnemyBase.TakeDamage()
  Guard 봉인 활성?     → 방패 무시 → EnemyBase.TakeDamage()
  정면 공격 (Guard 없음) → 방패 막힘 플래시
  후면 공격 (Guard 없음) → LockComponent.TakeDamage()
```

### EnemySensor Gizmos 색상 범례

```
노란선  : 순찰 직선 감지 Ray (patrolSightRange)
빨간선  : 벽 감지 Ray (wallCheckDistance)
보라선  : 낭떠러지 하향 Ray (cliffCheckDistance)
주황원  : 추격 OverlapCircle (chaseSightRadius)
빨간원  : 공격 사정거리 (attackRange)
```

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

---

## Scene 공통 (추후)

```
GameManager        씬 전역 관리
CinemachineCamera  플레이어 추적
SpawnPoint         플레이어 시작 위치
```