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
| Player.controller 에디터 수정 | 🔲 미착수 | v0.7 가이드 참고 |
| Attack 클립 Loop Time OFF | 🔲 필수 | PlayerAttack01/02/03.anim |
| 스프라이트 / 애니메이션 클립 | 🔲 미착수 | 완성 후 클립 연결 |
| AnimatorOverrideController 세팅 | 🔲 보류 | 스프라이트 완성 후 |
| SealProjectile Prefab 생성 | 🔲 미착수 | Hierarchy 가이드 참고 |
| EnemySealComponent 적 부착 | 🔲 미착수 | Enemy_Knight 에 우선 부착 |
| SealData 에셋 생성 | 🔲 미착수 | Assets/KEY/DataSO/Seals/ |
| 자물쇠 해제 조건 다양화 | 🔲 미착수 | LockComponent 확장 필요 |
| 테스트 씬 구성 | 🔲 미착수 | 봉인 시스템 포함 전투 테스트 |
| GameManager | 🔲 미착수 | 씬 전역 관리 |
| CinemachineCamera | 🔲 미착수 | 플레이어 추적 카메라 |