// ============================================================
// EnemyAI.cs  v5.0
// 적 공용 AI — 리모델링 (기사형 차징 전용)
//
// [v5.0 리모델링 변경]
//
//   ① _attack (EnemyKnightAttack 근접 공격) 참조 제거
//       기사형은 차징 돌진만 사용.
//       근접 공격 컴포넌트(EnemyKnightAttack) 불필요.
//       _chargeAttack 하나만 유지.
//
//   ② CheckAttackRange() 제거
//       EnemySensor v2.0 에서 attackRange 관련 메서드 제거됨.
//       Chase 상태에서 CheckChargeRange() 만 체크.
//
//   ③ OnEnterAttack() 단순화
//       Knight: 차징 쿨타임 완료 + 차징 범위 → 차징 실행.
//               쿨타임 중 → Chase 복귀.
//
//   ④ HandleNormalAttackFinished() 제거
//       근접 공격 완료 핸들러 불필요.
//       차징 완료 or 벽 충돌 → EnterGroggy() 만 남음.
//
// [v4.4 변경]
//   FlipAttackHitboxes() 제거 → OnFlipped 이벤트 발행.
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
    /// <summary>
    /// 적 공용 AI 컴포넌트. (v5.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [상태 전환 다이어그램]
    ///
    ///   Patrol ──(전방 감지)──────→ Chase
    ///   Patrol ──(벽/낭떠러지)────→ Flip → TryIdle
    ///   Idle   ──(대기 완료)──────→ Patrol
    ///   Idle   ──(플레이어 감지)──→ Chase
    ///   Chase  ──(범위 이탈)──────→ Patrol
    ///   Chase  ──(차징 범위 진입)──→ Attack (차징 실행)
    ///   Attack ──(완료/정상 도달)──→ Groggy(groggyDuration) → Chase
    ///   Attack ──(벽 충돌)────────→ Groggy(groggyDuration) → Chase  (ChargeAttack 직접 호출)
    ///   Attack ──(봉인 취소)──────→ Groggy(groggyDuration) → Chase  (ChargeAttack 직접 호출)
    ///   Groggy ──(종료)──────────→ TurnTowardPlayer → Chase
    ///
    /// [Flip 구조]
    ///   SetFacing(dir) → SpriteRenderer.flipX + Sensor.SetFacingDirection + OnFlipped 이벤트
    ///   OnFlipped 구독자들이 각자 히트박스 / 방패 / 자물쇠 위치 반전 처리.
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(EnemySensor))]
    public class EnemyAI : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 상태 열거형
        // ──────────────────────────────────────────

        public enum AIState
        {
            /// <summary> 순찰 — 좌우 이동, 전방 감지. </summary>
            Patrol,
            /// <summary> 랜덤 정지 — 대기 후 Patrol 복귀. </summary>
            Idle,
            /// <summary> 추격 — 플레이어 추적. </summary>
            Chase,
            /// <summary> 공격 — 차징 돌진 실행 중. </summary>
            Attack,
            /// <summary>
            /// 그로기 — 완전 정지.
            /// 돌진 벽 충돌 / 봉인 취소 / 정상 완료 후 진입.
            /// groggyDuration 후 플레이어 방향 전환 → Chase 복귀.
            /// 플레이어가 Lock 을 공격하는 핵심 타이밍.
            /// </summary>
            Groggy,
        }

        // ──────────────────────────────────────────
        // 내부 데이터 — Inspector 연결 없음
        // ──────────────────────────────────────────

        private EnemyDataSO _settings;
        private EnemyKnightChargeAttack _chargeAttack;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private EnemySensor _sensor;
        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;
        private SealComponent _sealComponent;
        private EnemyKnightAttack _meleeAttack;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private AIState _currentState = AIState.Patrol;
        private float _facingDirection = 1f;
        private float _flipCooldownTimer;
        private Coroutine _idleCoroutine;
        private Coroutine _groggyCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 방향 전환 시 발행. 파라미터: 새 방향 (+1 오른쪽, -1 왼쪽).
        ///
        /// [구독자 목록 — 각자 자체 처리]
        ///   EnemyKnight         : ShieldCollider localPosition.x 반전
        ///   LockComponent       : Lock localPosition.x 반전
        ///   EnemyKnightChargeAttack : ChargeHitbox localPosition.x 반전
        ///
        /// EnemyAI 는 구독자가 무엇인지 알 필요 없음.
        /// 새 컴포넌트 추가 시 해당 컴포넌트의 Start() 에서 구독만 추가.
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
            _sealComponent = GetComponent<SealComponent>();
            _chargeAttack = GetComponent<EnemyKnightChargeAttack>();
            _meleeAttack = GetComponent<EnemyKnightAttack>();

            // DataSO 는 EnemyBase 에서 취득
            var enemyBase = GetComponent<EnemyBase>();
            if (enemyBase != null)
                _settings = enemyBase.Settings;
            else
                Debug.LogError("[EnemyAI] EnemyBase 가 없습니다.");
        }

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogError("[EnemyAI] EnemyDataSO 취득 실패. EnemyBase 슬롯을 확인하세요.");
                enabled = false;
                return;
            }

            _sensor.SetData(_settings);
            _sensor.SetFacingDirection(_facingDirection);

            if (_chargeAttack != null)
            {
                _chargeAttack.SetData(_settings);
                _chargeAttack.OnAttackFinished += HandleChargeAttackFinished;
            }

            if (_meleeAttack != null)
            {
                _meleeAttack.SetData(_settings);
                _meleeAttack.OnAttackFinished += HandleMeleeAttackFinished;
            }

            // Dummy 타입은 AI 비활성
            if (_settings.enemyType == EnemyType.Dummy ||
                _settings.enemyType == EnemyType.DummyLocked)
            {
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_chargeAttack != null)
                _chargeAttack.OnAttackFinished -= HandleChargeAttackFinished;
            if (_meleeAttack != null)
                _meleeAttack.OnAttackFinished -= HandleMeleeAttackFinished;
        }

        private void Update()
        {
            if (_flipCooldownTimer > 0f)
                _flipCooldownTimer -= Time.deltaTime;
            UpdateState();
        }
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
        /// EnemyKnightChargeAttack 에서 다음 상황에 호출:
        ///   ① 돌진 중 벽 충돌
        ///   ② 카운트다운 중 Dash 봉인 감지 → 취소
        /// 차징 정상 완료 시에는 HandleChargeAttackFinished() 에서 호출.
        /// </summary>
        /// <param name="duration">그로기 시간. 0 이하면 groggyDuration 사용.</param>
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
                    // 추격 범위 이탈 → Patrol
                    if (!_sensor.CheckChaseRange())
                    {
                        ChangeState(AIState.Patrol);
                        return;
                    }

                    // Dash 봉인 중 → 차징 시도 스킵, 플레이어 방향만 유지
                    // (OnEnterAttack 에서 Chase 복귀 → 매 프레임 무한 반복 방지)
                    if (IsSealed(SealType.Dash))
                    {
                        UpdateChaseDirection();
                        break;
                    }

                    // 차징 발동 범위 + 쿨타임 완료 → Attack
                    if (_chargeAttack != null
                        && _chargeAttack.CanAttack
                        && _sensor.CheckChargeRange())
                    {
                        ChangeState(AIState.Attack);
                        return;
                    }

                    UpdateChaseDirection();
                    break;

                case AIState.Attack:
                    // 차징 완료는 HandleChargeAttackFinished / EnterGroggy 이벤트로 처리
                    break;

                case AIState.Groggy:
                    // GroggyRoutine 코루틴이 처리
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

            if (_settings == null) return;
            _rigid2D.linearVelocity = new Vector2(
                _facingDirection * _settings.patrolSpeed,
                _rigid2D.linearVelocity.y);
        }

        private void OnChaseMove()
        {
            if (IsSealed(SealType.Move))
            {
                StopHorizontal();
                return;
            }

            if (_settings == null) return;
            _rigid2D.linearVelocity = new Vector2(
                _facingDirection * _settings.chaseSpeed,
                _rigid2D.linearVelocity.y);
        }

        /// <summary>
        /// 공격(차징) 상태 진입.
        /// 차징 쿨타임 완료 시 차징 실행.
        /// 봉인 or 쿨타임 중이면 Chase 복귀.
        /// </summary>
        private void OnEnterAttack()
        {
            StopHorizontal();

            // 중복 진입 차단
            if (_chargeAttack != null && _chargeAttack.IsAttacking)
                return;

            // Attack 봉인 → Chase 복귀
            if (IsSealed(SealType.Attack))
            {
                ChangeState(AIState.Chase);
                return;
            }

            // Dash 봉인 활성 → 차징 불가 → 근접 공격 시도
            if (IsSealed(SealType.Dash))
            {
                // 근접 사정거리 안 + 쿨타임 완료 → 근접 1타
                if (_meleeAttack != null
                    && _meleeAttack.CanAttack
                    && _sensor.CheckMeleeRange())
                {
                    _meleeAttack.TryAttack(_meleeAttack.MeleeCooldown);
                    Debug.Log("[EnemyAI] Dash 봉인 → 근접 1타 실행");
                    return;
                }

                // 사정거리 밖 or 쿨타임 중 → Chase 유지 (플레이어 추격)
                Debug.Log("[EnemyAI] Dash 봉인 → 근접 범위 밖, Chase 유지");
                ChangeState(AIState.Chase);
                return;
            }

            // 차징 공격 실행
            if (_chargeAttack != null && _chargeAttack.CanAttack)
            {
                _chargeAttack.TryAttack(_chargeAttack.ChargeCooldown);
                Debug.Log("[EnemyAI] Knight 차징 돌진 실행");
                return;
            }

            // 차징 쿨타임 중 → Chase 복귀
            ChangeState(AIState.Chase);
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 차징 정상 완료 (목표 거리 도달) → Groggy 진입.
        /// </summary>
        private void HandleChargeAttackFinished()
        {
            EnterGroggy();
        }

        /// <summary>
        /// 근접 1타 완료 → 짧은 Groggy → Chase 복귀.
        /// 차징보다 훨씬 짧은 경직. 플레이어 추격 재개.
        /// </summary>
        private void HandleMeleeAttackFinished()
        {
            float shortGroggy = _settings != null ? _settings.groggyDuration * 0.25f : 0.5f;
            EnterGroggy(shortGroggy);
        }

        // ══════════════════════════════════════════════════════
        // Groggy 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Groggy 코루틴.
        /// groggyDuration 동안 완전 정지.
        /// 종료 시 플레이어 방향 전환 → Chase 복귀.
        /// </summary>
        private IEnumerator GroggyRoutine(float duration)
        {
            Debug.Log($"[EnemyAI] Groggy 진입 ({duration:F1}초)");
            StopHorizontal();

            yield return new WaitForSeconds(duration);

            // Groggy 종료 → 플레이어 방향 전환
            TurnTowardPlayer();

            _groggyCoroutine = null;
            Debug.Log("[EnemyAI] Groggy 종료 → Chase 복귀");
            ApplyState(AIState.Chase);
        }

        /// <summary>
        /// DetectedPlayer 방향으로 즉시 전환.
        /// Groggy 종료 시 호출.
        /// 플레이어가 뒤에 있을 때 등 뒤를 보이지 않도록 정면을 맞춤.
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
        // 이동 보조
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

            // 방향 전환 쿨타임 체크
            // 쿨타임 중이면 방향 전환 안 함 → 플레이어가 등 뒤 공략 시간 확보
            if (_flipCooldownTimer > 0f) return;

            _flipCooldownTimer = _settings != null ? _settings.flipCooldown : 2.0f;
            SetFacing(dir);
        }

        /// <summary>
        /// 방향 설정 통합 함수.
        /// SpriteRenderer.flipX + EnemySensor.SetFacingDirection + OnFlipped 이벤트 발행.
        ///
        /// [OnFlipped 구독자들의 처리]
        ///   EnemyKnight         : ShieldCollider localPosition.x = +originalX * dir (정면)
        ///   LockComponent       : localPosition.x = -originalX * dir (후방)
        ///   EnemyKnightChargeAttack : ChargeHitbox localPosition.x 반전
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
            // 봉인 상태 표시
            if (_sealComponent != null && _sealComponent.HasAnySeal)
            {
                UnityEditor.Handles.color = Color.cyan;
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2.5f,
                    $"[봉인] {_sealComponent.SealCount}개");
            }

            // 현재 상태 표시
            UnityEditor.Handles.color = _currentState switch
            {
                AIState.Groggy => Color.yellow,
                AIState.Attack => Color.red,
                AIState.Chase => Color.green,
                _ => Color.white,
            };
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.0f,
                $"[{_currentState}]");
        }
#endif
    }
}