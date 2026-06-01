// ============================================================
// TestBossAI.cs  v1.0
// 테스트 보스 AI
//
// [BossKnightAI 시행착오 반영 — 개선 사항]
//
//   BossKnightAI (기존)             TestBossAI (개선)
//   ─────────────────────────────   ─────────────────────────────
//   10상태 (복잡)                   5상태 (Idle/Chase/Warning/Active/Recovery)
//                                   + Groggy / DilTime (TestBossCore 가 관리)
//
//   Chase → 즉시 Idle 전환          Chase = 실제 이동 상태 유지
//   (UpdateStateLogic에서 바로 전환) (플레이어 추적 이동 → 패턴 범위 진입 시 Idle)
//
//   BossKnightDataSO 직접 의존       TestBossDataSO 에서 이동속도 등 수치 참조
//
//   BossPatternBase → BossKnightAI  TestBossPatternBase → TestBossAI
//   강결합 (_ai 직접 참조)           이벤트만 사용 (OnPatternGroggy)
//
//   회피기동 AI 내부 구현            생략 (간이 테스트용)
//   Counter 상태                    생략 (봉인 투사체 시스템 미포함)
//   PhaseTransition 상태             생략 (단일 루프 테스트)
//   Dodge 상태                      생략
//
// [상태 다이어그램]
//
//   Idle ──(패턴 선택)──────→ Warning → Active → Recovery → Idle
//   Idle ──(패턴 범위 외)───→ Chase
//   Chase ──(패턴 범위 내)──→ Idle
//   Recovery ──(그로기 이벤트)→ Groggy (TestBossCore.EnterGroggy 호출)
//   ※ Groggy / DilTime 상태는 TestBossCore 가 관리
//     AI 는 _isStopped 플래그로 이동/패턴 선택 정지만 처리
//
// [패턴 선택 구조]
//   Idle 상태에서 TrySelectPattern() 호출
//   → 플레이어가 _patternRange 이내 → 실행 가능 패턴 수집
//   → 실행 가능 패턴 없음 → Chase 전환 (쿨타임 대기)
//   → 실행 가능 패턴 있음 → 랜덤 선택 → Warning → Active → Recovery
//
// [그로기 유도 흐름]
//   TestBossPatternBase.OnPatternGroggy 발행
//   → HandlePatternGroggy() 수신
//   → TestBossCore.EnterGroggy() 호출
//   → TestBossCore 가 OnGroggyEnter/Exit 발행 → 처형 감지 시작
//   → 그로기 종료 → ResumeFromGroggy() → AI 재개
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 AI. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [이 스크립트가 하는 것]
    ///   - 상태 관리 (Idle / Chase / Warning / Active / Recovery)
    ///   - 플레이어 추적 이동 (Chase)
    ///   - 패턴 선택 및 실행 코루틴 관리
    ///   - 그로기/딜타임 중 이동·패턴 정지
    ///   - 방향 전환 (SpriteRenderer.flipX)
    ///
    /// [이 스크립트가 하지 않는 것]
    ///   - HP 관리 → TestBossCore
    ///   - 그로기/딜타임 상태 관리 → TestBossCore
    ///   - 처형 입력 처리 → TestBossExecution
    ///   - 코어 활성화 → TestBossCore
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class TestBossAI : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 상태 열거형
        // ──────────────────────────────────────────

        /// <summary>
        /// 테스트 보스 AI 상태.
        /// Groggy / DilTime 은 TestBossCore 가 별도 관리.
        /// AI 는 _isStopped 플래그로 이 구간을 처리.
        /// </summary>
        public enum TestBossAIState
        {
            /// <summary>
            /// 패턴 대기.
            /// 플레이어 방향 추적 + 패턴 선택 시도.
            /// 플레이어가 _patternRange 밖이면 Chase 전환.
            /// </summary>
            Idle,

            /// <summary>
            /// 플레이어 추적 이동.
            /// _patternRange 이내 진입 시 Idle 전환 → 패턴 선택.
            /// </summary>
            Chase,

            /// <summary>
            /// 패턴 예고 중.
            /// TestBossPatternBase.ExecuteWarning() 실행 중.
            /// </summary>
            Warning,

            /// <summary>
            /// 패턴 시전 중.
            /// TestBossPatternBase.ExecuteActive() 실행 중.
            /// 히트박스 활성.
            /// </summary>
            Active,

            /// <summary>
            /// 패턴 후딜레이.
            /// TestBossPatternBase.ExecuteRecovery() 실행 중.
            /// 플레이어 공격 가능 구간.
            /// 완료 후 OnPatternGroggy 발행 시 TestBossCore.EnterGroggy() 호출.
            /// </summary>
            Recovery,
        }

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── DataSO 연결 (필수) ──────────────────────")]

        /// <summary>
        /// TestBossDataSO. 이동속도 등 수치 참조.
        /// 미연결 시 TestBossCore 에서 주입.
        /// </summary>
        [Tooltip("TestBossDataSO. 필수 연결.")]
        [SerializeField] private TestBossDataSO _data;

        [Header("── 패턴 목록 ──────────────────────")]

        /// <summary>
        /// 패턴 목록.
        /// Inspector 에서 TestBossPattern_Charge, TestBossPattern_Stomp 연결.
        /// </summary>
        [Tooltip("패턴 목록. Inspector 에서 연결.")]
        [SerializeField] private List<TestBossPatternBase> _patterns = new();

        [Header("── AI 수치 ──────────────────────")]

        /// <summary>
        /// 이동 속도 (units/s).
        /// Chase 상태에서 플레이어 방향으로 이동.
        /// </summary>
        [Tooltip("이동 속도 (units/s). 권장: 2~5.")]
        [Min(0.1f)]
        [SerializeField] private float _moveSpeed = 3.5f;

        /// <summary>
        /// 패턴 발동 범위 (units).
        /// 플레이어가 이 범위 이내이면 Idle 에서 패턴 선택.
        /// 이 범위 밖이면 Chase.
        /// </summary>
        [Tooltip("패턴 발동 범위 (units). 권장: 4~8.")]
        [Min(0.5f)]
        [SerializeField] private float _patternRange = 6.0f;

        /// <summary>
        /// 방향 전환 쿨타임 (초).
        /// 너무 자주 뒤집히지 않도록 제한.
        /// </summary>
        [Tooltip("방향 전환 쿨타임 (초). 권장: 0.5~2.0.")]
        [Min(0f)]
        [SerializeField] private float _flipCooldown = 1.0f;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;
        private TestBossCore _core;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private TestBossAIState _currentState = TestBossAIState.Idle;

        /// <summary>
        /// 현재 실행 중인 패턴.
        /// null = 패턴 없음.
        /// </summary>
        private TestBossPatternBase _currentPattern;

        /// <summary>
        /// 현재 패턴 코루틴 핸들.
        /// 강제 중단(Interrupt) 시 StopCoroutine 에 사용.
        /// </summary>
        private Coroutine _patternCoroutine;

        /// <summary>
        /// 이동/패턴 정지 플래그.
        /// Groggy / DilTime 중 true → 이동 및 패턴 선택 완전 정지.
        /// </summary>
        private bool _isStopped;

        /// <summary>
        /// 현재 바라보는 방향. +1 = 오른쪽, -1 = 왼쪽.
        /// </summary>
        private float _facingDirection = 1f;

        /// <summary> 방향 전환 쿨타임 잔여. </summary>
        private float _flipCooldownTimer;

        // ──────────────────────────────────────────
        // 플레이어 참조
        // ──────────────────────────────────────────

        private Transform _playerTransform;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 방향 전환 시 발행.
        /// 향후 ObjectFlipController 연동용.
        /// </summary>
        public event Action<float> OnFlipped;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 AI 상태. </summary>
        public TestBossAIState CurrentState => _currentState;

        /// <summary> 현재 방향. </summary>
        public float FacingDirection => _facingDirection;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _core = GetComponent<TestBossCore>();
        }

        private void Start()
        {
            // DataSO 미연결 시 TestBossCore 에서 가져오기
            if (_data == null && _core != null)
                _data = _core.Data;

            if (_data == null)
            {
                Debug.LogError("[TestBossAI] TestBossDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            // 플레이어 탐색
            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
                _playerTransform = players[0].transform;
            else
                Debug.LogWarning("[TestBossAI] PlayerMover 를 씬에서 찾을 수 없습니다.");

            // TestBossCore 이벤트 구독
            if (_core != null)
            {
                _core.OnGroggyEnter += HandleGroggyEnter;
                _core.OnGroggyExit += HandleGroggyExit;
                _core.OnDilTimeEnter += HandleDilTimeEnter;
                _core.OnDilTimeExit += HandleDilTimeExit;
                _core.OnDead += HandleBossDead;
            }

            // 패턴 이벤트 구독
            foreach (var pattern in _patterns)
            {
                if (pattern == null) continue;
                pattern.OnPatternGroggy += HandlePatternGroggy;
            }

            Debug.Log("[TestBossAI] 초기화 완료.");
        }

        private void OnDestroy()
        {
            if (_core != null)
            {
                _core.OnGroggyEnter -= HandleGroggyEnter;
                _core.OnGroggyExit -= HandleGroggyExit;
                _core.OnDilTimeEnter -= HandleDilTimeEnter;
                _core.OnDilTimeExit -= HandleDilTimeExit;
                _core.OnDead -= HandleBossDead;
            }

            foreach (var pattern in _patterns)
            {
                if (pattern == null) continue;
                pattern.OnPatternGroggy -= HandlePatternGroggy;
            }
        }

        private void Update()
        {
            if (_core != null && _core.IsDead) return;
            if (_isStopped) return;

            UpdateTimers();
            UpdateStateLogic();
        }

        private void FixedUpdate()
        {
            if (_core != null && _core.IsDead) return;
            if (_isStopped)
            {
                StopHorizontal();
                return;
            }

            UpdateMovement();
        }

        // ══════════════════════════════════════════════════════
        // 타이머
        // ══════════════════════════════════════════════════════

        private void UpdateTimers()
        {
            if (_flipCooldownTimer > 0f)
                _flipCooldownTimer -= Time.deltaTime;
        }

        // ══════════════════════════════════════════════════════
        // 상태 로직
        // ══════════════════════════════════════════════════════

        private void UpdateStateLogic()
        {
            switch (_currentState)
            {
                case TestBossAIState.Idle:
                    UpdateFacingTowardPlayer();

                    // 플레이어가 패턴 범위 밖 → Chase
                    if (!IsPlayerInRange(_patternRange))
                    {
                        ChangeState(TestBossAIState.Chase);
                        return;
                    }

                    // 패턴 선택 시도
                    TrySelectPattern();
                    break;

                case TestBossAIState.Chase:
                    UpdateFacingTowardPlayer();

                    // 패턴 범위 진입 → Idle 전환
                    if (IsPlayerInRange(_patternRange))
                        ChangeState(TestBossAIState.Idle);
                    break;

                case TestBossAIState.Warning:
                case TestBossAIState.Active:
                case TestBossAIState.Recovery:
                    // 코루틴(ExecutePattern)이 처리
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 이동
        // ══════════════════════════════════════════════════════

        private void UpdateMovement()
        {
            switch (_currentState)
            {
                case TestBossAIState.Chase:
                    MoveTowardPlayer();
                    break;

                case TestBossAIState.Idle:
                case TestBossAIState.Warning:
                case TestBossAIState.Active:
                case TestBossAIState.Recovery:
                    StopHorizontal();
                    break;
            }
        }

        private void MoveTowardPlayer()
        {
            if (_rigid2D == null) return;

            _rigid2D.linearVelocity = new Vector2(
                _facingDirection * _moveSpeed,
                _rigid2D.linearVelocity.y);
        }

        private void StopHorizontal()
        {
            if (_rigid2D != null)
                _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
        }

        // ══════════════════════════════════════════════════════
        // 방향 전환
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어 방향으로 부드럽게 전환 (쿨타임 적용).
        /// Idle / Chase 상태에서 매 프레임 호출.
        /// </summary>
        private void UpdateFacingTowardPlayer()
        {
            if (_playerTransform == null) return;

            float dir = _playerTransform.position.x > transform.position.x ? 1f : -1f;
            if (Mathf.Approximately(dir, _facingDirection)) return;
            if (_flipCooldownTimer > 0f) return;

            _flipCooldownTimer = _flipCooldown;
            SetFacing(dir);
        }

        /// <summary>
        /// 즉시 플레이어 방향 전환 (쿨타임 무시).
        /// 그로기 종료 / 패턴 시작 시 호출.
        /// </summary>
        private void TurnTowardPlayerImmediate()
        {
            if (_playerTransform == null) return;

            float dir = _playerTransform.position.x > transform.position.x ? 1f : -1f;
            SetFacing(dir);
        }

        /// <summary>
        /// 방향 설정 + SpriteRenderer.flipX + OnFlipped 발행.
        /// </summary>
        private void SetFacing(float dir)
        {
            _facingDirection = dir;

            if (_spriteRenderer != null)
                _spriteRenderer.flipX = _facingDirection < 0f;

            OnFlipped?.Invoke(_facingDirection);
        }

        // ══════════════════════════════════════════════════════
        // 패턴 선택 및 실행
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Idle 상태에서 실행 가능한 패턴 선택.
        /// 이미 패턴 실행 중이면 무시.
        ///
        /// [선택 방식]
        ///   실행 가능(CanExecute == true) 패턴 수집 → 랜덤 선택.
        ///   실행 가능한 패턴 없으면 패턴 대기 (Idle 유지).
        ///   → 모두 쿨타임 중이면 Chase 전환으로 자연스럽게 이동.
        /// </summary>
        private void TrySelectPattern()
        {
            if (_currentPattern != null) return;
            if (_patterns == null || _patterns.Count == 0) return;

            // 실행 가능 패턴 수집
            var available = new List<TestBossPatternBase>();
            foreach (var p in _patterns)
            {
                if (p != null && p.CanExecute)
                    available.Add(p);
            }

            if (available.Count == 0) return;

            // 랜덤 선택 (추후 가중치/우선순위 확장 가능)
            int idx = UnityEngine.Random.Range(0, available.Count);
            var selected = available[idx];

            _currentPattern = selected;
            _patternCoroutine = StartCoroutine(ExecutePattern(selected));
        }

        /// <summary>
        /// 패턴 실행 코루틴.
        /// Warning → Active → Recovery 순서.
        ///
        /// [BossKnightAI.ExecutePattern 과의 차이]
        ///   상태 체크 조건이 단순:
        ///   _isStopped 체크로 Groggy/DilTime 중 즉시 중단.
        /// </summary>
        private IEnumerator ExecutePattern(TestBossPatternBase pattern)
        {
            // Warning
            ChangeState(TestBossAIState.Warning);
            yield return StartCoroutine(pattern.ExecuteWarning());

            // _isStopped = 그로기/딜타임 진입 → 패턴 중단
            if (_isStopped)
            {
                CleanupPattern();
                yield break;
            }

            // Active
            if (_currentState == TestBossAIState.Warning)
            {
                ChangeState(TestBossAIState.Active);
                yield return StartCoroutine(pattern.ExecuteActive());
            }

            if (_isStopped)
            {
                CleanupPattern();
                yield break;
            }

            // Recovery
            if (_currentState == TestBossAIState.Active)
            {
                ChangeState(TestBossAIState.Recovery);
                yield return StartCoroutine(pattern.ExecuteRecovery());
            }

            // 패턴 종료 정리
            CleanupPattern();

            // Groggy/DilTime 이 아닌 정상 종료 → Idle 복귀
            if (!_isStopped)
                ChangeState(TestBossAIState.Idle);
        }

        /// <summary>
        /// 패턴 종료 정리.
        /// _currentPattern / _patternCoroutine 초기화.
        /// </summary>
        private void CleanupPattern()
        {
            _currentPattern = null;
            _patternCoroutine = null;
        }

        // ══════════════════════════════════════════════════════
        // TestBossCore 이벤트 수신
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 그로기 진입 수신.
        /// 이동/패턴 즉시 정지.
        /// 현재 패턴 강제 중단.
        /// </summary>
        private void HandleGroggyEnter()
        {
            _isStopped = true;
            StopHorizontal();

            // 현재 패턴 강제 중단
            if (_currentPattern != null)
            {
                _currentPattern.Interrupt();

                if (_patternCoroutine != null)
                {
                    StopCoroutine(_patternCoroutine);
                    _patternCoroutine = null;
                }

                _currentPattern = null;
            }

            Debug.Log("[TestBossAI] 그로기 진입 → 이동/패턴 정지");
        }

        /// <summary>
        /// 그로기 종료 수신.
        /// 이동/패턴 재개. 플레이어 방향 즉시 전환 후 Idle 복귀.
        /// </summary>
        private void HandleGroggyExit()
        {
            _isStopped = false;

            TurnTowardPlayerImmediate();
            ChangeState(TestBossAIState.Idle);

            Debug.Log("[TestBossAI] 그로기 종료 → Idle 복귀");
        }

        /// <summary>
        /// 딜타임 진입 수신.
        /// 이동/패턴 정지 (그로기와 동일).
        /// </summary>
        private void HandleDilTimeEnter()
        {
            _isStopped = true;
            StopHorizontal();

            if (_currentPattern != null)
            {
                _currentPattern.Interrupt();
                if (_patternCoroutine != null)
                {
                    StopCoroutine(_patternCoroutine);
                    _patternCoroutine = null;
                }
                _currentPattern = null;
            }

            Debug.Log("[TestBossAI] 딜타임 진입 → 이동/패턴 정지");
        }

        /// <summary>
        /// 딜타임 종료 수신.
        /// 이동/패턴 재개 + Idle 복귀.
        /// </summary>
        private void HandleDilTimeExit()
        {
            _isStopped = false;

            TurnTowardPlayerImmediate();
            ChangeState(TestBossAIState.Idle);

            Debug.Log("[TestBossAI] 딜타임 종료 → Idle 복귀");
        }

        /// <summary>
        /// 보스 처치 수신.
        /// AI 완전 정지.
        /// </summary>
        private void HandleBossDead()
        {
            _isStopped = true;
            StopHorizontal();

            if (_patternCoroutine != null)
            {
                StopCoroutine(_patternCoroutine);
                _patternCoroutine = null;
            }

            _currentPattern = null;
            enabled = false;

            Debug.Log("[TestBossAI] 보스 처치 → AI 정지");
        }

        // ══════════════════════════════════════════════════════
        // 패턴 이벤트 수신
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// TestBossPatternBase.OnPatternGroggy 수신.
        /// TestBossCore.EnterGroggy() 를 호출하여 그로기 상태로 전환.
        ///
        /// [흐름]
        ///   패턴 Recovery 완료 → OnPatternGroggy 발행
        ///   → HandlePatternGroggy() 수신
        ///   → TestBossCore.EnterGroggy()
        ///   → TestBossCore.OnGroggyEnter 발행
        ///   → HandleGroggyEnter() 수신 → _isStopped = true
        /// </summary>
        private void HandlePatternGroggy()
        {
            if (_core != null)
                _core.EnterGroggy();
        }

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어가 지정 범위 이내에 있는지 확인.
        /// </summary>
        private bool IsPlayerInRange(float range)
        {
            if (_playerTransform == null) return false;
            return Vector2.Distance(transform.position, _playerTransform.position) <= range;
        }

        /// <summary>
        /// 상태 전환. 동일 상태면 무시.
        /// </summary>
        private void ChangeState(TestBossAIState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            Debug.Log($"[TestBossAI] 상태 전환 → {newState}");
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어 Transform 수동 주입.
        /// 씬 탐색 실패 시 외부에서 호출.
        /// </summary>
        public void SetPlayer(Transform player) => _playerTransform = player;

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            // 패턴 발동 범위
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, _patternRange);

#if UNITY_EDITOR
            Color stateColor = _currentState switch
            {
                TestBossAIState.Warning => new Color(1f, 0.5f, 0f),
                TestBossAIState.Active => Color.red,
                TestBossAIState.Recovery => Color.yellow,
                TestBossAIState.Chase => Color.cyan,
                _ => Color.green,
            };

            UnityEditor.Handles.color = stateColor;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 3.5f,
                $"[AI] {_currentState}  Stopped:{_isStopped}  " +
                $"Pattern:{(_currentPattern != null ? _currentPattern.GetType().Name : "없음")}");
#endif
        }
    }
}