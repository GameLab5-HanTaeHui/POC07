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

**작업 내용**

기존 `PlayerMovement` 네임스페이스 이동 패키지를 `KEY` 네임스페이스로 이전.

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `MovementSettings.cs` | 이동 수치 ScriptableObject | v1.0 |
| `MovementAnimator.cs` | Animator 파라미터 동기화 | v1.1 |

**Animator Controller 완성 스테이트**

PlayerIdle / PlayerMove / PlayerJump / PlayerFall / PlayerDash / PlayerDoubleJump

**알려진 이슈**

- `MovementSettings.GroundLayer` 미설정 시 착지 감지 불가 → Awake 경고 추가됨

---

### v0.2 — 입력 통합 + 무기 시스템 1차

**작업 내용**

1. `MovementInput` + `WeaponInput` → `InputManager` 병합
2. 플레이어 이동 컴포넌트 `KEY` 네임스페이스 적용
3. 녹슨 열쇠 무기 시스템 1차 구현

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `InputManager.cs` | 입력 통합 관리 (신규) | v1.0 |
| `PlayerMover.cs` | 이동 물리 | v1.3 |
| `PlayerMovementFacade.cs` | 외부 진입점 | v1.1 |
| `IDamageable.cs` | 피격 인터페이스 (신규) | v1.0 |
| `PlayerWeaponBase.cs` | 무기 추상 베이스 (신규) | v1.0 |
| `PlayerWeaponHitboxManager.cs` | 히트박스 관리 (신규) | v1.0 |
| `RustyKeyWeapon.cs` | 녹슨 열쇠 구현체 (신규) | v1.0 |

**구조 결정 사항**

- `InputManager` 싱글턴 — DontDestroyOnLoad 금지, 씬마다 새로 생성
- 지상/공중 판별: `PlayerMovementFacade.Instance.IsGrounded` 참조
- 히트박스 중복 피격 방지: `HashSet<Collider2D> _hitTargets`
- 콤보 윈도우: `_comboWindowTimer` 만료 시 `ComboReset()`

**콤보 수치 기본값**

| 단계 | 모션 | 데미지 배율 |
|---|---|---|
| Combo1 | 가로 휘두르기 | 1.0x |
| Combo2 | 대각선 내리기 | 1.2x |
| Combo3 | 앞으로 찌르기 | 1.5x (피니셔) |
| AirAttack | 아래 내리찍기 | 1.3x |

---

### v0.3 — 열쇠 데이터 구조 + 무기 교체 시스템

**작업 내용**

1. 열쇠 타입 enum, 데이터 SO, 인벤토리 SO 설계 및 구현
2. `PlayerWeaponController` — 열쇠 교체 핵심 컨트롤러 구현
3. `PlayerWeaponBase` v1.1 — `SetKeyData()` 추가, KeyDataSO 수치 연동
4. `RustyKeyWeapon` v1.1 — 하드코딩 수치 제거, KeyDataSO 에서 읽도록 전면 수정
5. `KeyInventorySO` → `KeyInventoryDataSO` 명칭 변경
6. `PlayerWeaponController` v1.1 — `WeaponEntry.weapon` 타입을 `MonoBehaviour`로 변경, 런타임 캐스팅으로 해결

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyType.cs` | 열쇠 타입 enum | v1.0 |
| `KeyDataSO.cs` | 열쇠 데이터 SO | v1.0 |
| `KeyInventoryDataSO.cs` | 보유 열쇠 목록 SO | v1.0 |
| `PlayerWeaponController.cs` | 열쇠 교체 컨트롤러 | v1.1 |
| `PlayerWeaponBase.cs` | 무기 베이스 | v1.1 |
| `RustyKeyWeapon.cs` | 녹슨 열쇠 구현체 | v1.1 |

**구조 결정 사항**

- 열쇠 수치 전부 `KeyDataSO` 집중 — 무기 컴포넌트 Inspector 수치 필드 제거
- `WeaponEntry.weapon` = `MonoBehaviour` 타입 → Inspector 에서 구현체 드래그 연결 가능
- 런타임 `as PlayerWeaponBase` 캐스팅 — 미상속 컴포넌트 연결 시 LogError 출력
- 열쇠 교체 흐름: `KeyInventoryDataSO.EquipKey()` → `OnKeyEquipped` → `PlayerWeaponController` 처리
- 무기 컴포넌트 전부 비활성 대기, 장착 시에만 `enabled = true`
- `AnimatorOverrideController` 스왑 자리 확보 — 스프라이트 완성 후 활성화

**애니메이션 결정 사항 (보류)**

- 방식: `Player.controller` Base Layer(이동) + Attack Layer(공격) + `AnimatorOverrideController`
- 달리면서 공격, 공중 공격 모두 자연스럽게 처리 가능
- **현재 보류** — 스프라이트 없음. 완성 후 `PlayerWeaponAnimator.cs` 작성

**Inspector 연결 방법**

```
PlayerWeaponController
└── Weapon Entries
      [0] keyType = Rusty  /  weapon = RustyKeyWeapon 컴포넌트 드래그
      [1] keyType = Hook   /  weapon = HookKeyWeapon 컴포넌트 드래그 (추후)
```

**유니티 적용 체크리스트**

- [ ] 신규 파일 6개 import
- [ ] `RustyKeyData.asset` 생성 (Create → KEY → Key Data)
- [ ] `KeyInventory.asset` 생성 (Create → KEY → Key Inventory)
- [ ] `KeyInventory.asset._defaultKeys[0]` = RustyKeyData.asset
- [ ] Weapon 오브젝트에 `PlayerWeaponController` 추가
- [ ] `_inventory` = KeyInventory.asset 연결
- [ ] `_weaponEntries[0]` keyType = Rusty, weapon = RustyKeyWeapon 컴포넌트

---

### v0.4 — 적 시스템 (진행 중)

**다음 작업 예정**

- [ ] 적 시스템 기획 확정
- [ ] `EnemyBase.cs` — IDamageable 구현, 체력/상태 관리
- [ ] `EnemyAI.cs` — 기본 행동 패턴
- [ ] `LockComponent.cs` — 자물쇠 구조 (해제 조건 판별)

---

## 미결 항목

| 항목 | 상태 | 메모 |
|---|---|---|
| 자물쇠 해제 조건 기획 | 🔲 미정 | 적 AI 구현 후 결정 |
| 무기 Animator Controller | 🔲 보류 | 스프라이트 완성 후 |
| `PlayerWeaponAnimator.cs` | 🔲 보류 | 스프라이트 완성 후 |
| `EnemyBase.cs` | 🔲 미착수 | v0.4 예정 |
| `EnemyAI.cs` | 🔲 미착수 | v0.4 예정 |
| `LockComponent.cs` | 🔲 미착수 | v0.4 예정 |

---

### v0.4 — 더미 적 시스템

**작업 내용**

1. `EnemyDataSO` — 적 수치 ScriptableObject
2. `EnemyBase` — 적 추상 베이스, IDamageable 구현 (체력/넉백/iFrame/피격 플래시)
3. `LockComponent` — 자물쇠 컴포넌트 (피격 횟수 누적 → 해제 이벤트)
4. `EnemyDummy` — 자물쇠 없는 정지 더미
5. `EnemyDummyLocked` — 자물쇠 있는 정지 더미

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `EnemyDataSO.cs` | 적 수치 SO (신규) | v1.0 |
| `EnemyBase.cs` | 적 추상 베이스 (신규) | v1.0 |
| `LockComponent.cs` | 자물쇠 컴포넌트 (신규) | v1.0 |
| `EnemyDummy.cs` | 자물쇠 없는 더미 (신규) | v1.0 |
| `EnemyDummyLocked.cs` | 자물쇠 있는 더미 (신규) | v1.0 |

**구조 결정 사항**

- 더미 사망 없음 — 체력 최솟값 1 고정
- 넉백: `AddForce(Impulse)` — 방향은 `DamageInfo.Direction`
- iFrame: 코루틴 기반, 피격 플래시(빨간 깜빡임) 동반
- `EnemyDummyLocked` 보호막 구조
  - 자물쇠 해제 전: 본체 TakeDamage → 데미지 무시 + 파란 플래시
  - 자물쇠 해제 후: 본체 TakeDamage → 정상 처리 + 빨간 스프라이트로 약점 표시
- `LockComponent` 해제 조건: 현재 피격 횟수 기반. 추후 방향/공격유형 조건 확장 가능

**유니티 적용 체크리스트**

Enemy_Dummy (자물쇠 없음)
- [ ] 빈 오브젝트 생성 → `EnemyDummy`, `Rigidbody2D`, `CapsuleCollider2D`, `SpriteRenderer` 부착
- [ ] `DummyEnemyData.asset` 생성 (Create → KEY → Enemy Data)
- [ ] `EnemyDummy._settings` = DummyEnemyData.asset
- [ ] `Rigidbody2D` Constraints → FreezeRotation Z 체크
- [ ] Layer → Enemy

Enemy_DummyLocked (자물쇠 있음)
- [ ] 위와 동일 + `EnemyDummyLocked` 부착
- [ ] 자식 오브젝트 `Lock` 생성 → `LockComponent`, `SpriteRenderer`, `BoxCollider2D`(isTrigger=ON) 부착
- [ ] `BoxCollider2D` Layer → PlayerHitbox 감지 설정
- [ ] `EnemyDummyLocked._lockComponent` = Lock 오브젝트의 LockComponent

**다음 작업 예정**

- [ ] 기사형 적 (EnemyKnight) 기획 확정 및 구현

---

### v0.4.1 — 더미 적 넉백 버그픽스

**문제**

- `gravityScale=0` + `AddForce(Impulse)` 조합에서 마찰/감속 없음
  → velocity 누적으로 피격 후 계속 날아가는 버그
- `gravityScale=1` 변경 시 중력까지 더해져 더 심해짐

**원인**

- `AddForce` 는 물리 엔진 내부 적분에 의존
- `gravityScale=0` + `FreezePositionY` 환경에서는 Linear Drag 도 사실상 무의미
- velocity 가 누적되어 멈추는 시점이 없음

**해결**

- `AddForce` 제거 → `KnockbackRoutine` 코루틴으로 교체
- `velocity.x = direction.x * knockbackForce` 로 초기 속도 직접 설정
- `WaitForFixedUpdate` 루프에서 `velocity.x *= knockbackDecay` 로 매 프레임 감속
- `velocity.magnitude < 0.1f` or 최대 0.5초 경과 시 `velocity = zero` 로 완전 정지

**변경 파일**

| 파일 | 변경 내용 | 버전 |
|---|---|---|
| `EnemyDataSO.cs` | `knockbackDecay` 필드 추가 | v1.1 |
| `EnemyBase.cs` | `AddForce` → `KnockbackRoutine` 코루틴으로 교체 | v1.1 |
| `EnemyDummy.cs` | `FreezePositionY` 추가 | v1.1 |
| `EnemyDummyLocked.cs` | `FreezePositionY` 추가 | v1.1 |

**Rigidbody2D 설정 (더미 공통)**

```
gravityScale : 0
Constraints  : FreezePositionY + FreezeRotation Z
Linear Drag  : 0 (코드에서 직접 제어하므로 무관)
```

**knockbackDecay 튜닝 가이드**

| 값 | 느낌 |
|---|---|
| 0.7 | 짧고 강하게 밀림 |
| 0.8 | 자연스러운 중간 (기본값) |
| 0.9 | 느리게 미끄러지듯 정지 |

---

### v0.5 — 기사형 적 시스템

**작업 내용**

1. 공용 계층 3종 (`EnemySensor`, `EnemyAI`, `EnemyAttackBase`) 설계 및 구현
2. 기사형 전용 구현체 3종 (`KnightDataSO`, `KnightAI`, `EnemyKnightAttack`, `EnemyKnight`)
3. 기존 `EnemyBase`, `EnemyDataSO` 연계

**완성 파일**

| 파일 | 계층 | 역할 | 버전 |
|---|---|---|---|
| `EnemySensor.cs` | 공용 | Raycast×3 + OverlapCircle×2 감지 | v1.0 |
| `EnemyAI.cs` | 공용 추상 | Patrol/Idle/Chase/Attack 상태머신 | v1.0 |
| `EnemyAttackBase.cs` | 공용 추상 | 공격 쿨타임 + 히트박스 공통 처리 | v1.0 |
| `KnightDataSO.cs` | Knight | EnemyDataSO 상속, 이동/감지/공격 수치 | v1.0 |
| `KnightAI.cs` | Knight | EnemyAI 상속, 순찰/추격 이동 구현 | v1.0 |
| `EnemyKnightAttack.cs` | Knight | EnemyAttackBase 상속, 내려치기 단타 | v1.0 |
| `EnemyKnight.cs` | Knight | EnemyBase 상속, 방패+자물쇠 피격 판단 | v1.0 |

**구조 결정 사항**

- 데이터 흐름: `KnightAI.Start()` → `SetData()` → `EnemySensor` + `EnemyKnightAttack` 에 주입
- 정면/후면 판단: `DamageInfo.Direction` × `KnightAI.FacingDirection` dot product
  - 음수 = 정면 공격 = 방패 막힘
  - 양수 = 후면 공격 = 자물쇠 피격
- 공격 완료 → `EnemyAttackBase.OnAttackFinished` 이벤트 → `EnemyAI` Chase 복귀
- 낭떠러지 감지: 발 앞 오프셋에서 하향 Ray, 지면 없으면 방향 반전

**유니티 적용 체크리스트**

- [ ] 신규 파일 7개 import
- [ ] `KnightData.asset` 생성 (Create → KEY → Knight Data)
- [ ] `Enemy_Knight` 오브젝트 구성:
  - `EnemyKnight`, `KnightAI`, `EnemyKnightAttack`, `EnemySensor` 부착
  - `Rigidbody2D` (gravityScale=1, FreezeRotation Z)
  - `CapsuleCollider2D`
- [ ] `Lock_Back` 자식 생성 → `LockComponent`, `BoxCollider2D`(isTrigger=ON)
- [ ] `AttackHitbox` 자식 생성 → `BoxCollider2D`(isTrigger=ON)
- [ ] `KnightAI._knightData` = KnightData.asset
- [ ] `KnightDataSO.playerLayer` = Player 레이어
- [ ] `KnightDataSO.groundLayer` = Ground 레이어

**다음 작업 예정**

- [ ] MD 파일 Hierarchy 업데이트
- [ ] 테스트 씬 구성 후 동작 확인

---

### v0.5.1 — 적 AI 구조 개선 (단일 컴포넌트 통합)

**문제**

- `KnightAI` + `EnemyAI` 추상 클래스 → 적마다 AI 컴포넌트 2개 필요
- 적 10종이면 AI 파일 10개 → 유지보수 불가
- `KnightDataSO`가 `EnemyDataSO`와 기능 중복

**해결**

- `KnightAI` 제거 → `EnemyAI` 단일 컴포넌트로 통합
- `KnightDataSO` 제거 → `EnemyDataSO`에 `EnemyType` enum + 모든 수치 통합
- `EnemyAI` 내부 `switch(enemyType)` 분기로 타입별 행동 처리
- 새 적 추가 시 `EnemyType` enum 항목 추가 + switch 케이스만 추가

**변경/삭제 파일**

| 파일 | 변경 내용 |
|---|---|
| `EnemyDataSO.cs` | v2.0 — EnemyType enum + 전 타입 수치 통합 |
| `EnemyAI.cs` | v2.0 — 추상 클래스→일반 클래스, switch 분기 |
| `EnemySensor.cs` | KnightDataSO → EnemyDataSO 참조 교체 |
| `EnemyKnightAttack.cs` | v1.1 — KnightDataSO → EnemyDataSO 참조 교체 |
| `EnemyKnight.cs` | v1.1 — KnightAI → EnemyAI 참조 교체 |
| ~~`KnightAI.cs`~~ | 삭제 — EnemyAI 로 통합 |
| ~~`KnightDataSO.cs`~~ | 삭제 — EnemyDataSO 로 통합 |

**오브젝트에 붙는 컴포넌트 (개선 후)**

```
Enemy_Knight
├── [EnemyKnight]     피격 로직 (EnemyBase 상속)
├── [EnemyAI]         AI 상태머신 — enemyType=Knight 설정
├── [EnemyKnightAttack]    공격 구현체 (EnemyAttackBase 상속)
├── [EnemySensor]     감지 전담
├── [Rigidbody2D]
└── ...
```

**새 적 추가 시 작업량 (개선 후)**

```
1. EnemyType 에 항목 추가
2. EnemyAI.OnPatrolMove / OnChaseMove / OnEnterAttack switch 케이스 추가
3. EnemyBase 상속 피격 클래스 작성
4. EnemyAttackBase 상속 공격 클래스 작성 (모션이 다른 경우)
→ EnemyAI 컴포넌트 자체는 교체 없음
```

---

### v0.6 — 무기 스윙 이동 + PlayerWeaponAnimator

**작업 내용**

1. `KeyDataSO` v1.1 — 스윙 이동 수치 섹션 추가 (`swingDistance` / `swingDuration` / `returnDuration` / `airSwingDistance`)
2. `PlayerWeaponMover.cs` — Weapon 오브젝트 DOTween 스윙 이동 전담
3. `PlayerWeaponAnimator.cs` — 무기 이벤트 구독, Animator Trigger 발행 + PlayerWeaponMover 연동
4. `PlayerWeaponController.cs` — 열쇠 교체 시 PlayerWeaponAnimator.SetWeapon() / PlayerWeaponMover.SetKeyData() 연동

**완성 파일**

| 파일 | 역할 | 버전 |
|---|---|---|
| `KeyDataSO.cs` | 스윙 수치 추가 | v1.1 |
| `PlayerWeaponMover.cs` | Weapon 오브젝트 스윙 이동 (신규) | v1.0 |
| `PlayerWeaponAnimator.cs` | Animator Trigger + PlayerWeaponMover 연동 (신규) | v1.0 |
| `PlayerWeaponController.cs` | PlayerWeaponAnimator / PlayerWeaponMover 연동 추가 | v1.2 |

**스윙 이동 흐름**

```
RustyKeyWeapon.OnCombo1Started
  → PlayerWeaponAnimator.HandleCombo1()
      → Animator.SetTrigger("AttackCombo1")   (Attack Layer — 스프라이트 후 클립 연결)
      → PlayerWeaponMover.PlaySwing(AttackType.Combo1)
          → DOLocalMove(앞으로 swingDistance, swingDuration, Ease.OutQuart)
          → WaitForSeconds(hitboxDuration - swingDuration) 유지
          → DOLocalMove(원점, returnDuration, Ease.InQuart)
```

**콤보별 이동 방향**

| 공격 | 이동 방향 | 거리 |
|---|---|---|
| Combo1 / Combo2 / Combo3 | FacingDirection(X) 앞으로 | swingDistance |
| AirAttack | Y 음수(아래) + X 소량 | airSwingDistance |

**KeyDataSO 스윙 기본값**

```
swingDistance    : 0.5  (앞으로 이동 거리)
swingDuration    : 0.08 (앞으로 뻗는 시간)
returnDuration   : 0.15 (복귀 시간)
airSwingDistance : 0.4  (공중 아래 이동 거리)
```

**Attack Layer 파라미터 (Player.controller 에 추가 필요)**

```
AttackCombo1 (Trigger)
AttackCombo2 (Trigger)
AttackCombo3 (Trigger)
AirAttack    (Trigger)
스테이트: Empty → Combo1/2/3/AirAttack → Empty (ExitTime)
클립: 스프라이트 완성 후 연결
```

**유니티 적용 체크리스트**

- [ ] `KeyDataSO.cs` / `PlayerWeaponMover.cs` / `PlayerWeaponAnimator.cs` / `PlayerWeaponController.cs` 교체
- [ ] Player 루트에 `PlayerWeaponAnimator` 컴포넌트 추가
- [ ] Weapon 오브젝트에 `PlayerWeaponMover` 컴포넌트 추가
- [ ] `Player.controller` Attack Layer 추가:
  - 파라미터 4개 (AttackCombo1 / AttackCombo2 / AttackCombo3 / AirAttack, 모두 Trigger)
  - Empty 스테이트 + 각 공격 스테이트 + ExitTime 전환
- [ ] `RustyKeyData.asset` 스윙 수치 설정

**다음 작업 예정**

- [ ] Player.controller Attack Layer 구조 완성 가이드
- [ ] 스프라이트 완성 후 클립 연결 + AnimatorOverrideController 세팅