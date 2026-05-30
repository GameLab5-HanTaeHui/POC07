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
| Input System | New Input System — 코드 직접 바인딩 방식 |
| DOTween | 최신 안정 버전 (HOTween v2) |
| TextMeshPro | UI 텍스트 전용 |

---

## 세션 기록

---

### v0.1 — 이동 패키지

| 파일 | 역할 | 버전 |
|---|---|---|
| `MovementSettings.cs` | 이동 수치 ScriptableObject | v1.0 |
| `MovementAnimator.cs` | Animator 파라미터 동기화 | v1.1 |

---

### v0.2 — 입력 통합 + 무기 시스템 1차

| 파일 | 역할 | 버전 |
|---|---|---|
| `InputManager.cs` | 입력 통합 관리 | v1.0 |
| `PlayerMover.cs` | 이동 물리 | v1.3 |
| `PlayerMovementFacade.cs` | 외부 단일 진입점 (싱글턴) | v1.1 |
| `IDamageable.cs` | 피격 인터페이스 | v1.0 |
| `PlayerWeaponBase.cs` | 무기 추상 베이스 | v1.0 |
| `PlayerWeaponHitboxManager.cs` | 히트박스 관리 | v1.0 |
| `RustyKeyWeapon.cs` | 녹슨 열쇠 구현체 | v1.0 |

---

### v0.3 — 열쇠 데이터 구조 + 무기 교체 시스템

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyType.cs` | 열쇠 타입 enum | v1.0 |
| `KeyDataSO.cs` | 열쇠 데이터 SO | v1.0 |
| `KeyInventoryDataSO.cs` | 보유 열쇠 목록 SO | v1.0 |
| `PlayerWeaponController.cs` | 열쇠 교체 컨트롤러 | v1.1 |

---

### v0.4 — 더미 적 시스템

| 파일 | 역할 | 버전 |
|---|---|---|
| `EnemyDataSO.cs` | 적 수치 SO | v1.0 |
| `EnemyBase.cs` | 적 추상 베이스 | v1.0 |
| `LockComponent.cs` | 자물쇠 컴포넌트 | v1.0 |
| `EnemyDummy.cs` | 자물쇠 없는 정지 더미 | v1.2 |
| `EnemyDummyLocked.cs` | 자물쇠 있는 정지 더미 | v1.2 |

**구조 결정**
- 넉백: `KnockbackRoutine` 코루틴 (`velocity.x *= knockbackDecay`)
- Rigidbody2D: `gravityScale=1` / `FreezeRotation Z`
- 더미 사망 없음 — 체력 최솟값 1 고정

---

### v0.5 — 기사형 적 시스템

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

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyDataSO.cs` | 스윙 이동 수치 추가 | v1.1 |
| `PlayerWeaponMover.cs` | Weapon DOTween 스윙 이동 | v1.0 |
| `PlayerWeaponAnimator.cs` | 무기 이벤트 구독 → 스윙 이동 연동 | v1.0 |
| `PlayerWeaponController.cs` | 명칭 변경 + 연동 추가 | v1.2 |

---

### v0.7 — Animator 파라미터 개편

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerMover.cs` | OnJumped 이벤트, VelocityY 프로퍼티 추가 | v1.4 |
| `MovementAnimator.cs` | 파라미터 전면 개편, 무기 Trigger 통합 | v2.0 |
| `PlayerWeaponAnimator.cs` | Trigger 발행 제거 → 스윙 이동만 담당 | v1.1 |
| `PlayerWeaponController.cs` | MovementAnimator 연동 추가 | v1.3 |

**Animator 파라미터 전체 목록**

| 파라미터 | 타입 | 갱신 방식 |
|---|---|---|
| `Speed` | Float | 매 프레임 |
| `VelocityY` | Float | 매 프레임 |
| `IsGrounded` | Bool | 매 프레임 |
| `IsFiring` | Bool | 외부 호출 |
| `Jump` | Trigger | PlayerMover.OnJumped |
| `DoubleJump` | Trigger | PlayerMover.OnDoubleJumped |
| `Dash` | Trigger | PlayerMover.OnDashStarted |
| `AttackCombo1` | Trigger | RustyKeyWeapon.OnCombo1Started |
| `AttackCombo2` | Trigger | RustyKeyWeapon.OnCombo2Started |
| `AttackCombo3` | Trigger | RustyKeyWeapon.OnCombo3Started |
| `AirAttack` | Trigger | RustyKeyWeapon.OnAirAttackStarted |

**Player.controller 전환 조건**
```
Idle/Move → PlayerJump      : Jump(Trigger)
PlayerJump → PlayerFall     : VelocityY < -0.1
AnyState → PlayerAttack01   : AttackCombo1 + IsGrounded=true
Attack01 → Attack02         : AttackCombo2 + ExitTime 0.5
Attack02 → Attack03         : AttackCombo3 + ExitTime 0.5
Attack01/02/03 → PlayerIdle : ExitTime 1.0
AnyState → PlayerAirAttack  : AirAttack + IsGrounded=false
PlayerAirAttack → PlayerFall: ExitTime 1.0
Attack 클립 Loop Time = OFF 필수
```

---

### v0.8 — 버그픽스

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerMover.cs` | 대쉬 DOMove → MovePosition 코루틴, OnFlipped 이벤트 추가 | v1.5 |
| `PlayerWeaponMover.cs` | OnFlipped 구독, Weapon X 동기화 | v1.1 |
| `MovementAnimator.cs` | ResetTrigger 추가 | v2.1 |
| `RustyKeyWeapon.cs` | normalizedTime 폴링, Trigger 선발행 버그 수정 | v1.3 |
| `KeyDataSO.cs` | Animator 콤보 타이밍 필드 추가 | v1.2 |

**버그 수정 내용**

| 버그 | 원인 | 수정 |
|---|---|---|
| 대쉬 얇은 벽 관통 | DOMove 물리 무시 | MovePosition 코루틴 + CastCollider |
| 무기 왼쪽 방향 위치 오류 | _originLocalPosition X 고정 | OnFlipped 이벤트로 X 반전 |
| 콤보 타이밍 불일치 | elapsed 타이머 vs Animator 불일치 | normalizedTime 직접 폴링 |
| Trigger 큐 잔류 | SetTrigger 미소비 | ResetTrigger 일괄 클리어 |

---

### v0.9 — 프레임 방어 코드

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `RustyKeyWeapon.cs` | `_lastAttackInputFrame` 프레임 방어 추가 | v1.4 |

---

### v0.10 — 봉인 열쇠 시스템

| 파일 | 역할 | 버전 |
|---|---|---|
| `SealType.cs` | 봉인 타입 enum | v1.0 |
| `SealDataSO.cs` | 봉인 열쇠 수치 SO | v1.0 |
| `SealKeyWeapon.cs` | 봉인 열쇠 무기 | v1.0 |
| `SealProjectile.cs` | 봉인 투사체 | v1.0 |
| `EnemySealComponent.cs` | 봉인 상태 관리 | v1.0 |
| `EnemyAI.cs` | 봉인 체크 추가 | v3.0 |
| `EnemyKnight.cs` | Guard 봉인 체크 추가 | v1.2 |

**봉인 타입별 효과**

| SealType | 차단 행동 |
|---|---|
| Dash | 돌진 / 급이동 |
| Jump | 점프 / 상승 |
| Ranged | 원거리 공격 |
| Guard | 방어 / 가드 → 정면 피격 허용 |
| Move | 이동 전체 정지 |
| Attack | 모든 공격 차단 |

---

### v0.11 — 히트박스 좌우 반전 처리

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerWeaponHitboxManager.cs` | FlipHitboxes() 추가, _HitBoxPosition 캐시 | v1.1 |
| `PlayerWeaponMover.cs` | HandleFlipped()에 SpriteRenderer.flipX 추가 | v1.1 |

---

### v0.12 — 무기 교체 UI (WeaponHUD)

| 파일 | 역할 | 버전 |
|---|---|---|
| `WeaponSlotUI.cs` | 개별 무기 슬롯 UI (키 바인딩 텍스트 포함) | v1.1 |
| `WeaponHUDController.cs` | HUD 전체 관리 | v1.2 |

**패널 동작**
```
Ctrl 누름  → 패널 열림 (SetPanelVisible true)
Ctrl 뗌    → 패널 닫힘 (SetPanelVisible false)
무기 교체  → 패널 닫힘 (HandleKeyEquipped 에서 자동)
```

**WeaponSlot Prefab 구조**
```
WeaponSlot
├── [WeaponSlotUI]
├── [Image]           슬롯 배경 (장착=노랑 / 미장착=어두운 회색)
├── Icon [Image]      keySprite
├── KeyName [TMP]     keyName
├── KeyBinding [TMP]  슬롯 키 이름 ("1","Q","A","Z" 등)
└── EquippedIndicator 장착 중 강조
```

---

### v0.13 — 입력 시스템 재편 + KeySwap 모드

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `InputManager.cs` | 키 바인딩 변경 + InGame/KeySwap 2계층 분리 | v2.0→v2.1 |
| `WeaponHUDController.cs` | OnKeySwap / OnKeySwapModeChanged 구독 추가 | v1.1 |
| `WeaponSlotUI.cs` | Button 제거, 키 바인딩 텍스트 추가 | v1.1 |

**키 바인딩 최종 (v0.13 기준)**

| 동작 | 키 | 모드 |
|---|---|---|
| 이동 | ← → | 항상 |
| 점프 | Space | 항상 |
| 대쉬 | Left Shift | 항상 |
| 공격 | A | InGame |
| KeySwap 모드 | Left Ctrl (누름 유지) | 항상 |
| 슬롯 0~3 | 1 2 3 4 | KeySwap 중 |
| 슬롯 4~7 | Q W E R | KeySwap 중 |
| 슬롯 8~11 | A S D F | KeySwap 중 (A=공격 겸용) |
| 슬롯 12~15 | Z X C V | KeySwap 중 |

---

### v0.14 — 차징 공격 시스템 1차

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyDataSO.cs` | 차징 수치 추가 | v1.3 |
| `InputManager.cs` | OnChargeStart / OnChargeRelease / OnAimAdjust 이벤트 | v2.2 |
| `IChargeProjectile.cs` | 투사체 인터페이스 `Launch(dir, power)` | v1.0 |
| `ChargeProjectile.cs` | 투사체 구현체 — 충돌/소멸 처리 | v1.0 |
| `ChargeAimLine.cs` | LineRenderer + DOTween 차징 피드백 | v1.0 |
| `PlayerChargeAttack.cs` | 차징 상태 관리 + 발사 | v1.0 |

**ChargeProjectile 충돌 처리**
```
Enemy 레이어    → LockComponent TakeDamage / IDamageable TakeDamage → Die()
Ground/Wall     → Die() 즉시 소멸
lifetime 초과   → Die() 자동 소멸
Die()           → velocity=0 → DOScale(0, 0.1s) → Destroy
```

**DOTween 피드백**
```
ChargeAimLine.Show()      : 라인 0 → minLength (OutQuart, 0.12s)
ChargeAimLine.UpdateCharge: 라인 색 흰→노→빨 / 길이 min→max
ChargeAimLine.최대 차징   : Player DOPunchPosition (시위 떨림)
ChargeAimLine.Hide()      : 라인 → 0 (InQuart, 0.08s)
ChargeProjectile.Launch() : DOPunchScale 발사 충격
ChargeProjectile.Die()    : DOScale → 0 (InQuart, 0.1s)
```

---

### v0.15 — 차징 공격 개선

**문제 및 해결**

| 문제 | 해결 |
|---|---|
| 차징 중 이동/점프/대쉬 가능 | BlockMove + BlockDash + BlockJump + velocity.x 매 프레임 0 유지 |
| 최대 차징 자동 발사 | maxChargeTime 자동 발사 로직 제거. S 뗌으로만 발사 |
| 각도 n도씩 단계적 변화 | OnAimAdjust int→float. ↑↓ 누름 유지 → 매 프레임 연속 변화 |
| 차징 중 방향 전환 미반영 | PlayerMover.ForceFlip() API 추가 → OnFlipped 연쇄 발행 |
| FirePoint 위치 미반영 | HandleChargeFlip에서 _firePoint.localPosition.x 부호 반전 |

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `InputManager.cs` | BlockMove/BlockDash API, OnAimAdjust float, OnChargeFlip 이벤트 | v2.4 |
| `PlayerMover.cs` | ForceFlip() 외부 API, FlipSprite에 OnFlipped 발행 추가 | v1.6 |
| `PlayerChargeAttack.cs` | 이동 전면 차단, 각도 연속 변화, 방향 전환 연쇄 플립, FirePoint 플립 | v1.3 |

**차징 중 플립 연쇄 흐름**
```
← 방향키
  → InputManager.OnChargeFlip(-1f)
      → HandleChargeFlip(-1f)
          → PlayerMover.ForceFlip(-1f)
              → SpriteRenderer.flipX = true
              → OnFlipped(-1f)
                  → PlayerWeaponMover.HandleFlipped(-1f)
                  → PlayerWeaponHitboxManager.FlipHitboxes(-1f)
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

**차징 키 바인딩 최종**

| 동작 | 키 |
|---|---|
| 차징 시작 | S 누름 유지 |
| 발사 | S 뗌 (minChargeTime 이상) |
| 취소 | S 뗌 (minChargeTime 미만) |
| 조준 위/아래 | ↑ / ↓ 누름 유지 (연속 변화) |
| 발사 방향 전환 | ← → 방향키 |

---

### v0.16 — Enemy 시스템 전면 개편

**주요 작업**

1. EnemyAI / EnemyBase DataSO 참조 구조 수정 (중복 Inspector 연결 제거)
2. 플레이어 피격 시스템 신규 구현 (PlayerHealth)
3. 기사형 차징 돌진 공격 구현 (EnemyKnightChargeAttack)
4. EnemySensor 차징 감지 범위 추가

---

#### 1단계 — DataSO 단일 연결 지점 확립

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `EnemyBase.cs` | `Settings` public 프로퍼티 추가 | v1.2 |
| `EnemyAI.cs` | `_settings` Inspector 제거 → `EnemyBase.Settings` 참조 | v4.0 |

**변경 구조**
```
기존: EnemyBase._settings (Inspector 연결)
      EnemyAI._settings   (Inspector 연결) ← 중복

변경: EnemyBase._settings (Inspector 연결) ← 유일한 연결 지점
      EnemyAI.Awake()   → GetComponent<EnemyBase>().Settings 참조
      EnemySensor       → EnemyAI 가 SetData() 주입
      EnemyKnightAttack → EnemyAI 가 SetData() 주입
```

---

#### 2단계 — 플레이어 피격 피드백 + 차징 돌진

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `PlayerHealth.cs` | 신규 — IDamageable 구현 / iFrame / 넉백 / 피격플래시 / 사망(OnDead) | v1.0 |
| `EnemyDataSO.cs` | attackHitLayer + 차징 수치 6종 + chargeDetectRange 추가 | v2.1 |
| `EnemyKnightAttack.cs` | attackHitLayer 사용 / _overlapBuffer GC 방지 / FlipHitbox() 추가 | v1.2 |
| `EnemyKnightChargeAttack.cs` | LineRenderer 점증 + ScanForObstacle + MovePosition 돌진 | v1.4 |
| `EnemySensor.cs` | CheckChargeRange() 추가 | v1.1 |
| `EnemyAI.cs` | _chargeAttack 구독 / FlipAttackHitboxes() / 중복 진입 차단 | v4.1 |

**EnemyKnightChargeAttack 돌진 흐름**
```
① Countdown (3초)
   LineRenderer 0 → 최대길이 점증 + 색상 노→빨
   매 프레임 ScanForObstacle (벽 수평Ray + 낭떠러지 하향Ray + 이진탐색)
   → 장애물 감지 시 _confirmedLength 고정, 선 멈춤

② _confirmedLength < 0.3f → 차징 취소

③ Charge
   MovePosition 코루틴, 매 FixedUpdate HitWall / HitPlayer Raycast

④ 종료
   velocity=0 → OnAttackFinished → Chase 복귀
```

**EnemyAI 공격 우선순위 (v4.1)**
```
OnEnterAttack() Knight 분기:
  chargeReady && inChargeRange && !inAttackRange → 차징 돌진 (우선)
  normalReady && inAttackRange                  → 일반 근접 공격
  chargeReady && inChargeRange                  → 차징 돌진 (근접 범위 내)
  모두 쿨다운                                   → ChangeState(Chase)
```

**EnemyDataSO v2.1 추가 필드**

| 필드 | 기본값 | 용도 |
|---|---|---|
| `attackHitLayer` | Player | 공격 히트박스 전용 레이어 |
| `chargeDetectRange` | 5.0 | 차징 발동 감지 거리 |
| `chargeSpeed` | 14 | 돌진 속도 |
| `chargeDuration` | 0.8 | 돌진 지속 시간 |
| `chargeDamage` | 25 | 돌진 피해량 |
| `chargeKnockbackMultiplier` | 2.0 | 돌진 넉백 배율 |
| `chargeCooldown` | 5.0 | 차징 재사용 대기 |

**PlayerHealth v1.0 구조**
```
IDamageable.TakeDamage(DamageInfo)
  → iFrame 중이면 무시
  → HP 감소 → HP <= 0 → OnDead 이벤트
  → iFrame 코루틴 시작 (iFrameDuration)
  → 넉백 코루틴 (KnockbackRoutine)
  → 피격 플래시 (HitFlashRoutine)
```

---

---

### v0.18 — Enemy 시스템 리모델링 (콜라이더 레이어 기반 방어)

**리모델링 배경**
방향 벡터 dot product 기반 정면/후면 판단 → Flip 연동 복잡 + 버그 다수.
콜라이더 레이어가 방패/자물쇠를 물리적으로 정의하는 구조로 전면 재설계.

**완성 파일**

| 파일 | 버전 | 핵심 변경 |
|---|---|---|
| `EnemyDataSO.cs` | v3.0 | 공통 수치 + 차징 수치 + groggyDuration 포함 |
| `EnemyBase.cs` | v2.0 | virtual TakeDamage, OnDead 이벤트 |
| `LockComponent.cs` | v2.0 | OnFlipped 구독, localPosition.x 자동 반전 |
| `EnemyKnight.cs` | v2.0 | IsFrontalAttack 제거, ShieldCollider Flip 추가 |
| `EnemySensor.cs` | v2.0 | CheckAttackRange 제거, CheckChargeRange 유지 |
| `EnemyAI.cs` | v5.0 | 근접 공격 제거, 차징 전용, Groggy 상태 추가, OnFlipped 이벤트 발행 |
| `EnemyKnightChargeAttack.cs` | v2.0 | 확정 거리 계산 버그 수정 |
| `PlayerWeaponHitboxManager.cs` | v1.3 | EnemyShield 레이어 무시 분기 추가 |

**핵심 설계 변경**

```
[기존] 방향 벡터 dot product 로 정면/후면 판단
  IsFrontalAttack(info.Direction) → Flip 연동 복잡, 버그 다수

[변경] 콜라이더 레이어가 정면/후면 물리적으로 정의
  EnemyShield (Layer 18) → 방패 — PlayerWeaponHitboxManager OnTrigger 에서 무시
  EnemyLock   (Layer 17) → 자물쇠 — LockComponent.TakeDamage() 직접 호출
  Enemy       (Layer 15) → 본체 — Lock 전부 해제 후만 EnemyKnight.TakeDamage 가능
```

**Flip 연쇄 구조**

```
EnemyAI.SetFacing(dir) → OnFlipped(dir) 이벤트 발행
  ↳ EnemyKnight.FlipShield()           ShieldCollider = +originalX × dir (정면)
  ↳ LockComponent.FlipPosition()        Lock = -originalX × dir (후방)
  ↳ EnemyKnightChargeAttack.FlipHitbox() ChargeHitbox = +originalX × dir
```

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

**EnemyKnightAttack 제거**
기사형은 차징 돌진만 사용. 근접 공격 제거.
EnemyAI v5.0 에서 _attack 참조 완전 제거.

**신규 오브젝트 — Enemy_Knight Prefab 구조**

```
Enemy_Knight
├── [EnemyKnight]
├── [EnemyAI]
├── [EnemyKnightChargeAttack]
├── [EnemySensor]
├── [EnemySealComponent]
├── [Rigidbody2D] / [CapsuleCollider2D] / [SpriteRenderer]
│
├── ShieldCollider              Layer: EnemyShield   isTrigger=OFF  localPos=(+0.5, 0, 0)
│     └── [BoxCollider2D]
│
├── Lock                        Layer: EnemyLock     isTrigger=ON   localPos=(-1.7, 0, 0)
│     ├── [LockComponent]
│     └── [BoxCollider2D]
│
├── ChargeHitbox                Layer: EnemyAttackHit  isTrigger=ON
│     └── [BoxCollider2D]
│
└── SealOverlay
      └── [SpriteRenderer]
```

**Layer 신규 추가**

| Layer | 번호 | 용도 |
|---|---|---|
| EnemyShield | 18 | 방패 콜라이더 — 플레이어 물리 막힘 |
| EnemyLock | 17 | 자물쇠 콜라이더 — PlayerAttackHit 감지 |

**Physics 2D Matrix 필수 설정**

| | Player | PlayerAttackHit | Enemy | EnemyLock | EnemyShield | EnemyAttackHit |
|---|---|---|---|---|---|---|
| Player | | | | | **ON** | **ON** |
| PlayerAttackHit | | | **ON** | **ON** | **OFF** | |
| EnemyShield | **ON** | **OFF** | | | | |
| EnemyAttackHit | **ON** | | | | | |

---

### v0.19 — ObjectFlipController + EnemyDataSO v4.0 리팩토링

**완성 파일**

| 파일 | 버전 | 역할 |
|---|---|---|
| `ObjectFlipController.cs` | v1.0 | 자식 오브젝트 좌우 반전 일괄 관리 (신규) |
| `EnemyDataSO.cs` | v4.0 | 공통 수치만 유지 — 차징 수치 분리 |
| `EnemyKnightChargeAttack.cs` | v2.1 | 차징 수치 Inspector 직접 관리 |
| `EnemyAI.cs` | v5.1 | ChargeCooldown → ChargeAttack 프로퍼티 참조 |

**ObjectFlipController v1.0**

배경: 기존에 각 스크립트가 OnFlipped 구독 + `_originalLocalX` 캐싱 패턴을 중복으로 가지고 있었음.
해결: `ObjectFlipController` 하나가 `_flipTargets` 리스트를 일괄 관리.

```csharp
// Inspector 설정 예시 — Enemy_Knight
_flipSourceType = EnemyAI
_flipTargets:
  [0] ShieldCollider  _invertList[0] = false  (+dir = 정면)
  [1] Lock            _invertList[1] = true   (-dir = 후방)
  [2] ChargeHitbox    _invertList[2] = false  (+dir = 정면)

// Inspector 설정 예시 — Player.Weapon
_flipSourceType = PlayerMover
_flipTargets:
  [0] Weapon / Hitbox_Combo1 / Hitbox_Combo2 / ...
```

반전 공식:
```
invert=false : localPosition.x = +originalAbsX × dir  (정면)
invert=true  : localPosition.x = -originalAbsX × dir  (후방)
```

**기존 스크립트에서 제거 가능한 코드**

| 스크립트 | 제거된 코드 |
|---|---|
| `EnemyKnightChargeAttack` | `_originalChargeHitboxLocalX`, `FlipHitbox()`, `OnFlipped` 구독 |
| `EnemyKnight` | `_originalShieldLocalX`, `FlipShield()`, `OnFlipped` 구독 |
| `LockComponent` | `FlipPosition()`, `OnFlipped` 구독 |
| `PlayerWeaponMover` | `HandleFlipped()` 내 localPosition 반전 부분 |
| `PlayerWeaponHitboxManager` | `FlipHitboxes()`, `_HitBoxPosition` 캐시 |

**EnemyDataSO v4.0 — 차징 수치 분리**

EnemyDataSO에서 제거된 필드 → 이동 위치:

| 필드 | 이동 위치 |
|---|---|
| `chargeSpeed` | `EnemyKnightChargeAttack._chargeSpeed` |
| `chargeDuration` | `EnemyKnightChargeAttack._chargeDuration` |
| `chargeDamage` | `EnemyKnightChargeAttack._chargeDamage` |
| `chargeCooldown` | `EnemyKnightChargeAttack._chargeCooldown` |

유지된 필드: `chargeDetectRange` (EnemySensor 공통 사용), `groggyDuration` (EnemyAI 공통 사용)

`EnemyKnightChargeAttack.ChargeCooldown` public 프로퍼티 추가
→ EnemyAI v5.1 에서 `_chargeAttack.TryAttack(_chargeAttack.ChargeCooldown)` 으로 참조

**Player.prefab 컴포넌트 확인 결과**

```
Player (루트)                       Layer: Player (8)
├── [InputManager]                  v2.4
├── [PlayerMover]                   v1.6
├── [MovementAnimator]              v2.1
├── [PlayerMovementFacade]
├── [PlayerHealth]                  v1.0  _maxHp=5 / _iFrameDuration=0.6
├── [PlayerChargeAttack]            v1.3  _aimLine + _firePoint 연결됨
├── [ObjectFlipController]          v1.0  _flipSourceType=PlayerMover(1)
│     _flipTargets: Weapon, HitBox01~04, FirePoint (6개)
│     _invertList: (비어있음 = 전부 false = 정면 방향)
├── [Animator]
├── [Rigidbody2D]
├── [SpriteRenderer]
├── [CapsuleCollider2D]
│
├── GroundCheck
├── AimLine
│     ├── [ChargeAimLine]
│     └── [LineRenderer]
│
└── Weapon                          자식 오브젝트
      ├── [PlayerWeaponController]  v1.4
      ├── [RustyKeyWeapon]          v1.4
      ├── [SealKeyWeapon]           v1.0
      ├── [PlayerWeaponAnimator]    v1.1
      ├── [PlayerWeaponMover]       v1.1
      ├── [PlayerWeaponHitboxManager] v1.3
      │
      ├── HitBox01  Layer: PlayerAttackHit(11)  localPos=(0.7, 0, 0)
      ├── HitBox02  Layer: PlayerAttackHit(11)
      ├── HitBox03  Layer: PlayerAttackHit(11)
      ├── HitBox04  Layer: PlayerAttackHit(11)
      └── FirePoint
```

**파일 버전 스냅샷 (v0.19 기준)**

| 파일 | 버전 | 비고 |
|---|---|---|
| `InputManager.cs` | v2.4 | |
| `PlayerMover.cs` | v1.6 | |
| `PlayerChargeAttack.cs` | v1.3 | |
| `PlayerHealth.cs` | v1.0 | |
| `ObjectFlipController.cs` | v1.0 | Flip 일괄 담당 |
| `PlayerWeaponMover.cs` | v1.2 | HandleFlipped 제거 |
| `PlayerWeaponHitboxManager.cs` | v1.3 | FlipHitboxes 제거 |
| `EnemyBase.cs` | v2.0 | |
| `EnemyAI.cs` | v5.1 | FlipAttackHitboxes 제거 |
| `EnemyDataSO.cs` | v4.0 | |
| `EnemySensor.cs` | v2.0 | |
| `EnemyKnight.cs` | v2.1 | FlipShield 제거 |
| `EnemyKnightChargeAttack.cs` | v2.1 | FlipHitbox 제거 |
| `LockComponent.cs` | v2.1 | FlipPosition 제거 |

---

### v0.23 — VFX / 피격 피드백 시스템 (DOTween + 파티클)

**구현 배경**
기존 피격 피드백이 단순 색상 변경 수준.
콤보별 임팩트 없이 단순 이동만 하던 무기 스윙에 타격감 추가.
HitFeedback 을 파티클 연동 구조로 확장하여 모든 피격 상황 시각화.

**완성 파일**

| 파일 | 버전 | 역할 |
|---|---|---|
| `PlayerWeaponMover.cs` | v1.3 | 콤보별 DOTween 임팩트 + 히트스탑 |
| `PlayerWeaponAnimator.cs` | v1.2 | 공중 4방향 이벤트 구독 추가 |
| `HitFeedback.cs` | v2.0 | 파티클 연동 + SealApplied/LockUnlocked 신규 |
| `HitFeedbackConfig.cs` | v1.0 | 파티클 프리팹 등록 SO (신규) |
| `HitFeedbackInitializer.cs` | v1.0 | 씬 Config 주입 컴포넌트 (신규) |
| `SealComponent.cs` | v1.3 | StartSealVisual() HitFeedback.SealApplied() 연동 |
| `IDamageable.cs` | — | AttackType enum AirAttackDown / AirAttackUp 추가 |

**신규 파티클 프리팹 5종**

| 프리팹 | 색상 | 용도 |
|---|---|---|
| `HitEnemyParticle` | 흰+노랑 | 적/플레이어 피격 스파크 |
| `HitLockParticle` | 파랑+흰 | 자물쇠 피격 마찰 |
| `UnLockParticle` | 금색 | 자물쇠 해제 폭발 ★ 핵심 |
| `BlockedShield` | 파랑 | 방패 막힘 |
| `SealApplied` | 파랑+보라 | 봉인 적용 링 |

**PlayerWeaponMover v1.3 — 콤보별 DOTween 임팩트**

| 콤보 | DOTween 효과 |
|---|---|
| Combo1 | DOPunchPosition(X) + DOPunchRotation(Z) |
| Combo2 | DOPunchPosition(Y하) + DOPunchScale |
| Combo3 | 히트스탑(0.06초) + DOPunchPosition(X강) + DOPunchScale |
| AirSide | DOPunchPosition(X) + DOPunchRotation(Z소) |
| AirDown | DOPunchPosition(Y강하) + DOPunchScale |
| AirUp | DOPunchPosition(Y상) + DOPunchRotation(Z역) |

**히트스탑 구현**
```
Combo3 전용. Time.timeScale = 0 → 0.06초 대기 → Time.timeScale = 1
WaitForSecondsRealtime 사용 (언스케일드 타임)
Inspector: _hitStopDuration = 0.06f (0 = 비활성)
```

**HitFeedback v2.0 — 6가지 피드백**

| 메서드 | 파티클 | DOTween |
|---|---|---|
| `EnemyHitPlayer` | HitEnemyParticle | 빨간 플래시 + PunchPosition + PunchScale |
| `PlayerHitLock` | HitLockParticle (progress 비례) | 노랑/빨강 플래시 + PunchScale |
| `LockUnlocked` ★ | UnLockParticle (금색 폭발) | 금색 플래시 + 큰 PunchScale |
| `PlayerHitEnemy` | HitEnemyParticle | 흰→빨 플래시 + PunchPosition + PunchScale |
| `PlayerAttackBlocked` | BlockedShield | 파랑 플래시 + ShakePosition + 무기 반발 |
| `SealApplied` ★ | SealApplied (링) | 파랑 플래시 + ShakeScale |

**씬 배치**
```
GameManager 오브젝트
  └── [HitFeedbackInitializer]
        _config = HitFeedbackConfig.asset
```

**HitFeedbackConfig.asset 경로**
```
Assets/KEY/DataSO/HitFeedbackConfig.asset
  fxHitEnemy     = HitEnemyParticle.prefab
  fxHitLock      = HitLockParticle.prefab
  fxUnlockLock   = UnLockParticle.prefab
  fxBlockedShield = BlockedShield.prefab
  fxSealApplied  = SealApplied.prefab
```

**파일 버전 스냅샷 (v0.23 기준)**

| 파일 | 버전 |
|---|---|
| `InputManager.cs` | v2.5 |
| `PlayerMover.cs` | v1.6 |
| `PlayerChargeAttack.cs` | v1.4 |
| `PlayerHealth.cs` | v1.0 |
| `ObjectFlipController.cs` | v1.2 |
| `PlayerWeaponMover.cs` | v1.3 |
| `PlayerWeaponAnimator.cs` | v1.2 |
| `PlayerWeaponController.cs` | v1.5 |
| `PlayerWeaponHitboxManager.cs` | v1.3 |
| `RustyKeyWeapon.cs` | v1.5 |
| `KeyDataSO.cs` | v1.4 |
| `SealProjectile.cs` | v2.0 |
| `SealComponent.cs` | v1.3 |
| `HitFeedback.cs` | v2.0 |
| `HitFeedbackConfig.cs` | v1.0 |
| `HitFeedbackInitializer.cs` | v1.0 |
| `EnemyBase.cs` | v2.0 |
| `EnemyAI.cs` | v5.5 |
| `EnemyDataSO.cs` | v4.2 |
| `EnemySensor.cs` | v2.1 |
| `EnemyKnight.cs` | v2.1 |
| `EnemyKnightAttack.cs` | v1.0 |
| `EnemyKnightChargeAttack.cs` | v2.1 |
| `LockComponent.cs` | v2.1 |

## 미결 항목

| 항목 | 상태 | 메모 |
|---|---|---|
| Player.controller 에디터 수정 | ✅ 완료 | v0.7 가이드 |
| Attack 클립 Loop Time OFF | ✅ 완료 | v0.8 |
| SealProjectile Prefab 생성 | ✅ 완료 | Assets/KEY/Prefabs/ |
| EnemySealComponent 적 부착 | ✅ 완료 | Enemy_Knight 우선 |
| SealData 에셋 생성 | ✅ 완료 | Assets/KEY/DataSO/Seals/ |
| LockComponent 단일 → List 변환 | ✅ 완료 | v0.17 EnemyKnight v1.4 |
| Enemy 콜라이더 레이어 기반 방어 리모델링 | ✅ 완료 | v0.18 |
| ObjectFlipController 도입 | ✅ 완료 | v0.19 |
| EnemyDataSO 차징 수치 분리 | ✅ 완료 | v0.19 |
| ChargeProjectile Prefab 생성 | 🔲 미착수 | RustyKeyData.chargeProjectilePrefab 연결 필요 |
| using Unity.VisualScripting 제거 | 🔲 미착수 | PlayerWeaponHitboxManager |
| 스프라이트 / 애니메이션 클립 | 🔲 미착수 | 완성 후 클립 연결 |
| AnimatorOverrideController | 🔲 보류 | 스프라이트 완성 후 |
| LockComponent 해제 조건 다양화 | 🔲 미착수 | 방향/위상/시간 조건 확장 |
| SealProjectile ↔ Enemy 레이어 확인 | 🔲 미착수 | Physics 2D Matrix 설정 검증 필요 |
| KeyType enum 4종 추가 | 🔲 미착수 | 봉인/반전/연쇄/귀환 열쇠 |
| 테스트 씬 구성 | 🔲 미착수 | 차징 공격 + 기사 돌진 전투 테스트 |
| GameManager | 🔲 미착수 | 씬 전역 관리 |
| CinemachineCamera | 🔲 미착수 | 플레이어 추적 카메라 |

---

### v0.17 — Enemy 개선 (히트박스 플립 / 상태전환 딜레이 / 자물쇠 List 확장)

**완성 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `EnemyKnightChargeAttack.cs` | FlipHitbox() 추가, _originalChargeHitboxLocalX 캐싱 | v1.5 |
| `EnemyAI.cs` | FlipAttackHitboxes에 ChargeAttack FlipHitbox 연결, 상태전환 딜레이 추가 | v4.2 |
| `EnemyDataSO.cs` | stateTransitionDelay 필드 추가 | v2.2 |
| `EnemyKnight.cs` | _backLock 단일 → _locks List<LockComponent> 변환 | v1.4 |

**① ChargeAttack 히트박스 플립**

```
방향 전환 시 FlipAttackHitboxes(dir)
  → EnemyKnightAttack.FlipHitbox(dir)       근접 히트박스 (기존)
  → EnemyKnightChargeAttack.FlipHitbox(dir)  돌진 히트박스 (추가)
      _originalChargeHitboxLocalX × dir → localPosition.x 갱신
      _chargeHitbox == null 이면 무시
```

**② 상태전환 딜레이**

```
EnemyDataSO.stateTransitionDelay (기본값 0.4초)
  Chase → Attack : 딜레이 후 전환 (공격 결정이 느리게)
  Attack → Chase : 딜레이 후 전환 (공격 후 잠깐 멈춤)
  딜레이 중      : _isTransitioning = true → 추가 전환 요청 무시
  Patrol ↔ Idle  : 딜레이 미적용 (즉각 전환 유지)
```

| 값 | 느낌 |
|---|---|
| `0.0` | 즉각 반응 (기존 동작) |
| `0.3` | 약간 둔함 |
| `0.4` | 기본값 — 적당히 둔함 |
| `0.8` | 매우 느린 반응 |

**③ EnemyKnight 자물쇠 List 확장**

```
기존: LockComponent _backLock          (단일)
변경: List<LockComponent> _locks       (리스트)
      int _unlockedCount               (해제된 수 추적)
      bool _isAllLocksUnlocked         (전부 해제 여부)
```

**후면 공격 처리 변경**
```
기존: _backLock.TakeDamage(info)

변경: GetFirstLockedLock()
        _locks 순서 순회 → IsUnlocked == false 인 첫 번째에 TakeDamage
```

**해제 조건 — CheckAllUnlocked()**
```csharp
// 현재: 전부 해제
private bool CheckAllUnlocked()
    => _unlockedCount >= _locks.Count;

// 추후 확장: 이 메서드만 수정
// → 일부 해제: _unlockedCount >= requiredCount
// → 속성 조건: 특정 타입 자물쇠만 체크
```

**Inspector 변경**

| 변경 전 | 변경 후 |
|---|---|
| `_backLock` (단일 슬롯) | `_locks` (리스트 — 드래그 추가) |
| `_isLockUnlocked` (bool) | `_unlockedCount` + `_isAllLocksUnlocked` |

**추후 속성 자물쇠 추가 시**
Inspector에서 `_locks` 리스트에 새 `LockComponent` 추가만 하면 됩니다. `EnemyKnight` 코드 수정 불필요.