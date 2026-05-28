# Key_DevSession — 개발 세션 기록

Unity 버전 6000.3.10f1 | 2D Universal | namespace : KEY

---

## 코딩 규칙

| 항목 | 규칙 |
|---|---|
| 네임스페이스 | `KEY` 통일 |
| 변수명 | `_camelCase` (언더스코어 접두사) |
| 접근 제한자 | `[SerializeField] private` 또는 `public` 명시 필수 |
| 주석 | 모든 함수·변수에 `/// <summary>` 필수 |
| 인스펙터 변수 | `[SerializeField]` 에 반드시 `[Tooltip]` 추가 |
| 싱글턴 | `public static T Instance { get; private set; }` |
| 충돌 판단 | `CompareTag` 금지 → `LayerMask` 비트 연산 |
| DOTween | 이동·페이드·펀치·쉐이크 전반 활용 |

---

## 유니티 환경

| 항목 | 내용 |
|---|---|
| Unity | 6000.3.10f1 — 2D URP |
| Cinemachine | `Unity.Cinemachine` — `CinemachineCamera` Priority 방식 |
| Input System | New Input System — 코드 직접 방식 |
| DOTween | 최신 안정 버전 (HOTween v2) |
| TextMeshPro | UI 텍스트 전용 |

---

## 세션 기록

---

### v0.1 — 이동 패키지 (POC07 → KEY 이전)

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `MovementSettings.cs` | 이동 수치 ScriptableObject | v1.0 |
| `MovementAnimator.cs` | Animator 파라미터 동기화 | v1.1 |

---

### v0.2 — 입력 통합 + 무기 시스템 1차

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `InputManager.cs` | 입력 통합 관리 (이동 + 무기) | v1.0 |
| `PlayerMover.cs` | 이동 물리 | v1.3 |
| `PlayerMovementFacade.cs` | 외부 단일 진입점 (싱글턴) | v1.1 |
| `IDamageable.cs` | 피격 인터페이스 | v1.0 |
| `PlayerWeaponBase.cs` | 무기 추상 베이스 | v1.0 |
| `PlayerWeaponHitboxManager.cs` | 히트박스 관리 | v1.0 |
| `RustyKeyWeapon.cs` | 녹슨 열쇠 구현체 | v1.0 |

---

### v0.3 — 열쇠 데이터 구조 + 무기 교체 시스템

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyType.cs` | 열쇠 타입 enum (6종) | v1.0 |
| `KeyDataSO.cs` | 열쇠 데이터 SO | v1.0 |
| `KeyInventoryDataSO.cs` | 보유 열쇠 목록 SO | v1.0 |
| `PlayerWeaponController.cs` | 열쇠 교체 컨트롤러 | v1.1 |
| `PlayerWeaponBase.cs` | 무기 베이스 | v1.1 |
| `RustyKeyWeapon.cs` | 녹슨 열쇠 구현체 | v1.1 |

---

### v0.4 — 더미 적 시스템

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `EnemyDataSO.cs` | 적 수치 SO | v1.0 |
| `EnemyBase.cs` | 적 추상 베이스, IDamageable 구현 | v1.0 |
| `LockComponent.cs` | 자물쇠 컴포넌트 | v1.0 |
| `EnemyDummy.cs` | 자물쇠 없는 정지 더미 | v1.0 |
| `EnemyDummyLocked.cs` | 자물쇠 있는 정지 더미 | v1.0 |

**구조 결정**
- 넉백: `KnockbackRoutine` 코루틴 (`velocity.x *= knockbackDecay`)
- Rigidbody2D: `gravityScale=1` / `FreezeRotation Z`
- 더미 사망 없음 — 체력 최솟값 1 고정

---

### v0.5 — 기사형 적 시스템

**완성 파일**

| 파일 | 계층 | 역할 | 버전 |
|---|---|---|---|
| `EnemySensor.cs` | 공용 | Raycast×3 + OverlapCircle×2 | v1.0 |
| `EnemyAI.cs` | 공용 | Patrol/Idle/Chase/Attack 상태머신 | v2.0 |
| `EnemyAttackBase.cs` | 공용 | 공격 쿨타임 + 완료 이벤트 | v1.0 |
| `EnemyDataSO.cs` | 공용 | EnemyType enum + 전 타입 수치 통합 | v2.0 |
| `EnemyKnightAttack.cs` | Knight | 근접 내려치기 단타 | v1.1 |
| `EnemyKnight.cs` | Knight | 정면 방패 + 등 뒤 자물쇠 피격 판단 | v1.1 |

**구조 결정**
- `KnightAI` / `KnightDataSO` 제거 → `EnemyAI` + `EnemyDataSO` 단일 통합
- `switch(enemyType)` 분기로 타입별 행동 처리
- 정면/후면 판단: `dot(기사방향, 공격방향) < 0` = 정면 = 방패 막힘

---

### v0.6 — 무기 스윙 이동 + PlayerWeaponAnimator

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyDataSO.cs` | 스윙 이동 수치 추가 | v1.1 |
| `PlayerWeaponMover.cs` | Weapon 오브젝트 DOTween 스윙 이동 | v1.0 |
| `PlayerWeaponAnimator.cs` | 무기 이벤트 구독 → 스윙 이동 연동 | v1.0 |
| `PlayerWeaponController.cs` | 명칭 변경 + 연동 추가 | v1.2 |

**스윙 기본값**
```
swingDistance : 0.5 / swingDuration : 0.08
returnDuration : 0.15 / airSwingDistance : 0.4
```

---

### v0.7 — Animator 파라미터 개편 + PlayerMover 이벤트 추가

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerMover.cs` | OnJumped 이벤트, VelocityY 프로퍼티 추가 | v1.4 |
| `MovementAnimator.cs` | 파라미터 전면 개편, 무기 Trigger 통합, SetWeapon() | v2.0 |
| `PlayerWeaponAnimator.cs` | Trigger 발행 제거 → 스윙 이동만 담당 | v1.1 |
| `PlayerWeaponController.cs` | MovementAnimator 연동 추가 | v1.3 |

**Animator 파라미터 전체 목록**

| 파라미터 | 타입 | 갱신 방식 | 용도 |
|---|---|---|---|
| `Speed` | Float | 매 프레임 | 이동 블렌드 |
| `VelocityY` | Float | 매 프레임 | Fall 전환 조건 |
| `IsGrounded` | Bool | 매 프레임 | 지상/공중 판별 |
| `IsFiring` | Bool | 외부 호출 | 공격 상태 표시 |
| `Jump` | Trigger | OnJumped | 1단 점프 진입 |
| `DoubleJump` | Trigger | OnDoubleJumped | 2단 점프 진입 |
| `Dash` | Trigger | OnDashStarted | 대쉬 진입 |
| `AttackCombo1` | Trigger | OnCombo1Started | 지상 1단 콤보 |
| `AttackCombo2` | Trigger | OnCombo2Started | 지상 2단 콤보 |
| `AttackCombo3` | Trigger | OnCombo3Started | 지상 3단 콤보 |
| `AirAttack` | Trigger | OnAirAttackStarted | 공중 공격 |

**Player.controller 수정 완료 항목**
```
Idle/Move → PlayerJump      : Jump(Trigger) 조건
PlayerJump → PlayerFall     : VelocityY < -0.1
AnyState → PlayerAttack01   : AttackCombo1 + IsGrounded=true
Attack01 → Attack02         : AttackCombo2 + ExitTime 0.5
Attack02 → Attack03         : AttackCombo3 + ExitTime 0.5
Attack01/02/03 → PlayerIdle : ExitTime 1.0
AnyState → PlayerAirAttack  : AirAttack + IsGrounded=false
PlayerAirAttack → PlayerFall: ExitTime 1.0
```

---

### v0.8 — 버그픽스 (대쉬 관통 / 무기 좌우 반전 / Trigger 잔류)

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerMover.cs` | 대쉬 DOMove → MovePosition 코루틴, OnFlipped 이벤트 추가 | v1.5 |
| `PlayerWeaponMover.cs` | OnFlipped 구독, Weapon localPosition X 동기화 | v1.1 |
| `MovementAnimator.cs` | ResetTrigger 클리어 추가 | v2.1 |
| `RustyKeyWeapon.cs` | normalizedTime 폴링, Trigger 선발행 버그 수정 | v1.3 |
| `KeyDataSO.cs` | Animator 콤보 타이밍 필드 추가 | v1.2 |

**버그 수정 내용**

| 버그 | 원인 | 수정 |
|---|---|---|
| 대쉬 얇은 벽 관통 | DOMove 물리 무시 | MovePosition 코루틴 + CastCollider 벽 감지 |
| 무기 왼쪽 방향 위치 오류 | _originLocalPosition X 고정 | OnFlipped 이벤트로 X 부호 반전 |
| 클릭 없이 Attack02 전환 | elapsed 타이머 vs Animator 타이밍 불일치 | normalizedTime 직접 폴링 |
| Trigger 큐 잔류 | SetTrigger 후 미소비 | ResetTrigger 일괄 클리어 |

**Attack 클립 필수 설정**
```
PlayerAttack01/02/03.anim → Loop Time = OFF
Loop ON 상태면 normalizedTime >= 1.0 조건 미도달 → 루프 무한 지속
```

---

### v0.9 — 입력 2단 방지 (프레임 방어 코드)

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `RustyKeyWeapon.cs` | `_lastAttackInputFrame` 프레임 방어 추가 | v1.4 |

**구조**
```csharp
if (_lastAttackInputFrame == Time.frameCount) return;
_lastAttackInputFrame = Time.frameCount;
// ComboReset 에서: _lastAttackInputFrame = -1;
```

---

### v0.10 — 봉인 열쇠 시스템 (SealKey)

**컨셉**
플레이어가 적에게 자물쇠를 "걸어" 특정 행동을 봉인.
기존 열쇠(자물쇠 해제) + 봉인 열쇠(행동 봉인) = 쌍방향 자물쇠 구조 완성.

**완성 파일 (신규)**

| 파일 | 역할 | 버전 |
|---|---|---|
| `SealType.cs` | 봉인 타입 enum (Dash/Jump/Ranged/Guard/Move/Attack) | v1.0 |
| `SealDataSO.cs` | 봉인 열쇠 수치 SO | v1.0 |
| `SealKeyWeapon.cs` | 봉인 열쇠 무기 — 투사체 발사 + 쿨타임 | v1.0 |
| `SealProjectile.cs` | 봉인 투사체 — 적 명중 시 EnemySealComponent 호출 | v1.0 |
| `EnemySealComponent.cs` | 봉인 상태 관리 — 봉인 적용/해제/타이머 | v1.0 |
| `EnemyAI.cs` | 봉인 체크 추가 (`IsSealed()`) | v3.0 |
| `EnemyKnight.cs` | Guard 봉인 체크 추가 (방패 무력화) | v1.2 |

**봉인 타입별 효과**

| SealType | 차단 행동 | 주요 대상 |
|---|---|---|
| Dash | 돌진 / 급이동 | 기사형 돌진 |
| Jump | 점프 / 상승 | 드론형 상승 |
| Ranged | 원거리 공격 | 궁수형, 드론형 |
| Guard | 방어 / 가드 → 정면 피격 허용 | 기사형 방패 |
| Move | 이동 전체 정지 (가장 강력) | 모든 적 |
| Attack | 모든 공격 차단 | 모든 적 |

**EnemyAI 봉인 체크 구조**
```
OnPatrolMove()  : IsSealed(Move) || IsSealed(Dash) → StopHorizontal()
OnChaseMove()   : IsSealed(Move)                   → StopHorizontal()
OnEnterAttack() : IsSealed(Attack)                 → ChangeState(Chase)
```

**EnemyKnight Guard 봉인 체크**
```
TakeDamage(info)
  자물쇠 해제됨?     → EnemyBase.TakeDamage()
  Guard 봉인 활성?   → 방패 무시 → EnemyBase.TakeDamage()
  정면 공격?         → 방패 막힘 플래시
  후면 공격?         → LockComponent.TakeDamage()
```

**SealData 에셋 기본값**

| 에셋 | sealType | sealDuration |
|---|---|---|
| `SealData_Dash.asset` | Dash | 4.0 |
| `SealData_Guard.asset` | Guard | 3.5 |
| `SealData_Move.asset` | Move | 1.5 |
| `SealData_Attack.asset` | Attack | 2.5 |

---

### v0.11 — 히트박스 좌우 반전 처리

**배경**
`SpriteRenderer.flipX` 는 렌더링만 뒤집고 `Collider2D` 월드 위치에는 영향 없음.
왼쪽 방향 공격 시 히트박스 판정이 오른쪽에 남아있는 버그.

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerWeaponHitboxManager.cs` | `FlipHitboxes()` 추가, `_HitBoxPosition` 캐시, `OnFlipped` 구독 | v1.1 |
| `PlayerWeaponMover.cs` | `HandleFlipped()` 에 `SpriteRenderer.flipX` 추가 | v1.1 |

**수정 구조**
```csharp
// Awake — 초기 localPosition 캐싱
_HitBoxPosition[i] = box.gameObject.transform.localPosition;

// FlipHitboxes — X 부호 반전
_HitBoxPosition[i] = new Vector3(
    Mathf.Abs(_HitBoxPosition[i].x) * newDir,
    _HitBoxPosition[i].y, _HitBoxPosition[i].z);
box.transform.localPosition = _HitBoxPosition[i];
```

**OnFlipped 구독자 최종 목록**

| 구독자 | 처리 내용 |
|---|---|
| `PlayerWeaponMover.HandleFlipped` | `_originLocalPosition.x` 반전 + `SpriteRenderer.flipX` |
| `PlayerWeaponHitboxManager.FlipHitboxes` | 각 Hitbox `localPosition.x` 반전 |

**주의**
`PlayerWeaponHitboxManager` 에 `using Unity.VisualScripting;` 실수 추가됨 → 제거 예정.

---

## 미결 항목

| 항목 | 상태 | 메모 |
|---|---|---|
| Player.controller 에디터 수정 | ✅ 완료 | v0.7 가이드 |
| Attack 클립 Loop Time OFF | ✅ 완료 | v0.8 |
| SealProjectile Prefab 생성 | ✅ 완료 | Assets/KEY/Prefabs/ |
| EnemySealComponent 적 부착 | ✅ 완료 | Enemy_Knight 우선 |
| SealData 에셋 생성 | ✅ 완료 | Assets/KEY/DataSO/Seals/ |
| ChargeProjectile Prefab 생성 | 🔲 미착수 | RustyKeyData.chargeProjectilePrefab 연결 필요 |
| using Unity.VisualScripting 제거 | 🔲 미착수 | PlayerWeaponHitboxManager |
| 스프라이트 / 애니메이션 클립 | 🔲 미착수 | 완성 후 클립 연결 |
| AnimatorOverrideController | 🔲 보류 | 스프라이트 완성 후 |
| LockComponent 해제 조건 다양화 | 🔲 미착수 | 방향/위상/시간 조건 확장 |
| KeyType enum 4종 추가 | 🔲 미착수 | 봉인/반전/연쇄/귀환 열쇠 |
| 테스트 씬 구성 | 🔲 미착수 | 차징 공격 포함 전투 테스트 |
| GameManager | 🔲 미착수 | 씬 전역 관리 |
| CinemachineCamera | 🔲 미착수 | 플레이어 추적 카메라 |

---

### v0.12 — 무기 교체 UI (WeaponHUD)

**작업 내용**

1. `WeaponSlotUI.cs` — 개별 슬롯 UI (아이콘 + 이름 + 장착 강조 + 클릭 교체)
2. `WeaponHUDController.cs` — HUD 전체 관리 (슬롯 동적 생성 + 현재 장착 표시)

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `WeaponSlotUI.cs` | 개별 무기 슬롯 UI | v1.0 |
| `WeaponHUDController.cs` | 무기 HUD 전체 관리 | v1.0 |

**UI 흐름**

```
게임 시작
  WeaponHUDController.Start()
    → inventory.OwnedKeys 순회 → 슬롯 동적 생성
    → 현재 장착 표시 초기화

열쇠 획득 (인게임)
  inventory.OnKeyAcquired → WeaponHUDController.HandleKeyAcquired()
    → AddSlot() → 슬롯 하나 추가

슬롯 클릭
  WeaponSlotUI.OnSlotClicked()
    → inventory.EquipKey(index)
      → OnKeyEquipped 이벤트
        → WeaponHUDController.HandleKeyEquipped()
            → 이전 슬롯 강조 해제
            → 새 슬롯 강조
            → 현재 장착 표시(아이콘 + 이름) 갱신
        → PlayerWeaponController.HandleKeyEquipped()
            → 무기 컴포넌트 교체
```

**Scene Hierarchy**

```
Canvas
└── WeaponHUD
      ├── [WeaponHUDController]
      │
      ├── EquippedWeaponDisplay       현재 장착 무기 표시
      │     ├── EquippedIcon [Image]  keyData.keySprite
      │     └── EquippedName [TMP]    keyData.keyName
      │
      └── SlotContainer               슬롯 동적 생성 부모
            └── (WeaponSlot Prefab 들이 런타임 생성)

WeaponSlot (Prefab)
├── [WeaponSlotUI]
├── [Button]              클릭 시 EquipKey(index)
├── [Image]               슬롯 배경 (장착 시 노란색)
├── Icon [Image]          keyData.keySprite
├── KeyName [TMP]         keyData.keyName
└── EquippedIndicator     장착 중 강조 오브젝트 (테두리 등)
```

**Inspector 연결**

| 컴포넌트 | 필드 | 연결 |
|---|---|---|
| WeaponHUDController | _inventory | KeyInventory.asset |
| WeaponHUDController | _slotPrefab | WeaponSlot.prefab |
| WeaponHUDController | _slotContainer | SlotContainer Transform |
| WeaponHUDController | _equippedIcon | EquippedIcon Image |
| WeaponHUDController | _equippedName | EquippedName TMP |
| WeaponHUDController | _emptySprite | (선택) 빈 슬롯 스프라이트 |

**자물쇠 해제 조건 확장**
- 현재: 피격 횟수 기반 유지
- 추후 구현 예정 (방향 조건 등)

---

### v0.13 — 입력 시스템 재편 + KeySwap 모드

**작업 내용**

1. `InputManager` v2.0 — 키 바인딩 전면 변경 + KeySwap 모드 추가
2. `WeaponHUDController` v1.1 — `OnKeySwap` / `OnKeySwapModeChanged` 구독 추가

**변경 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `InputManager.cs` | 키 바인딩 변경 + InGame/KeySwap 2계층 분리 | v2.0 |
| `WeaponHUDController.cs` | KeySwap 이벤트 구독, 슬롯 키 → EquipKey() 연결 | v1.1 |

**키 바인딩 최종 정리**

| 동작 | 키 | 모드 |
|---|---|---|
| 이동 (왼쪽) | ← 방향키 | InGame |
| 이동 (오른쪽) | → 방향키 | InGame |
| 점프 | Space | InGame |
| 대쉬 | Left Shift | InGame |
| 공격 | A | InGame (KeySwap 모드 시 슬롯 8번) |
| KeySwap 모드 | Left Ctrl (누름 유지) | 항상 |
| 슬롯 0~3 | 1 2 3 4 | KeySwap 모드 중 |
| 슬롯 4~7 | Q W E R | KeySwap 모드 중 |
| 슬롯 8~11 | A S D F | KeySwap 모드 중 (A = 공격키 겸용) |
| 슬롯 12~15 | Z X C V | KeySwap 모드 중 |

**KeySwap 모드 동작**

```
Left Ctrl 누름
  → EnterKeySwapMode()
      → OnMove(0f) 강제 발행 (이동 즉시 정지)
      → OnKeySwapModeChanged(true)
          → WeaponHUDController: SlotContainer 활성화

슬롯 키 입력 (예: W = 슬롯 5)
  → OnKeySwap(5)
      → WeaponHUDController.HandleKeySwap(5)
          → inventory.EquipKey(5)
              → OnKeyEquipped → 무기 컴포넌트 교체

Left Ctrl 뗌
  → ExitKeySwapMode()
      → OnKeySwapModeChanged(false)
          → WeaponHUDController: SlotContainer 비활성화
```

**A 키 겸용 처리**
```
KeySwap 모드 OFF → A 키 = OnAttack 이벤트 (공격)
KeySwap 모드 ON  → A 키 = OnKeySwap(8)  (슬롯 8번 교체)
```

**ActionMap 구조**
```
InGame ActionMap   : Move(1DAxis) / Jump / Dash / Attack
KeySwap ActionMap  : SwapMode(Button) / Slot0~15 (슬롯 8은 InGame Attack 에서 처리)
두 ActionMap 동시 Enable — 이벤트 발행은 코드에서 분기
```

---

### v0.14 — 차징 공격 시스템

**작업 내용**

1. `KeyDataSO` v1.3 — 차징 수치 섹션 추가
2. `InputManager` v2.2 — S키 차징 / ↑↓ 조준 이벤트 추가
3. `IChargeProjectile` — 투사체 인터페이스
4. `ChargeProjectile` — 투사체 구현체 (Ground/Wall 충돌 소멸)
5. `ChargeAimLine` — LineRenderer + DOTween 차징 피드백
6. `PlayerChargeAttack` — 차징 상태 관리 + 각도 조절 + 발사

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyDataSO.cs` | 차징 수치 추가 (minChargeTime 등) | v1.3 |
| `InputManager.cs` | OnChargeStart / OnChargeRelease / OnAimAdjust 이벤트 | v2.2 |
| `IChargeProjectile.cs` | 투사체 인터페이스 `Launch(dir, power)` | v1.0 |
| `ChargeProjectile.cs` | 투사체 구현체 — 충돌/소멸 처리 | v1.0 |
| `ChargeAimLine.cs` | 조준선 + DOTween 차징 피드백 | v1.0 |
| `PlayerChargeAttack.cs` | 차징 상태 관리 + 발사 | v1.0 |

**차징 흐름**
```
S 누름
  → HandleChargeStart()
      → 이동 차단 (BlockJump + velocity=0)
      → _aimAngle = 0 초기화
      → ChargeAimLine.Show()

매 프레임 (Charging 중)
  → _chargeTimer += deltaTime
  → ratio = timer / maxChargeTime
  → ChargeAimLine.UpdateCharge(ratio)
      → 라인 길이 / 색상 / Punch 갱신
  → ratio >= 1.0 → 자동 발사

↑ / ↓ 입력
  → _aimAngle ± angleStep (±angleRange 클램프)
  → ChargeAimLine.UpdateAim(direction)

S 뗌
  → timer >= minChargeTime → Fire(ratio)
  → timer < minChargeTime  → 취소 → EndCharge()

Fire()
  → Instantiate(chargeProjectilePrefab)
  → IChargeProjectile.Launch(direction, chargePower)
  → EndCharge() → 이동 차단 해제 + AimLine 숨김
```

**ChargeProjectile 충돌 처리**
```
Enemy 레이어 명중
  → LockComponent 있으면 TakeDamage() (자물쇠 피격)
  → LockComponent 없으면 IDamageable.TakeDamage() (일반 피격)
  → Die()

Ground / Wall 레이어 충돌
  → Die() 즉시 소멸

lifetime 초과
  → Die() 자동 소멸

Die() 처리
  → velocity = zero
  → DOScale(0, 0.1s, InQuart) 축소 연출
  → Destroy(gameObject)
```

**DOTween 피드백 목록**
```
ChargeAimLine:
  Show()      : 라인 0 → minLength (Ease.OutQuart, 0.12s)
  UpdateCharge: 라인 색 흰→노→빨 / 길이 min→max
  최대 차징   : Player DOPunchPosition (시위 떨림)
  Hide()      : 라인 → 0 (Ease.InQuart, 0.08s)

ChargeProjectile:
  Launch()    : DOPunchScale (발사 충격 크기 펀치)
  Die()       : DOScale → 0 (Ease.InQuart, 0.1s)
```

**KeyDataSO 차징 기본값**
```
minChargeTime       : 0.3
maxChargeTime       : 1.5
chargeAimAngleStep  : 15
chargeAimAngleRange : 60
chargeProjectilePrefab : (추후 연결)
```

**유니티 적용 체크리스트**
- [ ] Player 오브젝트에 `PlayerChargeAttack` 컴포넌트 추가
- [ ] Player 자식에 `AimLine` 오브젝트 생성 → `ChargeAimLine` + `LineRenderer` 부착
- [ ] `ChargeAimLine._playerTransform` = Player Transform
- [ ] `ChargeProjectile.prefab` 생성 (Rigidbody2D GravityScale=0, CircleCollider2D isTrigger=ON)
- [ ] `ChargeProjectile._enemyLayer` = Enemy 레이어
- [ ] `ChargeProjectile._terrainLayer` = Ground + Wall 레이어
- [ ] `RustyKeyData.asset.chargeProjectilePrefab` = ChargeProjectile.prefab 연결

---

### v0.15 — 차징 공격 개선 (이동 차단 / 각도 조절 / 방향 전환)

**문제 및 해결**

| 문제 | 해결 |
|---|---|
| 차징 중 이동/점프/대쉬 가능 | BlockMove() + BlockDash() + BlockJump() 동시 호출 + velocity.x 매 프레임 0 유지 |
| 최대 차징 자동 발사 (불필요) | maxChargeTime 자동 발사 로직 제거. S 뗌으로만 발사 |
| 각도가 n도씩 단계적으로 바뀜 | OnAimAdjust int→float 변경. ↑↓ 누름 유지 → 매 프레임 연속 변화 |
| 차징 중 방향 전환 시 스프라이트/무기/히트박스 미반영 | PlayerMover.ForceFlip() API 추가 → OnFlipped 연쇄 발행 |
| FirePoint 위치 미반영 | HandleChargeFlip 에서 _firePoint.localPosition.x 부호 반전 추가 |

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `InputManager.cs` | BlockMove/BlockDash API 추가, OnAimAdjust float 변경, OnChargeFlip 이벤트 추가 | v2.4 |
| `PlayerMover.cs` | ForceFlip() 외부 API 추가, FlipSprite에 OnFlipped 발행 추가 | v1.6 |
| `PlayerChargeAttack.cs` | 이동 전면 차단, 각도 연속 변화, OnChargeFlip 구독, FirePoint 플립 | v1.3 |

**차징 키 바인딩 최종**

| 동작 | 키 |
|---|---|
| 차징 시작 | S 누름 유지 |
| 발사 | S 뗌 (minChargeTime 이상) |
| 취소 | S 뗌 (minChargeTime 미만) |
| 조준 위/아래 | ↑ / ↓ 누름 유지 (연속 변화) |
| 발사 방향 전환 | ← → 방향키 |

**차징 중 플립 연쇄 흐름**

```
← 방향키 입력
  → InputManager.OnChargeFlip(-1f)
      → PlayerChargeAttack.HandleChargeFlip(-1f)
          → PlayerMover.ForceFlip(-1f)
              → SpriteRenderer.flipX = true
              → OnFlipped(-1f) 발행
                  → PlayerWeaponMover.HandleFlipped(-1f)   Weapon 위치 반전
                  → PlayerWeaponHitboxManager.FlipHitboxes(-1f) 히트박스 반전
          → _facingOverride = -1f
          → _firePoint.localPosition.x 부호 반전
          → ChargeAimLine.UpdateAim()
```

**InputManager 차단 API 전체 목록 (v2.4)**

| API | 용도 |
|---|---|
| BlockJump() / UnblockJump() | 점프 차단 |
| BlockMove() / UnblockMove() | 이동 차단 (차징 중) |
| BlockDash() / UnblockDash() | 대쉬 차단 (차징 중) |