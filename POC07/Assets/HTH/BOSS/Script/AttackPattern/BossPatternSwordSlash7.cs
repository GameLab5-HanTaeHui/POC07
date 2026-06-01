// ============================================================
// BossPattern_SwordSlash7.cs  v1.0
// Phase 2 — 검 제식 7 (횡베기 1회) 패턴
//
// [기획]
//   검을 옆으로 크게 빼서 빠르게 횡베기 1회.
//   예고 중 봉인 성공 → 패턴 일시 중지 + 검 무식 + 재개.
//
// [Warning] 1.5초
//   검을 옆으로 빼는 모션. 부채꼴 범위 표시.
//   봉인 가능 구간.
//
// [Active]
//   빠른 횡베기. OverlapCircle 부채꼴 판정.
//   봉인 불가.
//
// [Recovery] 0.7초
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
    /// Phase 2 검 제식 7 패턴. (v1.0)
    /// </summary>
    public class BossPattern_SwordSlash7 : BossPatternBase
    {
        [Header("── 검 제식 7 설정 ──────────────────────")]

        [Tooltip("횡베기 피해량.")]
        [Min(0f)]
        [SerializeField] private float _slashDamage = 20f;

        [Tooltip("횡베기 범위 반경.")]
        [Min(0f)]
        [SerializeField] private float _slashRadius = 5f;

        [Tooltip("횡베기 히트박스 Collider2D.")]
        [SerializeField] private Collider2D _slashHitbox;

        private readonly List<Collider2D> _overlapBuffer = new();
        private readonly HashSet<Collider2D> _hitTargets = new();

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p2.swordSlash7Cooldown;

        private void Awake()
        {
            _canInterruptDuringWarning = true;   // 예고 중 봉인 가능
            _canInterruptDuringActive = false;
            _isSwordPattern = true;
        }

        protected override IEnumerator OnWarning()
        {
            _rangeIndicator?.UpdateCircleRadius(_slashRadius);
            yield return WaitScaled(_warningDuration);
        }

        protected override IEnumerator OnActive()
        {
            _hitTargets.Clear();
            if (_slashHitbox != null) _slashHitbox.enabled = true;

            // OverlapCircle 즉발 판정
            var filter = new ContactFilter2D();
            filter.SetLayerMask(_data != null ? _data.attackHitLayer : ~0);
            filter.useTriggers = true;

            int count = Physics2D.OverlapCircle(
                transform.position, _slashRadius, filter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                if (_hitTargets.Contains(_overlapBuffer[i])) continue;
                if (!_overlapBuffer[i].TryGetComponent<IDamageable>(out var dmg)) continue;
                _hitTargets.Add(_overlapBuffer[i]);
                dmg.TakeDamage(new DamageInfo(
                    transform.position, _slashDamage,
                    (_overlapBuffer[i].transform.position - transform.position).normalized,
                    AttackType.Combo1));
            }

            yield return new WaitForSeconds(0.15f);
            if (_slashHitbox != null) _slashHitbox.enabled = false;
        }

        protected override IEnumerator OnRecovery()
        {
            yield return WaitScaled(_recoveryDuration);
        }
    }
}