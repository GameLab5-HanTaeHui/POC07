// ============================================================
// BossPattern_Advance.cs  v1.0
// Phase 2 — 전방 진군 (3연속 돌진) 패턴
//
// [기획]
//   방패를 유지하고 3회 짧은 돌진 반복.
//   각 돌진 사이 정지 구간에서 봉인 감지.
//   1회라도 봉인 성공 → 그로기 진입.
//
// [Warning] 1.0초
//   방패 전진 자세.
//   3회 경로 범위 표시.
//   봉인 가능 구간.
//
// [Active]
//   1회 돌진 → 정지 0.3초(봉인 감지) → 2회 → 정지 → 3회
//   전 3회 성공 시 후딜레이.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// Phase 2 전방 진군 패턴. (v1.0)
    /// </summary>
    public class BossPattern_Advance : BossPatternBase
    {
        [Header("── 전방 진군 설정 ──────────────────────")]

        [Tooltip("각 돌진 속도.")]
        [Min(1f)]
        [SerializeField] private float _dashSpeed = 10f;

        [Tooltip("각 돌진 거리.")]
        [Min(0.5f)]
        [SerializeField] private float _dashDistance = 5f;

        [Tooltip("돌진 사이 정지 시간 (봉인 감지 윈도우).")]
        [Min(0.1f)]
        [SerializeField] private float _pauseBetweenDash = 0.3f;

        [Tooltip("돌진 히트박스.")]
        [SerializeField] private Collider2D _chargeHitbox;

        private Rigidbody2D _rigid2D;
        private bool _sealSuccess;

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
            => _cooldown = data.p2.advanceCooldown;

        private void Awake()
        {
            _rigid2D = GetComponentInParent<Rigidbody2D>();
            _canInterruptDuringWarning = true;
            _canInterruptDuringActive = true;
            _isSwordPattern = false;
        }

        protected override IEnumerator OnWarning()
        {
            yield return WaitScaled(_warningDuration);
        }

        protected override IEnumerator OnActive()
        {
            _sealSuccess = false;
            if (_chargeHitbox != null) _chargeHitbox.enabled = true;

            for (int i = 0; i < 3; i++)
            {
                if (_isInterrupted || _sealSuccess) break;

                // 돌진 실행
                yield return StartCoroutine(PerformDash());

                if (_isInterrupted || _sealSuccess) break;

                // 정지 구간 — 봉인 감지 윈도우
                float pauseElapsed = 0f;
                while (pauseElapsed < _pauseBetweenDash)
                {
                    if (_isInterrupted) break;
                    pauseElapsed += Time.deltaTime;
                    yield return null;
                }
            }

            _rigid2D.linearVelocity = Vector2.zero;
            if (_chargeHitbox != null) _chargeHitbox.enabled = false;

            if (!_isInterrupted && !_sealSuccess)
                TriggerGroggy();
        }

        private IEnumerator PerformDash()
        {
            float facingDir = _ai != null ? _ai.FacingDirection : 1f;
            float traveled = 0f;

            while (traveled < _dashDistance)
            {
                if (_isInterrupted) yield break;

                yield return new WaitForFixedUpdate();

                float step = Mathf.Min(
                    _dashSpeed * Time.fixedDeltaTime,
                    _dashDistance - traveled);

                // 벽 감지
                if (HitWall(facingDir, step + 0.1f))
                {
                    TriggerGroggy();
                    yield break;
                }

                HitPlayer(facingDir, step + 0.2f);
                _rigid2D.MovePosition(_rigid2D.position + new Vector2(facingDir * step, 0f));
                traveled += step;
            }

            _rigid2D.linearVelocity = Vector2.zero;
        }

        // 봉인 성공 시 외부에서 호출 가능하도록 OnSealHit override
        public override BossPatternSealResult OnSealHit(bool isDuringWarning, bool isDuringActive)
        {
            if (isDuringActive)
            {
                _sealSuccess = true;
                TriggerGroggy();
                return BossPatternSealResult.Interrupted;
            }
            return base.OnSealHit(isDuringWarning, isDuringActive);
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
                dmg.TakeDamage(new DamageInfo(transform.position, 15f,
                    new Vector2(dir, 0.1f).normalized, AttackType.Combo1));
        }
    }
}