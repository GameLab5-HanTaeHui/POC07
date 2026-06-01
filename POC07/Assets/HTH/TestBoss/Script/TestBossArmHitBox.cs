// ============================================================
// TestBossArmHitbox.cs  v1.0
// 테스트 보스 팔 히트박스 — 피격 수신 컴포넌트
//
// [역할]
//   Arm_L / Arm_R 오브젝트에 부착.
//   OnTriggerEnter2D 로 Player 충돌을 수신하여
//   연결된 패턴(TestBossPatternBase) 에 피격 사실을 전달.
//
// [문제 해결]
//   패턴 스크립트(PunchDown/PunchShot)와 Collider(Arm_L/R)가
//   다른 오브젝트에 있어 OnTriggerEnter2D 가 패턴에 전달 안 됨.
//   → 이 컴포넌트를 Arm_L/R 에 부착하여 수신 후 패턴에 위임.
//
// [Prefab 설정]
//   Arm_L 에 부착 → _pattern = PunchDown 연결
//   Arm_R 에 부착 → _pattern = PunchShot 연결
//   _playerLayer  = Player 레이어
//   Arm_L/R Layer = EnemyAttackHit (16)
//   Arm_L/R BoxCollider2D IsTrigger = true
//
// [Physics2D Matrix 요구사항]
//   EnemyAttackHit ↔ Player 충돌 ON 필요
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 팔 히트박스 피격 수신 컴포넌트. (v1.0)
    /// Arm_L / Arm_R 오브젝트에 부착하여 Player 충돌을 감지.
    /// </summary>
    public class TestBossArmHitbox : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 패턴 연결 (필수) ──────────────────────")]

        /// <summary>
        /// 이 팔을 사용하는 패턴.
        /// Arm_L → PunchDown 의 TestBossPattern_PunchDown
        /// Arm_R → PunchShot 의 TestBossPattern_PunchShot
        /// </summary>
        [Tooltip("이 팔을 사용하는 패턴 컴포넌트.")]
        [SerializeField] private TestBossPatternBase _pattern;

        /// <summary>
        /// 피격 데미지.
        /// 패턴 스크립트의 _punchDamage 와 동일하게 설정.
        /// </summary>
        [Tooltip("피격 데미지.")]
        [Min(0f)]
        [SerializeField] private float _damage = 15f;

        [Header("── 레이어 ──────────────────────")]

        /// <summary>
        /// 플레이어 감지 레이어.
        /// Player 레이어 선택.
        /// </summary>
        [Tooltip("플레이어 감지 레이어. Player 레이어 선택.")]
        [SerializeField] private LayerMask _playerLayer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 이 패턴이 Active 중인지 여부.
        /// 패턴이 실행 중일 때만 피격 판정.
        /// </summary>
        private bool _isActive;

        /// <summary>
        /// 이번 Active 구간에서 이미 피격했는지.
        /// 한 패턴당 1회만 피격.
        /// </summary>
        private bool _hasHitThisSwing;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Start()
        {
            if (_pattern != null)
            {
                _pattern.OnPatternStart += HandlePatternStart;
                _pattern.OnPatternEnd += HandlePatternEnd;
            }
        }

        private void OnDestroy()
        {
            if (_pattern != null)
            {
                _pattern.OnPatternStart -= HandlePatternStart;
                _pattern.OnPatternEnd -= HandlePatternEnd;
            }
        }

        // ══════════════════════════════════════════════════════
        // 패턴 이벤트 수신
        // ══════════════════════════════════════════════════════

        private void HandlePatternStart(TestBossPatternBase p)
        {
            _isActive = true;
            _hasHitThisSwing = false;
        }

        private void HandlePatternEnd(TestBossPatternBase p)
        {
            _isActive = false;
            _hasHitThisSwing = false;
        }

        // ══════════════════════════════════════════════════════
        // 충돌 감지
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Player 레이어 충돌 수신.
        /// 패턴 Active 중 + 미피격 상태일 때만 TakeDamage 호출.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isActive || _hasHitThisSwing) return;

            // 레이어 체크
            int layer = 1 << other.gameObject.layer;
            if ((_playerLayer.value & layer) == 0) return;

            if (!other.TryGetComponent<IDamageable>(out var damageable)) return;
            // 보스 자신 제외
            if (other.GetComponentInParent<TestBossCore>() != null) return;

            // 공격 방향: 팔 → 플레이어
            Vector2 dir = ((Vector2)other.transform.position
                - (Vector2)transform.position).normalized;

            var info = new DamageInfo(
                transform.position,
                _damage,
                dir,
                AttackType.Combo1);

            damageable.TakeDamage(info);
            _hasHitThisSwing = true;

            Debug.Log($"[TestBossArmHitbox] {gameObject.name} → 플레이어 피격: -{_damage}");
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 외부에서 Active 강제 해제 (패턴 Interrupt 시).
        /// </summary>
        public void ForceDeactivate()
        {
            _isActive = false;
            _hasHitThisSwing = false;
        }
    }
}