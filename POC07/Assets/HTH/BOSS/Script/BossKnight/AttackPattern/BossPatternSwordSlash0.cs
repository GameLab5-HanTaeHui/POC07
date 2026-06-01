// ============================================================
// BossPattern_Slash0.cs  v1.0
// Phase 3 — 검 제식 0 (연속 4회 확장 베기)
//
// [기획]
//   4회 연속. 회차마다 도넛 범위가 커짐.
//   내부 안전구역 유지. 봉인 불가.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    public class BossPattern_SwordSlash0 : BossPatternBase
    {
        [Header("── 검 제식 0 설정 ──────────────────────")]
        [Tooltip("1회차 외부 반경.")][Min(0f)][SerializeField] private float _startRadius = 3f;
        [Tooltip("회차마다 증가하는 반경.")][Min(0f)][SerializeField] private float _radiusStep = 2f;
        [Tooltip("내부 안전 반경.")][Min(0f)][SerializeField] private float _innerRadius = 1.5f;
        [Tooltip("피해량.")][Min(0f)][SerializeField] private float _damage = 18f;
        [Tooltip("회차 간격.")][Min(0f)][SerializeField] private float _interval = 0.4f;
        [SerializeField] private Collider2D _slashHitbox;

        private readonly List<Collider2D> _buf = new();
        private readonly HashSet<Collider2D> _hits = new();

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p3.swordSlash0Cooldown;

        private void Awake()
        {
            _canInterruptDuringWarning = false;
            _canInterruptDuringActive = false;
            _isSwordPattern = true;
        }

        protected override IEnumerator OnWarning()
            => WaitScaled(_warningDuration);

        protected override IEnumerator OnActive()
        {
            _hits.Clear();
            for (int round = 0; round < 4; round++)
            {
                if (_isInterrupted) yield break;
                float radius = _startRadius + _radiusStep * round;
                yield return StartCoroutine(DoSlash(radius));
                yield return new WaitForSeconds(_interval * _speedMultiplier);
            }
        }

        private IEnumerator DoSlash(float outerRadius)
        {
            if (_slashHitbox != null) _slashHitbox.enabled = true;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(_data != null ? _data.attackHitLayer : ~0);
            filter.useTriggers = true;

            int count = Physics2D.OverlapCircle(transform.position, outerRadius, filter, _buf);
            for (int i = 0; i < count; i++)
            {
                if (_hits.Contains(_buf[i])) continue;
                if (Vector3.Distance(transform.position, _buf[i].transform.position) < _innerRadius) continue;
                if (!_buf[i].TryGetComponent<IDamageable>(out var dmg)) continue;
                _hits.Add(_buf[i]);
                dmg.TakeDamage(new DamageInfo(transform.position, _damage,
                    (_buf[i].transform.position - transform.position).normalized, AttackType.Combo1));
            }

            yield return new WaitForSeconds(0.1f);
            if (_slashHitbox != null) _slashHitbox.enabled = false;
        }

        protected override IEnumerator OnRecovery()
            => WaitScaled(_recoveryDuration);
    }
}