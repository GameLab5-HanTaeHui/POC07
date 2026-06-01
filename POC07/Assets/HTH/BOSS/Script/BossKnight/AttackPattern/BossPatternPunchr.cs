// ============================================================
// BossPattern_PunchR.cs  v1.0
// Phase 1 — 오른팔 주먹 공격 패턴
//
// [기획]
//   오른팔 자물쇠 해제 후에만 발동.
//   오른팔 봉인 상태 → 속도 느림 (_speedMultiplier > 1.0).
//   오른팔 해제 상태 → 빠른 내리찍기.
//   지면 타격 후 원형 충격파.
//
// [Warning]
//   오른팔을 높이 드는 모션 + 원형 범위 표시.
//   오른팔 봉인 여부에 따라 WaitScaled 로 속도 다름.
//   봉인 불가 구간.
//
// [Active]
//   지면 내리찍기 → OverlapCircle 피격 판정.
//   봉인 불가.
//
// [Recovery]
//   오른팔 봉인 상태: 긴 후딜레이.
//   오른팔 해제 상태: 짧은 후딜레이.
//
// [발동 조건]
//   _armRPart.IsUnlocked == true (오른팔 자물쇠 해제 상태)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// Phase 1 오른팔 주먹 공격 패턴. (v1.0)
    /// </summary>
    public class BossPattern_PunchR : BossPatternBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 주먹 공격 설정 ──────────────────────")]

        [Tooltip("주먹 공격 피해량.")]
        [Min(0f)]
        [SerializeField] private float _punchDamage = 20f;

        [Tooltip("지면 타격 충격파 반경.")]
        [Min(0f)]
        [SerializeField] private float _punchRadius = 3f;

        [Tooltip("오른팔 자물쇠 해제 상태 후딜레이.")]
        [Min(0f)]
        [SerializeField] private float _shortRecovery = 0.4f;

        [Tooltip("오른팔 봉인 상태 후딜레이 (느림).")]
        [Min(0f)]
        [SerializeField] private float _longRecovery = 0.9f;

        [Header("── 발동 조건 ──────────────────────")]

        [Tooltip("오른팔 BossPartComponent. 해제 상태에서만 발동.")]
        [SerializeField] private BossPartComponent _armRPart;

        [Header("── 히트박스 ──────────────────────")]

        [Tooltip("주먹 히트박스 Collider2D.")]
        [SerializeField] private Collider2D _punchHitbox;

        // ──────────────────────────────────────────
        // 내부
        // ──────────────────────────────────────────

        private readonly List<Collider2D> _overlapBuffer = new();
        private readonly HashSet<Collider2D> _hitTargets = new();

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p1.punchCooldown;

        private void Awake()
        {
            _canInterruptDuringWarning = false;
            _canInterruptDuringActive = false;
            _isSwordPattern = false;
        }

        // 발동 조건
        public new bool CanExecute
            => base.CanExecute && (_armRPart == null || _armRPart.IsUnlocked);

        // ══════════════════════════════════════════════════════
        // Warning
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnWarning()
        {
            // 원형 범위 표시
            _rangeIndicator?.UpdateCircleRadius(_punchRadius);

            // 오른팔 봉인 여부로 속도 결정 (BossPartComponent 가 _speedMultiplier 주입)
            yield return WaitScaled(_warningDuration);
        }

        // ══════════════════════════════════════════════════════
        // Active
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnActive()
        {
            _hitTargets.Clear();
            if (_punchHitbox != null) _punchHitbox.enabled = true;

            // 내리찍기 순간 OverlapCircle 판정
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_data != null ? _data.attackHitLayer : ~0);
            filter.useTriggers = true;

            int count = Physics2D.OverlapCircle(
                transform.position, _punchRadius, filter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                var col = _overlapBuffer[i];
                if (_hitTargets.Contains(col)) continue;
                if (!col.TryGetComponent<IDamageable>(out var dmg)) continue;

                _hitTargets.Add(col);
                dmg.TakeDamage(new DamageInfo(
                    transform.position,
                    _punchDamage,
                    (col.transform.position - transform.position).normalized,
                    AttackType.Combo1));
            }

            yield return new WaitForSeconds(0.1f);

            if (_punchHitbox != null) _punchHitbox.enabled = false;
            _hitTargets.Clear();
        }

        // ══════════════════════════════════════════════════════
        // Recovery
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnRecovery()
        {
            // 오른팔 해제 상태 → 짧은 후딜레이, 봉인 상태 → 긴 후딜레이
            bool isArmFree = _armRPart != null && _armRPart.IsUnlocked;
            float duration = isArmFree ? _shortRecovery : _longRecovery;

            yield return WaitScaled(duration);
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _punchRadius);
        }
    }
}