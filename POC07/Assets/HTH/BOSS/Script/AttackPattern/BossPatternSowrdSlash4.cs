// ============================================================
// BossPattern_Slash4.cs  v1.0
// Phase 3 — 검 제식 4 (도넛 원형 베기) 패턴
//
// [기획]
//   플레이어 방향 1회 도넛 모양 원형 베기.
//   내부 안전구역 존재.
//   예고 중 봉인 → 대타 출동 우선 → 불가 시 검 무식.
//
// [Warning] 1.5초  도넛 범위 표시 (내부 비어 있음). 봉인 가능.
// [Active]  도넛 범위 OverlapCircle (내부 제외). 봉인 불가.
// [Recovery] 0.6초.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    public class BossPattern_Slash4 : BossPatternBase
    {
        [Header("── 검 제식 4 설정 ──────────────────────")]
        [Tooltip("도넛 외부 반경.")][Min(0f)][SerializeField] private float _outerRadius = 6f;
        [Tooltip("도넛 내부 안전 반경.")][Min(0f)][SerializeField] private float _innerRadius = 2f;
        [Tooltip("피해량.")][Min(0f)][SerializeField] private float _damage = 22f;
        [SerializeField] private Collider2D _slashHitbox;

        private readonly List<Collider2D> _buf = new();
        private readonly HashSet<Collider2D> _hits = new();

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p3.swordSlash4Cooldown;

        private void Awake()
        {
            _canInterruptDuringWarning = true;
            _canInterruptDuringActive = false;
            _isSwordPattern = true;
        }

        protected override IEnumerator OnWarning()
        {
            _rangeIndicator?.UpdateCircleRadius(_outerRadius);
            yield return WaitScaled(_warningDuration);
        }

        protected override IEnumerator OnActive()
        {
            _hits.Clear();
            if (_slashHitbox != null) _slashHitbox.enabled = true;

            var filter = new ContactFilter2D();
            filter.SetLayerMask(_data != null ? _data.attackHitLayer : ~0);
            filter.useTriggers = true;

            int count = Physics2D.OverlapCircle(transform.position, _outerRadius, filter, _buf);
            for (int i = 0; i < count; i++)
            {
                if (_hits.Contains(_buf[i])) continue;
                // 내부 안전구역 제외
                if (Vector3.Distance(transform.position, _buf[i].transform.position) < _innerRadius)
                    continue;
                if (!_buf[i].TryGetComponent<IDamageable>(out var dmg)) continue;
                _hits.Add(_buf[i]);
                dmg.TakeDamage(new DamageInfo(transform.position, _damage,
                    (_buf[i].transform.position - transform.position).normalized, AttackType.Combo1));
            }

            yield return new WaitForSeconds(0.15f);
            if (_slashHitbox != null) _slashHitbox.enabled = false;
        }

        protected override IEnumerator OnRecovery()
            => WaitScaled(_recoveryDuration);
    }
}