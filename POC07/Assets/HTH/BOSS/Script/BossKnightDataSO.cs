// ============================================================
// BossKnightDataSO.cs  v1.0
// 봉인된 기사 보스 수치 ScriptableObject
//
// [역할]
//   BossKnight 시스템 전체 수치를 단일 SO 에서 관리.
//   Inspector 연결 지점: BossKnight._bossData 하나.
//   BossKnightAI / BossPatternBase / BossCounterSystem 등
//   모든 보스 컴포넌트는 Initialize() 주입으로 참조.
//
// [구조]
//   Phase별 수치를 Serializable struct 로 묶음.
//   Inspector 에서 Phase1Settings / Phase2Settings / Phase3Settings
//   펼쳐서 편집 가능.
//
// [EnemyDataSO 와의 관계]
//   보스는 EnemyDataSO 를 사용하지 않음.
//   EnemyBase 상속으로 인한 공통 수치(HP, 넉백 등)는
//   이 SO 에서 직접 관리.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 봉인된 기사 보스 수치 SO. (v1.0)
    /// </summary>
    [CreateAssetMenu(fileName = "BossKnightData", menuName = "KEY/Boss/Knight Data", order = 20)]
    public class BossKnightDataSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 기본 정보
        // ──────────────────────────────────────────

        [Header("── 기본 정보 ──────────────────────")]

        [Tooltip("보스 이름. UI 표시용.")]
        [SerializeField] public string bossName = "봉인된 기사";

        /// <summary>
        /// 최대 체력.
        /// Phase 전환 시 HP 회복 기준.
        /// Phase 2→3 전환: HP 0% → HP 100% (이 값으로) 회복.
        /// </summary>
        [Tooltip("보스 최대 체력.")]
        [Min(1f)]
        [SerializeField] public float maxHp = 1000f;

        // ──────────────────────────────────────────
        // 피격 반응 (EnemyBase 공통)
        // ──────────────────────────────────────────

        [Header("── 피격 반응 ──────────────────────")]

        [Tooltip("넉백 초기 속도. 자물쇠 해제 후 본체 피격 시 적용. 0 = 없음.")]
        [Min(0f)]
        [SerializeField] public float knockbackForce = 3f;

        [Tooltip("넉백 감속 비율. 권장: 0.75~0.85.")]
        [Range(0.5f, 0.99f)]
        [SerializeField] public float knockbackDecay = 0.8f;

        [Tooltip("피격 무적 시간 (초).")]
        [Range(0.05f, 2.0f)]
        [SerializeField] public float iFrameDuration = 0.2f;

        [Tooltip("피격 플래시 간격.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] public float hitFlashInterval = 0.05f;

        // ──────────────────────────────────────────
        // Phase 전환
        // ──────────────────────────────────────────

        [Header("── Phase 전환 ──────────────────────")]

        /// <summary>
        /// Phase 1 → 2 전환 HP 비율.
        /// HP 가 이 비율 이하가 되면 Phase 2 진입.
        /// </summary>
        [Tooltip("Phase 1→2 전환 HP 비율. 0.5 = HP 50%.")]
        [Range(0f, 1f)]
        [SerializeField] public float phase1To2HpRatio = 0.5f;

        /// <summary>
        /// Phase 2 → 3 전환 HP 비율.
        /// HP 가 이 비율 이하가 되면 Phase 3 진입 + HP 회복.
        /// </summary>
        [Tooltip("Phase 2→3 전환 HP 비율. 0 = HP 0%.")]
        [Range(0f, 1f)]
        [SerializeField] public float phase2To3HpRatio = 0.0f;

        // ──────────────────────────────────────────
        // 그로기 / 딜타임
        // ──────────────────────────────────────────

        [Header("── 그로기 / 딜타임 ──────────────────────")]

        /// <summary>
        /// 그로기 지속 시간 (초).
        /// 패턴 봉인 성공 / 충돌 후 진입하는 완전 정지 구간.
        /// 플레이어 A키 홀드 처형 가능.
        /// </summary>
        [Tooltip("그로기 지속 시간. 플레이어 처형 타이밍. 권장: 3.0~5.0.")]
        [Min(0.5f)]
        [SerializeField] public float groggyDuration = 4.0f;

        /// <summary>
        /// 코어 딜타임 지속 시간 (초).
        /// 왼팔 + 오른팔 동시 봉인 → 코어 해제 → 딜타임 진입.
        /// 지속 후 자동 코어 봉인 + 충격파.
        /// </summary>
        [Tooltip("코어 딜타임 지속 시간. 권장: 5.0~10.0.")]
        [Min(1.0f)]
        [SerializeField] public float dilTimeDuration = 7.0f;

        // ──────────────────────────────────────────
        // 충격파 (Shockwave)
        // ──────────────────────────────────────────

        [Header("── 충격파 ──────────────────────")]

        /// <summary>
        /// 충격파 범위 반지름.
        /// Phase 전환 / 그로기 회복 / 딜타임 종료 시 발동.
        /// 데미지 없음. 플레이어 밀침만.
        /// </summary>
        [Tooltip("충격파 범위 반지름.")]
        [Min(0.1f)]
        [SerializeField] public float shockwaveRadius = 8f;

        /// <summary>
        /// 충격파 밀침 강도.
        /// 플레이어 Rigidbody2D 에 적용되는 힘.
        /// </summary>
        [Tooltip("충격파 밀침 강도.")]
        [Min(0f)]
        [SerializeField] public float shockwavePower = 20f;

        // ──────────────────────────────────────────
        // A키 홀드 처형
        // ──────────────────────────────────────────

        [Header("── A키 홀드 처형 ──────────────────────")]

        /// <summary>
        /// 처형 발동에 필요한 홀드 시간 (초).
        /// 그로기 상태 + 부위 범위 내 + A키를 이 시간 이상 홀드 시 처형 실행.
        /// </summary>
        [Tooltip("처형 홀드 필요 시간 (초).")]
        [Min(0.1f)]
        [SerializeField] public float executionHoldDuration = 1.5f;

        /// <summary>
        /// 처형 가능 범위.
        /// 플레이어가 부위에서 이 거리 이하일 때 처형 입력 가능.
        /// </summary>
        [Tooltip("처형 가능 범위 반지름.")]
        [Min(0.1f)]
        [SerializeField] public float executionRange = 2.0f;

        // ──────────────────────────────────────────
        // 방향 전환
        // ──────────────────────────────────────────

        [Header("── 방향 전환 ──────────────────────")]

        /// <summary>
        /// Chase / Idle 중 방향 전환 쿨타임 (초).
        /// 플레이어가 보스 등 뒤를 노릴 수 있는 시간 확보.
        /// Groggy 종료 / PhaseTransition 완료 후 즉시 전환은 예외.
        /// </summary>
        [Tooltip("방향 전환 쿨타임. 플레이어 후방 공략 시간 확보. 권장: 2.0~4.0.")]
        [Min(0f)]
        [SerializeField] public float flipCooldown = 3.0f;

        // ──────────────────────────────────────────
        // 회피 기동
        // ──────────────────────────────────────────

        [Header("── 회피 기동 ──────────────────────")]

        /// <summary>
        /// 회피 기동 쿨타임 (초).
        /// 전 패턴 쿨타임 상태일 때 이 시간이 지나야 회피 기동 가능.
        /// 너무 짧으면 플레이어 공격 기회 박탈 → 권장: 8초 이상.
        /// </summary>
        [Tooltip("회피 기동 쿨타임. 권장: 8.0~15.0.")]
        [Min(1.0f)]
        [SerializeField] public float dodgeCooldown = 10.0f;

        /// <summary>
        /// 회피 기동 최소 발동 간격 (초).
        /// 연속 회피 기동 방지.
        /// </summary>
        [Tooltip("회피 기동 최소 발동 간격.")]
        [Min(0f)]
        [SerializeField] public float dodgeMinInterval = 5.0f;

        /// <summary>
        /// 순간이동 오프셋 (units).
        /// 플레이어로부터 이 거리만큼 반대편으로 순간이동.
        /// </summary>
        [Tooltip("순간이동 오프셋. 플레이어 반대편 이동 거리.")]
        [Min(1.0f)]
        [SerializeField] public float dodgeTeleportOffset = 5.0f;

        /// <summary>
        /// 백스탭 이동 속도 (units/s).
        /// </summary>
        [Tooltip("백스탭 이동 속도.")]
        [Min(1.0f)]
        [SerializeField] public float dodgeBackstepSpeed = 8.0f;

        /// <summary>
        /// 백스탭 지속 시간 (초).
        /// </summary>
        [Tooltip("백스탭 지속 시간.")]
        [Min(0.1f)]
        [SerializeField] public float dodgeBackstepDuration = 0.3f;

        // ──────────────────────────────────────────
        // 반격 패턴 (검 무식 / 대타 출동 공통)
        // ──────────────────────────────────────────

        [Header("── 반격 패턴 공통 ──────────────────────")]

        /// <summary>
        /// 반격 패턴 초기 쿨타임 (초).
        /// 전투 시작 후 이 시간 동안 검 무식 / 대타 출동 발동 불가.
        /// 초반 플레이어 불쾌감 방지.
        /// </summary>
        [Tooltip("반격 패턴 초기 쿨타임. 권장: 10~15초.")]
        [Min(0f)]
        [SerializeField] public float counterInitialCooldown = 12.0f;

        /// <summary>
        /// Phase 2 검 무식 쿨타임 (초).
        /// </summary>
        [Tooltip("Phase 2 검 무식 쿨타임.")]
        [Min(1.0f)]
        [SerializeField] public float counterCooldownPhase2 = 60.0f;

        /// <summary>
        /// Phase 3 검 무식 쿨타임 (초).
        /// </summary>
        [Tooltip("Phase 3 검 무식 쿨타임.")]
        [Min(1.0f)]
        [SerializeField] public float counterCooldownPhase3 = 30.0f;

        /// <summary>
        /// 봉인 투사체 감지 반경.
        /// BossCounterSystem 이 이 범위 내 SealProjectile 을 감지.
        /// </summary>
        [Tooltip("봉인 투사체 감지 반경.")]
        [Min(1.0f)]
        [SerializeField] public float counterDetectRadius = 15.0f;

        // ──────────────────────────────────────────
        // 예상 범위 표시
        // ──────────────────────────────────────────

        [Header("── 예상 범위 표시 ──────────────────────")]

        /// <summary>
        /// 패턴 예상 범위 시각화 on/off.
        /// Inspector 에서 토글 가능.
        /// false 로 설정하면 BossRangeIndicator 전부 비활성.
        /// </summary>
        [Tooltip("패턴 예상 범위 시각화. false = 전체 비활성.")]
        [SerializeField] public bool rangeIndicatorEnabled = true;

        // ──────────────────────────────────────────
        // 레이어
        // ──────────────────────────────────────────

        [Header("── 레이어 ──────────────────────")]

        /// <summary>
        /// 보스 공격이 플레이어를 감지하는 레이어.
        /// 패턴 히트박스 OverlapCollider 에 사용.
        /// </summary>
        [Tooltip("공격 히트박스 플레이어 감지 레이어. Player 레이어 선택.")]
        [SerializeField] public LayerMask attackHitLayer;

        /// <summary>
        /// 플레이어 탐지 레이어.
        /// BossCounterSystem SealProjectile 감지 + 처형 거리 체크.
        /// </summary>
        [Tooltip("플레이어 탐지 레이어. Player 레이어 선택.")]
        [SerializeField] public LayerMask playerLayer;

        /// <summary>
        /// 지형 레이어.
        /// 회피 기동 / 충격파 범위 계산에 사용.
        /// </summary>
        [Tooltip("지형 레이어. Ground + Wall 레이어 선택.")]
        [SerializeField] public LayerMask groundLayer;

        // ──────────────────────────────────────────
        // Phase 1 수치
        // ──────────────────────────────────────────

        [Header("── Phase 1 ──────────────────────")]
        [SerializeField]
        public Phase1Settings p1 = new Phase1Settings
        {
            moveSpeed = 2.0f,
            shieldChargeCooldown = 5.0f,
            defenseStanceCooldown = 6.0f,
            defenseStanceDuration = 3.0f,
            punchCooldown = 4.0f,
        };

        // ──────────────────────────────────────────
        // Phase 2 수치
        // ──────────────────────────────────────────

        [Header("── Phase 2 ──────────────────────")]
        [SerializeField]
        public Phase2Settings p2 = new Phase2Settings
        {
            moveSpeed = 3.0f,
            advanceCooldown = 4.0f,
            chargeCooldown = 6.0f,
            swordSlash7Cooldown = 5.0f,
            swordSlash12Cooldown = 7.0f,
        };

        // ──────────────────────────────────────────
        // Phase 3 수치
        // ──────────────────────────────────────────

        [Header("── Phase 3 ──────────────────────")]
        [SerializeField]
        public Phase3Settings p3 = new Phase3Settings
        {
            moveSpeed = 4.0f,
            swordSlash4Cooldown = 5.0f,
            swordSlash0Cooldown = 8.0f,
            swordSlash1Cooldown = 5.0f,
            punchDashCooldown = 4.0f,
            grabCooldown = 7.0f,
        };
    }

    // ══════════════════════════════════════════════════════
    // Phase별 수치 Struct
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// Phase 1 수치 묶음.
    /// Inspector 에서 펼쳐 편집 가능.
    /// </summary>
    [System.Serializable]
    public struct Phase1Settings
    {
        [Tooltip("추적 이동 속도.")]
        [Min(0f)] public float moveSpeed;

        [Tooltip("방패 돌진 쿨타임 (초).")]
        [Min(0f)] public float shieldChargeCooldown;

        [Tooltip("방어 자세 쿨타임 (초).")]
        [Min(0f)] public float defenseStanceCooldown;

        [Tooltip("방어 자세 지속 시간 (초). 권장: 2.0~4.0.")]
        [Min(0f)] public float defenseStanceDuration;

        [Tooltip("주먹 공격 쿨타임 (초).")]
        [Min(0f)] public float punchCooldown;
    }

    /// <summary>
    /// Phase 2 수치 묶음.
    /// </summary>
    [System.Serializable]
    public struct Phase2Settings
    {
        [Tooltip("추적 이동 속도.")]
        [Min(0f)] public float moveSpeed;

        [Tooltip("전방 진군 (3연속 돌진) 쿨타임.")]
        [Min(0f)] public float advanceCooldown;

        [Tooltip("전방 돌격 (긴 돌진) 쿨타임.")]
        [Min(0f)] public float chargeCooldown;

        [Tooltip("검 제식 7 (횡베기 1회) 쿨타임.")]
        [Min(0f)] public float swordSlash7Cooldown;

        [Tooltip("검 제식 12 (짧은+긴 베기) 쿨타임.")]
        [Min(0f)] public float swordSlash12Cooldown;
    }

    /// <summary>
    /// Phase 3 수치 묶음.
    /// </summary>
    [System.Serializable]
    public struct Phase3Settings
    {
        [Tooltip("추적 이동 속도.")]
        [Min(0f)] public float moveSpeed;

        [Tooltip("검 제식 4 (도넛 원형 베기) 쿨타임.")]
        [Min(0f)] public float swordSlash4Cooldown;

        [Tooltip("검 제식 0 (연속 4회 확장 베기) 쿨타임.")]
        [Min(0f)] public float swordSlash0Cooldown;

        [Tooltip("검 제식 1 (직선 돌진 찌르기) 쿨타임.")]
        [Min(0f)] public float swordSlash1Cooldown;

        [Tooltip("주먹 돌진 쿨타임.")]
        [Min(0f)] public float punchDashCooldown;

        [Tooltip("횡 잡기 쿨타임.")]
        [Min(0f)] public float grabCooldown;
    }
}