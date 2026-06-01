// ============================================================
// BossKnightAI.cs  v1.0
// 봉인된 기사 보스 AI
//
// [EnemyAI 와의 차이]
//   EnemyAI
//     Patrol / Idle / Chase / Attack / Groggy 5상태
//     단일 패턴 (차징 돌진)
//     EnemySensor 기반 순찰/감지
//     소형 적 전용
//
//   BossKnightAI
//     Idle / Chase / Warning / Active / Recovery /
//     Groggy / DilTime / Counter / Dodge / PhaseTransition 10상태
//     Phase별 다수 패턴 목록 관리
//     패턴 선택 우선순위 + 쿨타임 통합 관리
//     보스 룸 고정 → Patrol / Idle 없음
//     항상 플레이어 인식
//     회피 기동 (순간이동 / 백스탭) 추가
//     Counter 상태 (검 무식 / 대타 출동) 통합
//
// [패턴 실행 흐름]
//   Idle → 패턴 선택 → Warning → Active → Recovery → Idle
//   패턴 중 봉인 성공 → Groggy
//   Groggy 중 양팔 봉인 → DilTime
//   봉인 투사체 감지 → Counter
//   전 패턴 쿨타임 → Dodge
//   HP 임계값 → PhaseTransition
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
    /// 봉인된 기사 보스 AI. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [상태 전환 다이어그램]
    ///
    ///   Idle      ──(패턴 선택)──────────→ Warning
    ///   Idle      ──(전 패턴 쿨타임)──────→ Dodge
    ///   Chase     ──(패턴 범위 진입)───────→ Idle (패턴 선택 대기)
    ///   Warning   ──(예고 완료)─────────→ Active
    ///   Warning   ──(봉인 성공 가능 패턴)→ Groggy (패턴 중단)
    ///   Active    ──(패턴 완료)─────────→ Recovery
    ///   Active    ──(봉인 성공)──────────→ Groggy
    ///   Recovery  ──(완료)──────────────→ Chase or Idle
    ///   Groggy    ──(지속시간)────────────→ Chase
    ///   DilTime   ──(지속시간)────────────→ Chase + 충격파
    ///   Counter   ──(완료)──────────────→ 이전 상태 복귀
    ///   Dodge     ──(완료)──────────────→ Chase
    ///   PhaseTransition ──(완료)────────→ Idle
    ///
    /// [OnFlipped 이벤트]
    ///   EnemyAI 와 동일 구조.
    ///   ObjectFlipController / BossPartComponent 가 구독.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class BossKnightAI : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 상태 열거형
        // ──────────────────────────────────────────

        public enum BossAIState
        {
            /// <summary> 패턴 대기. 플레이어 방향 추적. </summary>
            Idle,
            /// <summary> 플레이어 추적 이동. </summary>
            Chase,
            /// <summary> 패턴 예고 중. 예상 범위 표시. </summary>
            Warning,
            /// <summary> 패턴 시전 중. 히트박스 활성. </summary>
            Active,
            /// <summary> 패턴 후딜레이. 플레이어 공격 기회. </summary>
            Recovery,
            /// <summary>
            /// 그로기 — 완전 정지.
            /// A키 홀드 처형 가능 구간.
            /// 패턴 봉인 성공 / 충돌 / Phase 전환 후 진입.
            /// </summary>
            Groggy,
            /// <summary>
            /// 코어 딜타임 — 완전 정지.
            /// 왼팔 + 오른팔 동시 봉인 → 코어 해제 → 진입.
            /// 지속시간 후 충격파 + 코어 자동 봉인.
            /// </summary>
            DilTime,
            /// <summary>
            /// 반격 중 — 검 무식 / 대타 출동 실행.
            /// 완료 후 이전 상태로 복귀.
            /// </summary>
            Counter,
            /// <summary>
            /// 회피 기동 — 순간이동 or 백스탭.
            /// 전 패턴 쿨타임 시 발동.
            /// </summary>
            Dodge,
            /// <summary>
            /// Phase 전환 연출 중 — 무적.
            /// 충격파 + 애니메이션 재생.
            /// 완료 후 Idle 복귀.
            /// </summary>
            PhaseTransition,
        }

        // ──────────────────────────────────────────
        // 외부 참조 — Inspector 연결 없음 (BossKnight 에서 주입)
        // ──────────────────────────────────────────

        private BossKnightDataSO _data;
        private BossKnight _boss;
        private BossCounterSystem _counterSystem;
        private Transform _playerTransform;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 패턴 목록 (Phase별)
        // ──────────────────────────────────────────

        /// <summary>
        /// Phase 1 패턴 목록.
        /// BossKnight.InitializePhase(Phase1) 에서 등록.
        /// </summary>
        private List<BossPatternBase> _phase1Patterns = new();

        /// <summary> Phase 2 패턴 목록. </summary>
        private List<BossPatternBase> _phase2Patterns = new();

        /// <summary> Phase 3 패턴 목록. </summary>
        private List<BossPatternBase> _phase3Patterns = new();

        /// <summary> 현재 Phase 패턴 목록 참조. </summary>
        private List<BossPatternBase> _currentPatterns = new();

        /// <summary> 현재 실행 중인 패턴. </summary>
        private BossPatternBase _currentPattern;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private BossAIState _currentState = BossAIState.Idle;
        private BossAIState _stateBeforeCounter = BossAIState.Idle;

        private float _facingDirection = 1f;
        private float _flipCooldownTimer;
        private float _dodgeCooldownTimer;
        private float _dodgeMinIntervalTimer;

        private Coroutine _groggyCoroutine;
        private Coroutine _patternCoroutine;
        private Coroutine _dodgeCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 방향 전환 시 발행.
        /// ObjectFlipController / BossPartComponent 가 구독.
        /// </summary>
        public event Action<float> OnFlipped;

        /// <summary> 그로기 진입 시 발행. BossExecutionHandler 가 구독. </summary>
        public event Action OnGroggyEnter;

        /// <summary> 그로기 종료 시 발행. </summary>
        public event Action OnGroggyExit;

        /// <summary> 딜타임 진입 시 발행. </summary>
        public event Action OnDilTimeEnter;

        /// <summary> 딜타임 종료 시 발행. </summary>
        public event Action OnDilTimeExit;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        public BossAIState CurrentState => _currentState;
        public float FacingDirection => _facingDirection;
        public bool IsGroggy => _currentState == BossAIState.Groggy;
        public bool IsDilTime => _currentState == BossAIState.DilTime;
        public bool IsCounter => _currentState == BossAIState.Counter;
        public bool IsPhaseTransition => _currentState == BossAIState.PhaseTransition;
        public BossPatternBase CurrentPattern => _currentPattern;

        // ══════════════════════════════════════════════════════
        // 초기화 (BossKnight 에서 호출)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화. BossKnight.Awake() 에서 호출.
        /// DataSO / Boss 참조 주입.
        /// </summary>
        public void Initialize(
            BossKnight boss,
            BossKnightDataSO data,
            BossCounterSystem counterSystem)
        {
            _boss = boss;
            _data = data;
            _counterSystem = counterSystem;
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// 패턴 목록 등록. BossKnight.InitializePhase() 에서 호출.
        /// </summary>
        public void RegisterPatterns(
            List<BossPatternBase> phase1,
            List<BossPatternBase> phase2,
            List<BossPatternBase> phase3)
        {
            _phase1Patterns = phase1;
            _phase2Patterns = phase2;
            _phase3Patterns = phase3;
        }

        /// <summary>
        /// Phase 전환 시 현재 패턴 목록 교체.
        /// </summary>
        public void SwitchPhase(BossPhase phase)
        {
            _currentPatterns = phase switch
            {
                BossPhase.Phase1 => _phase1Patterns,
                BossPhase.Phase2 => _phase2Patterns,
                BossPhase.Phase3 => _phase3Patterns,
                _ => _phase1Patterns,
            };

            // 실행 중 패턴 중단
            if (_currentPattern != null)
            {
                _currentPattern.Interrupt();
                _currentPattern = null;
            }

            if (_patternCoroutine != null)
            {
                StopCoroutine(_patternCoroutine);
                _patternCoroutine = null;
            }

            Debug.Log($"[BossKnightAI] Phase 전환 → {phase}");
        }

        /// <summary>
        /// 플레이어 Transform 주입. BossKnight.Start() 에서 호출.
        /// </summary>
        public void SetPlayer(Transform player) => _playerTransform = player;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Update()
        {
            if (_data == null || _boss == null) return;
            if (_boss.IsDead) return;

            UpdateTimers();
            UpdateStateLogic();
        }

        private void FixedUpdate()
        {
            if (_data == null || _boss == null) return;
            UpdateMovement();
        }

        // ══════════════════════════════════════════════════════
        // 타이머
        // ══════════════════════════════════════════════════════

        private void UpdateTimers()
        {
            if (_flipCooldownTimer > 0f)
                _flipCooldownTimer -= Time.deltaTime;
            if (_dodgeCooldownTimer > 0f)
                _dodgeCooldownTimer -= Time.deltaTime;
            if (_dodgeMinIntervalTimer > 0f)
                _dodgeMinIntervalTimer -= Time.deltaTime;
        }

        // ══════════════════════════════════════════════════════
        // 상태 로직
        // ══════════════════════════════════════════════════════

        private void UpdateStateLogic()
        {
            switch (_currentState)
            {
                case BossAIState.Idle:
                    UpdateFacingTowardPlayer();
                    TrySelectPattern();
                    break;

                case BossAIState.Chase:
                    UpdateFacingTowardPlayer();
                    // 패턴 실행 조건 체크는 Idle 에서 처리
                    ChangeState(BossAIState.Idle);
                    break;

                case BossAIState.Warning:
                case BossAIState.Active:
                case BossAIState.Recovery:
                case BossAIState.Groggy:
                case BossAIState.DilTime:
                case BossAIState.Counter:
                case BossAIState.PhaseTransition:
                    // 코루틴이 처리
                    break;

                case BossAIState.Dodge:
                    // DodgeRoutine 코루틴이 처리
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
                case BossAIState.Chase:
                    MoveTowardPlayer();
                    break;

                case BossAIState.Idle:
                case BossAIState.Warning:
                case BossAIState.Active:
                case BossAIState.Recovery:
                case BossAIState.Groggy:
                case BossAIState.DilTime:
                case BossAIState.Counter:
                case BossAIState.PhaseTransition:
                    StopHorizontal();
                    break;
                    // Dodge 는 DogeRoutine 이 직접 이동 처리
            }
        }

        private void MoveTowardPlayer()
        {
            if (_playerTransform == null || _data == null) return;

            float moveSpeed = _boss.CurrentPhase switch
            {
                BossPhase.Phase1 => _data.p1.moveSpeed,
                BossPhase.Phase2 => _data.p2.moveSpeed,
                BossPhase.Phase3 => _data.p3.moveSpeed,
                _ => _data.p1.moveSpeed,
            };

            _rigid2D.linearVelocity = new Vector2(
                _facingDirection * moveSpeed,
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
        /// 매 프레임 플레이어 방향 갱신.
        /// Idle / Chase 상태에서 호출.
        /// 쿨타임 적용 (단, Groggy 종료 후 즉시 전환은 예외).
        /// </summary>
        private void UpdateFacingTowardPlayer()
        {
            if (_playerTransform == null) return;

            float dir = _playerTransform.position.x > transform.position.x ? 1f : -1f;
            if (Mathf.Approximately(dir, _facingDirection)) return;
            if (_flipCooldownTimer > 0f) return;

            _flipCooldownTimer = _data?.flipCooldown ?? 2.0f;
            SetFacing(dir);
        }

        /// <summary>
        /// 쿨타임 무시 즉시 플레이어 방향 전환.
        /// Groggy 종료 시 / PhaseTransition 완료 시 호출.
        /// </summary>
        public void TurnTowardPlayerImmediate()
        {
            if (_playerTransform == null) return;
            float dir = _playerTransform.position.x > transform.position.x ? 1f : -1f;
            SetFacing(dir);
        }

        /// <summary>
        /// 방향 설정 + SpriteRenderer.flipX + OnFlipped 이벤트 발행.
        /// EnemyAI.SetFacing() 과 동일 구조.
        /// </summary>
        private void SetFacing(float dir)
        {
            _facingDirection = dir;
            if (_spriteRenderer != null)
                _spriteRenderer.flipX = _facingDirection < 0f;
            OnFlipped?.Invoke(_facingDirection);
        }

        // ══════════════════════════════════════════════════════
        // 패턴 선택
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Idle 상태에서 실행 가능한 패턴 선택.
        ///
        /// [선택 우선순위]
        ///   1. 실행 가능 패턴 목록 수집 (CanExecute == true)
        ///   2. 목록이 비어 있으면 → 회피 기동 체크
        ///   3. 목록에서 우선순위 / 가중치 기반 선택
        ///   4. 선택된 패턴 실행
        /// </summary>
        private void TrySelectPattern()
        {
            if (_currentPattern != null) return;
            if (_currentPatterns == null || _currentPatterns.Count == 0) return;

            // 실행 가능한 패턴 수집
            var available = new List<BossPatternBase>();
            foreach (var p in _currentPatterns)
            {
                if (p != null && p.CanExecute)
                    available.Add(p);
            }

            // 실행 가능한 패턴 없음 → 회피 기동
            if (available.Count == 0)
            {
                TryDodge();
                return;
            }

            // 패턴 선택 (현재: 랜덤. 추후 우선순위/가중치로 확장 가능)
            int idx = UnityEngine.Random.Range(0, available.Count);
            BossPatternBase selected = available[idx];

            _currentPattern = selected;
            _patternCoroutine = StartCoroutine(ExecutePattern(selected));
        }

        /// <summary>
        /// 패턴 실행 코루틴.
        /// Warning → Active → Recovery 순서로 진행.
        /// </summary>
        private IEnumerator ExecutePattern(BossPatternBase pattern)
        {
            // Warning
            ChangeState(BossAIState.Warning);
            yield return StartCoroutine(pattern.ExecuteWarning());

            // Active (Warning 중 그로기 진입하지 않았다면)
            if (_currentState == BossAIState.Warning)
            {
                ChangeState(BossAIState.Active);
                yield return StartCoroutine(pattern.ExecuteActive());
            }

            // Recovery
            if (_currentState == BossAIState.Active)
            {
                ChangeState(BossAIState.Recovery);
                yield return StartCoroutine(pattern.ExecuteRecovery());
            }

            // 패턴 종료
            _currentPattern = null;
            _patternCoroutine = null;

            // 그로기/딜타임이 아닌 경우 Idle 복귀
            if (_currentState == BossAIState.Recovery)
                ChangeState(BossAIState.Idle);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — BossPatternBase / BossCounterSystem 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 그로기 상태 진입.
        /// BossPatternBase.OnPatternGroggy 이벤트 수신 시 호출.
        /// A키 홀드 처형 가능 구간 시작.
        /// </summary>
        public void EnterGroggy(float duration = -1f)
        {
            float t = duration > 0f
                ? duration
                : (_data?.groggyDuration ?? 3.0f);

            // 실행 중 패턴 중단
            if (_currentPattern != null)
            {
                _currentPattern.Interrupt();
                _currentPattern = null;
            }
            if (_patternCoroutine != null)
            {
                StopCoroutine(_patternCoroutine);
                _patternCoroutine = null;
            }

            ChangeState(BossAIState.Groggy);
            OnGroggyEnter?.Invoke();

            if (_groggyCoroutine != null) StopCoroutine(_groggyCoroutine);
            _groggyCoroutine = StartCoroutine(GroggyRoutine(t));
        }

        /// <summary>
        /// 딜타임 진입.
        /// BossCoreLock 에서 코어 해제 시 호출.
        /// </summary>
        public void EnterDilTime(float duration = -1f)
        {
            float t = duration > 0f
                ? duration
                : (_data?.dilTimeDuration ?? 7.0f);

            ChangeState(BossAIState.DilTime);
            OnDilTimeEnter?.Invoke();

            StartCoroutine(DilTimeRoutine(t));
        }

        /// <summary>
        /// Counter 상태 진입. BossCounterSystem 에서 호출.
        /// 완료 후 이전 상태로 복귀.
        /// </summary>
        public void EnterCounter()
        {
            _stateBeforeCounter = _currentState;

            // 현재 패턴 일시 중지
            _currentPattern?.Pause();

            ChangeState(BossAIState.Counter);
        }

        /// <summary>
        /// Counter 상태 종료. BossCounterSystem 에서 호출.
        /// </summary>
        public void ExitCounter()
        {
            // 패턴 재개
            _currentPattern?.Resume();

            // 이전 상태 복귀
            ChangeState(_stateBeforeCounter);
        }

        /// <summary>
        /// Phase 전환 상태 진입. BossPhaseManager 에서 호출.
        /// 전환 완료 후 Idle 복귀는 BossPhaseManager 가 처리.
        /// </summary>
        public void EnterPhaseTransition()
        {
            // 실행 중 패턴 강제 중단
            if (_currentPattern != null)
            {
                _currentPattern.Interrupt();
                _currentPattern = null;
            }
            if (_patternCoroutine != null)
            {
                StopCoroutine(_patternCoroutine);
                _patternCoroutine = null;
            }

            StopHorizontal();
            ChangeState(BossAIState.PhaseTransition);
        }

        /// <summary>
        /// Phase 전환 완료 후 Idle 복귀.
        /// BossPhaseManager 에서 호출.
        /// </summary>
        public void ExitPhaseTransition()
        {
            TurnTowardPlayerImmediate();
            ChangeState(BossAIState.Idle);
        }

        // ══════════════════════════════════════════════════════
        // 그로기 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator GroggyRoutine(float duration)
        {
            Debug.Log($"[BossKnightAI] Groggy 진입 ({duration:F1}초)");
            StopHorizontal();

            yield return new WaitForSeconds(duration);

            _groggyCoroutine = null;

            // 그로기 종료 → 플레이어 방향 즉시 전환
            TurnTowardPlayerImmediate();

            OnGroggyExit?.Invoke();
            Debug.Log("[BossKnightAI] Groggy 종료 → Idle 복귀");
            ChangeState(BossAIState.Idle);
        }

        // ══════════════════════════════════════════════════════
        // 딜타임 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator DilTimeRoutine(float duration)
        {
            Debug.Log($"[BossKnightAI] DilTime 진입 ({duration:F1}초)");
            StopHorizontal();

            yield return new WaitForSeconds(duration);

            OnDilTimeExit?.Invoke();

            // 충격파 → BossKnight 가 처리
            _boss.TriggerShockwave();

            Debug.Log("[BossKnightAI] DilTime 종료 → Idle 복귀");
            ChangeState(BossAIState.Idle);
        }

        // ══════════════════════════════════════════════════════
        // 회피 기동
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 전 패턴 쿨타임 시 회피 기동 시도.
        /// 쿨타임 / 최소 발동 간격 체크 후 실행.
        /// </summary>
        private void TryDodge()
        {
            if (_dodgeCooldownTimer > 0f) return;
            if (_dodgeMinIntervalTimer > 0f) return;
            if (_currentState != BossAIState.Idle) return;

            _dodgeCoroutine = StartCoroutine(DodgeRoutine());
        }

        private IEnumerator DodgeRoutine()
        {
            ChangeState(BossAIState.Dodge);

            // 회피 유형 랜덤 선택
            bool isTeleport = UnityEngine.Random.value > 0.5f;

            if (isTeleport)
                yield return StartCoroutine(DodgeTeleport());
            else
                yield return StartCoroutine(DodgeBackstep());

            _dodgeCooldownTimer = _data?.dodgeCooldown ?? 8.0f;
            _dodgeMinIntervalTimer = _data?.dodgeMinInterval ?? 5.0f;

            ChangeState(BossAIState.Idle);
        }

        /// <summary>
        /// 순간이동 — 플레이어 반대편으로 이동.
        /// </summary>
        private IEnumerator DodgeTeleport()
        {
            if (_playerTransform == null) yield break;

            float offsetX = -_facingDirection * _data.dodgeTeleportOffset;
            Vector3 targetPos = new Vector3(
                _playerTransform.position.x + offsetX,
                transform.position.y,
                transform.position.z);

            // 텔레포트 이펙트 (추후 파티클 연결)
            transform.position = targetPos;
            TurnTowardPlayerImmediate();

            yield return new WaitForSeconds(0.1f);

            Debug.Log("[BossKnightAI] 회피 기동: 순간이동");
        }

        /// <summary>
        /// 백스탭 — 플레이어 반대 방향으로 빠르게 후퇴.
        /// </summary>
        private IEnumerator DodgeBackstep()
        {
            float backstepSpeed = _data?.dodgeBackstepSpeed ?? 8.0f;
            float backstepDuration = _data?.dodgeBackstepDuration ?? 0.3f;
            float dir = -_facingDirection; // 후퇴 방향 = 현재 방향 반대

            float elapsed = 0f;
            while (elapsed < backstepDuration)
            {
                elapsed += Time.fixedDeltaTime;
                _rigid2D.linearVelocity = new Vector2(
                    dir * backstepSpeed,
                    _rigid2D.linearVelocity.y);
                yield return new WaitForFixedUpdate();
            }

            StopHorizontal();
            TurnTowardPlayerImmediate();

            Debug.Log("[BossKnightAI] 회피 기동: 백스탭");
        }

        // ══════════════════════════════════════════════════════
        // 상태 전환
        // ══════════════════════════════════════════════════════

        private void ChangeState(BossAIState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;
            Debug.Log($"[BossKnightAI] 상태 전환 → {newState}");
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 현재 상태 표시
            Color stateColor = _currentState switch
            {
                BossAIState.Groggy => Color.yellow,
                BossAIState.DilTime => Color.cyan,
                BossAIState.Active => Color.red,
                BossAIState.Warning => new Color(1f, 0.5f, 0f),
                BossAIState.Counter => Color.magenta,
                BossAIState.PhaseTransition => Color.white,
                _ => Color.green,
            };

            UnityEditor.Handles.color = stateColor;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 3.0f,
                $"[Boss] {_currentState}  Phase:{(_boss != null ? _boss.CurrentPhase.ToString() : "?")}");
        }
#endif
    }
}