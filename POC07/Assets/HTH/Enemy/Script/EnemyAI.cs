// ============================================================
// EnemyAI.cs  v4.2
// 적 공용 AI — ChargeAttack FlipHitbox 추가 + 상태전환 딜레이
//
// [v4.2 변경]
//   ① FlipAttackHitboxes() 에 EnemyKnightChargeAttack.FlipHitbox() 추가.
//       방향 전환 시 돌진 히트박스 localPosition.x 도 반전.
//
//   ② 상태전환 딜레이 추가 — 적이 둔하게 반응.
//       Chase → Attack 전환 시 _stateTransitionDelay 만큼 대기.
//       Attack → Chase 전환 시도 시에도 딜레이 적용.
//       EnemyDataSO 에 stateTransitionDelay 필드 추가.
//       딜레이 중 추가 전환 요청 무시 (_isTransitioning 플래그).
//
// [v4.1 변경]
//   _chargeAttack 구독, FlipAttackHitboxes(), 중복 진입 차단.
//
// [v4.0 변경]
//   DataSO 단일 연결 지점 — EnemyBase.Settings 취득.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 공용 AI 컴포넌트. (v4.2)
    /// </summary>
    [RequireComponent(typeof(EnemySensor))]
    public class EnemyAI : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 상태 열거형
        // ──────────────────────────────────────────

        public enum AIState
        {
            Patrol,
            Idle,
            Chase,
            Attack,
        }

        // ──────────────────────────────────────────
        // DataSO — Inspector 연결 없음
        // ──────────────────────────────────────────

        /// <summary>
        /// 적 수치 SO. Awake 에서 EnemyBase.Settings 로 취득.
        /// </summary>
        private EnemyDataSO _settings;

        /// <summary>
        /// 차징 돌진 공격 컴포넌트.
        /// Awake 에서 자동 취득.
        /// </summary>
        private EnemyKnightChargeAttack _chargeAttack;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private EnemySensor _sensor;
        private EnemyAttackBase _attack;
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;
        private EnemySealComponent _sealComponent;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private AIState _currentState = AIState.Patrol;
        private float _facingDirection = 1f;
        private Coroutine _idleCoroutine;

        /// <summary>
        /// 상태전환 딜레이 진행 중 플래그.
        /// true 동안 ChangeState() 요청 무시 — 적이 즉각 반응하지 않고 둔하게 동작.
        /// </summary>
        private bool _isTransitioning;

        private Coroutine _transitionCoroutine;

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
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _sealComponent = GetComponent<EnemySealComponent>();
            _chargeAttack = GetComponent<EnemyKnightChargeAttack>();

            // EnemyKnightAttack 만 명시 취득 (ChargeAttack 겹침 방지)
            _attack = GetComponent<EnemyKnightAttack>() as EnemyAttackBase
                      ?? GetComponent<EnemyAttackBase>();

            var enemyBase = GetComponent<EnemyBase>();
            if (enemyBase != null)
                _settings = enemyBase.Settings;
            else
                Debug.LogError("[EnemyAI] EnemyBase 컴포넌트가 없습니다.");
        }

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogError("[EnemyAI] EnemyDataSO 취득 실패.");
                enabled = false;
                return;
            }

            _sensor.SetData(_settings);
            _sensor.SetFacingDirection(_facingDirection);

            var knightAttack = GetComponent<EnemyKnightAttack>();
            if (knightAttack != null) knightAttack.SetData(_settings);
            if (_chargeAttack != null) _chargeAttack.SetData(_settings);

            if (_attack != null) _attack.OnAttackFinished += HandleAttackFinished;
            if (_chargeAttack != null) _chargeAttack.OnAttackFinished += HandleAttackFinished;

            if (_settings.enemyType == EnemyType.Dummy ||
                _settings.enemyType == EnemyType.DummyLocked)
                enabled = false;
        }

        private void OnDestroy()
        {
            if (_attack != null) _attack.OnAttackFinished -= HandleAttackFinished;
            if (_chargeAttack != null) _chargeAttack.OnAttackFinished -= HandleAttackFinished;
        }

        private void Update() => UpdateState();
        private void FixedUpdate() => UpdateMovement();

        // ══════════════════════════════════════════════════════
        // 봉인 체크
        // ══════════════════════════════════════════════════════

        private bool IsSealed(SealType sealType)
            => _sealComponent != null && _sealComponent.IsSealedAction(sealType);

        // ══════════════════════════════════════════════════════
        // 상태 업데이트
        // ══════════════════════════════════════════════════════

        private void UpdateState()
        {
            // 상태전환 딜레이 중 — 조건 확인 중단
            if (_isTransitioning) return;

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
                    if (_chargeAttack != null && _chargeAttack.CanAttack
                        && _sensor.CheckChargeRange())
                    {
                        ChangeState(AIState.Attack);
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
        // 상태별 행동
        // ══════════════════════════════════════════════════════

        private void OnPatrolMove()
        {
            if (IsSealed(SealType.Move) || IsSealed(SealType.Dash))
            {
                StopHorizontal();
                return;
            }

            switch (_settings.enemyType)
            {
                case EnemyType.Knight:
                    _rigid2D.linearVelocity = new Vector2(
                        _facingDirection * _settings.patrolSpeed,
                        _rigid2D.linearVelocity.y);
                    break;
            }
        }

        private void OnChaseMove()
        {
            if (IsSealed(SealType.Move))
            {
                StopHorizontal();
                return;
            }

            switch (_settings.enemyType)
            {
                case EnemyType.Knight:
                    _rigid2D.linearVelocity = new Vector2(
                        _facingDirection * _settings.chaseSpeed,
                        _rigid2D.linearVelocity.y);
                    break;
            }
        }

        private void OnEnterAttack()
        {
            StopHorizontal();

            if (IsSealed(SealType.Attack))
            {
                ChangeState(AIState.Chase);
                return;
            }

            if (_attack == null)
            {
                ChangeState(AIState.Chase);
                return;
            }

            switch (_settings.enemyType)
            {
                case EnemyType.Knight:
                    bool inChargeRange = _sensor.CheckChargeRange();
                    bool chargeReady = _chargeAttack != null && _chargeAttack.CanAttack;
                    bool inAttackRange = _sensor.CheckAttackRange();
                    bool normalReady = _attack != null && _attack.CanAttack;

                    if (chargeReady && inChargeRange && !inAttackRange)
                    {
                        _chargeAttack.TryAttack(_settings.chargeCooldown);
                        Debug.Log("[EnemyAI] Knight 차징 돌진");
                    }
                    else if (normalReady && inAttackRange)
                    {
                        _attack.TryAttack(_settings.attackCooldown);
                        Debug.Log("[EnemyAI] Knight 근접 공격");
                    }
                    else if (chargeReady && inChargeRange)
                    {
                        _chargeAttack.TryAttack(_settings.chargeCooldown);
                        Debug.Log("[EnemyAI] Knight 차징 돌진 (근접 내)");
                    }
                    else
                    {
                        ChangeState(AIState.Chase);
                    }
                    break;

                default:
                    ChangeState(AIState.Chase);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        private void HandleAttackFinished() => ChangeState(AIState.Chase);

        // ══════════════════════════════════════════════════════
        // 상태 전환
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 상태 전환 요청.
        /// stateTransitionDelay > 0 이면 딜레이 후 전환 — 적이 둔하게 반응.
        /// 딜레이 중 요청은 무시 (_isTransitioning).
        ///
        /// [딜레이 적용 전환]
        ///   Chase → Attack : 공격 결정에 딜레이 (둔한 반응)
        ///   Attack → Chase : 공격 후 추격 복귀에 딜레이 (잠깐 멈춤)
        ///
        /// [즉시 전환]
        ///   Patrol ↔ Idle  : 순찰/대기 전환은 즉시 (딜레이 불필요)
        ///   Chase → Patrol : 범위 이탈 즉시 복귀
        /// </summary>
        private void ChangeState(AIState newState)
        {
            if (_currentState == newState) return;
            if (_isTransitioning) return;

            // 딜레이 적용 전환 (Chase↔Attack)
            float delay = _settings != null ? _settings.stateTransitionDelay : 0f;
            bool useDelay = delay > 0f &&
                           ((_currentState == AIState.Chase && newState == AIState.Attack) ||
                            (_currentState == AIState.Attack && newState == AIState.Chase));

            if (useDelay)
            {
                if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = StartCoroutine(DelayedTransition(newState, delay));
                return;
            }

            ApplyState(newState);
        }

        /// <summary>
        /// 딜레이 후 상태 전환 코루틴.
        /// 딜레이 중 이동은 유지 (멈추지 않음 — 어색한 정지 방지).
        /// </summary>
        private IEnumerator DelayedTransition(AIState newState, float delay)
        {
            _isTransitioning = true;
            yield return new WaitForSeconds(delay);
            _isTransitioning = false;
            ApplyState(newState);
        }

        /// <summary>
        /// 실제 상태 적용.
        /// ChangeState 와 DelayedTransition 양쪽에서 호출.
        /// </summary>
        private void ApplyState(AIState newState)
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
            FlipAttackHitboxes(_facingDirection);
        }

        /// <summary>
        /// 방향 전환 시 모든 공격 히트박스 localPosition.x 반전.
        ///
        /// [v4.2 변경]
        ///   EnemyKnightAttack.FlipHitbox()       — 근접 히트박스
        ///   EnemyKnightChargeAttack.FlipHitbox() — 돌진 히트박스 ← 추가
        /// </summary>
        private void FlipAttackHitboxes(float dir)
        {
            // 근접 히트박스 반전
            var knightAttack = GetComponent<EnemyKnightAttack>();
            if (knightAttack != null)
                knightAttack.FlipHitbox(dir);

            // 돌진 히트박스 반전 (v4.2 추가)
            if (_chargeAttack != null)
                _chargeAttack.FlipHitbox(dir);
        }

        private void UpdateChaseDirection()
        {
            Transform player = _sensor.DetectedPlayer;
            if (player == null) return;

            float dir = player.position.x > transform.position.x ? 1f : -1f;
            if (!Mathf.Approximately(dir, _facingDirection))
            {
                _facingDirection = dir;
                _spriteRenderer.flipX = dir < 0f;
                _sensor.SetFacingDirection(dir);
                FlipAttackHitboxes(dir);
            }
        }

        private void StopHorizontal()
        {
            if (_rigid2D != null)
                _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
        }

        // ══════════════════════════════════════════════════════
        // 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator IdleRoutine()
        {
            float idleTime = Random.Range(_settings.idleTimeMin, _settings.idleTimeMax);
            yield return new WaitForSeconds(idleTime);
            ChangeState(AIState.Patrol);
        }
    }
}