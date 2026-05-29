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

**Animator Controller 스테이트**
PlayerIdle / PlayerMove / PlayerJump / PlayerFall / PlayerDash / PlayerDoubleJump

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

**콤보 수치 기본값**

| 단계 | 모션 | 데미지 배율 |
|---|---|---|
| Combo1 | 가로 휘두르기 | 1.0x |
| Combo2 | 대각선 내리기 | 1.2x |
| Combo3 | 앞으로 찌르기 | 1.5x (피니셔) |
| AirAttack | 아래 내리찍기 | 1.3x |

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

**구조 결정 사항**
- 열쇠 수치 전부 `KeyDataSO` 집중
- `WeaponEntry.weapon` = `MonoBehaviour` → Inspector 드래그 연결, 런타임 캐스팅
- 열쇠 교체 흐름: `KeyInventoryDataSO.EquipKey()` → `OnKeyEquipped` → `PlayerWeaponController`

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

**구조 결정 사항**
- 더미 사망 없음 — 체력 최솟값 1 고정
- 넉백: `KnockbackRoutine` 코루틴 (`velocity.x *= knockbackDecay` 매 프레임 감속)
- iFrame: 코루틴 기반, 피격 플래시 동반
- Rigidbody2D: `gravityScale=1` / `FreezeRotation Z`

**knockbackDecay 튜닝 가이드**

| 값 | 느낌 |
|---|---|
| 0.7 | 짧고 강하게 밀림 |
| 0.8 | 자연스러운 중간 (기본값) |
| 0.9 | 느리게 미끄러지듯 정지 |

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

**구조 결정 사항**
- `KnightAI` / `KnightDataSO` 제거 → `EnemyAI` + `EnemyDataSO` 단일 통합
- `EnemyAI` 내부 `switch(enemyType)` 분기로 타입별 행동 처리
- 정면/후면 판단: `dot(기사방향, 공격방향) < 0` = 정면 = 방패 막힘
- 새 적 추가: EnemyType 항목 + switch 케이스 추가만 필요

**EnemyAI 상태 전환**
```
Patrol ──(직선 감지)──→ Chase
Patrol ──(벽/낭떠러지)─→ 방향 반전 → (idleChance) → Idle
Idle   ──(대기 완료)──→ Patrol
Chase  ──(사정거리)───→ Attack
Chase  ──(범위 이탈)──→ Patrol
Attack ──(완료)───────→ Chase
```

---

### v0.6 — 무기 스윙 이동 + PlayerWeaponAnimator

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyDataSO.cs` | 스윙 이동 수치 추가 | v1.1 |
| `PlayerWeaponMover.cs` | Weapon 오브젝트 DOTween 스윙 이동 | v1.0 |
| `PlayerWeaponAnimator.cs` | 무기 이벤트 구독 → 스윙 이동 연동 | v1.0 |
| `PlayerWeaponController.cs` | 명칭 변경 + 연동 추가 | v1.2 |

**스윙 이동 흐름**
```
RustyKeyWeapon.OnCombo1Started
  → PlayerWeaponAnimator → PlayerWeaponMover.PlaySwing(Combo1)
      → DOLocalMove(앞 swingDistance, Ease.OutQuart)
      → 유지 (hitboxDuration - swingDuration)
      → DOLocalMove(원점, Ease.InQuart)
```

---

### v0.7 — Animator 파라미터 개편 + PlayerMover 이벤트 추가

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerMover.cs` | OnJumped 이벤트 추가, VelocityY 프로퍼티 추가 | v1.4 |
| `MovementAnimator.cs` | 파라미터 전면 개편, 무기 Trigger 통합, SetWeapon() 추가 | v2.0 |
| `PlayerWeaponAnimator.cs` | Trigger 발행 제거 → 스윙 이동만 담당 | v1.1 |
| `PlayerWeaponController.cs` | 명칭 정리 + MovementAnimator 연동 추가 | v1.3 |

**Animator 파라미터 전체 목록 (v0.7 기준)**

| 파라미터 | 타입 | 갱신 방식 | 용도 |
|---|---|---|---|
| `Speed` | Float | 매 프레임 | 이동 블렌드 |
| `VelocityY` | Float | 매 프레임 | Fall 전환 조건 |
| `IsGrounded` | Bool | 매 프레임 | 지상/공중 판별 |
| `IsFiring` | Bool | 외부 호출 | 공격 상태 표시 |
| `Jump` | Trigger | PlayerMover.OnJumped | 1단 점프 진입 |
| `DoubleJump` | Trigger | PlayerMover.OnDoubleJumped | 2단 점프 진입 |
| `Dash` | Trigger | PlayerMover.OnDashStarted | 대쉬 진입 |
| `AttackCombo1` | Trigger | RustyKeyWeapon.OnCombo1Started | 지상 1단 콤보 |
| `AttackCombo2` | Trigger | RustyKeyWeapon.OnCombo2Started | 지상 2단 콤보 |
| `AttackCombo3` | Trigger | RustyKeyWeapon.OnCombo3Started | 지상 3단 콤보 |
| `AirAttack` | Trigger | RustyKeyWeapon.OnAirAttackStarted | 공중 공격 |

**역할 분리 확정**

| 컴포넌트 | 역할 |
|---|---|
| `MovementAnimator` | 모든 Animator 파라미터 단독 관리 (Float/Bool/Trigger 전부) |
| `PlayerWeaponAnimator` | Weapon 오브젝트 스윙 이동(PlayerWeaponMover) 전담 |

**Unity 에디터 작업 (Player.controller)**

```
AnyState → PlayerAttack01   : AttackCombo1(Trigger) + IsGrounded=true
Attack01 → Attack02         : AttackCombo2(Trigger) + ExitTime 0.5
Attack02 → Attack03         : AttackCombo3(Trigger) + ExitTime 0.5
Attack01/02/03 → PlayerIdle : ExitTime 1.0
AnyState → PlayerAirAttack  : AirAttack(Trigger) + IsGrounded=false
PlayerAirAttack → PlayerFall: ExitTime 1.0
```

---

### v0.8 — 버그픽스 (대쉬 관통 / 무기 좌우 반전 / 콤보 Trigger 잔류)

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerMover.cs` | 대쉬 DOMove → MovePosition 코루틴 교체, OnFlipped 이벤트 추가 | v1.5 |
| `PlayerWeaponMover.cs` | OnFlipped 구독, Weapon localPosition X 좌우 동기화 | v1.1 |
| `MovementAnimator.cs` | ResetTrigger 클리어 추가 (미소비 Trigger 잔류 방지) | v2.1 |
| `RustyKeyWeapon.cs` | Animator normalizedTime 직접 폴링, Trigger 선발행 버그 수정 | v1.3 |
| `KeyDataSO.cs` | Animator 콤보 타이밍 필드 추가 | v1.2 |

**버그 수정 내용**

| 버그 | 원인 | 수정 |
|---|---|---|
| 대쉬 얇은 벽 관통 | DOMove 가 물리 무시 | MovePosition 코루틴 + CastCollider 벽 감지 |
| 무기 왼쪽 방향 위치 오류 | _originLocalPosition X 고정 | OnFlipped 이벤트로 X 부호 반전 |
| 클릭 없이 Attack02 전환 | elapsed 타이머 vs Animator 타이밍 불일치 | normalizedTime 직접 폴링 |
| Trigger 큐 잔류 | SetTrigger 후 소비 안 된 채 남음 | ResetTrigger 일괄 클리어 |

**Attack 클립 필수 설정**
```
PlayerAttack01/02/03.anim
  Loop Time = OFF  (m_LoopTime: 0)
  → Loop ON 상태면 normalizedTime >= 1.0 조건 미도달 → while 루프 무한 지속
```

---

### v0.9 — 입력 2단 방지 (프레임 방어 코드)

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `RustyKeyWeapon.cs` | `_lastAttackInputFrame` 프레임 방어 추가, ComboReset 에서 초기화 | v1.4 |

**구조**
```csharp
// 같은 프레임 중복 입력 차단
if (_lastAttackInputFrame == Time.frameCount) return;
_lastAttackInputFrame = Time.frameCount;

// ComboReset 에서 초기화 (리셋 직후 입력 씹힘 방지)
_lastAttackInputFrame = -1;
```

---

### v0.10 — 봉인 열쇠 시스템 (SealKey)

**컨셉**
플레이어가 적에게 자물쇠를 "걸어" 특정 행동을 봉인하는 시스템.
기존 열쇠(해제 방향)와 반대 방향의 쌍방향 자물쇠 구조 완성.

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `SealType.cs` | 봉인 타입 enum (6종) | v1.0 |
| `KeyType.cs` | Seal 항목 추가 | v1.1 |
| `SealDataSO.cs` | 봉인 수치 ScriptableObject | v1.0 |
| `EnemySealComponent.cs` | 적 봉인 상태 관리 | v1.0 |
| `SealProjectile.cs` | 봉인 투사체 | v1.0 |
| `SealKeyWeapon.cs` | 봉인 열쇠 무기 구현체 | v1.0 |
| `PlayerWeaponBase.cs` | IsReadyToFire 가상 프로퍼티 추가 | v1.2 |
| `PlayerWeaponController.cs` | SealKeyWeapon 분기 + WeaponEntry.sealData 추가 | v1.4 |
| `EnemyAI.cs` | EnemySealComponent 연동, 봉인 행동 차단 체크 | v3.0 |
| `EnemyKnight.cs` | Guard 봉인 체크, 방패 무시 피격 처리 | v1.2 |

**봉인 타입 6종**

| 타입 | 차단 행동 | 주요 대상 |
|---|---|---|
| `Dash` | 돌진 / 급이동 | 기사형, 드론형 |
| `Jump` | 점프 / 상승 | 드론형 |
| `Ranged` | 원거리 투사체 | 궁수형 |
| `Guard` | 방어 / 가드 → 정면 피격 허용 | 기사형 방패 |
| `Move` | 이동 전체 | 모든 적 (강력 — 지속시간 짧게) |
| `Attack` | 모든 공격 | 모든 적 |

**봉인 적용 흐름**
```
InputManager.OnAttack
  → SealKeyWeapon.FireProjectile()
      → Instantiate(SealProjectile)
      → SealProjectile.Launch(sealData, facingDir)
          → 직진 이동
          → OnTriggerEnter2D (Enemy 레이어)
              → EnemySealComponent.ApplySeal(sealData)
                  → _activeSeals[SealType] = duration 등록
                  → SealFlashRoutine 시작 (깜빡임)
```

**EnemyAI 봉인 체크 위치**

| 함수 | 체크 봉인 | 봉인 시 동작 |
|---|---|---|
| `OnPatrolMove()` | Move / Dash | StopHorizontal() |
| `OnChaseMove()` | Move | StopHorizontal() |
| `OnEnterAttack()` | Attack | ChangeState(Chase) |

**EnemyKnight Guard 봉인 흐름**
```
TakeDamage(info)
  → 자물쇠 해제됨?    → EnemyBase.TakeDamage()
  → Guard 봉인 활성?  → 방패 무시 → EnemyBase.TakeDamage()
  → 정면 공격?        → 방패 막힘 플래시
  → 후면 공격?        → 자물쇠 피격
```

**PlayerWeaponBase.IsReadyToFire**
```csharp
// 기본값 (일반 열쇠)
protected virtual bool IsReadyToFire => _keyData != null;

// SealKeyWeapon override
protected override bool IsReadyToFire => _sealData != null;
```

**EnemySealComponent 중복 봉인 규칙**
- 같은 SealType 재명중 → 타이머 리셋 (스택 없음)
- 다른 SealType → maxSealCount 까지 동시 적용
- 초과 시 가장 오래된 봉인 제거 후 추가

**SealData 에셋 권장 기본값**

| 에셋 | sealType | sealDuration | 비고 |
|---|---|---|---|
| `SealData_Dash.asset` | Dash | 4.0 | 기본 봉인 |
| `SealData_Guard.asset` | Guard | 3.5 | 방패 내림 |
| `SealData_Move.asset` | Move | 1.5 | 전체 정지 — 짧게 |
| `SealData_Attack.asset` | Attack | 2.5 | 공격 차단 |

---

## 미결 항목

| 항목 | 상태 | 메모 |
|---|---|---|
| Player.controller 에디터 수정 | ✅ 완료 | v0.7 가이드 참고 |
| Attack 클립 Loop Time OFF | ✅ 완료 | PlayerAttack01/02/03.anim |
| 스프라이트 / 애니메이션 클립 | 🔲 미착수 | 완성 후 클립 연결 |
| AnimatorOverrideController 세팅 | 🔲 보류 | 스프라이트 완성 후 |
| SealProjectile Prefab 생성 | ✅ 완료 | Hierarchy 가이드 참고 |
| EnemySealComponent 적 부착 | ✅ 완료 | Enemy_Knight 에 우선 부착 |
| SealData 에셋 생성 | ✅ 완료 | Assets/KEY/DataSO/Seals/ |
| 자물쇠 해제 조건 다양화 | 🔲 미착수 | LockComponent 확장 필요 |
| 테스트 씬 구성 | 🔲 미착수 | 봉인 시스템 포함 전투 테스트 |
| GameManager | 🔲 미착수 | 씬 전역 관리 |
| CinemachineCamera | 🔲 미착수 | 플레이어 추적 카메라 |
| WeaponHUD Prefab 세팅 | 🔲 미착수 | Canvas > WeaponHUD > SlotContainer 구성 |
| AimLine 오브젝트 생성 | 🔲 미착수 | Player 자식 + ChargeAimLine + LineRenderer |
| ChargeProjectile Prefab 생성 | 🔲 미착수 | Rigidbody2D GravityScale=0 / CircleCollider2D isTrigger=ON |

---

### v0.11 — 히트박스 좌우 반전 처리

**배경**
`PlayerMover.SpriteRenderer.flipX` 로 플레이어 스프라이트를 반전하고
`PlayerWeaponMover` 가 Weapon `localPosition.x` 를 반전하지만
Hitbox 오브젝트의 `BoxCollider2D` 판정 위치는 그대로 유지되는 버그.
왼쪽 방향 공격 시 히트박스 판정이 오른쪽에 남아있는 현상.

**원인**
`SpriteRenderer.flipX` 는 렌더링만 뒤집고 `Collider2D` 의 월드 위치 계산에는 영향을 주지 않음.
`box.transform.localPosition` 은 flipX 와 무관하게 씬 배치 기준 그대로 유지.

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerWeaponHitboxManager.cs` | `FlipHitboxes()` 추가, `_HitBoxPosition` 캐시, `OnFlipped` 구독 | v1.1 |
| `PlayerWeaponMover.cs` | `HandleFlipped()` 에 `_spriteRenderer.flipX` 추가 | v1.1 |

**수정 구조**

```csharp
// PlayerWeaponHitboxManager — Awake 에서 초기 localPosition 캐싱
_HitBoxPosition[i] = box.gameObject.transform.localPosition;

// FlipHitboxes — HandleFlipped 와 동일 패턴
_HitBoxPosition[i] = new Vector3(
    Mathf.Abs(_HitBoxPosition[i].x) * newDir,
    _HitBoxPosition[i].y,
    _HitBoxPosition[i].z);
box.transform.localPosition = _HitBoxPosition[i];

// PlayerWeaponMover.HandleFlipped — Weapon 스프라이트 반전 추가
_spriteRenderer.flipX = newDir > 0 ? false : true;
```

**OnFlipped 구독자 최종 목록**

| 구독자 | 처리 내용 |
|---|---|
| `PlayerWeaponMover.HandleFlipped` | Weapon `localPosition.x` 반전 + `SpriteRenderer.flipX` |
| `PlayerWeaponHitboxManager.FlipHitboxes` | 각 Hitbox `transform.localPosition.x` 반전 |

**주의 사항**
`PlayerWeaponHitboxManager` 에 `using Unity.VisualScripting;` 이 실수로 추가됨 → 제거 예정 (본인 처리).


---

### v0.12 — 무기 교체 UI (WeaponHUD)

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `WeaponSlotUI.cs` | 개별 무기 슬롯 UI (아이콘 + 이름 + 장착 강조 + 클릭 교체) | v1.0 |
| `WeaponHUDController.cs` | 무기 HUD 전체 관리 (슬롯 동적 생성 + 현재 장착 표시) | v1.0 |

**UI 흐름**
```
게임 시작
  WeaponHUDController.Start()
    → inventory.OwnedKeys 순회 → 슬롯 동적 생성
    → 현재 장착 표시 초기화

열쇠 획득 (인게임)
  inventory.OnKeyAcquired → HandleKeyAcquired() → AddSlot()

슬롯 클릭
  WeaponSlotUI.OnSlotClicked()
    → inventory.EquipKey(index)
      → OnKeyEquipped 이벤트
          → WeaponHUDController: 슬롯 강조 + 장착 표시 갱신
          → PlayerWeaponController: 무기 컴포넌트 교체
```

**Hierarchy**
```
Canvas
└── WeaponHUD
      ├── [WeaponHUDController]
      ├── EquippedWeaponDisplay
      │     ├── EquippedIcon [Image]
      │     └── EquippedName [TMP]
      └── SlotContainer  (WeaponSlot Prefab 런타임 생성)

WeaponSlot (Prefab)
├── [WeaponSlotUI] / [Button]
├── [Image] 슬롯 배경
├── Icon [Image] / KeyName [TMP]
└── EquippedIndicator (강조 테두리)
```

---

### v0.13 — 입력 시스템 재편 + KeySwap 모드

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `InputManager.cs` | 키 바인딩 전면 변경 + InGame/KeySwap 2계층 분리 | v2.0 |
| `WeaponHUDController.cs` | OnKeySwap / OnKeySwapModeChanged 구독 추가 | v1.1 |

**키 바인딩 최종**

| 동작 | 키 | 모드 |
|---|---|---|
| 이동 | ← → | InGame |
| 점프 | Space | InGame |
| 대쉬 | LShift | InGame |
| 공격 | A | InGame (KeySwap ON 시 슬롯 8번) |
| KeySwap 모드 | LCtrl 누름 유지 | 항상 |
| 슬롯 0~3 | 1 2 3 4 | KeySwap 중 |
| 슬롯 4~7 | Q W E R | KeySwap 중 |
| 슬롯 8~11 | A S D F | KeySwap 중 |
| 슬롯 12~15 | Z X C V | KeySwap 중 |

**A 키 겸용 처리**
```
KeySwap OFF → A = OnAttack
KeySwap ON  → A = OnKeySwap(8)
```

**KeySwap 모드 동작**
```
LCtrl 누름 → EnterKeySwapMode() → OnMove(0f) 강제 + OnKeySwapModeChanged(true)
슬롯 키 입력 → OnKeySwap(index) → inventory.EquipKey(index)
LCtrl 뗌   → ExitKeySwapMode() → OnKeySwapModeChanged(false)
```

---

### v0.14 — 차징 공격 시스템

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyDataSO.cs` | 차징 수치 섹션 추가 (minChargeTime 등) | v1.3 |
| `InputManager.cs` | OnChargeStart / OnChargeRelease / OnAimAdjust 이벤트 추가 | v2.2 |
| `IChargeProjectile.cs` | 투사체 인터페이스 Launch(dir, power) | v1.0 |
| `ChargeProjectile.cs` | 투사체 구현체 — Ground/Wall 충돌 소멸 + DOTween | v1.0 |
| `ChargeAimLine.cs` | LineRenderer + DOTween 차징 피드백 | v1.0 |
| `PlayerChargeAttack.cs` | 차징 상태 관리 + 각도 조절 + 발사 | v1.0 |

**차징 흐름**
```
S 누름 → BlockJump + velocity=0 + _aimAngle=0 + AimLine.Show()
↑↓ 입력 → _aimAngle ± angleStep (클램프) + AimLine.UpdateAim()
ratio >= 1.0 → 자동 발사
S 뗌
  timer >= minChargeTime → Fire(ratio) → IChargeProjectile.Launch() → EndCharge()
  timer < minChargeTime  → 취소 → EndCharge()
```

**ChargeProjectile 충돌 처리**
```
Enemy 명중   → LockComponent 있으면 TakeDamage() / 없으면 IDamageable.TakeDamage() → Die()
Ground/Wall  → Die() 즉시 소멸
lifetime 초과 → Die()
Die()        → velocity=0 + DOScale(0, 0.1s) + Destroy
```

**DOTween 피드백**
```
AimLine.Show()      : 라인 0 → minLength (OutQuart, 0.12s)
AimLine.UpdateCharge: 색상 흰→노→빨 / 길이 min→max
최대 차징           : Player DOPunchPosition (시위 떨림)
AimLine.Hide()      : 라인 → 0 (InQuart, 0.08s)
ChargeProjectile.Launch : DOPunchScale
ChargeProjectile.Die    : DOScale → 0 (InQuart, 0.1s)
```

**KeyDataSO 차징 기본값**
```
minChargeTime       : 0.3
maxChargeTime       : 1.5
chargeAimAngleStep  : 15  (초당 각도 — v0.15 에서 의미 변경)
chargeAimAngleRange : 60
chargeProjectilePrefab : (추후 연결)
```

**유니티 적용 체크리스트**
- [ ] Player 에 `PlayerChargeAttack` 컴포넌트 추가
- [ ] Player 자식에 `AimLine` 오브젝트 → `ChargeAimLine` + `LineRenderer` 부착
- [ ] `ChargeProjectile.prefab` 생성 (Rigidbody2D GravityScale=0 / CircleCollider2D isTrigger=ON)
- [ ] `ChargeProjectile._enemyLayer` = Enemy 레이어
- [ ] `ChargeProjectile._terrainLayer` = Ground + Wall 레이어
- [ ] `RustyKeyData.asset.chargeProjectilePrefab` 연결

---

### v0.15 — 차징 공격 개선 (이동 차단 / 각도 연속 변화 / 방향 전환)

**문제 및 해결**

| 문제 | 해결 |
|---|---|
| 차징 중 이동/점프/대쉬 가능 | BlockMove() + BlockDash() + BlockJump() 동시 호출 + velocity.x 매 프레임 0 유지 |
| 최대 차징 자동 발사 (불필요) | maxChargeTime 자동 발사 로직 제거 — S 뗌으로만 발사 |
| 각도 단계적 변화 | OnAimAdjust int→float / _aimInput 저장 / Update 매 프레임 × deltaTime 연속 변화 |
| 차징 중 방향 전환 미반영 | PlayerMover.ForceFlip() API 추가 → OnFlipped 연쇄 발행 → 스프라이트/무기/히트박스 동기화 |
| FirePoint 위치 미반영 | HandleChargeFlip 에서 _firePoint.localPosition.x 부호 반전 |

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `InputManager.cs` | BlockMove/BlockDash API 추가, OnAimAdjust float 변경, OnChargeFlip 이벤트 추가 | v2.4 |
| `PlayerMover.cs` | ForceFlip() 외부 API 추가 | v1.6 |
| `PlayerChargeAttack.cs` | 이동 전면 차단, 각도 연속 변화, OnChargeFlip 구독, FirePoint 플립 | v1.3 |

**차징 키 바인딩 최종**

| 동작 | 키 |
|---|---|
| 차징 시작 | S 누름 유지 |
| 발사 | S 뗌 (minChargeTime 이상) |
| 취소 | S 뗌 (minChargeTime 미만) |
| 조준 위/아래 | ↑ / ↓ 누름 유지 (연속 변화) |
| 발사 방향 전환 | ← → 방향키 |

**차징 중 방향 전환 연쇄 흐름**
```
← 방향키 입력
  → InputManager.OnChargeFlip(-1f)
      → PlayerChargeAttack.HandleChargeFlip(-1f)
          → PlayerMover.ForceFlip(-1f)
              → SpriteRenderer.flipX = true
              → OnFlipped(-1f) 발행
                  → PlayerWeaponMover: Weapon localPosition.x 반전
                  → PlayerWeaponHitboxManager: Hitbox localPosition.x 반전
          → _facingOverride = -1f
          → _firePoint.localPosition.x 부호 반전
          → ChargeAimLine.UpdateAim()
```

**InputManager 차단 API 전체 목록 (v2.4)**

| API | 용도 |
|---|---|
| BlockJump() / UnblockJump() | 점프 차단 — 기존 |
| BlockMove() / UnblockMove() | 이동 차단 — v0.15 추가 |
| BlockDash() / UnblockDash() | 대쉬 차단 — v0.15 추가 |

**파일 버전 스냅샷 (v0.15 기준)**

| 파일 | 버전 |
|---|---|
| `InputManager.cs` | v2.4 |
| `PlayerMover.cs` | v1.6 |
| `PlayerChargeAttack.cs` | v1.3 |
| `KeyDataSO.cs` | v1.3 |
| `ChargeProjectile.cs` | v1.0 |
| `ChargeAimLine.cs` | v1.0 |
| `IChargeProjectile.cs` | v1.0 |
| `WeaponSlotUI.cs` | v1.0 |
| `WeaponHUDController.cs` | v1.1 |


---

### v0.16 — Enemy 시스템 전면 개편

**주요 작업 4단계**

1. EnemyAI / EnemyBase DataSO 참조 구조 수정
2. 플레이어 피격 피드백 + 차징 돌진 추가
3. LockComponent 단일 → List 변환 (예정)
4. SealComponent ↔ Player 레이어 연결 정리 (예정)

---

#### 1단계 — DataSO 단일 연결 지점 확립

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `EnemyBase.cs` | `Settings` public 프로퍼티 추가 | v1.2 |
| `EnemyAI.cs` | `[SerializeField] _settings` 제거, `EnemyBase.Settings` 로 취득 | v4.0 |

**변경 구조**
```
기존: EnemyBase._settings (Inspector 연결)
      EnemyAI._settings   (Inspector 연결) ← 중복

변경: EnemyBase._settings (Inspector 연결) ← 유일한 연결 지점
      EnemyAI.Awake()     → GetComponent<EnemyBase>().Settings 참조
      EnemySensor         → EnemyAI 가 SetData() 주입
      EnemyKnightAttack   → EnemyAI 가 SetData() 주입
```

---

#### 2단계 — 플레이어 피격 피드백 + 차징 돌진

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerHealth.cs` | 신규 — IDamageable 구현, iFrame, 넉백, 피격플래시, 사망(OnDead) | v1.0 |
| `EnemyDataSO.cs` | attackHitLayer + 차징 수치 6종 + chargeDetectRange 추가 | v2.1 |
| `EnemyKnightAttack.cs` | attackHitLayer 사용, _overlapBuffer GC 방지, FlipHitbox() 추가 | v1.2 |
| `EnemyKnightChargeAttack.cs` | LineRenderer 점증 + ScanForObstacle 이진탐색 + MovePosition 돌진 | v1.4 |
| `EnemySensor.cs` | CheckChargeRange() 추가 (chargeDetectRange 사용) | v1.1 |
| `EnemyAI.cs` | _chargeAttack 구독, FlipAttackHitboxes(), 중복 진입 차단 | v4.1 |

**EnemyKnightChargeAttack 돌진 흐름**
```
① Countdown(3초): LineRenderer 0→최대길이 점증 + 색상 노→빨
   매 프레임 ScanForObstacle(벽 수평Ray + 낭떠러지 하향Ray + 이진탐색)
   → 장애물 감지 시 _confirmedLength 고정, 선 멈춤
② _confirmedLength < 0.3f → 취소
③ Charge: MovePosition 코루틴, 매 FixedUpdate HitWall/HitPlayer Raycast
④ 종료: velocity=0, OnAttackFinished → Chase 복귀
```

**EnemyKnightAttack FlipHitbox 구조**
```csharp
// EnemyAI.Flip() / UpdateChaseDirection() → FlipAttackHitboxes() → 호출
public void FlipHitbox(float dir)
{
    Vector3 pos = _hitbox.transform.localPosition;
    _hitbox.transform.localPosition = new Vector3(
        _originalHitboxLocalX * dir, pos.y, pos.z);
}
```

**EnemyAI 공격 우선순위 (v4.1)**
```
OnEnterAttack() Knight 분기:
  chargeReady && inChargeRange && !inAttackRange → 차징 돌진 (주력)
  normalReady && inAttackRange                  → 일반 근접 공격 (보조)
  chargeReady && inChargeRange                  → 차징 돌진 (근접 사정거리 내)
  모두 쿨다운                                   → ChangeState(Chase)
```

**EnemyKnight 전투 흐름 (v1.3)**
```
정면 공격 → 아무 반응 없음 (방패 완전 흡수)
후면 공격 → LockComponent 피격 카운트
자물쇠 해제 → 색상 빨간 + 이후 모든 공격 피격 가능
Guard 봉인 → 정면도 피격 허용
```

**PlayerHealth 연결 경로**
```
EnemyKnightAttack.CheckHit()
  → ContactFilter2D(attackHitLayer)
    → TryGetComponent<IDamageable>()
      → PlayerHealth.TakeDamage(info)
        → 체력 감소 + iFrame + 넉백 + 피격플래시
        → HP <= 0 → OnDead 이벤트
```

**KnightData.asset 추가 수치**

| 필드 | 권장값 | 용도 |
|---|---|---|
| `attackHitLayer` | Player | 공격 히트박스 감지 |
| `chargeDetectRange` | 5.0 | 차징 발동 범위 |
| `chargeSpeed` | 14 | 돌진 속도 |
| `chargeDuration` | 0.8 | 돌진 지속 시간 |
| `chargeDamage` | 25 | 돌진 피해량 |
| `chargeCooldown` | 5.0 | 돌진 쿨타임 |

**버그 수정 내역**

| 버그 | 원인 | 수정 |
|---|---|---|
| 근접+차징 동시 실행 | _attack GetComponent가 ChargeAttack도 반환 | EnemyKnightAttack 명시 취득 + 중복 진입 차단 |
| 돌진 후 AI 멈춤 | _chargeAttack.OnAttackFinished 미구독 | Start()에 구독 추가 |
| 적 공격 히트박스 방향 미반영 | Flip() 에서 hitbox localPosition 미변경 | FlipAttackHitboxes() 추가 |
| DOTween velocity 람다 미작동 | linearVelocity 구조체 복사본 수정 | MovePosition 코루틴으로 교체 |

**Layer 추가**

| Layer | 용도 |
|---|---|
| `EnemyAttackHit` | 적 공격 판정 (AttackHitbox, ChargeHitbox) |

**Physics 2D Matrix 필수 추가**
```
EnemyAttackHit ↔ Player : ON
```


---

### v0.17 — Enemy 개선 (히트박스 플립 / 상태전환 딜레이 / 자물쇠 List 확장)

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `EnemyKnightChargeAttack.cs` | FlipHitbox() 추가, `_originalChargeHitboxLocalX` 캐싱 | v1.5 |
| `EnemyKnightAttack.cs` | FlipHitbox() 추가, `_originalHitboxLocalX` 캐싱 (v1.2에서 반영) | v1.2 |
| `EnemyAI.cs` | FlipAttackHitboxes에 ChargeAttack.FlipHitbox 연결, 상태전환 딜레이 추가 | v4.2 |
| `EnemyDataSO.cs` | `stateTransitionDelay` 필드 추가 | v2.2 |
| `EnemyKnight.cs` | `_backLock` 단일 → `_locks List<LockComponent>` 변환 | v1.4 |

---

#### ① ChargeAttack 히트박스 플립 (EnemyKnightChargeAttack v1.5)

```
방향 전환 시 EnemyAI.FlipAttackHitboxes(dir)
  → EnemyKnightAttack.FlipHitbox(dir)            근접 히트박스
  → EnemyKnightChargeAttack.FlipHitbox(dir)       돌진 히트박스 (v1.5 추가)
      _originalChargeHitboxLocalX × dir → localPosition.x 갱신
      _chargeHitbox == null 이면 무시 (Raycast 전용 모드)
```

**구현 패턴 — EnemyKnightAttack 과 동일**
```csharp
// Awake 에서 캐싱
_originalChargeHitboxLocalX = Mathf.Abs(_chargeHitbox.transform.localPosition.x);
_chargeHitbox.enabled = false;

// FlipHitbox()
Vector3 pos = _chargeHitbox.transform.localPosition;
_chargeHitbox.transform.localPosition = new Vector3(
    _originalChargeHitboxLocalX * dir, pos.y, pos.z);
```

---

#### ② 상태전환 딜레이 (EnemyAI v4.2)

```
EnemyDataSO.stateTransitionDelay (기본값 0.4초)
  Chase → Attack : 딜레이 코루틴 후 OnEnterAttack() 실행
  Attack → Chase : 딜레이 코루틴 후 ChangeState(Chase) 실행
  딜레이 중      : _isTransitioning = true → 추가 전환 요청 무시
  Patrol ↔ Idle  : 딜레이 미적용 (즉각 전환 유지)
```

| stateTransitionDelay 값 | 느낌 |
|---|---|
| `0.0` | 즉각 반응 (기존 동작) |
| `0.3` | 약간 둔함 |
| `0.4` | 기본값 — 자연스러운 반응 |
| `0.8` | 매우 느린 반응 |

---

#### ③ EnemyKnight 자물쇠 List 확장 (EnemyKnight v1.4)

```
기존: LockComponent _backLock              (단일)
변경: List<LockComponent> _locks           (리스트)
      int _unlockedCount                   (해제된 수 추적)
      bool _isAllLocksUnlocked             (전부 해제 여부)
```

**후면 공격 처리 변경**
```
GetFirstLockedLock()
  _locks 순서 순회 → IsUnlocked == false 인 첫 번째에 TakeDamage
  모두 해제됐으면 → EnemyBase 정상 피격
```

**해제 조건 확장 구조**
```csharp
// CheckAllUnlocked() — 이 메서드만 수정하면 조건 변경 가능
private bool CheckAllUnlocked()
    => _unlockedCount >= _locks.Count;  // 현재: 전부 해제

// 추후 확장:
//   일부 해제 조건 → _unlockedCount >= requiredCount
//   속성 조건      → 특정 SealType/AttackType 자물쇠만 체크
```

**TakeDamage 분기 흐름 (v1.4)**
```
① 모든 자물쇠 해제 완료 → EnemyBase.TakeDamage()
② Guard 봉인 활성       → 방패 무시 → EnemyBase.TakeDamage()
③ 정면 공격             → 방패 완전 무효 (반응 없음)
④ 후면 공격             → GetFirstLockedLock().TakeDamage()
```

---

**파일 버전 스냅샷 (v0.17 기준)**

| 파일 | 버전 |
|---|---|
| `EnemyBase.cs` | v1.2 |
| `EnemyAI.cs` | v4.2 |
| `EnemyDataSO.cs` | v2.2 |
| `EnemySensor.cs` | v1.1 |
| `EnemyKnight.cs` | v1.4 |
| `EnemyKnightAttack.cs` | v1.2 |
| `EnemyKnightChargeAttack.cs` | v1.5 |
| `LockComponent.cs` | v1.0 |
| `PlayerHealth.cs` | v1.0 |

---

### v0.18 — Enemy 시스템 리모델링 9단계 완료

**리모델링 배경**
기존 코드의 정면/후면 판단이 방향 벡터(dot product)에 의존하여
Flip 연동이 복잡하고 버그가 많았음.
콜라이더 레이어 기반으로 전면 재설계.

**완성 파일 (9단계)**

| 단계 | 파일 | 버전 | 핵심 변경 |
|---|---|---|---|
| 1 | `EnemyDataSO.cs` | v3.0 | 공통 수치 + 차징 수치 포함 |
| 2 | `EnemyBase.cs` | v2.0 | virtual TakeDamage, 사망 처리 OnDead |
| 3 | `LockComponent.cs` | v2.0 | OnFlipped 구독, localPosition.x 자동 반전 |
| 4 | `EnemyKnight.cs` | v2.0 | IsFrontalAttack 제거, override TakeDamage, ShieldCollider Flip |
| 5 | `EnemySensor.cs` | v2.0 | CheckAttackRange 제거, CheckChargeRange 유지 |
| 6 | `EnemyAI.cs` | v5.0 | 근접 공격 제거, 차징 전용, OnFlipped 이벤트 발행 |
| 7 | `EnemyKnightChargeAttack.cs` | v2.0 | 확정 거리 짧음 버그 수정 |
| 8 | `PlayerWeaponHitboxManager.cs` | v1.3 | EnemyShield 레이어 무시 분기 추가 |
| 9 | Enemy_Knight Prefab 가이드 | — | ShieldCollider 신규, Lock localPos -1.7 |

**핵심 설계 변경**

```
[기존] 방향 벡터 dot product 로 정면/후면 판단
  IsFrontalAttack(DamageInfo.Direction) → Flip 연동 복잡, 버그 다수

[변경] 콜라이더 레이어가 정면/후면 정의
  EnemyShield (Layer 18) → 방패 정면 — PlayerWeaponHitboxManager 무시
  EnemyLock   (Layer 17) → 자물쇠 후방 — LockComponent.TakeDamage() 직접 호출
  Enemy       (Layer 15) → 본체 — Lock 전부 해제 후만 피격

[Flip 구조]
  EnemyAI.SetFacing(dir) → OnFlipped 이벤트 발행
    ↳ EnemyKnight.FlipShield        ShieldCollider = +originalX * dir (정면)
    ↳ LockComponent.FlipPosition    Lock = -originalX * dir (후방)
    ↳ EnemyKnightChargeAttack.FlipHitbox
```

**EnemyKnightAttack 제거**
기사형은 차징 돌진만 사용. 근접 공격 제거.
EnemyAI v5.0 에서 _attack 참조 완전 제거.

**EnemyAI 상태 전환 (v5.0)**
```
Patrol → Chase → Attack(차징) → Groggy → Chase
봉인 취소 / 벽 충돌 → Groggy 직행
Groggy 종료 → TurnTowardPlayer → Chase
```

**TakeDamage 분기 (EnemyKnight v2.0)**
```
Lock 전부 해제 → base.TakeDamage() (사망 가능)
Guard 봉인 활성 → base.TakeDamage() (방패 무시)
그 외 → 무시 (ShieldCollider 가 물리적으로 차단)
```

**신규 오브젝트 — Enemy_Knight Prefab**
```
ShieldCollider        Layer: EnemyShield  isTrigger=OFF  localPos=(+0.5, 0, 0)
Lock                  Layer: EnemyLock    isTrigger=ON   localPos=(-1.7, 0, 0) ← 수정
```

**Physics 2D Matrix 필수 설정**
```
Player ↔ EnemyShield        = ON  (플레이어가 방패에 막힘)
PlayerAttackHit ↔ EnemyShield = OFF  (무기 히트박스는 방패 무시)
PlayerAttackHit ↔ Enemy      = ON
PlayerAttackHit ↔ EnemyLock  = ON
Player ↔ EnemyAttackHit      = ON
```

---

### v0.19 — ObjectFlipController + EnemyDataSO v4.0 리팩토링

**완성 파일**

| 파일 | 버전 | 역할 |
|---|---|---|
| `ObjectFlipController.cs` | v1.0 | 자식 오브젝트 좌우 반전 일괄 관리 (신규) |
| `EnemyDataSO.cs` | v4.0 | 공통 수치만 유지 — 차징 수치 제거 |
| `EnemyKnightChargeAttack.cs` | v2.1 | 차징 수치 Inspector 직접 관리 |
| `EnemyAI.cs` | v5.1 | chargeCooldown → ChargeAttack.ChargeCooldown 참조 |

---

#### ObjectFlipController v1.0

**배경**
SpriteRenderer.flipX 로 스프라이트를 반전할 때 자식 오브젝트의
localPosition 은 World 좌표 체계로 인해 자동 반전되지 않음.
기존: 각 스크립트(EnemyKnightChargeAttack, LockComponent, EnemyKnight 등)가
각자 OnFlipped 구독 + _originalLocalX 캐싱 → 같은 패턴 중복.

**해결**
ObjectFlipController 에 반전 대상 Transform 을 List 로 등록.
PlayerMover.OnFlipped 또는 EnemyAI.OnFlipped 구독.
방향 전환 시 List 의 모든 오브젝트 localPosition.x 일괄 반전.

**Inspector 구성**

```
ObjectFlipController
  _flipSourceType : PlayerMover / EnemyAI / Both
  _flipTargets    : [Transform 목록]
  _invertList     : [bool 목록] false=정면 / true=후방
```

**반전 공식**
```
invert = false : localPosition.x = +originalAbsX * dir  (정면 — 히트박스, 방패)
invert = true  : localPosition.x = -originalAbsX * dir  (후방 — 자물쇠)
```

**Inspector 설정 예시**

Enemy_Knight:
```
_flipSourceType = EnemyAI
_flipTargets[0] = ShieldCollider  _invertList[0] = false  (정면)
_flipTargets[1] = Lock            _invertList[1] = true   (후방)
_flipTargets[2] = ChargeHitbox    _invertList[2] = false  (정면)
```

Player.Weapon:
```
_flipSourceType = PlayerMover
_flipTargets[0] = Weapon
_flipTargets[1] = Hitbox_Combo1
...
```

**기존 스크립트 정리 가능 항목 (ObjectFlipController 도입 시)**

| 스크립트 | 제거 가능 코드 |
|---|---|
| EnemyKnightChargeAttack | _originalChargeHitboxLocalX, FlipHitbox(), OnFlipped 구독 |
| EnemyKnight | _originalShieldLocalX, FlipShield(), OnFlipped 구독 |
| LockComponent | _originalLocalX, FlipPosition(), OnFlipped 구독 |
| PlayerWeaponMover | HandleFlipped() 중 localPosition 반전 부분 |
| PlayerWeaponHitboxManager | FlipHitboxes(), _HitBoxPosition 캐시 |

---

#### EnemyDataSO v4.0 — 방향 C 적용 (공통 수치만)

**설계 방향 C**
```
EnemyDataSO = 모든 Enemy 가 공통으로 쓰는 수치만 보관
타입 전용 수치 = 해당 Attack 스크립트 Inspector 필드로 직접 관리
```

**EnemyDataSO 에서 제거된 필드**

| 필드 | 이동 위치 |
|---|---|
| `chargeSpeed` | `EnemyKnightChargeAttack._chargeSpeed` |
| `chargeDuration` | `EnemyKnightChargeAttack._chargeDuration` |
| `chargeDamage` | `EnemyKnightChargeAttack._chargeDamage` |
| `chargeCooldown` | `EnemyKnightChargeAttack._chargeCooldown` |

**EnemyDataSO 최종 필드 목록 (v4.0)**

| 섹션 | 필드 |
|---|---|
| 기본 정보 | enemyName, enemyType |
| 체력 | maxHp |
| 피격 반응 | knockbackForce, knockbackDecay, iFrameDuration, hitFlashInterval |
| 이동 | patrolSpeed, chaseSpeed, idleTimeMin, idleTimeMax, idleChance |
| 감지 | patrolSightRange, chaseSightRadius, chargeDetectRange, wallCheckDistance, cliffCheckDistance, cliffCheckOffset |
| AI | groggyDuration |
| 레이어 | playerLayer, groundLayer, attackHitLayer |

**EnemyKnightChargeAttack v2.1 신규 Inspector 필드**
```
── 차징 수치 ──
_chargeSpeed    = 14
_chargeDuration = 0.8
_chargeDamage   = 20
_chargeCooldown = 5
```

`ChargeCooldown` public 프로퍼티 추가 → EnemyAI.OnEnterAttack() 에서
`_chargeAttack.TryAttack(_chargeAttack.ChargeCooldown)` 으로 참조.

**추후 Enemy 추가 패턴**
```
새 Enemy Attack 스크립트 (예: EnemyDroneAttack)
  [SerializeField] private float _laserDamage = 10f;
  [SerializeField] private float _laserCooldown = 3f;
  → EnemyDataSO 수정 없음
  → 기존 DataSO 에셋(KnightData.asset 등) 영향 없음
```