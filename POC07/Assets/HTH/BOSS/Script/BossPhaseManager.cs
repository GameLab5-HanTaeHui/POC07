// ============================================================
// BossPhaseManager.cs  v1.0
// 보스 Phase 전환 관리 컴포넌트
//
// [역할]
//   HP 비율을 매 프레임 감시하여 Phase 전환 조건 충족 시 전환 실행.
//   BossKnight.OnDamaged() → CheckPhaseTransition() 호출 방식.
//
// [Phase 전환 규칙]
//   Phase1 → Phase2 : HP <= phase1To2HpRatio (기본 0.5)
//   Phase2 → Phase3 : HP <= phase2To3HpRatio (기본 0.0)
//                     HP 0% 도달 → HP 100% 회복 후 Phase3 진입
//
// [중복 전환 방지]
//   _isTransitioning 플래그로 전환 중 추가 전환 방지.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 보스 Phase 전환 관리 컴포넌트. (v1.0)
    /// </summary>
    public class BossPhaseManager : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private BossKnight _boss;
        private BossKnightDataSO _data;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private bool _isTransitioning;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화. BossKnight.Start() 에서 호출.
        /// </summary>
        public void Initialize(BossKnight boss, BossKnightDataSO data)
        {
            _boss = boss;
            _data = data;
        }

        // ══════════════════════════════════════════════════════
        // Phase 전환 체크
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// HP 비율 체크 후 전환 조건 충족 시 전환 실행.
        /// BossKnight.OnDamaged() 에서 호출.
        /// </summary>
        public void CheckPhaseTransition(float hpRatio)
        {
            if (_isTransitioning) return;
            if (_boss == null || _data == null) return;

            BossPhase current = _boss.CurrentPhase;

            switch (current)
            {
                case BossPhase.Phase1:
                    if (hpRatio <= _data.phase1To2HpRatio)
                        TriggerTransition(BossPhase.Phase2);
                    break;

                case BossPhase.Phase2:
                    if (hpRatio <= _data.phase2To3HpRatio)
                        TriggerTransition(BossPhase.Phase3);
                    break;

                case BossPhase.Phase3:
                    // Phase3 는 실제 사망으로 처리 (BossKnight.Die())
                    break;
            }
        }

        private void TriggerTransition(BossPhase nextPhase)
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            Debug.Log($"[BossPhaseManager] Phase 전환 시작 → {nextPhase}");
            _boss.EnterPhaseTransition(nextPhase);

            // 전환 완료 후 플래그 해제는 BossKnight.PhaseTransitionRoutine 에서
            // OnPhaseChanged 이벤트 구독으로 처리
            _boss.OnPhaseChanged += OnPhaseTransitionComplete;
        }

        private void OnPhaseTransitionComplete(BossPhase newPhase)
        {
            _isTransitioning = false;
            _boss.OnPhaseChanged -= OnPhaseTransitionComplete;
            Debug.Log($"[BossPhaseManager] Phase 전환 완료 → {newPhase}");
        }
    }
}