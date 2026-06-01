// ============================================================
// BossExecutionHandler.cs  v1.1
// A키 홀드 처형 처리 컴포넌트
//
// [v1.1 변경]
//
//   ① 처형 흐름 재설계 — 기획서 기준으로 재작성
//       기존: A키 홀드 완료 → 플레이어 이동 → 처형 실행
//       변경: A키 홀드 시작 즉시 플레이어 자동 이동 시작
//             이동 완료 → 처형 실행 (ReLock / ForceUnlock)
//
//       [기획 처형 흐름]
//         그로기 상태 + 부위 범위 내 + A키 홀드 시작
//         → 플레이어 → 부위 위치로 자동 이동 (이동 중 A키 유지 필요)
//         → 이동 완료 → 잠금 / 해제 실행
//         → 그로기 회복 시 처형 강제 중단 + 충격파
//
//   ② 코어 처형 조건 명확화
//       기존: IsActive 체크만 — 코어 비활성 상태에서도 처형 대상에 잡힘
//       변경: BossCoreLock.IsCoreActive 체크 추가
//             코어는 활성화된 상태에서만 처형 가능
//             일반 부위(팔/검/방패)는 기존대로 IsActive 체크
//
//   ③ 플레이어 이동 방식 수정
//       기존: Transform.position 직접 수정 → Rigidbody2D 물리 무시, 벽 통과
//       변경: InputManager.BlockMove() + BlockJump() + BlockDash() 로 입력 차단
//             Rigidbody2D.MovePosition() 으로 물리 이동
//             처형 완료 / 중단 시 Unblock 호출로 이동 복원
//
//   ④ A키 홀드 유지 조건 추가
//       이동 중 A키를 놓으면 처형 중단 (기획 의도)
//       InputManager.IsAttackHeld 매 프레임 체크
//
// [처형 흐름 — v1.1]
//   1. 그로기 진입 → OnGroggyEnter() → 입력 감지 루프 시작
//   2. 플레이어가 부위 범위 내 + A키 홀드 시작
//   3. 즉시 이동 차단 + MovePlayerToPart 코루틴 시작
//   4. 이동 중 A키 놓으면 → InterruptExecution()
//   5. 이동 완료 → ReLock() or ForceUnlock() 실행
//   6. 처형 이펙트 재생 후 이동 차단 해제
//   7. 그로기 종료(OnGroggyExit) → 처형 강제 중단 + 충격파
//
// [InputManager 연동]
//   IsAttackHeld : A키 홀드 상태 폴링
//   BlockMove / UnblockMove : 처형 중 이동 차단
//   BlockJump / UnblockJump : 처형 중 점프 차단
//   BlockDash / UnblockDash : 처형 중 대쉬 차단
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
    /// A키 홀드 처형 처리 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [처형 가능 대상]
    ///   일반 부위 (팔/검/방패) : IsActive == true → 항상 처형 가능
    ///   코어                  : BossCoreLock.IsCoreActive == true 일 때만
    ///
    /// [이동 방식]
    ///   Rigidbody2D.MovePosition() — 물리 충돌 유지
    ///   이동 중 InputManager 입력 전부 차단
    ///   처형 완료 / 중단 시 차단 해제
    ///
    /// [A키 홀드 유지]
    ///   이동 중 A키 홓드를 놓으면 즉시 처형 중단
    ///   기획: 플레이어가 의도적으로 처형을 취소할 수 있음
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class BossExecutionHandler : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 이동 설정 ──────────────────────")]

        /// <summary>
        /// 처형 이동 속도 (units/s).
        /// 플레이어가 부위 위치로 이동할 때 사용.
        /// </summary>
        [Tooltip("처형 이동 속도 (units/s). 권장: 12~20.")]
        [Min(1f)]
        [SerializeField] private float _executionMoveSpeed = 15f;

        /// <summary>
        /// 이동 완료 판정 거리 (units).
        /// 부위와의 거리가 이 값 이하가 되면 도착으로 판정.
        /// </summary>
        [Tooltip("이동 완료 판정 거리. 권장: 0.2~0.5.")]
        [Min(0.05f)]
        [SerializeField] private float _arrivalThreshold = 0.3f;

        [Header("── 처형 이펙트 ──────────────────────")]

        /// <summary>
        /// 처형 완료 파티클.
        /// 부위 위치에서 재생.
        /// </summary>
        [Tooltip("처형 완료 파티클.")]
        [SerializeField] private ParticleSystem _executionEffect;

        /// <summary>
        /// 처형 이동 중 루프 파티클.
        /// 플레이어 위치에서 재생.
        /// </summary>
        [Tooltip("처형 이동 중 루프 파티클.")]
        [SerializeField] private ParticleSystem _executionProgressEffect;

        // ──────────────────────────────────────────
        // 참조 (Initialize() 에서 주입)
        // ──────────────────────────────────────────

        private BossKnight _boss;
        private BossKnightAI _ai;
        private BossKnightDataSO _data;
        private BossCoreLock _coreLock;

        // ──────────────────────────────────────────
        // 플레이어 참조
        // ──────────────────────────────────────────

        private Transform _playerTransform;
        private Rigidbody2D _playerRigid2D;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 그로기 상태 여부. </summary>
        private bool _isGroggy;

        /// <summary> 처형 실행 중 여부. </summary>
        private bool _isExecuting;

        /// <summary> 현재 처형 대상 부위. </summary>
        private BossPartComponent _targetPart;

        /// <summary> 실행 중인 처형 코루틴. </summary>
        private Coroutine _executionCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 처형 강제 중단 시 발행.
        /// 그로기 회복에 의한 중단 시 충격파 포함.
        /// </summary>
        public event Action OnExecutionInterrupted;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 처형 실행 중 여부. </summary>
        public bool IsExecuting => _isExecuting;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화. BossKnight.Start() 에서 호출.
        /// </summary>
        public void Initialize(
            BossKnight boss,
            BossKnightAI ai,
            BossKnightDataSO data)
        {
            _boss = boss;
            _ai = ai;
            _data = data;
            _coreLock = boss.GetComponent<BossCoreLock>();

            // 플레이어 탐색
            var playerMovers = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (playerMovers.Length > 0)
            {
                _playerTransform = playerMovers[0].transform;
                _playerRigid2D = playerMovers[0].GetComponent<Rigidbody2D>();
            }

            if (_playerTransform == null)
                Debug.LogWarning("[BossExecutionHandler] PlayerMover 를 찾을 수 없습니다.");
        }

        // ══════════════════════════════════════════════════════
        // 그로기 이벤트 수신 (BossKnightAI 에서 구독)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 그로기 진입 시 호출.
        /// BossKnightAI.OnGroggyEnter 에서 구독.
        /// </summary>
        public void OnGroggyEnter()
        {
            _isGroggy = true;
            _targetPart = null;

            Debug.Log("[BossExecutionHandler] 그로기 진입 — 처형 입력 감지 시작");
        }

        /// <summary>
        /// 그로기 종료 시 호출.
        /// BossKnightAI.OnGroggyExit 에서 구독.
        /// 처형 진행 중이면 강제 중단 + 충격파.
        /// </summary>
        public void OnGroggyExit()
        {
            _isGroggy = false;

            if (_isExecuting)
                InterruptExecution(triggerShockwave: true);
        }

        // ══════════════════════════════════════════════════════
        // 처형 입력 감지 (Update)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 매 프레임 처형 입력 감지.
        ///
        /// [감지 조건]
        ///   그로기 상태 + 처형 미진행 중
        ///   + 부위 범위 내 + A키 홀드 시작
        ///
        /// [처형 시작]
        ///   조건 충족 즉시 ExecuteRoutine 코루틴 시작
        ///   → 플레이어 자동 이동 → 처형 실행
        /// </summary>
        private void Update()
        {
            if (!_isGroggy) return;
            if (_isExecuting) return;
            if (_playerTransform == null) return;
            if (InputManager.Instance == null) return;

            // A키 홀드 체크
            if (!InputManager.Instance.IsAttackHeld) return;

            // 처형 가능한 가장 가까운 부위 탐색
            BossPartComponent nearPart = FindNearestExecutablePart();
            if (nearPart == null) return;

            // 처형 시작
            _targetPart = nearPart;
            _executionCoroutine = StartCoroutine(ExecuteRoutine(_targetPart));
        }

        // ══════════════════════════════════════════════════════
        // 처형 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 처형 실행 코루틴.
        ///
        /// [흐름]
        ///   1. 이동 차단 (Move / Jump / Dash)
        ///   2. 진행 이펙트 시작
        ///   3. MovePlayerToPart() — 부위 위치로 이동
        ///      이동 중 A키 놓으면 → 중단
        ///   4. 이동 완료 → ReLock / ForceUnlock 실행
        ///   5. 완료 이펙트 재생
        ///   6. 이동 차단 해제
        /// </summary>
        private IEnumerator ExecuteRoutine(BossPartComponent part)
        {
            _isExecuting = true;

            // ① 이동 전 차단
            BlockPlayerInput();

            // ② 진행 이펙트 시작
            if (_executionProgressEffect != null)
                _executionProgressEffect.Play();

            // ③ 부위 위치로 자동 이동
            bool arrivedSuccessfully = false;
            yield return StartCoroutine(
                MovePlayerToPart(part, result => arrivedSuccessfully = result));

            // 이동 실패 (A키 해제 or 그로기 종료)
            if (!arrivedSuccessfully)
            {
                FinishExecution(success: false);
                yield break;
            }

            // ④ 그로기 상태 재확인 (이동 중 그로기 종료됐을 수 있음)
            if (!_isGroggy)
            {
                FinishExecution(success: false);
                yield break;
            }

            // ⑤ 잠금 / 해제 실행
            if (part.IsUnlocked)
            {
                part.ReLock();
                Debug.Log($"[BossExecutionHandler] 처형 완료 → {part.PartType} 재잠금");
            }
            else
            {
                part.ForceUnlock();
                Debug.Log($"[BossExecutionHandler] 처형 완료 → {part.PartType} 해제");
            }

            // ⑥ 완료 이펙트
            if (_executionEffect != null)
            {
                _executionEffect.transform.position = part.transform.position;
                _executionEffect.Play();
            }

            yield return new WaitForSeconds(0.2f);

            FinishExecution(success: true);
        }

        /// <summary>
        /// 플레이어를 부위 위치로 Rigidbody2D.MovePosition 이동.
        ///
        /// [이동 중 중단 조건]
        ///   A키를 놓은 경우 → 즉시 중단, result = false
        ///   그로기 종료 → 즉시 중단, result = false
        ///   도착 → result = true
        ///
        /// [물리 방식]
        ///   Rigidbody2D.MovePosition() — 충돌 레이어 유지
        ///   매 FixedUpdate 단위로 호출
        /// </summary>
        private IEnumerator MovePlayerToPart(BossPartComponent part, Action<bool> result)
        {
            if (_playerRigid2D == null)
            {
                result(false);
                yield break;
            }

            while (true)
            {
                // 중단 조건 — A키 해제
                if (!InputManager.Instance.IsAttackHeld)
                {
                    result(false);
                    yield break;
                }

                // 중단 조건 — 그로기 종료
                if (!_isGroggy)
                {
                    result(false);
                    yield break;
                }

                Vector2 currentPos = _playerRigid2D.position;
                Vector2 targetPos = (Vector2)part.transform.position;
                float dist = Vector2.Distance(currentPos, targetPos);

                // 도착 판정
                if (dist <= _arrivalThreshold)
                {
                    result(true);
                    yield break;
                }

                // MovePosition 으로 한 스텝 이동
                Vector2 nextPos = Vector2.MoveTowards(
                    currentPos,
                    targetPos,
                    _executionMoveSpeed * Time.fixedDeltaTime);

                _playerRigid2D.MovePosition(nextPos);

                yield return new WaitForFixedUpdate();
            }
        }

        // ══════════════════════════════════════════════════════
        // 처형 완료 / 중단 정리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 처형 성공 / 실패 후 공통 정리.
        /// 이펙트 정지 + 이동 차단 해제 + 플래그 초기화.
        /// </summary>
        private void FinishExecution(bool success)
        {
            if (_executionProgressEffect != null)
                _executionProgressEffect.Stop();

            UnblockPlayerInput();

            _isExecuting = false;
            _targetPart = null;
            _executionCoroutine = null;

            if (!success)
                Debug.Log("[BossExecutionHandler] 처형 중단");
        }

        /// <summary>
        /// 그로기 회복에 의한 처형 강제 중단.
        /// 충격파 옵션 포함.
        /// </summary>
        private void InterruptExecution(bool triggerShockwave)
        {
            if (_executionCoroutine != null)
            {
                StopCoroutine(_executionCoroutine);
                _executionCoroutine = null;
            }

            FinishExecution(success: false);

            if (triggerShockwave)
                _boss?.TriggerShockwave();

            OnExecutionInterrupted?.Invoke();

            Debug.Log("[BossExecutionHandler] 처형 강제 중단" +
                      (triggerShockwave ? " + 충격파" : ""));
        }

        // ══════════════════════════════════════════════════════
        // 플레이어 입력 차단 / 해제
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 처형 이동 중 플레이어 입력 전부 차단.
        /// Move / Jump / Dash 차단.
        /// Rigidbody2D velocity.x 즉시 0.
        /// </summary>
        private void BlockPlayerInput()
        {
            if (InputManager.Instance == null) return;

            InputManager.Instance.BlockMove();
            InputManager.Instance.BlockJump();
            InputManager.Instance.BlockDash();

            // velocity.x 즉시 정지 (관성 제거)
            if (_playerRigid2D != null)
                _playerRigid2D.linearVelocity = new Vector2(
                    0f, _playerRigid2D.linearVelocity.y);
        }

        /// <summary>
        /// 처형 완료 / 중단 후 플레이어 입력 차단 해제.
        /// </summary>
        private void UnblockPlayerInput()
        {
            if (InputManager.Instance == null) return;

            InputManager.Instance.UnblockMove();
            InputManager.Instance.UnblockJump();
            InputManager.Instance.UnblockDash();
        }

        // ══════════════════════════════════════════════════════
        // 처형 가능 부위 탐색
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어 주변에서 처형 가능한 가장 가까운 부위 탐색.
        ///
        /// [일반 부위 조건]
        ///   IsActive == true (현재 Phase 에서 활성)
        ///   처형 범위 내 (ExecutionRange)
        ///
        /// [코어 조건]
        ///   BossCoreLock.IsCoreActive == true (코어 활성화 상태)
        ///   처형 범위 내
        ///
        /// [코어가 활성화되면 코어 우선 반환]
        ///   기획: 코어 활성 시 코어 처형이 최우선
        /// </summary>
        private BossPartComponent FindNearestExecutablePart()
        {
            if (_boss == null || _playerTransform == null) return null;

            float defaultRange = _data?.executionRange ?? 2.0f;
            var parts = _boss.GetComponentsInChildren<BossPartComponent>();

            BossPartComponent corePart = null;
            BossPartComponent nearestPart = null;
            float minDist = float.MaxValue;

            foreach (var part in parts)
            {
                if (part == null) continue;
                if (!part.IsActive) continue;

                float dist = Vector2.Distance(
                    _playerTransform.position,
                    part.transform.position);

                float range = part.ExecutionRange(defaultRange);

                if (dist > range) continue;

                // 코어 부위 — IsCoreActive 조건 추가
                if (part.PartType == BossPartType.Core)
                {
                    if (_coreLock != null && _coreLock.IsCoreActive)
                    {
                        corePart = part;
                        // 코어는 즉시 반환 (최우선)
                    }
                    continue; // 코어 비활성이면 탐색 제외
                }

                // 일반 부위 — 가장 가까운 부위
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestPart = part;
                }
            }

            // 코어 활성 시 코어 우선 반환
            return corePart != null ? corePart : nearestPart;
        }
    }
}