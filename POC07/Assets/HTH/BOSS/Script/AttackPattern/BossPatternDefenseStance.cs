// ============================================================
// BossPattern_DefenseStance.cs  v1.0
// Phase 1 — 방어 자세 패턴
//
// [기획]
//   전방에 방패를 세워 방어 자세 유지.
//   Guard 봉인 성공 → 방어 해제 + 짧은 경직 (플레이어 공격 기회).
//   봉인 없으면 defenseStanceDuration 동안 유지 후 해제.
//
// [Warning] 0.5초
//   방패를 정면에 세우는 모션.
//   방어 범위 표시.
//
// [Active] defenseStanceDuration 동안
//   방어 상태 유지.
//   ShieldCollider 활성 → 전방 공격 차단.
//   Guard 봉인 감지 루프 → 봉인 시 TriggerGroggy().
//
// [Recovery] 0.5초
//   자세 해제 모션.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// Phase 1 방어 자세 패턴. (v1.0)
    /// </summary>
    public class BossPattern_DefenseStance : BossPatternBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 방어 자세 설정 ──────────────────────")]

        [Tooltip("방어 자세 지속 시간 (초). DataSO.p1.defenseStanceDuration 으로 덮어씀.")]
        [Min(0.5f)]
        [SerializeField] private float _stanceDuration = 3.0f;

        [Tooltip("방어 중 활성화할 방패 Collider2D.")]
        [SerializeField] private Collider2D _shieldCollider;

        [Tooltip("SealComponent 참조. Guard 봉인 감지용.")]
        [SerializeField] private SealComponent _sealComponent;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        protected override void OverrideCooldownFromData(BossKnightDataSO data)
        {
            _cooldown = data.p1.defenseStanceCooldown;
            _stanceDuration = data.p1.defenseStanceDuration;
        }

        private void Awake()
        {
            _canInterruptDuringWarning = false;
            _canInterruptDuringActive = true;   // Guard 봉인으로 중단 가능
            _isSwordPattern = false;

            if (_sealComponent == null)
                _sealComponent = GetComponentInParent<SealComponent>();
        }

        // ══════════════════════════════════════════════════════
        // Warning
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnWarning()
        {
            yield return WaitScaled(_warningDuration);
        }

        // ══════════════════════════════════════════════════════
        // Active
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnActive()
        {
            // 방패 콜라이더 활성 → 전방 공격 차단
            if (_shieldCollider != null)
                _shieldCollider.enabled = true;

            float elapsed = 0f;
            float duration = _stanceDuration * _speedMultiplier;

            while (elapsed < duration)
            {
                if (_isInterrupted) break;

                // Guard 봉인 감지
                if (_sealComponent != null &&
                    _sealComponent.IsSealedAction(SealType.Guard))
                {
                    // 방어 봉인 → 방어 해제 + 경직
                    Debug.Log("[DefenseStance] Guard 봉인 → 방어 해제");
                    TriggerGroggy();
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            // 방패 콜라이더 비활성
            if (_shieldCollider != null)
                _shieldCollider.enabled = false;
        }

        // ══════════════════════════════════════════════════════
        // Recovery
        // ══════════════════════════════════════════════════════

        protected override IEnumerator OnRecovery()
        {
            yield return WaitScaled(_recoveryDuration);
        }
    }
}