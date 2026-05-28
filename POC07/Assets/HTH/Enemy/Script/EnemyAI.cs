// ============================================================
// EnemyAI.cs  v4.0
// 적 공용 AI — DataSO 참조 구조 개선
//
// [v4.0 변경 — DataSO 단일 연결 지점]
//   [SerializeField] private EnemyDataSO _settings 제거.
//   Awake 에서 GetComponent<EnemyBase>().Settings 로 취득.
//   → Inspector 에서 EnemyAI 에 DataSO 를 별도 연결할 필요 없음.
//   → EnemyBase 에 연결된 DataSO 하나가 전체에 흘러감.
//
//   [데이터 흐름]
//     EnemyBase._settings (Inspector 연결)
//       → EnemyAI.Awake()    : _settings = GetComponent<EnemyBase>().Settings
//       → EnemySensor        : _sensor.SetData(_settings)
//       → EnemyKnightAttack  : knightAttack.SetData(_settings)
//
// [v3.0 변경]
//   EnemySealComponent 연동, 봉인 행동 차단 체크.
//
// [v2.0 변경]
//   KnightAI 제거 → 단일 통합 AI.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 공용 AI 컴포넌트. (v4.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [DataSO 취득 순서]
    ///   Awake 실행 순서: EnemyBase.Awake → EnemyAI.Awake
    ///   (같은 오브젝트의 Awake 는 컴포넌트 순서에 따름)
    ///   EnemyAI.Awake 에서 GetComponent<EnemyBase>() 를 호출하므로
    ///   EnemyBase 가 먼저 Awake 되어 있어야 함.
    ///   → Inspector 에서 EnemyBase(EnemyKnight 등) 를
    ///     EnemyAI 보다 위에 배치 권장.
    ///   → 순서 보장이 필요하면 Script Execution Order 설정 가능.
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
        // DataSO — Inspector 연결 없음 (v4.0)
        // ──────────────────────────────────────────

        /// <summary>
        /// 적 수치 + 타입 SO.
        /// Inspector 에서 직접 연결하지 않음.
        /// Awake 에서 EnemyBase.Settings 를 통해 취득.
        /// </summary>
        private EnemyDataSO _settings;

        /// <summary>
        /// 차징 돌진 공격 컴포넌트.
        /// Awake 에서 자동 취득. 없으면 차징 없이 일반 공격만.
        /// </summary>
        private EnemyKnightChargeAttack _chargeAttack;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private EnemySensor _sensor;
        private EnemyAttackBase _attack;
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        /// <summary>
        /// 봉인 상태 컴포넌트.
        /// Awake 에서 자동 취득. 미부착 시 null 허용.
        /// </summary>
        private EnemySealComponent _sealComponent;

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
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _sealComponent = GetComponent<EnemySealComponent>();
            _chargeAttack = GetComponent<EnemyKnightChargeAttack>();

            // _attack 은 EnemyKnightAttack(근접) 만 취득
            // GetComponent<EnemyAttackBase>() 는 EnemyKnightChargeAttack 도 반환할 수 있으므로
            // EnemyKnightAttack 을 명시적으로 취득
            _attack = GetComponent<EnemyKnightAttack>() as EnemyAttackBase
                      ?? GetComponent<EnemyAttackBase>();

            // ★ DataSO 는 EnemyBase 에서 가져옴 (Inspector 직접 연결 제거)
            var enemyBase = GetComponent<EnemyBase>();
            if (enemyBase != null)
            {
                _settings = enemyBase.Settings;
            }
            else
            {
                Debug.LogError("[EnemyAI] EnemyBase 컴포넌트를 찾을 수 없습니다. " +
                               "EnemyKnight 등 EnemyBase 상속 컴포넌트가 같은 오브젝트에 있어야 합니다.");
            }
        }

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogError("[EnemyAI] EnemyDataSO 취득 실패. " +
                               "EnemyBase 컴포넌트의 DataSO 슬롯을 확인하세요.");
                enabled = false;
                return;
            }

            // EnemySensor 에 DataSO 주입
            _sensor.SetData(_settings);
            _sensor.SetFacingDirection(_facingDirection);

            // EnemyKnightAttack 에 DataSO 주입
            var knightAttack = GetComponent<EnemyKnightAttack>();
            if (knightAttack != null)
                knightAttack.SetData(_settings);

            // EnemyKnightChargeAttack 에 DataSO 주입
            if (_chargeAttack != null)
                _chargeAttack.SetData(_settings);

            // 근접 공격 완료 이벤트 구독
            if (_attack != null)
                _attack.OnAttackFinished += HandleAttackFinished;

            // 차징 공격 완료 이벤트 구독 (없으면 돌진 후 AI 멈춤)
            if (_chargeAttack != null)
                _chargeAttack.OnAttackFinished += HandleAttackFinished;

            // Dummy 타입은 AI 비활성
            if (_settings.enemyType == EnemyType.Dummy ||
                _settings.enemyType == EnemyType.DummyLocked)
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_attack != null)
                _attack.OnAttackFinished -= HandleAttackFinished;
            if (_chargeAttack != null)
                _chargeAttack.OnAttackFinished -= HandleAttackFinished;
        }

        private void Update() => UpdateState();
        private void FixedUpdate() => UpdateMovement();

        // ══════════════════════════════════════════════════════
        // 봉인 체크
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지정 봉인 타입 활성 여부.
        /// _sealComponent 없으면 항상 false.
        /// </summary>
        private bool IsSealed(SealType sealType)
            => _sealComponent != null && _sealComponent.IsSealedAction(sealType);

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
                    // 차징 감지 범위 안 + 차징 쿨타임 완료 → 차징 공격 우선
                    if (_chargeAttack != null && _chargeAttack.CanAttack
                        && _sensor.CheckChargeRange())
                    {
                        ChangeState(AIState.Attack);
                        return;
                    }
                    // 근접 사정거리 안 → 일반 공격
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

        /// <summary>
        /// 순찰 이동.
        /// Move / Dash 봉인 활성 시 정지.
        /// </summary>
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

        /// <summary>
        /// 추격 이동.
        /// Move 봉인 활성 시 정지.
        /// </summary>
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

        /// <summary>
        /// 공격 상태 진입.
        /// Attack 봉인 활성 시 Chase 복귀.
        /// </summary>
        private void OnEnterAttack()
        {
            StopHorizontal();

            if (IsSealed(SealType.Attack))
            {
                Debug.Log($"[EnemyAI] 공격 봉인 활성 → 차단 ({_settings.enemyName})");
                ChangeState(AIState.Chase);
                return;
            }

            if (_attack == null)
            {
                Debug.LogWarning($"[EnemyAI] EnemyAttackBase 없음. ({_settings.enemyType})");
                ChangeState(AIState.Chase);
                return;
            }

            switch (_settings.enemyType)
            {
                case EnemyType.Knight:
                    // [공격 우선순위]
                    // 차징 감지 범위 안 + 쿨타임 완료 → 차징 돌진 (주력)
                    // 근접 사정거리 안 or 차징 쿨타임 중 → 일반 근접 공격 (보조)
                    bool inChargeRange = _sensor.CheckChargeRange();
                    bool chargeReady = _chargeAttack != null && _chargeAttack.CanAttack;
                    bool inAttackRange = _sensor.CheckAttackRange();
                    bool normalReady = _attack != null && _attack.CanAttack;

                    if (chargeReady && inChargeRange && !inAttackRange)
                    {
                        // 차징 범위 안에 있고 근접 사정거리 밖 → 차징 돌진
                        _chargeAttack.TryAttack(_settings.chargeCooldown);
                        Debug.Log("[EnemyAI] Knight 차징 돌진 실행");
                    }
                    else if (normalReady && inAttackRange)
                    {
                        // 근접 사정거리 안 → 일반 공격
                        _attack.TryAttack(_settings.attackCooldown);
                        Debug.Log("[EnemyAI] Knight 근접 공격 실행");
                    }
                    else if (chargeReady && inChargeRange)
                    {
                        // 근접 사정거리 안이지만 차징도 가능 → 차징 우선
                        _chargeAttack.TryAttack(_settings.chargeCooldown);
                        Debug.Log("[EnemyAI] Knight 차징 돌진 실행 (근접 사정거리 내)");
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
            FlipAttackHitboxes(_facingDirection);
        }

        /// <summary>
        /// 방향 전환 시 적 공격 히트박스 localPosition.x 반전.
        /// EnemyKnightAttack 의 AttackHitbox 와 UpdateChaseDirection 에서도 호출.
        /// </summary>
        private void FlipAttackHitboxes(float dir)
        {
            // EnemyKnightAttack 히트박스 반전
            var knightAttack = GetComponent<EnemyKnightAttack>();
            if (knightAttack != null)
                knightAttack.FlipHitbox(dir);
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
            FlipAttackHitboxes(_facingDirection);
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