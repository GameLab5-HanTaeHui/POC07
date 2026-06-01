---

### v0.29 — Phase1 전투 구조 전면 재설계 + 보스 시스템 버그 수정

**배경**
기존 Phase1 구조는 "플레이어가 팔 자물쇠를 공격으로 해제" 방식이었으나,
기획 재검토 결과 반대 방향으로 전면 수정.
Phase1 핵심 구조: 팔 해제 상태 시작 → 플레이어가 처형으로 잠금 → 코어 활성 → 딜타임

---

#### Phase1 기획 구조 변경

**기존 구조 (폐기)**
```
팔 잠금 상태 시작
→ 플레이어가 자물쇠를 공격해 해제
→ 팔 해제 = 약점 노출
→ 양팔 해제 시 코어 활성
```

**수정된 구조 (v0.29 확정)**
```
팔 해제 상태 시작 (붉은색)
→ 플레이어가 A키 홀드 처형으로 팔을 봉인(ReLock)
→ 양팔 봉인 시 코어 활성 (노출)
→ 플레이어가 코어 A키 홀드 처형 → 딜타임 진입
→ 딜타임 종료 → 코어 봉인 + 양팔 강제 해제 + 충격파
→ 반복
```

**팔 약점 노출 제거**
- 팔 해제 상태 = 약점 노출 아님 (직접 피격 불가)
- HP 감소는 딜타임 중 코어 공격으로만 가능

---

#### 코드 수정 내역

**`EnemyBossBase.cs` v1.0 (신규)**
- EnemyBase → EnemyBossBase 분리
- EnemyDataSO 억지 상속 해소
- abstract 프로퍼티 4개: BossMaxHp, BossKnockbackForce, BossKnockbackDecay, BossIFrameDuration
- _isPhaseInvincible 필드 이전

**`BossKnight.cs` v1.2**
- EnemyBossBase 상속으로 전환
- TakeDamage: 딜타임 중 코어 피격만 HP 감소 허용
  (팔/방패 해제 여부 무관하게 딜타임 외 피격 전부 무시)
- IsAllLocksCleared(): Core 타입 제외 + Phase2/3 확장용 보존

**`BossPartComponent.cs` v1.3**
- SpeedMultiplier 방향 수정
  - 봉인(Locked) → ApplySpeedMultiplier (패턴 느림)
  - 해제(Unlocked) → ResetSpeedMultiplier (패턴 빠름, 위험 증가)
  - 재잠금(ReLock) → ApplySpeedMultiplier (패턴 느림 복귀)
- Initialize(): 팔 타입(ArmL/ArmR)은 해제 상태로 시작, 나머지는 잠금 상태
- 색상 피드백 추가
  - 잠금(Locked) = 파란색 (0.3, 0.5, 1.0)
  - 해제(Unlocked) = 붉은색 (1.0, 0.3, 0.3)
  - _partSpriteRenderer 자식 자동 탐색
  - RefreshColor() 메서드 추가

**`BossCoreLock.cs` v1.2**
- CheckCoreActivation()에서 IsGroggy 조건 제거
  → 양팔 봉인 즉시 코어 활성 (그로기 무관)
- ExitDilTime() 종료 순서 수정
  1. DeactivateCore()
  2. _armL.ForceUnlock()
  3. _armR.ForceUnlock()
  4. TriggerShockwave()
- RegisterArmParts(): OnPartReLocked + OnPartUnlocked 양쪽 구독

**`BossExecutionHandler.cs` v1.1**
- 처형 흐름 재설계: A키 홀드 시작 즉시 이동 시작
- 이동 방식: Rigidbody2D.MovePosition (WaitForFixedUpdate 단위)
- 이동 중 A키 놓으면 처형 중단
- 코어 처형 완료 시 _coreLock.EnterDilTime() 호출 추가 ← 딜타임 진입 연결
- 코어 활성 상태(IsCoreActive)에서만 코어 처형 가능

---

#### Prefab 수정 내역 (v0.29)

**직접 수정 완료 항목**
```
Arm_L / Arm_R
  Lock_ArmL / Lock_ArmR 자식의 LockComponent 제거
  (일반 무기 피격으로 팔 자물쇠 해제되는 문제 차단)
  처형 메커니즘으로만 팔 상태 변경

Core._activePhases
  [Phase1, Phase2, Phase3] 으로 수정
  (Phase1에서 코어 활성화 가능해야 딜타임 진입 가능)

Core GameObject
  기본 SetActive = false 로 설정
  (시작부터 코어가 노출되는 문제 수정)

BossKnight.IsAllLocksCleared()
  Core 타입 제외 처리 추가
```

---

#### 확인된 버그 및 원인 분석

| 버그 | 원인 | 수정 |
|---|---|---|
| 시작부터 코어 노출 | Core GameObject Active = true | Prefab에서 SetActive = false |
| 양팔 봉인해도 코어 미활성 | CheckCoreActivation IsGroggy 조건 | IsGroggy 조건 제거 |
| SpeedMultiplier 방향 반대 | HandleLockUnlocked에서 Apply, ReLock에서 Reset | 방향 전환 |
| 딜페이즈 미시작 | ExecuteRoutine에서 EnterDilTime 미호출 | 코어 처형 완료 시 EnterDilTime() 추가 |
| Arm LockComponent 자동 해제 | 일반 무기 피격 → 피격 횟수 누적 → 자동 해제 | Prefab에서 LockComponent 제거 |

---

#### 파일 버전 스냅샷 (v0.29 기준)

| 파일 | 버전 | 변경 내용 |
|---|---|---|
| `EnemyBossBase.cs` | v1.0 | 신규 — 보스 전용 베이스 |
| `BossKnight.cs` | v1.2 | EnemyBossBase 상속. 딜타임 전용 TakeDamage |
| `BossPartComponent.cs` | v1.3 | SpeedMultiplier 방향 수정 + 색상 피드백 + 팔 해제 시작 |
| `BossCoreLock.cs` | v1.2 | IsGroggy 제거 + 딜타임 종료 시 양팔 해제 |
| `BossExecutionHandler.cs` | v1.1 | 처형 흐름 재설계 + EnterDilTime 연결 |