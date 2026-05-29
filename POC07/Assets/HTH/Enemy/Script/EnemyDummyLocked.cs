// ============================================================
// EnemyDummyLocked.cs  v1.2
// 자물쇠 있는 완전 정지 더미 적
//
// [v1.2 변경]
//   gravityScale = 1 (중력 적용 — 바닥 착지)
//   FreezePositionY 제거 (Y 축 자유 — 낙하 가능)
//   FreezeRotation Z 만 유지
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 자물쇠 있는 완전 정지 더미 적. (v1.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [자물쇠 연동 흐름]
    ///   자물쇠 해제 전 : 본체 TakeDamage → 무시 + 파란 플래시
    ///   자물쇠 해제 후 : 본체 TakeDamage → 정상 피격 처리
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemyDummyLocked : EnemyBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 자물쇠 연결 ──────────────────────")]

        /// <summary>
        /// 자식 오브젝트의 LockComponent.
        /// 미연결 시 Awake 에서 자동 탐색.
        /// </summary>
        [Tooltip("자식 LockComponent. 미연결 시 자동 탐색.")]
        [SerializeField] private LockComponent _lockComponent;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 자물쇠 해제 여부.
        /// false = 본체 피격 무시 / true = 정상 피격 처리.
        /// </summary>
        private bool _isLockUnlocked;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();

            if (_rigid2D != null)
            {
                // 중력 적용 — 바닥 착지
                // FreezeRotation Z 만 — 넉백 시 회전 방지
                _rigid2D.gravityScale = 1f;
                _rigid2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            if (_lockComponent == null)
                _lockComponent = GetComponentInChildren<LockComponent>();

            if (_lockComponent == null)
                Debug.LogWarning("[EnemyDummyLocked] LockComponent 를 찾을 수 없습니다.");
        }

        private void Start()
        {
            if (_lockComponent != null)
            {
                _lockComponent.OnLockUnlocked += HandleLockUnlocked;
                _lockComponent.OnLockHit += HandleLockHit;
            }
        }

        private void OnDestroy()
        {
            if (_lockComponent != null)
            {
                _lockComponent.OnLockUnlocked -= HandleLockUnlocked;
                _lockComponent.OnLockHit -= HandleLockHit;
            }
        }

        // ══════════════════════════════════════════════════════
        // IDamageable override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 본체 피격 처리.
        /// 자물쇠 해제 전 → 무시 + 파란 플래시.
        /// 자물쇠 해제 후 → EnemyBase 정상 처리.
        /// </summary>
        public new void TakeDamage(DamageInfo info)
        {
            if (!_isLockUnlocked)
            {
                Debug.Log("[EnemyDummyLocked] 자물쇠 미해제 — 본체 피격 무시");
                StartCoroutine(ShieldFlashRoutine());
                return;
            }

            base.TakeDamage(info);
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        private void HandleLockUnlocked()
        {
            _isLockUnlocked = true;
            Debug.Log("[EnemyDummyLocked] 자물쇠 해제 → 약점 노출!");

            if (_spriteRenderer != null)
                _spriteRenderer.color = new Color(1f, 0.4f, 0.4f, 1f);
        }

        private void HandleLockHit(int current, int required)
        {
            Debug.Log($"[EnemyDummyLocked] 자물쇠 피격 {current}/{required}");
        }

        // ══════════════════════════════════════════════════════
        // 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 보호막 피격 플래시 (파란색 깜빡임).
        /// </summary>
        private IEnumerator ShieldFlashRoutine()
        {
            _spriteRenderer.color = new Color(0.4f, 0.4f, 1f, 1f);
            yield return new WaitForSeconds(0.1f);
            _spriteRenderer.color = _isLockUnlocked
                ? new Color(1f, 0.4f, 0.4f, 1f)
                : Color.white;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 더미 전체 리셋 (체력 + 자물쇠 + velocity).
        /// </summary>
        public void ResetDummy()
        {
            _isLockUnlocked = false;
            _lockComponent?.ResetLock();

            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.white;
        }

        protected override void OnDamaged(DamageInfo info) { }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = _isLockUnlocked ? Color.yellow : Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}