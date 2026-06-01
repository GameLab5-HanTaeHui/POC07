// ============================================================
// TestBossExecution.cs  v1.1
// 테스트 보스 A키 홀드 처형 입력 처리 컴포넌트
//
// [v1.1 변경 — 공격/처형 충돌 수정 + 처형 후 딜레이 추가]
//
//   ① 공격/처형 충돌 수정
//       문제: A키 단타 (performed) 와 A키 홀드 (IsAttackHeld) 구분 없음
//             처형 감지 루프가 A키 누르는 즉시 반응
//             → 처형 시작 프레임에 OnAttack 이벤트도 동시 발행
//             → 콤보 공격 + 처형 동시 실행
//
//       해결:
//         _holdThreshold  : A키를 이 시간 이상 눌러야 처형 판정 시작
//                           단타 공격(performed = 즉시)과 홀드 처형 분리
//         BlockAttack()   : 처형 이동 시작 직전 PlayerWeaponBase.BlockAttack() 호출
//                           처형 중 공격 이벤트 원천 차단
//         UnblockAttack() : 처형 완료 / 중단 시 해제
//
//   ② 처형 완료 후 딜레이 추가
//       문제: ExecuteRoutine 완료 → 다음 프레임 DetectExecutionInput 루프 재진입
//             A키 홀드 유지 시 즉시 다음 처형 자동 발동
//
//       해결:
//         _executionCooldown  : 처형 완료 후 다음 처형 감지까지 대기 시간 (DataSO)
//         _mustReleaseKey     : 처형 완료 후 A키를 한 번 뗀 것을 확인해야 재감지 허용
//                               A키를 계속 누른 채로는 재발동 불가
//
//   ③ PlayerWeaponBase 탐색 자동화
//       Initialize() 에서 FindObjectsByType 으로 PlayerWeaponBase 자동 탐색
//       처형 진입/해제 시 BlockAttack / UnblockAttack 호출
//
// [처형 흐름 — v1.1]
//   그로기 진입 → OnGroggyEnter() → 감지 루프 시작
//   A키 홀드 시작
//     → _holdTimer 누적 (매 프레임 +deltaTime)
//     → A키 뗌 → _holdTimer 리셋 (단타 → 처형 불발)
//     → _holdThreshold 이상 홀드 + 부위 범위 내
//         → BlockAttack() (공격 차단)
//         → 이동 시작 (Rigidbody2D.MovePosition)
//         → 도착 → OnExecutionCompleted 발행 → 처형 실행
//         → _mustReleaseKey = true (A키 뗌 대기)
//         → _cooldownTimer = _executionCooldown (쿨다운 시작)
//         → UnblockAttack()
//   쿨다운 + A키 뗌 확인 후 → 재감지 허용
//
// [DataSO 추가 항목]
//   executionHoldThreshold  : 처형 판정 홀드 최소 시간 (권장 0.3~0.8초)
//   executionCooldown       : 처형 완료 후 재발동 대기 시간 (권장 0.5~1.0초)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 A키 홀드 처형 입력 처리 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [처형 가능 조건]
    ///   그로기 상태(_isGroggyActive == true)
    ///   + 부위 감지 범위 내
    ///   + A키 홀드 (executionHoldThreshold 이상 — 단타 공격과 분리)
    ///   + 쿨다운 완료 (_cooldownTimer <= 0)
    ///   + A키 재누름 확인 (_mustReleaseKey == false)
    ///
    /// [공격 차단 흐름]
    ///   처형 이동 시작 → BlockAttack()   → 공격 이벤트 차단
    ///   처형 완료/중단 → UnblockAttack() → 공격 이벤트 복구
    ///
    /// [처형 후 재발동 방지]
    ///   처형 완료 → _mustReleaseKey = true
    ///   A키 뗌 감지 → _mustReleaseKey = false (재감지 허용)
    ///   쿨다운(_executionCooldown) 경과 후에도 재감지 허용
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossExecution : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 내부 참조 (TestBossCore 에서 Initialize() 로 주입)
        // ──────────────────────────────────────────

        private TestBossCore _core;
        private TestBossArmPart _armL;
        private TestBossArmPart _armR;
        private TestBossDataSO _data;

        // ──────────────────────────────────────────
        // 플레이어 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// 플레이어 Transform.
        /// Initialize() 에서 PlayerMover 탐색.
        /// </summary>
        private Transform _playerTransform;

        /// <summary>
        /// 플레이어 Rigidbody2D.
        /// 처형 이동 시 MovePosition() 사용.
        /// </summary>
        private Rigidbody2D _playerRigid2D;

        /// <summary>
        /// 플레이어 무기 베이스.
        /// 처형 진입 시 BlockAttack() / 완료 시 UnblockAttack() 호출.
        /// </summary>
        private PlayerWeaponBase _playerWeapon;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 그로기 감지 활성 여부. </summary>
        private bool _isGroggyActive;

        /// <summary> 처형 실행 중 여부. 중복 실행 방지. </summary>
        private bool _isExecuting;

        /// <summary>
        /// A키 홀드 누적 시간.
        /// executionHoldThreshold 이상이어야 처형 판정.
        /// 단타 공격(performed 즉시)과 홀드 처형 분리.
        /// </summary>
        private float _holdTimer;

        /// <summary>
        /// 처형 완료 후 재발동 쿨다운 타이머.
        /// 0 이하가 되어야 재감지 허용.
        /// </summary>
        private float _cooldownTimer;

        /// <summary>
        /// 처형 완료 후 A키 재누름 확인 플래그.
        /// true = A키를 한 번 뗐다가 다시 눌러야 재처형 허용.
        /// A키 홀드 유지 중 연속 처형 방지.
        /// </summary>
        private bool _mustReleaseKey;

        // ──────────────────────────────────────────
        // 코루틴 핸들
        // ──────────────────────────────────────────

        private Coroutine _detectCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 처형 완료 시 발행.
        /// TestBossCore 에서 구독하여 ReLock() 또는 EnterDilTime() 호출.
        /// 파라미터: 처형된 부위 타입.
        /// </summary>
        public event Action<TestBossPartType> OnExecutionCompleted;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Update()
        {
            // 쿨다운 타이머 감소
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            // _mustReleaseKey 해제 조건:
            //   A키를 뗀 것을 감지하면 재감지 허용
            if (_mustReleaseKey && InputManager.Instance != null
                && !InputManager.Instance.IsAttackHeld)
            {
                _mustReleaseKey = false;
            }

            // 감지 루프 외부에서 A키 홀드 타이머 관리
            // 처형 감지 루프가 활성이 아닐 때도 누적 방지를 위해 리셋
            if (!_isGroggyActive || _isExecuting)
                _holdTimer = 0f;
        }

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화. TestBossCore.Start() 에서 호출.
        /// 참조 주입 및 플레이어 탐색.
        /// </summary>
        /// <param name="core">TestBossCore 참조.</param>
        /// <param name="armL">왼팔 TestBossArmPart.</param>
        /// <param name="armR">오른팔 TestBossArmPart.</param>
        public void Initialize(TestBossCore core, TestBossArmPart armL, TestBossArmPart armR)
        {
            _core = core;
            _armL = armL;
            _armR = armR;
            _data = core.Data;

            // 플레이어 탐색 — PlayerMover
            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
            {
                _playerTransform = players[0].transform;
                _playerRigid2D = players[0].GetComponent<Rigidbody2D>();
            }
            else
            {
                Debug.LogWarning("[TestBossExecution] PlayerMover 를 씬에서 찾을 수 없습니다.");
            }

            // 플레이어 무기 탐색 — PlayerWeaponBase (공격 차단용)
            var weapons = FindObjectsByType<PlayerWeaponBase>(FindObjectsSortMode.None);
            if (weapons.Length > 0)
                _playerWeapon = weapons[0];
            else
                Debug.LogWarning("[TestBossExecution] PlayerWeaponBase 를 씬에서 찾을 수 없습니다. 공격 차단 불가.");

            Debug.Log("[TestBossExecution] 초기화 완료.");
        }

        // ══════════════════════════════════════════════════════
        // 그로기 이벤트 수신 (TestBossCore 에서 구독)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 그로기 진입 시 TestBossCore.OnGroggyEnter 에서 호출.
        /// 처형 입력 감지 루프 시작.
        /// 홀드 타이머 / 쿨다운 / 키 해제 플래그 초기화.
        /// </summary>
        public void OnGroggyEnter()
        {
            _isGroggyActive = true;
            _holdTimer = 0f;
            // 쿨다운과 mustReleaseKey 는 이전 루프에서 이미 처리됨 — 유지

            if (_detectCoroutine != null) StopCoroutine(_detectCoroutine);
            _detectCoroutine = StartCoroutine(DetectExecutionInput());

            Debug.Log("[TestBossExecution] 처형 감지 시작");
        }

        /// <summary>
        /// 그로기 종료 시 TestBossCore.OnGroggyExit 에서 호출.
        /// 처형 감지 중단 + 처형 중이면 강제 중단.
        /// </summary>
        public void OnGroggyExit()
        {
            _isGroggyActive = false;
            _holdTimer = 0f;

            if (_detectCoroutine != null)
            {
                StopCoroutine(_detectCoroutine);
                _detectCoroutine = null;
            }

            // 처형 중이면 강제 중단
            if (_isExecuting)
                InterruptExecution();

            Debug.Log("[TestBossExecution] 처형 감지 종료");
        }

        // ══════════════════════════════════════════════════════
        // 처형 감지 루프
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 처형 입력 감지 코루틴.
        /// 그로기 상태 동안 매 프레임:
        ///   1. 쿨다운 / 키 해제 대기 체크
        ///   2. A키 홀드 시간 누적
        ///   3. 홀드 임계값 초과 + 부위 범위 내 → 처형 실행
        /// </summary>
        private IEnumerator DetectExecutionInput()
        {
            while (_isGroggyActive)
            {
                // 처형 실행 중이면 대기
                if (_isExecuting)
                {
                    _holdTimer = 0f;
                    yield return null;
                    continue;
                }

                // 쿨다운 대기
                if (_cooldownTimer > 0f)
                {
                    _holdTimer = 0f;
                    yield return null;
                    continue;
                }

                // 처형 완료 후 A키 재누름 대기
                if (_mustReleaseKey)
                {
                    _holdTimer = 0f;
                    yield return null;
                    continue;
                }

                // A키 홀드 타이머 누적
                if (InputManager.Instance != null && InputManager.Instance.IsAttackHeld)
                {
                    _holdTimer += Time.deltaTime;
                }
                else
                {
                    // A키 뗌 → 홀드 타이머 리셋 (단타 공격 후 재시작 가능하게)
                    _holdTimer = 0f;
                    yield return null;
                    continue;
                }

                // 홀드 임계값 미달 → 아직 처형 아님 (단타 공격 구간)
                float threshold = _data != null ? _data.executionHoldThreshold : 0.5f;
                if (_holdTimer < threshold)
                {
                    yield return null;
                    continue;
                }

                // 처형 가능 부위 탐색
                TestBossPartType? targetPart = FindExecutionTarget();
                if (targetPart == null)
                {
                    // 부위 범위 밖 — 홀드는 유지하되 처형 불발
                    yield return null;
                    continue;
                }

                // 조건 충족 → 처형 실행
                _holdTimer = 0f;
                yield return StartCoroutine(ExecuteRoutine(targetPart.Value));
            }
        }

        // ══════════════════════════════════════════════════════
        // 처형 가능 부위 탐색
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 처형 가능 부위 탐색.
        /// 플레이어 위치 기준으로 감지 범위 내 부위를 우선순위에 따라 반환.
        ///
        /// [우선순위]
        ///   Core (활성 상태) > Arm_L (해제 상태) > Arm_R (해제 상태)
        /// </summary>
        /// <returns>처형 가능 부위 타입. 없으면 null.</returns>
        private TestBossPartType? FindExecutionTarget()
        {
            if (_playerTransform == null || _data == null) return null;

            Vector2 playerPos = _playerTransform.position;

            // 코어 우선 확인
            if (_core.IsCoreActive)
            {
                Transform coreTransform = FindCoreTransform();
                if (coreTransform != null)
                {
                    float dist = Vector2.Distance(playerPos, coreTransform.position);
                    if (dist <= _data.executionDetectRange)
                        return TestBossPartType.Core;
                }
            }

            // Arm_L 확인 (해제 상태여야 처형 가능)
            if (_armL != null && _armL.IsUnlocked)
            {
                float dist = Vector2.Distance(playerPos, _armL.transform.position);
                if (dist <= _armL.ExecutionRange)
                    return TestBossPartType.ArmL;
            }

            // Arm_R 확인
            if (_armR != null && _armR.IsUnlocked)
            {
                float dist = Vector2.Distance(playerPos, _armR.transform.position);
                if (dist <= _armR.ExecutionRange)
                    return TestBossPartType.ArmR;
            }

            return null;
        }

        // ══════════════════════════════════════════════════════
        // 처형 실행 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 처형 실행 코루틴. (v1.1)
        ///
        /// [흐름]
        ///   BlockAttack() → 이동 시작 → A키 유지 체크
        ///   → 도착 → OnExecutionCompleted 발행
        ///   → _mustReleaseKey = true (A키 재누름 대기)
        ///   → _cooldownTimer = executionCooldown (쿨다운 시작)
        ///   → UnblockAttack()
        /// </summary>
        /// <param name="partType">처형 대상 부위 타입.</param>
        private IEnumerator ExecuteRoutine(TestBossPartType partType)
        {
            _isExecuting = true;

            // 처형 대상 Transform 결정
            Transform targetTransform = GetPartTransform(partType);
            if (targetTransform == null || _playerTransform == null)
            {
                _isExecuting = false;
                yield break;
            }

            // ① 입력 차단 (이동 + 공격 동시 차단)
            InputManager.Instance?.BlockMove();
            InputManager.Instance?.BlockJump();
            InputManager.Instance?.BlockDash();
            _playerWeapon?.BlockAttack();   // ← 공격 이벤트 차단 (콤보와 분리)

            Debug.Log($"[TestBossExecution] {partType} 처형 시작 — 이동 중");

            // ② 플레이어 → 부위 위치 이동
            bool moveSuccess = false;
            while (_isGroggyActive)
            {
                // A키 놓으면 처형 중단
                if (InputManager.Instance == null || !InputManager.Instance.IsAttackHeld)
                {
                    Debug.Log("[TestBossExecution] A키 해제 → 처형 중단");
                    break;
                }

                Vector2 current = _playerTransform.position;
                Vector2 target = targetTransform.position;
                float dist = Vector2.Distance(current, target);

                // 도착 판정
                if (dist <= (_data != null ? _data.executionArrivalDistance : 0.4f))
                {
                    moveSuccess = true;
                    break;
                }

                // Rigidbody2D.MovePosition 으로 물리 이동
                if (_playerRigid2D != null)
                {
                    float speed = _data != null ? _data.executionMoveSpeed : 14f;
                    Vector2 next = Vector2.MoveTowards(current, target, speed * Time.fixedDeltaTime);
                    _playerRigid2D.MovePosition(next);
                }

                yield return new WaitForFixedUpdate();
            }

            // ③ 입력 차단 해제
            InputManager.Instance?.UnblockMove();
            InputManager.Instance?.UnblockJump();
            InputManager.Instance?.UnblockDash();
            _playerWeapon?.UnblockAttack();  // ← 공격 이벤트 복구

            _isExecuting = false;

            if (moveSuccess)
            {
                // ④ 처형 완료 이벤트 발행
                OnExecutionCompleted?.Invoke(partType);
                Debug.Log($"[TestBossExecution] {partType} 처형 완료!");

                // ⑤ 재발동 방지
                //    A키를 한 번 뗀 뒤에만 재감지 허용
                _mustReleaseKey = true;

                //    쿨다운 시작
                float cooldown = _data != null ? _data.executionCooldown : 0.7f;
                _cooldownTimer = cooldown;

                Debug.Log($"[TestBossExecution] 처형 후 쿨다운 {cooldown:F1}초 + A키 재누름 대기");
            }
        }

        // ══════════════════════════════════════════════════════
        // 처형 중단
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 처형 강제 중단.
        /// 그로기 종료 or A키 해제 시 호출.
        /// 입력 차단 + 공격 차단 모두 해제.
        /// </summary>
        private void InterruptExecution()
        {
            InputManager.Instance?.UnblockMove();
            InputManager.Instance?.UnblockJump();
            InputManager.Instance?.UnblockDash();
            _playerWeapon?.UnblockAttack();

            _isExecuting = false;
            _holdTimer = 0f;

            Debug.Log("[TestBossExecution] 처형 강제 중단");
        }

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 부위 타입에서 Transform 반환.
        /// </summary>
        private Transform GetPartTransform(TestBossPartType partType)
        {
            return partType switch
            {
                TestBossPartType.ArmL => _armL?.transform,
                TestBossPartType.ArmR => _armR?.transform,
                TestBossPartType.Core => FindCoreTransform(),
                _ => null,
            };
        }

        /// <summary>
        /// Core 오브젝트 Transform 탐색.
        /// TestBossCore 하위에서 "Core" 포함 이름으로 탐색.
        /// </summary>
        private Transform FindCoreTransform()
        {
            if (_core == null) return null;
            foreach (Transform child in _core.transform)
            {
                if (child.name.Contains("Core"))
                    return child;
            }
            return null;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;

            // 홀드 진행 중 시각화
            if (_isGroggyActive && !_isExecuting)
            {
                float threshold = _data.executionHoldThreshold;
                float ratio = Mathf.Clamp01(_holdTimer / threshold);
                Gizmos.color = Color.Lerp(Color.white, Color.yellow, ratio);
                Gizmos.DrawWireSphere(transform.position, 0.5f + ratio * 0.5f);
            }

            // 처형 중 타겟 연결선
            if (_isExecuting && _playerTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_playerTransform.position, transform.position);
            }
        }
    }
}