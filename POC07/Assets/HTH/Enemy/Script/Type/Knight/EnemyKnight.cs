// ============================================================
// EnemyKnight.cs  v1.1
// 기사형 적 — EnemyBase 상속
//
// [v1.1 변경]
//   KnightAI 참조 → EnemyAI 참조로 교체 (단일 AI 컴포넌트 통합).
//
// [역할]
//   EnemyBase 를 상속하여 기사형 전용 피격 로직 구현.
//   정면 방패: 정면 공격 시 본체 피격 무시.
//   등 뒤 자물쇠: LockComponent 해제 후 본체 피격 정상 처리.
//
// [피격 판단 흐름]
//   TakeDamage(info) 호출
//     ├── 자물쇠 미해제 + 정면 공격 → 방패 막힘 (무시 + 파란 플래시)
//     ├── 자물쇠 미해제 + 후면 공격 → LockComponent.TakeDamage
//     └── 자물쇠 해제 후            → EnemyBase.TakeDamage (정상 피격)
//
// [정면/후면 판단]
//   dot(EnemyAI.FacingDirection, DamageInfo.Direction) < 0 = 정면 공격
//
// [Hierarchy]
//   Enemy_Knight
//   ├── [EnemyKnight]
//   ├── [EnemyAI]           enemyType = Knight
//   ├── [KnightAttack]
//   ├── [EnemySensor]
//   ├── [Rigidbody2D]
//   ├── [CapsuleCollider2D]
//   ├── [SpriteRenderer]
//   └── Lock_Back
//         ├── [LockComponent]
//         └── [BoxCollider2D] isTrigger=ON
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 적. EnemyBase 상속. (v1.1)
    /// </summary>
    public class EnemyKnight : EnemyBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 자물쇠 연결 ──────────────────────")]

        /// <summary>
        /// 등 뒤 자물쇠 LockComponent.
        /// 미연결 시 Awake 에서 자동 탐색.
        /// </summary>
        [Tooltip("등 뒤 LockComponent. 미연결 시 자동 탐색.")]
        [SerializeField] private LockComponent _backLock;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// EnemyAI 참조 — FacingDirection 읽기용.
        /// </summary>
        private EnemyAI _enemyAI;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 자물쇠 해제 여부.
        /// false = 방패/자물쇠 판단 / true = 정상 피격 처리.
        /// </summary>
        private bool _isLockUnlocked;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();

            _enemyAI = GetComponent<EnemyAI>();

            if (_backLock == null)
                _backLock = GetComponentInChildren<LockComponent>();

            if (_backLock == null)
                Debug.LogWarning("[EnemyKnight] LockComponent 를 찾을 수 없습니다.");
        }

        private void Start()
        {
            if (_backLock != null)
            {
                _backLock.OnLockUnlocked += HandleLockUnlocked;
                _backLock.OnLockHit += HandleLockHit;
            }
        }

        private void OnDestroy()
        {
            if (_backLock != null)
            {
                _backLock.OnLockUnlocked -= HandleLockUnlocked;
                _backLock.OnLockHit -= HandleLockHit;
            }
        }

        // ══════════════════════════════════════════════════════
        // IDamageable override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 기사형 피격 처리.
        /// 자물쇠 해제 전 → 정면/후면 판단.
        /// 자물쇠 해제 후 → EnemyBase 정상 처리.
        /// </summary>
        public new void TakeDamage(DamageInfo info)
        {
            if (_isLockUnlocked)
            {
                base.TakeDamage(info);
                return;
            }

            if (IsFrontalAttack(info.Direction))
            {
                // 정면 → 방패 막힘
                Debug.Log("[EnemyKnight] 방패로 막힘!");
                StartCoroutine(ShieldFlashRoutine());
            }
            else
            {
                // 후면 → 자물쇠 피격
                Debug.Log("[EnemyKnight] 후면 공격 → 자물쇠 피격");
                _backLock?.TakeDamage(info);
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        private void HandleLockUnlocked()
        {
            _isLockUnlocked = true;
            Debug.Log("[EnemyKnight] 자물쇠 해제 → 약점 노출!");

            if (_spriteRenderer != null)
                _spriteRenderer.color = new Color(1f, 0.4f, 0.4f, 1f);
        }

        private void HandleLockHit(int current, int required)
        {
            Debug.Log($"[EnemyKnight] 자물쇠 피격 {current}/{required}");
        }

        // ══════════════════════════════════════════════════════
        // EnemyBase override
        // ══════════════════════════════════════════════════════

        protected override void OnDamaged(DamageInfo info) { }

        // ══════════════════════════════════════════════════════
        // 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 방향이 정면 공격인지 판단.
        /// dot(기사 바라보는 방향, 공격 방향) &lt; 0 = 정면 공격.
        /// </summary>
        private bool IsFrontalAttack(Vector2 attackDirection)
        {
            float facing = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            return Vector2.Dot(new Vector2(facing, 0f), attackDirection) < 0f;
        }

        /// <summary>
        /// 방패 막힘 플래시 (파란색 깜빡임).
        /// </summary>
        private System.Collections.IEnumerator ShieldFlashRoutine()
        {
            _spriteRenderer.color = new Color(0.4f, 0.4f, 1f, 1f);
            yield return new WaitForSeconds(0.1f);
            _spriteRenderer.color = _isLockUnlocked
                ? new Color(1f, 0.4f, 0.4f, 1f)
                : Color.white;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = _isLockUnlocked ? Color.yellow : Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}