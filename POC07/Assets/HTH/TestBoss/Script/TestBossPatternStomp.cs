// ============================================================
// TestBossPattern_Stomp.cs  v1.0
// 테스트 보스 제자리 광역 패턴 (Stomp)
//
// [역할]
//   보스가 제자리에서 광역 충격을 발생시킨다.
//   단순 데미지 패턴 — 그로기 없음.
//   Charge 패턴과 교차 사용하여 패턴 다양성 제공.
//
// [3단계 흐름]
//   Warning  (1.2초)
//     보스 색상을 보라색으로 변경 (예고)
//     플레이어에게 패턴 인식 시간 제공
//     → 일정 거리 이상 떨어지면 데미지 회피 가능
//
//   Active
//     보스 주변 _stompRadius 반경 내 플레이어 감지
//     Physics2D.OverlapCircleAll 로 한 번 판정
//     히트 시 데미지 + 넉백
//
//   Recovery (1.0초)
//     색상 복구 + 경직
//     _triggerGroggyOnRecovery = false — 그로기 없음
//     (Stomp 는 단순 공격 패턴이므로 처형 기회 미제공)
//
// [TestBossAI 패턴 선택 조건]
//   CanExecute 기본값 사용 (쿨타임 + 실행 중 아님)
//
// [Prefab 연결]
//   TestBoss 자식 오브젝트 "Pattern_Stomp" 에 부착.
//   별도 히트박스 불필요 — Physics2D.OverlapCircleAll 사용.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 제자리 광역 패턴. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [Charge 패턴과의 차이]
    ///   Charge  : 이동 + 그로기 유도 (처형 기회)
    ///   Stomp   : 제자리 + 광역 데미지 (압박, 그로기 없음)
    ///
    ///   두 패턴이 교차하면서 플레이어에게 접근/회피 판단을 요구.
    ///   Stomp 후 Charge 가 나오면 → 처형 기회.
    ///   Stomp 만 반복되면 → 안전 구역 확보 후 대기.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossPattern_Stomp : TestBossPatternBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 광역 설정 ──────────────────────")]

        /// <summary>
        /// 광역 판정 반경 (units).
        /// 보스 위치 기준 이 반경 내 플레이어에게 데미지.
        /// </summary>
        [Tooltip("광역 판정 반경 (units). 권장: 3.0~6.0.")]
        [Min(0.5f)]
        [SerializeField] private float _stompRadius = 4.0f;

        /// <summary>
        /// 광역 데미지.
        /// </summary>
        [Tooltip("광역 데미지.")]
        [Min(0f)]
        [SerializeField] private float _stompDamage = 15f;

        /// <summary>
        /// 광역 판정 레이어 마스크.
        /// Player 레이어를 포함.
        /// </summary>
        [Tooltip("광역 판정 레이어. Player 레이어 포함.")]
        [SerializeField] private LayerMask _playerLayerMask;

        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        private SpriteRenderer _spriteRenderer;
        private Color _defaultColor;

        // ──────────────────────────────────────────
        // 색상 피드백 정의
        // ──────────────────────────────────────────

        private static readonly Color _warningColor = new Color(0.7f, 0.2f, 1.0f, 1f); // 보라
        private static readonly Color _activeColor = new Color(1.0f, 1.0f, 0.2f, 1f); // 노랑 (시전)

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _spriteRenderer = GetComponentInParent<SpriteRenderer>();

            if (_spriteRenderer != null)
                _defaultColor = _spriteRenderer.color;

            // Stomp 는 그로기 유도 없음 — 단순 공격 패턴
            _triggerGroggyOnRecovery = false;
        }

        // ══════════════════════════════════════════════════════
        // 3단계 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Warning 단계.
        /// 보라색으로 변경하여 광역 예고.
        /// </summary>
        protected override IEnumerator OnWarning()
        {
            SetBodyColor(_warningColor);

            yield return WaitScaled(_warningDuration);

            SetBodyColor(_defaultColor);
        }

        /// <summary>
        /// Active 단계.
        /// 즉발 광역 판정 한 번.
        /// Physics2D.OverlapCircleAll 로 플레이어 감지 → TakeDamage().
        /// </summary>
        protected override IEnumerator OnActive()
        {
            SetBodyColor(_activeColor);

            // 즉발 광역 판정
            var hits = Physics2D.OverlapCircleAll(
                transform.position,
                _stompRadius,
                _playerLayerMask);

            foreach (var col in hits)
            {
                if (col.TryGetComponent<IDamageable>(out var damageable))
                {
                    Vector2 dir = ((Vector2)col.transform.position
                        - (Vector2)transform.position).normalized;

                    var info = new DamageInfo(
                        transform.position,
                        _stompDamage,
                        dir,
                        AttackType.Combo1);

                    damageable.TakeDamage(info);
                    Debug.Log($"[TestBossPattern_Stomp] 플레이어 피격: -{_stompDamage}");
                }
            }

            // 한 프레임 대기 (즉발이지만 코루틴 구조 유지)
            yield return null;

            SetBodyColor(_defaultColor);
        }

        /// <summary>
        /// Recovery 단계.
        /// 경직 대기. 그로기 없음.
        /// </summary>
        protected override IEnumerator OnRecovery()
        {
            yield return WaitScaled(_recoveryDuration);
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            // 광역 범위 시각화
            Gizmos.color = IsExecuting
                ? new Color(1f, 1f, 0.2f, 0.4f)
                : new Color(0.7f, 0.2f, 1.0f, 0.2f);

            Gizmos.DrawWireSphere(transform.position, _stompRadius);
        }

        // ──────────────────────────────────────────
        // 유틸리티
        // ──────────────────────────────────────────

        private void SetBodyColor(Color color)
        {
            if (_spriteRenderer != null)
                _spriteRenderer.color = color;
        }
    }
}