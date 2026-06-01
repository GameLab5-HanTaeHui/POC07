// ============================================================
// BossPhase.cs  v1.0
// 보스 Phase / 상태 / 부위 타입 열거형 모음
//
// [역할]
//   BossKnight 시스템 전체에서 공통으로 사용하는 enum 정의.
//   단일 파일에 모아 관리 → 참조 혼선 방지.
//
// [포함 enum]
//   BossPhase     : 보스 페이즈 (Phase1 / Phase2 / Phase3)
//   BossPartType  : 보스 부위 종류
//   BossPatternInterruptResult : 패턴 봉인 결과
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

namespace KEY
{
    // ──────────────────────────────────────────
    // 보스 페이즈
    // ──────────────────────────────────────────

    /// <summary>
    /// 보스 페이즈.
    /// BossPhaseManager 가 HP 임계값에 따라 전환.
    /// </summary>
    public enum BossPhase
    {
        /// <summary>
        /// 1페이즈 — 봉인된 기사.
        /// HP 100% → 50%.
        /// 방패 / 왼팔 / 오른팔 자물쇠 보유.
        /// </summary>
        Phase1,

        /// <summary>
        /// 2페이즈 — 분노한 기사.
        /// HP 50% → 0%.
        /// 검 / 왼팔 / 오른팔 / 코어 자물쇠.
        /// </summary>
        Phase2,

        /// <summary>
        /// 3페이즈 — 해방된 기사.
        /// HP 0% 후 HP 100% 회복.
        /// 왼손검 / 오른손검 / 왼손2 / 오른손2 / 왼팔 / 오른팔 / 코어.
        /// </summary>
        Phase3,
    }

    // ──────────────────────────────────────────
    // 보스 부위 타입
    // ──────────────────────────────────────────

    /// <summary>
    /// 보스 부위 종류.
    /// BossPartComponent._partType 에 설정.
    /// BossKnightAI / BossCounterSystem 에서 부위 식별에 사용.
    /// </summary>
    public enum BossPartType
    {
        /// <summary> 방패. Phase 1 전용. </summary>
        Shield,

        /// <summary> 검. Phase 2 전용. </summary>
        Sword,

        /// <summary> 왼손 검. Phase 3 전용. </summary>
        SwordL,

        /// <summary> 오른손 검. Phase 3 전용. </summary>
        SwordR,

        /// <summary> 왼팔. 전 Phase 공통. </summary>
        ArmL,

        /// <summary> 오른팔. 전 Phase 공통. </summary>
        ArmR,

        /// <summary> 왼손2 (추가 팔). Phase 3 전용. </summary>
        Hand2L,

        /// <summary> 오른손2 (추가 팔). Phase 3 전용. </summary>
        Hand2R,

        /// <summary>
        /// 코어.
        /// 왼팔 + 오른팔 동시 봉인 시 활성화.
        /// A키 홀드 처형 → 딜타임 진입.
        /// </summary>
        Core,
    }

    // ──────────────────────────────────────────
    // 패턴 봉인 결과
    // ──────────────────────────────────────────

    /// <summary>
    /// 봉인 투사체가 패턴에 적중했을 때 결과.
    /// BossPatternBase.OnSealHit() 반환값.
    /// BossCounterSystem 이 결과에 따라 검 무식 / 대타 출동 발동 결정.
    /// </summary>
    public enum BossPatternSealResult
    {
        /// <summary>
        /// 봉인이 그냥 적중함. 반격 없음.
        /// 봉인 불가 패턴 or 예고 중 봉인 불가 상태.
        /// </summary>
        Absorbed,

        /// <summary>
        /// 봉인으로 패턴 중단됨.
        /// 검 무식 발동 조건 (Phase 1/2 봉인 가능 패턴).
        /// BossKnightAI.EnterGroggy() 호출.
        /// </summary>
        Interrupted,

        /// <summary>
        /// 검 무식 발동 요청.
        /// 전투 대기 중 or Phase 3 검 패턴 예고 중.
        /// BossCounterSystem 이 파악하여 처리.
        /// </summary>
        RequestParry,

        /// <summary>
        /// 대타 출동 발동 요청.
        /// Phase 3 주먹 패턴 or 시전 중.
        /// BossCounterSystem 이 파악하여 처리.
        /// </summary>
        RequestIntercept,
    }
}