// ============================================================
// EnemyAI.cs  v3.0
// 적 공용 AI — 봉인 시스템 연동
//
// [v3.0 변경 — EnemySealComponent 연동]
//   Awake 에서 EnemySealComponent 자동 취득.
//   행동 실행 직전 IsSealedAction(SealType) 체크 추가.
//
//   [체크 위치 3곳]
//     OnPatrolMove()  → SealType.Move / SealType.Dash
//     OnChaseMove()   → SealType.Move
//     OnEnterAttack() → SealType.Attack
//
//   [SealType.Move]
//     Patrol / Chase 이동 전부 차단.
//     이동 속도를 0 으로 유지하고 현재 상태를 변경하지 않음.
//     봉인 해제 시 자동으로 이동 재개.
//
//   [SealType.Dash]
//     OnPatrolMove 에서 Knight 의 돌진 유사 패턴 차단 전용.
//     현재 Patrol 이동을 차단하여 전진 억제.
//     추후 실제 Dash 패턴 추가 시 해당 위치에도 체크 추가.
//
//   [SealType.Attack]
//     OnEnterAttack() 진입 시 체크.
//     봉인 중이면 공격 실행 없이 Chase 로 복귀.
//
// [v2.0 변경]
//   KnightAI 제거 → 이 파일 하나로 모든 적 AI 통합.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 공용 AI 컴포넌트. (v3.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [봉인 체크 흐름]
    ///   SealProjectile → EnemySealComponent.ApplySeal()
    ///     → _activeSeals 에 SealType 등록
    ///       → EnemyAI 행동 시도 시 IsSealedAction() 확인
    ///         → true 이면 해당 행동 스킵
    ///
    /// [봉인 해제 시]
    ///   EnemySealComponent.Update() 에서 타이머 만료 감지
    ///     → _activeSeals 에서 해당 타입 제거
    ///       → 다음 프레임부터 EnemyAI 가 IsSealedAction() = false 로 판단
    ///         → 행동 자동 재개
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

        /// <summary>
        /// 봉인 상태 컴포넌트. v3.0 추가.
        /// Awake 에서 자동 취득. 미부착 시 null 허용
        /// (봉인 시스템 없이도 정상 동작).
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
            _attack = GetComponent<EnemyAttackBase>();
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // 봉인 컴포넌트 취득 — 없어도 경고 없음 (선택적 기능)
            _sealComponent = GetComponent<EnemySealComponent>();
        }

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogError($"[EnemyAI] EnemyDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            _sensor.SetData(_settings);
            _sensor.SetFacingDirection(_facingDirection);

            var knightAttack = GetComponent<EnemyKnightAttack>();
            if (knightAttack != null)
                knightAttack.SetData(_settings);

            if (_attack != null)
                _attack.OnAttackFinished += () => ChangeState(AIState.Chase);

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
        // 봉인 체크 — 내부 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지정 봉인 타입이 현재 활성 중인지 확인.
        ///
        /// [null 안전]
        ///   _sealComponent 가 없으면 항상 false 반환.
        ///   봉인 컴포넌트 미부착 적은 봉인 효과를 받지 않음.
        /// </summary>
        /// <param name="sealType">확인할 봉인 타입</param>
        /// <returns>봉인 활성 중이면 true, 미활성 or 컴포넌트 없으면 false</returns>
        private bool IsSealed(SealType sealType)
        {
            return _sealComponent != null && _sealComponent.IsSealedAction(sealType);
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
        // 상태별 행동 — EnemyType 분기 + 봉인 체크
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 순찰 이동.
        ///
        /// [봉인 체크]
        ///   SealType.Move  : 이동 전체 차단 → 정지
        ///   SealType.Dash  : Knight 전진 패턴 차단 → 정지
        ///   (두 봉인 중 하나라도 활성이면 정지)
        ///
        /// [봉인 해제 후]
        ///   다음 프레임 UpdateMovement() 에서 자동 재개.
        /// </summary>
        private void OnPatrolMove()
        {
            // Move 또는 Dash 봉인 활성 시 순찰 이동 정지
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

                // 추후 Drone 등 추가
                default:
                    break;
            }
        }

        /// <summary>
        /// 추격 이동.
        ///
        /// [봉인 체크]
        ///   SealType.Move : 이동 전체 차단 → 정지.
        ///   Dash 봉인은 추격 이동에는 영향 없음
        ///   (추격은 Dash 가 아닌 일반 이동 패턴).
        /// </summary>
        private void OnChaseMove()
        {
            // Move 봉인 활성 시 추격 이동 정지
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

                default:
                    break;
            }
        }

        /// <summary>
        /// 공격 상태 진입.
        ///
        /// [봉인 체크]
        ///   SealType.Attack : 공격 실행 차단 → 즉시 Chase 복귀.
        ///   봉인 중에는 Attack 상태에 머무는 것 자체를 막음.
        ///   Chase 로 복귀 후 사정거리 안에 있으면 재진입 시도하지만
        ///   봉인이 유지되는 한 계속 차단됨.
        ///
        /// [SealType.Ranged]
        ///   원거리 공격 전용 봉인.
        ///   EnemyAttackBase 하위 클래스가 원거리 공격이라면
        ///   해당 ExecuteAttack() 내부에서 별도 체크 권장.
        ///   여기서는 Attack 봉인만 전역 차단.
        /// </summary>
        private void OnEnterAttack()
        {
            StopHorizontal();

            // Attack 봉인 활성 시 공격 불가 → Chase 복귀
            if (IsSealed(SealType.Attack))
            {
                Debug.Log($"[EnemyAI] 공격 봉인 활성 — 공격 차단 ({_settings.enemyName})");
                ChangeState(AIState.Chase);
                return;
            }

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
            if (!Mathf.Approximately(dir, _facingDirection))
            {
                _facingDirection = dir;
                if (_spriteRenderer != null)
                    _spriteRenderer.flipX = _facingDirection < 0f;
                _sensor.SetFacingDirection(_facingDirection);
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

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_sealComponent == null || !_sealComponent.HasAnySeal) return;

            // 봉인 활성 시 AI 위에 [봉인] 표시
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"[AI봉인] {_sealComponent.SealCount}개");
        }
#endif
    }
}