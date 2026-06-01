// ============================================================
// BossPattern_SwordSlash12.cs  v1.0
// Phase 2 — 검 제식 12 (짧은+긴 베기) 패턴
//
// [기획]
//   짧은 베기 → 즉시 → 긴 베기 연속 2회.
//   연속이라 봉인 불가.
//
// [Warning] 0.8초  봉인 불가.
// [Active]  짧은 베기 → 0.15초 → 긴 베기. 봉인 불가.
// [Recovery] 0.5초.
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
    /// Phase 2 검 제식 12 패턴. (v1.0)
    /// </summary>
    public class BossPattern_SwordSlash12 : BossPatternBase
    {
        [Header("── 검 제식 12 설정 ──────────────────────")]

        [Tooltip("짧은 베기 피해량.")]
        [Min(0f)]
        [SerializeField] private float _shortSlashDamage = 12f;

        [Tooltip("짧은 베기 반경.")]
        [Min(0f)]
        [SerializeField] private float _shortSlashRadius = 3f;

        [Tooltip("긴 베기 피해량.")]
        [Min(0f)]
        [SerializeField] private float _longSlashDamage = 20f;

        [Tooltip("긴 베기 반경.")]
        [Min(0f)]
        [SerializeField] private float _longSlashRadius = 6f;

        [Tooltip("히트박스 Collider2D.")]
        [SerializeField] private Collider2D _slashHitbox;

        private readonly List<Collider2D> _overlapBuffer = new();
        private readonly HashSet<Collider2D> _hitTargets = new();

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p2.swordSlash12Cooldown;

        private void Awake()
        {
            _canInterruptDuringWarning = false;
            _canInterruptDuringActive = false;
            _isSwordPattern = true;
        }

        protected override IEnumerator OnWarning()
        {
            yield return WaitScaled(_warningDuration);
        }

        protected override IEnumerator OnActive()
        {
            _hitTargets.Clear();

            // 짧은 베기
            yield return StartCoroutine(DoSlash(_shortSlashRadius, _shortSlashDamage));

            yield return new WaitForSeconds(0.15f);
            if (_isInterrupted) yield break;

            // 긴 베기
            yield return StartCoroutine(DoSlash(_longSlashRadius, _longSlashDamage));
        }

        private IEnumerator DoSlash(float radius, float damage)
        {
            if (_slashHitbox != null) _slashHitbox.enabled = true;

            var filter = new ContactFilter2D();
            filter.SetLayerMask(_data != null ? _data.attackHitLayer : ~0);
            filter.useTriggers = true;

            int count = Physics2D.OverlapCircle(
                transform.position, radius, filter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                if (_hitTargets.Contains(_overlapBuffer[i])) continue;
                if (!_overlapBuffer[i].TryGetComponent<IDamageable>(out var dmg)) continue;
                _hitTargets.Add(_overlapBuffer[i]);
                dmg.TakeDamage(new DamageInfo(
                    transform.position, damage,
                    (_overlapBuffer[i].transform.position - transform.position).normalized,
                    AttackType.Combo1));
            }

            yield return new WaitForSeconds(0.1f);
            if (_slashHitbox != null) _slashHitbox.enabled = false;
        }

        protected override IEnumerator OnRecovery()
        {
            yield return WaitScaled(_recoveryDuration);
        }
    }
}