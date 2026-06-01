// ============================================================
// TestBossPattern_Charge.cs  v1.0
// 테스트 보스 돌진 패턴
//
// [역할]
//   보스가 플레이어 방향으로 빠르게 돌진한다.
//   돌진 후 Recovery 구간에서 그로기를 유도 →
//   플레이어에게 팔 처형 기회 제공.
//
// [3단계 흐름]
//   Warning  (1.0초)
//     보스 본체를 주황색으로 변경 (예고)
//     플레이어에게 돌진 방향 인식 시간 제공
//
//   Active
//     플레이어 방향으로 chargeSpeed 속도로 돌진
//     _chargeDuration 동안 이동
//     벽 충돌(wall) 또는 시간 초과 → 돌진 종료
//     히트박스: 보스 본체 Collider2D 사용 (트리거)
//
//   Recovery (0.8초)
//     정지 + 빨간색 변경 (경직 피드백)
//     완료 후 OnPatternGroggy 발행 → TestBossAI.EnterGroggy()
//     → 플레이어 A키 홀드 처형 구간 시작
//
// [벽 충돌 그로기]
//   OnTriggerEnter2D 에서 Wall 레이어 감지
//   → Active 도중 즉시 TriggerGroggy() 발행 (추가 그로기)
//   → 더 긴 그로기 제공 가능 (DataSO: chargeWallGroggyDuration)
//
// [Prefab 연결]
//   TestBoss 자식 오브젝트 "Pattern_Charge" 에 부착.
//   _hitbox : 보스 본체 BoxCollider2D (IsTrigger = true)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 돌진 패턴. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [그로기 유도 구조]
    ///   방법 A — Recovery 완료: _triggerGroggyOnRecovery = true (기본)
    ///   방법 B — 벽 충돌: Active 중 Wall 레이어 감지 → TriggerGroggy() 즉시 발행
    ///
    ///   방법 B 가 발동하면 Recovery 진입 시 추가로 발행되지 않도록
    ///   _wallHit 플래그로 관리.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossPattern_Charge : TestBossPatternBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 돌진 설정 ──────────────────────")]

        /// <summary>
        /// 돌진 속도 (units/s).
        /// </summary>
        [Tooltip("돌진 속도 (units/s). 권장: 8~15.")]
        [Min(1f)]
        [SerializeField] private float _chargeSpeed = 10f;

        /// <summary>
        /// 돌진 최대 지속 시간 (초).
        /// 벽 충돌 없으면 이 시간 후 Active 종료.
        /// </summary>
        [Tooltip("돌진 최대 지속 시간 (초). 권장: 0.5~1.5.")]
        [Min(0.1f)]
        [SerializeField] private float _chargeDuration = 0.8f;

        /// <summary>
        /// 돌진 히트박스 데미지.
        /// 플레이어 접촉 시 적용.
        /// </summary>
        [Tooltip("돌진 히트박스 데미지.")]
        [Min(0f)]
        [SerializeField] private float _chargeDamage = 10f;

        /// <summary>
        /// 벽 충돌 레이어 마스크.
        /// Wall / Ground 레이어를 포함.
        /// </summary>
        [Tooltip("벽 충돌 감지 레이어 마스크.")]
        [SerializeField] private LayerMask _wallLayerMask;

        [Header("── 컴포넌트 연결 ──────────────────────")]

        /// <summary>
        /// 돌진 히트박스 Collider2D.
        /// Active 중에만 활성화.
        /// 미연결 시 GetComponent 로 자동 탐색.
        /// </summary>
        [Tooltip("돌진 히트박스 Collider2D. 미연결 시 자동 탐색.")]
        [SerializeField] private Collider2D _hitbox;

        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        /// <summary> 보스 본체의 기본 색상. Warning/Recovery 피드백 후 복구에 사용. </summary>
        private Color _defaultColor;

        /// <summary> 돌진 방향. Active 시작 시 플레이어 방향으로 결정. </summary>
        private float _chargeDirection;

        /// <summary>
        /// 벽 충돌 발생 플래그.
        /// Active 코루틴 종료 조건 + Recovery 중복 그로기 방지.
        /// </summary>
        private bool _wallHit;

        /// <summary> 플레이어 Transform. Awake 에서 탐색. </summary>
        private Transform _playerTransform;

        // ──────────────────────────────────────────
        // 색상 피드백 정의
        // ──────────────────────────────────────────

        private static readonly Color _warningColor = new Color(1f, 0.5f, 0.1f, 1f); // 주황
        private static readonly Color _recoveryColor = new Color(0.8f, 0.2f, 0.2f, 1f); // 빨강

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _rigid2D = GetComponentInParent<Rigidbody2D>();
            _spriteRenderer = GetComponentInParent<SpriteRenderer>();

            if (_spriteRenderer != null)
                _defaultColor = _spriteRenderer.color;

            if (_hitbox == null)
                _hitbox = GetComponent<Collider2D>();

            // 플레이어 탐색
            var players = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (players.Length > 0)
                _playerTransform = players[0].transform;

            // 히트박스 초기 비활성
            if (_hitbox != null)
                _hitbox.enabled = false;

            // Recovery 후 그로기 유도 활성
            _triggerGroggyOnRecovery = true;
        }

        // ══════════════════════════════════════════════════════
        // 3단계 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Warning 단계.
        /// 보스 색상을 주황색으로 변경하여 돌진 예고.
        /// _warningDuration 동안 대기.
        /// </summary>
        protected override IEnumerator OnWarning()
        {
            // 돌진 방향 결정 (Warning 시점 기준)
            if (_playerTransform != null)
            {
                _chargeDirection = _playerTransform.position.x > transform.position.x
                    ? 1f : -1f;
            }
            else
            {
                _chargeDirection = 1f;
            }

            // 예고 색상
            SetBodyColor(_warningColor);

            yield return WaitScaled(_warningDuration);

            // 색상 복구
            SetBodyColor(_defaultColor);
        }

        /// <summary>
        /// Active 단계.
        /// _chargeDirection 방향으로 _chargeSpeed 로 이동.
        /// _chargeDuration 초 후 또는 벽 충돌 시 종료.
        /// 히트박스 활성 중 플레이어 접촉 → TakeDamage().
        /// </summary>
        protected override IEnumerator OnActive()
        {
            if (_rigid2D == null) yield break;

            _wallHit = false;

            // 히트박스 활성
            if (_hitbox != null) _hitbox.enabled = true;

            float elapsed = 0f;

            while (elapsed < _chargeDuration && !_isInterrupted)
            {
                // 벽 충돌 → 즉시 종료
                if (_wallHit) break;

                _rigid2D.linearVelocity = new Vector2(
                    _chargeDirection * _chargeSpeed,
                    _rigid2D.linearVelocity.y);

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            // 정지
            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);

            // 히트박스 비활성
            if (_hitbox != null) _hitbox.enabled = false;

            // 벽 충돌 시 즉시 그로기 발행 (Recovery 전)
            if (_wallHit && !_isInterrupted)
            {
                Debug.Log("[TestBossPattern_Charge] 벽 충돌 → 즉시 그로기 발행");
                TriggerGroggy();
                // Recovery 에서 중복 발행하지 않도록 플래그 유지
                _triggerGroggyOnRecovery = false;
            }
        }

        /// <summary>
        /// Recovery 단계.
        /// 보스 색상을 빨간색으로 변경하여 경직 피드백.
        /// _recoveryDuration 동안 대기 후 복구.
        /// 벽 충돌이 아닌 정상 돌진 완료 시 그로기 유도.
        /// </summary>
        protected override IEnumerator OnRecovery()
        {
            SetBodyColor(_recoveryColor);

            yield return WaitScaled(_recoveryDuration);

            SetBodyColor(_defaultColor);

            // 다음 실행을 위해 그로기 트리거 플래그 복구
            _triggerGroggyOnRecovery = true;
        }

        // ══════════════════════════════════════════════════════
        // 물리 충돌
        // ══════════════════════════════════════════════════════

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 벽 충돌 감지
            if ((_wallLayerMask.value & (1 << other.gameObject.layer)) != 0)
            {
                _wallHit = true;
                return;
            }

            // 플레이어 피격
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                var info = new DamageInfo(
                    transform.position,
                    _chargeDamage,
                    new Vector2(_chargeDirection, 0f),
                    AttackType.Combo1);

                damageable.TakeDamage(info);
                Debug.Log($"[TestBossPattern_Charge] 플레이어 피격: -{_chargeDamage}");
            }
        }

        // ══════════════════════════════════════════════════════
        // 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 보스 본체 SpriteRenderer 색상 설정.
        /// </summary>
        private void SetBodyColor(Color color)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = color;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (!IsExecuting) return;

            // 돌진 방향 시각화
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position,
                new Vector3(_chargeDirection * 3f, 0f, 0f));
        }
    }
}