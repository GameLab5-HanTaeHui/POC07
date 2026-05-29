// ============================================================
// EnemyAI.cs  v4.4
// 적 공용 AI — Groggy 상태 + OnFlipped 이벤트 방식으로 Flip 분리
//
// [v4.4 변경]
//   ① FlipAttackHitboxes() 제거 → OnFlipped 이벤트 발행으로 교체
//       EnemyAI 가 Attack 스크립트를 직접 참조하던 구조 제거.
//       Flip() / UpdateChaseDirection() / TurnTowardPlayer() 에서
//       OnFlipped 이벤트만 발행.
//       각 Attack 스크립트(EnemyKnightAttack, EnemyKnightChargeAttack)가
//       Start() 에서 OnFlipped 를 구독해 자체 히트박스 반전 처리.
//       → 추후 어떤 Enemy 든 Attack 스크립트가 늘어나도 EnemyAI 수정 불필요.
//
//   ② ChangeState() 딜레이 제거
//       stateTransitionDelay 필드가 EnemyDataSO 에 없음.
//       groggyDuration 을 딜레이로 쓰던 잘못된 참조 제거.
//       상태 전환은 즉시 처리. Groggy 상태가 경직을 담당.
//
// [v4.3 변경]
//   Groggy 상태 추가. EnterGroggy() 외부 API.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using UnityEngine;

namespace KEY
{
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
            /// <summary>
            /// 그로기 — 완전 정지 + 플레이어 공략 타이밍.
            /// 돌진 벽 충돌 or 봉인 취소 후 진입.
            /// groggyDuration 후 Chase 복귀.
            /// </summary>
            Groggy,
        }

        // ──────────────────────────────────────────
        // DataSO
        // ──────────────────────────────────────────

        private EnemyDataSO _settings;
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
        private Coroutine _groggyCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 방향 전환 시 발행.
        /// 파라미터: 새 방향 (+1 = 오른쪽, -1 = 왼쪽).
        /// EnemyKnightAttack / EnemyKnightChargeAttack 이 구독해
        /// 각자 히트박스 localPosition.x 반전 처리.
        /// EnemyAI 는 어떤 Attack 스크립트가 있는지 알 필요 없음.
        /// </summary>
        public event Action<float> OnFlipped;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        public AIState CurrentState => _currentState;
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

            _attack = GetComponent<EnemyKnightAttack>() as EnemyAttackBase
                      ?? GetComponent<EnemyAttackBase>();

            var enemyBase = GetComponent<EnemyBase>();
            if (enemyBase != null) _settings = enemyBase.Settings;
            else Debug.LogError("[EnemyAI] EnemyBase 가 없습니다.");
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

            if (_attack != null) _attack.OnAttackFinished += HandleNormalAttackFinished;
            if (_chargeAttack != null) _chargeAttack.OnAttackFinished += HandleChargeAttackFinished;

            if (_settings.enemyType == EnemyType.Dummy ||
                _settings.enemyType == EnemyType.DummyLocked)
                enabled = false;
        }

        private void OnDestroy()
        {
            if (_attack != null) _attack.OnAttackFinished -= HandleNormalAttackFinished;
            if (_chargeAttack != null) _chargeAttack.OnAttackFinished -= HandleChargeAttackFinished;
        }

        private void Update() => UpdateState();
        private void FixedUpdate() => UpdateMovement();

        // ══════════════════════════════════════════════════════
        // 봉인 체크
        // ══════════════════════════════════════════════════════

        private bool IsSealed(SealType sealType)
            => _sealComponent != null && _sealComponent.IsSealedAction(sealType);

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 그로기 상태 즉시 진입.
        /// EnemyKnightChargeAttack 에서 호출:
        ///   ① 돌진 중 벽 충돌
        ///   ② 카운트다운 중 Dash 봉인 감지 → 취소
        /// </summary>
        public void EnterGroggy(float duration = -1f)
        {
            float t = duration > 0f
                ? duration
                : (_settings != null ? _settings.groggyDuration : 2.0f);

            ApplyState(AIState.Groggy);

            if (_groggyCoroutine != null) StopCoroutine(_groggyCoroutine);
            _groggyCoroutine = StartCoroutine(GroggyRoutine(t));
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
                    if (_sensor.CheckPatrolSight()) ChangeState(AIState.Chase);
                    break;

                case AIState.Idle:
                    if (_sensor.CheckPatrolSight())
                    {
                        if (_idleCoroutine != null) StopCoroutine(_idleCoroutine);
                        ChangeState(AIState.Chase);
                    }
                    break;

                case AIState.Chase:
                    if (!_sensor.CheckChaseRange()) { ChangeState(AIState.Patrol); return; }

                    bool inChargeRange = _sensor.CheckChargeRange();
                    bool chargeReady = _chargeAttack != null && _chargeAttack.CanAttack;
                    bool inAttackRange = _sensor.CheckAttackRange();
                    bool normalReady = _attack != null && _attack.CanAttack;

                    if (chargeReady && inChargeRange && !inAttackRange)
                        ChangeState(AIState.Attack);
                    else if (normalReady && inAttackRange)
                        ChangeState(AIState.Attack);
                    else if (chargeReady && inChargeRange)
                        ChangeState(AIState.Attack);
                    else
                        UpdateChaseDirection();
                    break;

                case AIState.Attack:
                    break;

                case AIState.Groggy:
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
                case AIState.Groggy:
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

            bool attackBusy = _attack != null && _attack.IsAttacking;
            bool chargeBusy = _chargeAttack != null && _chargeAttack.IsAttacking;
            if (attackBusy || chargeBusy) return;

            if (IsSealed(SealType.Attack)) { ChangeState(AIState.Chase); return; }
            if (_attack == null) { ChangeState(AIState.Chase); return; }

            switch (_settings.enemyType)
            {
                case EnemyType.Knight:
                    bool inChargeRange = _sensor.CheckChargeRange();
                    bool chargeReady = _chargeAttack != null && _chargeAttack.CanAttack;
                    bool inAttackRange = _sensor.CheckAttackRange();
                    bool normalReady = _attack != null && _attack.CanAttack;

                    if (chargeReady && inChargeRange && !inAttackRange)
                        _chargeAttack.TryAttack(_settings.chargeCooldown);
                    else if (normalReady && inAttackRange)
                        _attack.TryAttack(_settings.attackCooldown);
                    else if (chargeReady && inChargeRange)
                        _chargeAttack.TryAttack(_settings.chargeCooldown);
                    else
                        ChangeState(AIState.Chase);
                    break;

                default:
                    ChangeState(AIState.Chase);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 근접 공격 완료 → 짧은 Groggy (groggyDuration × 0.4) → Chase.
        /// </summary>
        private void HandleNormalAttackFinished()
        {
            float t = (_settings != null ? _settings.groggyDuration : 2.0f) * 0.4f;
            EnterGroggy(t);
        }

        /// <summary>
        /// 차징 공격 정상 완료 → 기본 Groggy → Chase.
        /// 벽 충돌 시에는 EnemyKnightChargeAttack 이 직접 EnterGroggy() 호출.
        /// </summary>
        private void HandleChargeAttackFinished()
        {
            EnterGroggy();
        }

        // ══════════════════════════════════════════════════════
        // Groggy 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator GroggyRoutine(float duration)
        {
            Debug.Log($"[EnemyAI] Groggy 진입 ({duration:F1}초)");
            StopHorizontal();

            yield return new WaitForSeconds(duration);

            TurnTowardPlayer();

            _groggyCoroutine = null;
            Debug.Log("[EnemyAI] Groggy 종료 → Chase 복귀");
            ApplyState(AIState.Chase);
        }

        /// <summary>
        /// 플레이어 방향으로 즉시 전환. Groggy 종료 시 호출.
        /// </summary>
        private void TurnTowardPlayer()
        {
            Transform player = _sensor.DetectedPlayer;
            if (player == null) return;

            float dir = player.position.x > transform.position.x ? 1f : -1f;
            if (Mathf.Approximately(dir, _facingDirection)) return;

            SetFacing(dir);
        }

        // ══════════════════════════════════════════════════════
        // 상태 전환
        // ══════════════════════════════════════════════════════

        private void ChangeState(AIState newState)
        {
            if (_currentState == newState) return;
            ApplyState(newState);
        }

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
                case AIState.Groggy:
                    StopHorizontal();
                    break;
            }
        }

        private void TryEnterIdle()
        {
            if (UnityEngine.Random.value < _settings.idleChance)
                ChangeState(AIState.Idle);
        }

        // ══════════════════════════════════════════════════════
        // 이동 / 방향
        // ══════════════════════════════════════════════════════

        private void Flip()
        {
            SetFacing(_facingDirection * -1f);
        }

        private void UpdateChaseDirection()
        {
            Transform player = _sensor.DetectedPlayer;
            if (player == null) return;

            float dir = player.position.x > transform.position.x ? 1f : -1f;
            if (Mathf.Approximately(dir, _facingDirection)) return;

            SetFacing(dir);
        }

        /// <summary>
        /// 방향 설정 + SpriteRenderer 반전 + Sensor 갱신 + OnFlipped 이벤트 발행.
        /// Flip / UpdateChaseDirection / TurnTowardPlayer 모두 이 함수를 통함.
        /// Attack 스크립트들은 OnFlipped 를 구독해 자체 히트박스를 반전.
        /// </summary>
        private void SetFacing(float dir)
        {
            _facingDirection = dir;
            if (_spriteRenderer != null)
                _spriteRenderer.flipX = _facingDirection < 0f;
            _sensor.SetFacingDirection(_facingDirection);
            OnFlipped?.Invoke(_facingDirection);
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
            float t = UnityEngine.Random.Range(_settings.idleTimeMin, _settings.idleTimeMax);
            yield return new WaitForSeconds(t);
            ChangeState(AIState.Patrol);
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_sealComponent == null || !_sealComponent.HasAnySeal) return;
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"[AI봉인] {_sealComponent.SealCount}개");
        }
#endif
    }
}