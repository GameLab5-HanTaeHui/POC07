// ============================================================
// EnemyAI.cs  v2.0
// 적 공용 AI — 단일 컴포넌트, EnemyType 분기
//
// [v2.0 변경]
//   KnightAI 제거 → 이 파일 하나로 모든 적 AI 통합.
//   추상 클래스 → 일반 클래스로 변경.
//   OnPatrolMove / OnChaseMove / OnEnterAttack 내부에서
//   switch(enemyType) 로 타입별 행동 분기.
//
// [새 적 추가 시]
//   1. EnemyType 에 항목 추가
//   2. 이 파일의 각 switch 에 케이스 추가
//   3. EnemyBase / EnemyAttackBase 구현체 작성
//   → EnemyAI 컴포넌트 자체는 교체 불필요
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 공용 AI 컴포넌트. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [상태머신]
    ///   Patrol → (직선 감지) → Chase
    ///   Patrol → (랜덤)     → Idle
    ///   Idle   → (대기 완료) → Patrol
    ///   Idle   → (직선 감지) → Chase
    ///   Chase  → (사정거리)  → Attack
    ///   Chase  → (범위 이탈) → Patrol
    ///   Attack → (완료)      → Chase
    ///
    /// [타입별 분기]
    ///   OnPatrolMove / OnChaseMove / OnEnterAttack 에서
    ///   switch(_settings.enemyType) 로 분기.
    ///   Dummy 타입은 이동/공격 없음.
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(EnemySensor))]
    public class EnemyAI : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 상태 열거형
        // ──────────────────────────────────────────

        /// <summary> AI 행동 상태. </summary>
        public enum AIState
        {
            /// <summary> 순찰 — 좌우 이동, 직선 감지. </summary>
            Patrol,

            /// <summary> 랜덤 정지 — 대기 후 Patrol 복귀. </summary>
            Idle,

            /// <summary> 추격 — 플레이어 추적, 원형 감지. </summary>
            Chase,

            /// <summary> 공격 — 공격 모션 실행 중. </summary>
            Attack,
        }

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 필수 연결 ──────────────────────")]

        /// <summary>
        /// 적 수치 + 타입 SO.
        /// enemyType 으로 행동 분기.
        /// </summary>
        [Tooltip("EnemyDataSO. 필수 연결.")]
        [SerializeField] private EnemyDataSO _settings;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private EnemySensor _sensor;
        private EnemyAttackBase _attack;
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private AIState _currentState = AIState.Patrol;
        private float _facingDirection = 1f;
        private Coroutine _idleCoroutine;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 AI 상태. </summary>
        public AIState CurrentState => _currentState;

        /// <summary> 현재 바라보는 방향. 1 = 오른쪽, -1 = 왼쪽. </summary>
        public float FacingDirection => _facingDirection;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _sensor = GetComponent<EnemySensor>();
            _attack = GetComponent<EnemyAttackBase>();
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogError($"[EnemyAI] EnemyDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            // 센서에 수치 주입
            _sensor.SetData(_settings);
            _sensor.SetFacingDirection(_facingDirection);

            // KnightAttack 에 데이터 주입 (EnemyAttackBase 구현체)
            var knightAttack = GetComponent<EnemyKnightAttack>();
            if (knightAttack != null)
                knightAttack.SetData(_settings);

            // 공격 완료 이벤트 구독
            if (_attack != null)
                _attack.OnAttackFinished += () => ChangeState(AIState.Chase);

            // Dummy 타입은 AI 비활성
            if (_settings.enemyType == EnemyType.Dummy ||
                _settings.enemyType == EnemyType.DummyLocked)
            {
                enabled = false;
                return;
            }
        }

        private void OnDestroy()
        {
            if (_attack != null)
                _attack.OnAttackFinished -= () => ChangeState(AIState.Chase);
        }

        private void Update()
        {
            UpdateState();
        }

        private void FixedUpdate()
        {
            UpdateMovement();
        }

        // ══════════════════════════════════════════════════════
        // 상태 업데이트
        // ══════════════════════════════════════════════════════

        private void UpdateState()
        {
            switch (_currentState)
            {
                case AIState.Patrol:
                    if (_sensor.CheckWall() || _sensor.CheckCliff())
                    {
                        Flip();
                        TryEnterIdle();
                        return;
                    }
                    if (_sensor.CheckPatrolSight())
                        ChangeState(AIState.Chase);
                    break;

                case AIState.Idle:
                    if (_sensor.CheckPatrolSight())
                    {
                        if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
                        ChangeState(AIState.Chase);
                    }
                    break;

                case AIState.Chase:
                    if (!_sensor.CheckChaseRange())
                    {
                        ChangeState(AIState.Patrol);
                        return;
                    }
                    if (_sensor.CheckAttackRange() && _attack != null && _attack.CanAttack)
                    {
                        ChangeState(AIState.Attack);
                        return;
                    }
                    UpdateChaseDirection();
                    break;

                case AIState.Attack:
                    // 공격 완료는 OnAttackFinished 이벤트로 처리
                    break;
            }
        }

        private void UpdateMovement()
        {
            switch (_currentState)
            {
                case AIState.Patrol: OnPatrolMove(); break;
                case AIState.Chase: OnChaseMove(); break;
                case AIState.Idle:
                case AIState.Attack:
                    StopHorizontal();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 상태별 행동 — EnemyType 분기
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 순찰 이동.
        /// 타입별로 이동 방식이 다르면 여기서 분기.
        /// </summary>
        private void OnPatrolMove()
        {
            switch (_settings.enemyType)
            {
                case EnemyType.Knight:
                    _rigid2D.linearVelocity = new Vector2(
                        _facingDirection * _settings.patrolSpeed,
                        _rigid2D.linearVelocity.y);
                    break;

                // 추후 Drone: 공중 이동 로직
                // case EnemyType.Drone:
                //     break;

                default:
                    // Dummy 타입 등 — 이동 없음 (Start 에서 AI 비활성 처리)
                    break;
            }
        }

        /// <summary>
        /// 추격 이동.
        /// </summary>
        private void OnChaseMove()
        {
            switch (_settings.enemyType)
            {
                case EnemyType.Knight:
                    _rigid2D.linearVelocity = new Vector2(
                        _facingDirection * _settings.chaseSpeed,
                        _rigid2D.linearVelocity.y);
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// 공격 상태 진입.
        /// 이동 정지 후 타입별 공격 컴포넌트 실행.
        /// </summary>
        private void OnEnterAttack()
        {
            StopHorizontal();

            if (_attack == null)
            {
                Debug.LogWarning($"[EnemyAI] EnemyAttackBase 컴포넌트가 없습니다. ({_settings.enemyType})");
                ChangeState(AIState.Chase);
                return;
            }

            switch (_settings.enemyType)
            {
                case EnemyType.Knight:
                    _attack.TryAttack(_settings.attackCooldown);
                    break;

                // 추후 Drone, Golem 등 추가
                // case EnemyType.Drone:
                //     _attack.TryAttack(_settings.attackCooldown);
                //     break;

                default:
                    ChangeState(AIState.Chase);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 상태 전환
        // ══════════════════════════════════════════════════════

        private void ChangeState(AIState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;

            switch (newState)
            {
                case AIState.Idle:
                    if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
                    _idleCoroutine = StartCoroutine(IdleRoutine());
                    break;

                case AIState.Attack:
                    OnEnterAttack();
                    break;
            }
        }

        private void TryEnterIdle()
        {
            if (Random.value < _settings.idleChance)
                ChangeState(AIState.Idle);
        }

        // ══════════════════════════════════════════════════════
        // 이동 보조
        // ══════════════════════════════════════════════════════

        private void Flip()
        {
            _facingDirection *= -1f;
            if (_spriteRenderer != null)
                _spriteRenderer.flipX = _facingDirection < 0f;
            _sensor.SetFacingDirection(_facingDirection);
        }

        private void UpdateChaseDirection()
        {
            Transform player = _sensor.DetectedPlayer;
            if (player == null) return;

            float dir = player.position.x > transform.position.x ? 1f : -1f;
            if (Mathf.Approximately(dir, _facingDirection)) return;

            _facingDirection = dir;
            if (_spriteRenderer != null)
                _spriteRenderer.flipX = _facingDirection < 0f;
            _sensor.SetFacingDirection(_facingDirection);
        }

        private void StopHorizontal()
        {
            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
        }

        // ══════════════════════════════════════════════════════
        // Idle 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator IdleRoutine()
        {
            float wait = Random.Range(_settings.idleTimeMin, _settings.idleTimeMax);
            yield return new WaitForSeconds(wait);
            ChangeState(AIState.Patrol);
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"[{_settings?.enemyType}] {_currentState}  Dir:{_facingDirection}");
#endif
        }
    }
}