// ============================================================
// BossPattern_Charge.cs  v1.0
// Phase 2 — 전방 돌격 (긴 돌진) 패턴
//
// [기획]
//   방패를 유지하고 빠른 직선 돌진.
//   봉인 불가. 벽 충돌 시 그로기.
//
// [Warning] 2.0초
//   크게 웅크리는 모션. 화면 끝까지 범위 표시.
//   봉인 불가.
//
// [Active]
//   빠른 직선 돌진. 봉인 불가.
//   벽 충돌 시 그로기.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// Phase 2 전방 돌격 패턴. (v1.0)
    /// </summary>
    public class BossPattern_Charge : BossPatternBase
    {
        [Header("── 전방 돌격 설정 ──────────────────────")]

        [Tooltip("돌진 속도.")]
        [Min(1f)]
        [SerializeField] private float _chargeSpeed = 18f;

        [Tooltip("최대 돌진 거리.")]
        [Min(1f)]
        [SerializeField] private float _chargeMaxDistance = 30f;

        [Tooltip("돌진 히트박스.")]
        [SerializeField] private Collider2D _chargeHitbox;

        private Rigidbody2D _rigid2D;

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p2.chargeCooldown;

        private void Awake()
        {
            _rigid2D = GetComponentInParent<Rigidbody2D>();
            _canInterruptDuringWarning = false;
            _canInterruptDuringActive = false;
            _isSwordPattern = false;
        }

        protected override IEnumerator OnWarning()
        {
            // 길이 전체 표시 후 대기
            _rangeIndicator?.UpdateLineLength(
                _chargeMaxDistance, _ai != null ? _ai.FacingDirection : 1f);
            yield return WaitScaled(_warningDuration);
        }

        protected override IEnumerator OnActive()
        {
            if (_chargeHitbox != null) _chargeHitbox.enabled = true;

            float facingDir = _ai != null ? _ai.FacingDirection : 1f;
            float traveled = 0f;

            while (traveled < _chargeMaxDistance)
            {
                if (_isInterrupted) break;
                yield return new WaitForFixedUpdate();

                float step = Mathf.Min(
                    _chargeSpeed * Time.fixedDeltaTime,
                    _chargeMaxDistance - traveled);

                if (HitWall(facingDir, step + 0.1f))
                {
                    TriggerGroggy();
                    break;
                }

                HitPlayer(facingDir, step + 0.2f);
                _rigid2D.MovePosition(_rigid2D.position + new Vector2(facingDir * step, 0f));
                traveled += step;
            }

            _rigid2D.linearVelocity = Vector2.zero;
            if (_chargeHitbox != null) _chargeHitbox.enabled = false;

            if (!_isInterrupted)
                TriggerGroggy();
        }

        protected override IEnumerator OnRecovery()
        {
            yield return WaitScaled(_recoveryDuration);
        }

        private bool HitWall(float dir, float dist)
        {
            if (_data == null) return false;
            return Physics2D.Raycast(transform.position + Vector3.up * 0.5f,
                new Vector2(dir, 0f), dist, _data.groundLayer).collider != null;
        }

        private void HitPlayer(float dir, float dist)
        {
            if (_data == null) return;
            var hit = Physics2D.Raycast(transform.position + Vector3.up * 0.5f,
                new Vector2(dir, 0f), dist, _data.attackHitLayer);
            if (hit.collider != null &&
                hit.collider.TryGetComponent<IDamageable>(out var dmg))
                dmg.TakeDamage(new DamageInfo(transform.position, 25f,
                    new Vector2(dir, 0.1f).normalized, AttackType.Combo1));
        }
    }
}