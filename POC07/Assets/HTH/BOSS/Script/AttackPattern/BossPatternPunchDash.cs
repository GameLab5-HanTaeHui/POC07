// ============================================================
// BossPattern_PunchDash.cs  v1.0
// Phase 3 — 주먹 돌진 패턴
//
// [기획]
//   Hand2L / Hand2R 를 로켓처럼 발사.
//   날아오는 중 봉인 → 해당 주먹 정지 + 봉인 상태.
//   대타 출동 발동 시 해당 주먹 봉인.
//   양 손 모두 봉인 시 패턴 스킵.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    public class BossPattern_PunchDash : BossPatternBase
    {
        [Header("── 주먹 돌진 설정 ──────────────────────")]
        [Tooltip("발사 속도.")][Min(1f)][SerializeField] private float _punchSpeed = 18f;
        [Tooltip("피해량.")][Min(0f)][SerializeField] private float _punchDamage = 25f;
        [Tooltip("발사 주먹 파트 목록 (Hand2L, Hand2R).")][SerializeField] private BossPartComponent[] _hands;
        [Tooltip("주먹별 히트박스.")][SerializeField] private Collider2D[] _handHitboxes;

        private Transform _playerTransform;

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p3.punchDashCooldown;

        private void Awake()
        {
            _canInterruptDuringWarning = false;
            _canInterruptDuringActive = true;
            _isSwordPattern = false;

            var pm = FindObjectsByType<PlayerMover>(FindObjectsSortMode.None);
            if (pm.Length > 0) _playerTransform = pm[0].transform;
        }

        public new bool CanExecute
        {
            get
            {
                if (!base.CanExecute) return false;
                foreach (var h in _hands)
                    if (h != null && h.IsActive && h.IsUnlocked) return true;
                return false;
            }
        }

        protected override IEnumerator OnWarning()
            => WaitScaled(_warningDuration);

        protected override IEnumerator OnActive()
        {
            if (_playerTransform == null) yield break;

            for (int i = 0; i < _hands.Length; i++)
            {
                var hand = _hands.Length > i ? _hands[i] : null;
                if (hand == null || !hand.IsActive || hand.IsLocked) continue;

                var hitbox = _handHitboxes.Length > i ? _handHitboxes[i] : null;
                StartCoroutine(LaunchHand(hand, hitbox));
                yield return new WaitForSeconds(0.2f * _speedMultiplier);
            }

            yield return new WaitForSeconds(0.5f * _speedMultiplier);
        }

        private IEnumerator LaunchHand(BossPartComponent hand, Collider2D hitbox)
        {
            if (_playerTransform == null) yield break;
            if (hitbox != null) hitbox.enabled = true;

            Vector3 origin = hand.transform.position;
            Vector3 target = _playerTransform.position;
            float dist = Vector3.Distance(origin, target);
            float speed = _punchSpeed;
            float elapsed = 0f;
            float duration = dist / speed;

            while (elapsed < duration)
            {
                if (_isInterrupted || hand.IsLocked) { break; }
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                hand.transform.position = Vector3.Lerp(origin, target, t);
                yield return null;
            }

            // 플레이어 도달
            if (!hand.IsLocked)
            {
                var col = Physics2D.OverlapCircle(hand.transform.position, 0.5f, _data?.attackHitLayer ?? ~0);
                if (col != null && col.TryGetComponent<IDamageable>(out var dmg))
                    dmg.TakeDamage(new DamageInfo(hand.transform.position, _punchDamage,
                        (target - origin).normalized, AttackType.Combo1));
            }

            if (hitbox != null) hitbox.enabled = false;

            // 복귀
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                hand.transform.position = Vector3.Lerp(target, origin, t);
                yield return null;
            }
            hand.transform.position = origin;
        }

        protected override IEnumerator OnRecovery()
            => WaitScaled(_recoveryDuration);
    }
}