// ============================================================
// LockComponent.cs  v1.0
// 자물쇠 컴포넌트
//
// [역할]
//   적 오브젝트의 자식에 부착되는 자물쇠.
//   IDamageable 구현 — 피격 시 해제 조건 판별.
//   해제 조건 충족 시 OnLockUnlocked 이벤트 발행.
//   EnemyDummyLocked / EnemyKnight 등에서 구독하여 처리.
//
// [해제 조건]
//   현재: 일정 횟수 타격으로 해제 (AttackCountCondition)
//   추후: 방향 조건, 공격 유형 조건 등 확장 가능
//
// [Hierarchy]
//   Enemy_DummyLocked
//   └── Lock
//         ├── [LockComponent]
//         └── [BoxCollider2D] isTrigger=ON  (PlayerHitbox 레이어 감지)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 자물쇠 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [상태]
    ///   Locked   : 잠긴 상태. 피격 카운트 누적.
    ///   Unlocked : 해제 완료. 이후 피격 무시.
    ///
    /// [해제 조건]
    ///   _requiredHitCount 회 피격 시 해제.
    ///   추후 AttackType / 방향 조건 추가 가능.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class LockComponent : MonoBehaviour, IDamageable
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 스프라이트 ──────────────────────")]
        [Tooltip("자물쇠 시각 표현용 SpriteRenderer.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("── 해제 조건 ──────────────────────")]

        /// <summary>
        /// 해제에 필요한 피격 횟수.
        /// 이 횟수만큼 맞으면 OnLockUnlocked 발행.
        /// </summary>
        [Tooltip("해제에 필요한 피격 횟수.")]
        [Min(1)]
        [SerializeField] private int _requiredHitCount = 3;

        [Header("── 비주얼 ──────────────────────")]

        /// <summary>
        /// 잠긴 상태 색상. Inspector 에서 조정.
        /// </summary>
        [Tooltip("잠긴 상태 색상.")]
        [SerializeField] private Color _lockedColor = new Color(0.3f, 0.3f, 1f, 1f);

        /// <summary>
        /// 해제된 상태 색상.
        /// </summary>
        [Tooltip("해제된 상태 색상.")]
        [SerializeField] private Color _unlockedColor = new Color(1f, 0.8f, 0f, 1f);

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 누적 피격 횟수. </summary>
        private int _currentHitCount;

        /// <summary> 해제 완료 여부. </summary>
        private bool _isUnlocked;

        /// <summary> 이 자물쇠가 부착된 Collider2D. </summary>
        private Collider2D _collider;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 자물쇠 해제 완료 시 발행.
        /// EnemyDummyLocked / EnemyKnight 등에서 구독하여
        /// 보호막 붕괴 / 약점 노출 등 처리.
        /// </summary>
        public event Action OnLockUnlocked;

        /// <summary>
        /// 자물쇠 피격 시 발행 (해제 전).
        /// 파라미터: 현재 누적 횟수, 필요 횟수.
        /// UI 진행 표시 등에서 구독 가능.
        /// </summary>
        public event Action<int, int> OnLockHit;

        // ──────────────────────────────────────────
        // IDamageable 구현
        // ──────────────────────────────────────────

        /// <summary> 해제된 자물쇠는 사망 취급. </summary>
        public bool IsDead => _isUnlocked;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 해제 완료 여부. </summary>
        public bool IsUnlocked => _isUnlocked;

        /// <summary> 현재 피격 횟수 / 필요 횟수. </summary>
        public float UnlockProgress =>
            _requiredHitCount > 0 ? (float)_currentHitCount / _requiredHitCount : 1f;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            RefreshVisual();
        }

        // ══════════════════════════════════════════════════════
        // IDamageable 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리.
        /// 이미 해제된 자물쇠는 무시.
        /// 필요 횟수 누적 시 해제.
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (_isUnlocked) return;

            _currentHitCount++;
            OnLockHit?.Invoke(_currentHitCount, _requiredHitCount);

            Debug.Log($"[LockComponent] 피격 {_currentHitCount}/{_requiredHitCount}");

            if (_currentHitCount >= _requiredHitCount)
                Unlock();
        }

        // ══════════════════════════════════════════════════════
        // 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 자물쇠 해제 처리.
        /// 콜라이더 비활성 → 색상 변경 → 이벤트 발행.
        /// </summary>
        private void Unlock()
        {
            _isUnlocked = true;

            // 콜라이더 비활성 — 이후 피격 판정 없음
            if (_collider != null)
                _collider.enabled = false;

            RefreshVisual();
            OnLockUnlocked?.Invoke();

            Debug.Log("[LockComponent] 자물쇠 해제!");
        }

        /// <summary>
        /// 잠김/해제 상태에 따라 시각 갱신.
        /// </summary>
        private void RefreshVisual()
        {
            if (_spriteRenderer == null) return;
            _spriteRenderer.color = _isUnlocked ? _unlockedColor : _lockedColor;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 자물쇠를 초기 상태로 리셋한다.
        /// 더미 리셋 시 호출.
        /// </summary>
        public void ResetLock()
        {
            _currentHitCount = 0;
            _isUnlocked = false;

            if (_collider != null)
                _collider.enabled = true;

            RefreshVisual();
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isUnlocked ? Color.yellow : Color.blue;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                _isUnlocked
                    ? "UNLOCKED"
                    : $"Lock {_currentHitCount}/{_requiredHitCount}");
#endif
        }
    }
}