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

## 미결 항목

| 항목 | 상태 | 메모 |
|---|---|---|
| Player.controller 에디터 수정 | ✅ 완료 | v0.7 가이드 |
| Attack 클립 Loop Time OFF | ✅ 완료 | v0.8 |
| SealProjectile Prefab 생성 | ✅ 완료 | Assets/KEY/Prefabs/ |
| EnemySealComponent 적 부착 | ✅ 완료 | Enemy_Knight 우선 |
| SealData 에셋 생성 | ✅ 완료 | Assets/KEY/DataSO/Seals/ |
| LockComponent 단일 → List 변환 | ✅ 완료 | v0.17 EnemyKnight v1.4 |
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