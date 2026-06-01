// ============================================================
// BossPattern_Slash1.cs  v1.0
// Phase 3 — 검 제식 1 (직선 돌진 찌르기)
//
// [기획]
//   전방으로 검을 내밀며 직선 돌진.
//   돌진 중 봉인 → 그로기 진입.
//   봉인 가능 예고.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    public class BossPattern_Slash1 : BossPatternBase
    {
        [Header("── 검 제식 1 설정 ──────────────────────")]
        [Tooltip("돌진 속도.")][Min(1f)][SerializeField] private float _chargeSpeed = 20f;
        [Tooltip("최대 거리.")][Min(1f)][SerializeField] private float _chargeMaxDistance = 25f;
        [Tooltip("피해량.")][Min(0f)][SerializeField] private float _damage = 28f;
        [SerializeField] private Collider2D _chargeHitbox;

        private Rigidbody2D _rigid2D;

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p3.swordSlash1Cooldown;

        private void Awake()
        {
            _rigid2D = GetComponentInParent<Rigidbody2D>();
            _canInterruptDuringWarning = true;
            _canInterruptDuringActive = true;   // 돌진 중 봉인 → 그로기
            _isSwordPattern = true;
        }

        protected override IEnumerator OnWarning()
        {
            _rangeIndicator?.UpdateLineLength(
                _chargeMaxDistance, _ai != null ? _ai.FacingDirection : 1f);
            yield return WaitScaled(_warningDuration);
        }

        protected override IEnumerator OnActive()
        {
            if (_chargeHitbox != null) _chargeHitbox.enabled = true;
            float dir = _ai != null ? _ai.FacingDirection : 1f;
            float traveled = 0f;

            while (traveled < _chargeMaxDistance)
            {
                if (_isInterrupted) break;
                yield return new WaitForFixedUpdate();
                float step = Mathf.Min(_chargeSpeed * Time.fixedDeltaTime, _chargeMaxDistance - traveled);

                if (Physics2D.Raycast(transform.position + Vector3.up * 0.5f,
                    new Vector2(dir, 0f), step + 0.1f,
                    _data != null ? _data.groundLayer : ~0).collider != null)
                {
                    TriggerGroggy();
                    break;
                }

                var hit = Physics2D.Raycast(transform.position + Vector3.up * 0.5f,
                    new Vector2(dir, 0f), step + 0.2f,
                    _data != null ? _data.attackHitLayer : ~0);
                if (hit.collider != null &&
                    hit.collider.TryGetComponent<IDamageable>(out var dmg))
                    dmg.TakeDamage(new DamageInfo(transform.position, _damage,
                        new Vector2(dir, 0.1f).normalized, AttackType.Combo1));

                _rigid2D.MovePosition(_rigid2D.position + new Vector2(dir * step, 0f));
                traveled += step;
            }

            _rigid2D.linearVelocity = Vector2.zero;
            if (_chargeHitbox != null) _chargeHitbox.enabled = false;
            if (!_isInterrupted) TriggerGroggy();
        }

        protected override IEnumerator OnRecovery()
            => WaitScaled(_recoveryDuration);
    }
}