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
├── [MovementAnimator]             Animator 파라미터 동기화
├── [PlayerMovementFacade]         외부 단일 진입점 (싱글턴)
├── [Animator]                   * Player.controller 연결
├── [Rigidbody2D]                * Gravity Scale = MovementSettings.GravityScale
├── [SpriteRenderer]             * 플레이어 스프라이트
├── [CapsuleCollider2D]          * 물리 충돌 콜라이더
│
├── GroundCheck                    발 아래 빈 오브젝트 (지면 감지 기준점)
│     └── Transform 만 존재
│
└── Weapon
      ├── [WeaponKeyController]  * 열쇠 교체 핵심 컨트롤러
      │     ├── (SO) KeyInventoryDataSO  * 보유 열쇠 목록
      │     ├── _weaponEntries[0]  keyType=Rusty  / weapon=RustyKeyWeapon
      │     └── _weaponEntries[1]  keyType=Hook   / weapon=HookKeyWeapon (추후)
      │
      ├── [RustyKeyWeapon]         비활성 대기 (KeyType.Rusty)
      │     └── KeyDataSO 는 WeaponKeyController 가 런타임 주입
      ├── [HookKeyWeapon]          비활성 대기 (KeyType.Hook)   ← 추후
      ├── [SpringKeyWeapon]        비활성 대기 (KeyType.Spring) ← 추후
      │
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
| WeaponKeyController | _inventory | KeyInventoryDataSO |
| WeaponKeyController | _weaponEntries[0] | keyType=Rusty / weapon=RustyKeyWeapon |
| WeaponKeyController | _animator | Player Animator (추후) |
| RustyKeyWeapon | _hitboxManager | PlayerWeaponHitboxManager |
| PlayerWeaponHitboxManager | _hitboxes[0~3] | 각 Hitbox BoxCollider2D |
| PlayerWeaponHitboxManager | _hitLayer | Enemy 레이어 |
| Animator | Controller | Player.controller |

---

## ScriptableObject 목록

```
Assets/KEY/DataSO/
  MovementSettings.asset      이동 수치 SO

Assets/KEY/DataSO/Keys/
  RustyKeyData.asset          녹슨 열쇠 (KeyDataSO)
  HookKeyData.asset           갈고리 열쇠 ← 추후
  SpringKeyData.asset         태엽 열쇠   ← 추후

Assets/KEY/DataSO/Inventory/
  KeyInventory.asset          보유 열쇠 목록 SO (KeyInventoryDataSO)
    └── _defaultKeys[0] = RustyKeyData.asset

Assets/KEY/DataSO/Enemy/
  DummyData.asset             더미 적 수치 (EnemyDataSO / enemyType=Dummy)
  DummyLockedData.asset       자물쇠 더미 수치 (enemyType=DummyLocked)
  KnightData.asset            기사형 수치 (EnemyDataSO / enemyType=Knight)
```

### RustyKeyData.asset 기본값

```
keyName             : 녹슨 열쇠
keyType             : KeyType.Rusty
baseDamage          : 10
comboCount          : 3
comboWindowTime     : 0.8
hitboxDuration      : 0.15
comboMultipliers    : [1.0, 1.2, 1.5]
airAttackMultiplier : 1.3
keySprite           : (추후)
overrideController  : (추후)
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

## Animator Controller — Player.controller

```
Base Layer (이동)
  파라미터
    Speed      (Float)   Mathf.Abs(MoveInput)
    IsGrounded (Bool)    PlayerMover.IsGrounded
    IsFiring   (Bool)    PlayerMovementFacade.SetFiring()
    Dash       (Trigger) PlayerMover.OnDashStarted
    DoubleJump (Trigger) PlayerMover.OnDoubleJumped

  스테이트
    PlayerIdle / PlayerMove / PlayerJump / PlayerFall
    PlayerDash / PlayerDoubleJump

Attack Layer — 스프라이트 완성 후 추가 예정
  파라미터 (예정)
    AttackCombo1 / AttackCombo2 / AttackCombo3 / AirAttack (Trigger)
  AnimatorOverrideController
    열쇠 교체 시 WeaponKeyController.TrySwapAnimatorOverride() 로 스왑
```

---

## Layer 설정

| 오브젝트 | Layer |
|---|---|
| Player | Player |
| Hitbox_* (플레이어 무기) | PlayerHitbox |
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

### 컴포넌트 연결

| 컴포넌트 | 연결 항목 | 값 |
|---|---|---|
| EnemyDummy | _settings | DummyData.asset |

### DummyData.asset 기본값

```
enemyName        : 더미
enemyType        : EnemyType.Dummy
maxHp            : 100
knockbackForce   : 5
knockbackDecay   : 0.8
iFrameDuration   : 0.3
hitFlashInterval : 0.07
(이동/감지/공격 수치 — 미사용)
```

---

## Enemy_DummyLocked (자물쇠 있는 더미)

```
Enemy_DummyLocked
├── [EnemyDummyLocked]     자물쇠 있는 정지 더미 (EnemyBase 상속)
│     └── (SO) EnemyDataSO  * enemyType=DummyLocked
├── [Rigidbody2D]          gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]    물리 충돌
├── [SpriteRenderer]       본체 스프라이트
└── Lock                   자물쇠 자식 오브젝트
      ├── [LockComponent]  피격 횟수 누적 / 해제 이벤트
      ├── [SpriteRenderer] 자물쇠 스프라이트
      └── [BoxCollider2D]  isTrigger=ON
```

### 컴포넌트 연결

| 컴포넌트 | 연결 항목 | 값 |
|---|---|---|
| EnemyDummyLocked | _settings | DummyLockedData.asset |
| EnemyDummyLocked | _lockComponent | Lock 오브젝트의 LockComponent |
| LockComponent | _requiredHitCount | 3 (기본값) |

---

## Enemy_Knight (기사형)

```
Enemy_Knight
├── [EnemyKnight]          EnemyBase 상속 — 정면 방패 / 등 뒤 자물쇠 피격 판단
├── [EnemyAI]              * 공용 AI 상태머신 — enemyType=Knight 로 행동 분기
│     └── (SO) EnemyDataSO  * KnightData.asset
├── [EnemyKnightAttack]           EnemyAttackBase 상속 — 근접 내려치기 단타
├── [EnemySensor]            공용 감지 컴포넌트 (EnemyAI 가 데이터 주입)
├── [Rigidbody2D]            gravityScale=1 / FreezeRotation Z
├── [CapsuleCollider2D]      물리 충돌
├── [SpriteRenderer]
│
├── Lock_Back                등 뒤 자물쇠
│     ├── [LockComponent]    피격 횟수 누적 / 해제 이벤트
│     ├── [SpriteRenderer]   자물쇠 스프라이트
│     └── [BoxCollider2D]    isTrigger=ON
│
└── AttackHitbox             기사 공격 히트박스
      └── [BoxCollider2D]    isTrigger=ON
```

### 컴포넌트 연결

| 컴포넌트 | 연결 항목 | 값 |
|---|---|---|
| EnemyKnight | _settings | KnightData.asset |
| EnemyKnight | _backLock | Lock_Back의 LockComponent |
| EnemyAI | _settings | KnightData.asset |
| KnightAttack | _hitbox | AttackHitbox의 BoxCollider2D |

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

### EnemyAI 상태 전환 규칙

```
Patrol ──(직선 감지)──→ Chase
Patrol ──(벽/낭떠러지)─→ 방향 반전 → (idleChance 확률) → Idle
Idle   ──(대기 완료)──→ Patrol
Idle   ──(직선 감지)──→ Chase
Chase  ──(사정거리)───→ Attack
Chase  ──(범위 이탈)──→ Patrol
Attack ──(완료)───────→ Chase
```

---

## Scene 공통 (추후)

```
GameManager        씬 전역 관리
CinemachineCamera  플레이어 추적
SpawnPoint         플레이어 시작 위치
```