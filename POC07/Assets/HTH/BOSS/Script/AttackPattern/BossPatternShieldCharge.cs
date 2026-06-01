// ============================================================
// BossPattern_ShieldCharge.cs  v1.0
// Phase 1 — 방패 돌진 패턴
//
// [기획]
//   방패 자물쇠 해제 후에만 발동.
//   전방으로 방패를 치켜 세워 직선 돌진.
//   돌진 중 봉인 성공 → 그로기 진입.
//   벽 충돌 시 자동 그로기.
//
// [발동 조건]
//   _shieldPart.IsUnlocked == true (방패 자물쇠 해제 상태)
//
// [Warning]
//   방패를 치켜 올리는 예고 모션.
//   직선 범위 LineRenderer 표시.
//   봉인 불가 구간.
//
// [Active]
//   전방 직선 돌진. MovePosition 코루틴.
//   매 FixedUpdate 마다 벽/플레이어 Raycast.
//   돌진 중 봉인 투사체 감지 → TriggerGroggy().
//   벽 충돌 → 그로기.
//
// [Recovery]
//   정상 종료 시 짧은 경직.
//   벽 충돌 그로기 시에는 Recovery 없이 그로기 직행.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// Phase 1 방패 돌진 패턴. (v1.0)
    /// </summary>
    public class BossPattern_ShieldCharge : BossPatternBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 방패 돌진 설정 ──────────────────────")]

        [Tooltip("돌진 속도 (units/s).")]
        [Min(1f)]
        [SerializeField] private float _chargeSpeed = 12f;

        [Tooltip("돌진 최대 거리 (units).")]
        [Min(1f)]
        [SerializeField] private float _chargeMaxDistance = 20f;

        [Tooltip("Raycast 높이 오프셋.")]
        [Range(0f, 2f)]
        [SerializeField] private float _rayOriginHeight = 0.5f;

        [Header("── 발동 조건 ──────────────────────")]

        [Tooltip("방패 자물쇠 BossPartComponent. 해제 상태에서만 발동.")]
        [SerializeField] private BossPartComponent _shieldPart;

        [Header("── 히트박스 ──────────────────────")]

        [Tooltip("방패 돌진 히트박스 Collider2D.")]
        [SerializeField] private Collider2D _chargeHitbox;

        // ──────────────────────────────────────────
        // 내부
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private bool _hitWall;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p1.shieldChargeCooldown;

        private void Awake()
        {
            _rigid2D = GetComponentInParent<Rigidbody2D>();
            _canInterruptDuringWarning = false;
            _canInterruptDuringActive = true;   // 돌진 중 봉인 가능
            _isSwordPattern = false;
        }

        // 발동 조건 체크 override
        public new bool CanExecute
            => base.CanExecute && (_shieldPart == null || _shieldPart.IsUnlocked);

        // ══════════════════════════════════════════════════════
        // Warning
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnWarning()
        {
            _hitWall = false;

            // 예고 LineRenderer 점진적 표시
            float elapsed = 0f;
            float duration = _warningDuration * _speedMultiplier;

            while (elapsed < duration)
            {
                if (_isInterrupted) yield break;
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float length = _chargeMaxDistance * t;

                _rangeIndicator?.UpdateLineLength(
                    length, _ai != null ? _ai.FacingDirection : 1f);
                _rangeIndicator?.UpdateColor(
                    Color.Lerp(new Color(1f, 1f, 0f, 0.4f), new Color(1f, 0.1f, 0.1f, 0.8f), t));

                yield return null;
            }
        }

        // ══════════════════════════════════════════════════════
        // Active
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnActive()
        {
            if (_chargeHitbox != null) _chargeHitbox.enabled = true;

            float facingDir = _ai != null ? _ai.FacingDirection : 1f;
            float speed = _chargeSpeed;
            float traveled = 0f;

            Vector2 startPos = _rigid2D.position;
            Vector2 targetPos = startPos + new Vector2(facingDir * _chargeMaxDistance, 0f);

            while (traveled < _chargeMaxDistance)
            {
                if (_isInterrupted) break;

                yield return new WaitForFixedUpdate();

                float step = Mathf.Min(speed * Time.fixedDeltaTime,
                                       _chargeMaxDistance - traveled);

                // 벽 감지
                if (HitWall(facingDir, step + 0.1f))
                {
                    _hitWall = true;
                    TriggerGroggy();
                    break;
                }

                // 플레이어 피격
                HitPlayer(facingDir, step + 0.2f);

                _rigid2D.MovePosition(
                    _rigid2D.position + new Vector2(facingDir * step, 0f));
                traveled += step;
            }

            _rigid2D.linearVelocity = Vector2.zero;
            if (_chargeHitbox != null) _chargeHitbox.enabled = false;

            if (!_hitWall && !_isInterrupted)
            {
                // 정상 종료 → 짧은 그로기
                TriggerGroggy();
            }
        }

        // ══════════════════════════════════════════════════════
        // Recovery
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnRecovery()
        {
            // 그로기로 진입했으므로 Recovery 는 짧게
            yield return WaitScaled(_recoveryDuration);
        }

        // ══════════════════════════════════════════════════════
        // 감지
        // ══════════════════════════════════════════════════════

        private bool HitWall(float dir, float dist)
        {
            if (_data == null) return false;
            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;
            return Physics2D.Raycast(origin, new Vector2(dir, 0f),
                dist, _data.groundLayer).collider != null;
        }

        private void HitPlayer(float dir, float dist)
        {
            if (_data == null) return;
            Vector3 origin = transform.position + Vector3.up * _rayOriginHeight;
            var hit = Physics2D.Raycast(origin, new Vector2(dir, 0f),
                dist, _data.attackHitLayer);
            if (hit.collider == null) return;
            if (hit.collider.TryGetComponent<IDamageable>(out var dmg))
                dmg.TakeDamage(new DamageInfo(
                    transform.position, 25f,
                    new Vector2(dir, 0.1f).normalized,
                    AttackType.Combo1));
        }
    }
}