// ============================================================
// BossPattern_Grab.cs  v1.0
// Phase 3 — 횡 잡기 패턴
//
// [기획]
//   Hand2L / Hand2R 번갈아 좌→우 / 우→좌 휩쓸어 잡기.
//   어느 손이 먼저인지 매 발동 랜덤.
//   봉인 성공 → 해당 손 정지, 나머지 진행.
//   잡기 성공 → 바닥 내려침 → 던짐.
//   양 손 모두 봉인 시 패턴 스킵.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    public class BossPattern_Grab : BossPatternBase
    {
        [Header("── 횡 잡기 설정 ──────────────────────")]
        [Tooltip("휩쓸기 속도.")][Min(1f)][SerializeField] private float _sweepSpeed = 12f;
        [Tooltip("휩쓸기 거리.")][Min(1f)][SerializeField] private float _sweepDistance = 15f;
        [Tooltip("잡기 피해량.")][Min(0f)][SerializeField] private float _grabDamage = 30f;
        [Tooltip("Hand2L 파트.")][SerializeField] private BossPartComponent _hand2L;
        [Tooltip("Hand2R 파트.")][SerializeField] private BossPartComponent _hand2R;
        [Tooltip("Hand2L 히트박스.")][SerializeField] private Collider2D _hitboxL;
        [Tooltip("Hand2R 히트박스.")][SerializeField] private Collider2D _hitboxR;

        private Transform _playerTransform;

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p3.grabCooldown;

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
                bool lOk = _hand2L != null && _hand2L.IsActive && _hand2L.IsUnlocked;
                bool rOk = _hand2R != null && _hand2R.IsActive && _hand2R.IsUnlocked;
                return lOk || rOk;
            }
        }

        protected override IEnumerator OnWarning()
            => WaitScaled(_warningDuration);

        protected override IEnumerator OnActive()
        {
            // 순서 랜덤
            bool leftFirst = Random.value > 0.5f;
            var first = leftFirst ? _hand2L : _hand2R;
            var second = leftFirst ? _hand2R : _hand2L;
            var firstHitbox = leftFirst ? _hitboxL : _hitboxR;
            var secondHitbox = leftFirst ? _hitboxR : _hitboxL;
            float dir1 = leftFirst ? 1f : -1f;
            float dir2 = leftFirst ? -1f : 1f;

            if (first != null && first.IsActive && first.IsUnlocked)
                yield return StartCoroutine(Sweep(first, firstHitbox, dir1));

            yield return new WaitForSeconds(0.2f * _speedMultiplier);

            if (second != null && second.IsActive && second.IsUnlocked)
                yield return StartCoroutine(Sweep(second, secondHitbox, dir2));
        }

        private IEnumerator Sweep(BossPartComponent hand, Collider2D hitbox, float dir)
        {
            if (hitbox != null) hitbox.enabled = true;

            Vector3 startPos = hand.transform.position;
            float traveled = 0f;
            bool grabbed = false;

            while (traveled < _sweepDistance)
            {
                if (_isInterrupted || hand.IsLocked) break;

                float step = _sweepSpeed * Time.deltaTime * _speedMultiplier;
                hand.transform.position += new Vector3(dir * step, 0f, 0f);
                traveled += step;

                // 플레이어 잡기 감지
                if (!grabbed && _playerTransform != null)
                {
                    float dist = Vector3.Distance(hand.transform.position, _playerTransform.position);
                    if (dist < 1.0f)
                    {
                        grabbed = true;
                        yield return StartCoroutine(GrabSlam());
                        break;
                    }
                }

                yield return null;
            }

            if (hitbox != null) hitbox.enabled = false;

            // 원위치 복귀
            float returnElapsed = 0f;
            float returnDuration = 0.3f;
            Vector3 currentPos = hand.transform.position;
            while (returnElapsed < returnDuration)
            {
                returnElapsed += Time.deltaTime;
                hand.transform.position = Vector3.Lerp(currentPos, startPos,
                    Mathf.Clamp01(returnElapsed / returnDuration));
                yield return null;
            }
            hand.transform.position = startPos;
        }

        private IEnumerator GrabSlam()
        {
            if (_playerTransform == null) yield break;

            // 바닥 내려침
            Vector3 slamTarget = new Vector3(
                _playerTransform.position.x,
                transform.position.y - 1f,
                _playerTransform.position.z);

            float elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                _playerTransform.position = Vector3.Lerp(
                    _playerTransform.position, slamTarget,
                    Mathf.Clamp01(elapsed / 0.3f));
                yield return null;
            }

            // 피해 적용
            if (_playerTransform.TryGetComponent<IDamageable>(out var dmg))
                dmg.TakeDamage(new DamageInfo(transform.position, _grabDamage,
                    Vector2.down, AttackType.Combo1));

            yield return new WaitForSeconds(0.2f);
        }

        protected override IEnumerator OnRecovery()
            => WaitScaled(_recoveryDuration);
    }
}