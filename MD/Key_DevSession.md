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

**KeyDataSO 스윙 기본값**
```
swingDistance    : 0.5
swingDuration    : 0.08
returnDuration   : 0.15
airSwingDistance : 0.4
```

---

### v0.7 — Animator 파라미터 개편 + PlayerMover 이벤트 추가

**배경 — Player.controller 분석 결과**

| 문제 | 내용 |
|---|---|
| Jump Trigger 미연결 | 파라미터 존재하나 어떤 전환에도 연결 안 됨 |
| Fall 진입 조건 없음 | Jump ExitTime에만 의존, velocity 기반 조건 없음 |
| Attack 조건 없음 | AnyState + 조건 없음 → 아무 때나 Attack01 진입 |
| AttackCombo1/2/3 미존재 | Trigger 파라미터 자체가 없음 |
| 공중 공격 스테이트 없음 | PlayerAirAttack 스테이트 미존재 |

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

파라미터 추가
```
VelocityY     (Float)
Jump          (Trigger)
AttackCombo1  (Trigger)
AttackCombo2  (Trigger)
AttackCombo3  (Trigger)
AirAttack     (Trigger)
```

전환 수정
```
Idle/Move → PlayerJump      : Jump(Trigger) 조건으로 교체
PlayerJump → PlayerFall     : VelocityY < -0.1 조건 추가
AnyState → PlayerAttack01   : AttackCombo1(Trigger) + IsGrounded=true
Attack01 → Attack02         : AttackCombo2(Trigger) + ExitTime 0.5
Attack02 → Attack03         : AttackCombo3(Trigger) + ExitTime 0.5
Attack01/02/03 → PlayerIdle : ExitTime 1.0
AnyState → PlayerAirAttack  : AirAttack(Trigger) + IsGrounded=false
PlayerAirAttack → PlayerFall: ExitTime 1.0
```

신규 스테이트 추가
```
PlayerAirAttack — 공중 내리찍기 모션 (클립: 스프라이트 완성 후)
```

---

## 미결 항목

| 항목 | 상태 | 메모 |
|---|---|---|
| Player.controller 에디터 수정 | 🔲 미착수 | v0.7 가이드 참고 |
| 스프라이트 / 애니메이션 클립 | 🔲 미착수 | 완성 후 클립 연결 |
| AnimatorOverrideController 세팅 | 🔲 보류 | 스프라이트 완성 후 |
| 자물쇠 해제 조건 다양화 | 🔲 미착수 | LockComponent 확장 필요 |
| KeyType enum 4종 추가 | 🔲 미착수 | 봉인/반전/연쇄/귀환 열쇠 |
| 테스트 씬 구성 | 🔲 미착수 | 더미 적 + 플레이어 전투 확인 |
| GameManager | 🔲 미착수 | 씬 전역 관리 |
| CinemachineCamera | 🔲 미착수 | 플레이어 추적 카메라 |