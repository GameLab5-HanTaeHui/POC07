// ============================================================
// EnemyDataSO.cs  v4.2
// 적 공통 수치 ScriptableObject — 방향 C 적용
//
// [v4.0 변경 — 공통 수치만 유지]
//
//   [설계 방향 C]
//     EnemyDataSO = 모든 Enemy 타입이 공통으로 쓰는 수치만 보관.
//     타입 전용 수치(chargeSpeed 등)는 해당 Attack 스크립트가
//     Inspector 필드로 직접 관리.
//
//   [제거된 필드]
//     chargeSpeed              → EnemyKnightChargeAttack._chargeSpeed
//     chargeDuration           → EnemyKnightChargeAttack._chargeDuration
//     chargeDamage             → EnemyKnightChargeAttack._chargeDamage
//     chargeCooldown           → EnemyKnightChargeAttack._chargeCooldown
//     (chargeDetectRange 유지  → EnemySensor.CheckChargeRange() 에서 공통 사용)
//     (groggyDuration 유지     → EnemyAI.GroggyRoutine() 에서 공통 사용)
//
//   [유지된 필드]
//     공통: enemyName, enemyType, maxHp
//     피격: knockbackForce, knockbackDecay, iFrameDuration, hitFlashInterval
//     이동: patrolSpeed, chaseSpeed, idleTimeMin, idleTimeMax, idleChance
//     감지: patrolSightRange, chaseSightRadius, chargeDetectRange
//           wallCheckDistance, cliffCheckDistance, cliffCheckOffset
//     AI:   groggyDuration
//     레이어: playerLayer, groundLayer, attackHitLayer
//
//   [추후 Enemy 추가 시]
//     새 Enemy 전용 수치 → 해당 Enemy 의 Attack/Movement 스크립트에 직접 추가.
//     공통 수치(이동속도, 체력, 감지범위 등)만 이 SO 에서 관리.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 종류. EnemyAI 행동 분기에 사용.
    /// </summary>
    public enum EnemyType
    {
        /// <summary> 자물쇠 없는 정지 더미. AI 없음. </summary>
        Dummy,

        /// <summary> 자물쇠 있는 정지 더미. AI 없음. </summary>
        DummyLocked,

        /// <summary>
        /// 기사형.
        /// 전방 방패 돌진. 자물쇠 해제 후 본체 피격 가능.
        /// EnemyKnight + EnemyKnightChargeAttack 전용.
        /// </summary>
        Knight,
    }

    /// <summary>
    /// 적 공통 수치 ScriptableObject. (v4.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [포함 범위 — 공통 수치만]
    ///   ✅ 모든 Enemy 타입이 공통으로 사용하는 수치
    ///      (기본 정보, 체력, 피격 반응, 이동, 감지, 레이어)
    ///
    ///   ❌ 타입 전용 수치 → 각 Attack 스크립트 Inspector 필드
    ///      예) chargeSpeed, chargeDamage → EnemyKnightChargeAttack
    ///          flySpeed, laserDamage     → (추후) EnemyDroneAttack
    ///
    /// [DataSO 연결 지점]
    ///   EnemyBase._settings 에 Inspector 연결 (하나뿐).
    ///   EnemyAI / EnemySensor 는 EnemyBase.Settings 프로퍼티로 참조.
    /// ────────────────────────────────────────────────────
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "KEY/Enemy Data", order = 10)]
    public class EnemyDataSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 기본 정보
        // ──────────────────────────────────────────

        [Header("── 기본 정보 ──────────────────────")]

        /// <summary> 적 이름. 디버그 + UI 표시용. </summary>
        [Tooltip("적 이름. 디버그 및 UI 표시용.")]
        [SerializeField] public string enemyName = "적";

        /// <summary> 적 타입. EnemyAI 행동 분기 기준. </summary>
        [Tooltip("적 타입. EnemyAI 행동 분기 기준.")]
        [SerializeField] public EnemyType enemyType = EnemyType.Dummy;

        // ──────────────────────────────────────────
        // 체력
        // ──────────────────────────────────────────

        [Header("── 체력 ──────────────────────")]

        /// <summary>
        /// 최대 체력.
        /// Lock 전부 해제 후 0 이하가 되면 사망.
        /// Lock 해제 전에는 본체 체력 감소 없음.
        /// </summary>
        [Tooltip("최대 체력. Lock 해제 후 0 이하 → 사망.")]
        [Min(1f)]
        [SerializeField] public float maxHp = 100f;

        // ──────────────────────────────────────────
        // 피격 반응
        // ──────────────────────────────────────────

        [Header("── 피격 반응 ──────────────────────")]

        /// <summary>
        /// 넉백 초기 속도 (units/s).
        /// Lock 해제 후 본체 피격 시 적용. 0 = 넉백 없음.
        /// </summary>
        [Tooltip("넉백 초기 속도. 0 = 없음. 권장: 4~8.")]
        [Min(0f)]
        [SerializeField] public float knockbackForce = 5f;

        /// <summary>
        /// 넉백 감속 비율.
        /// 매 FixedUpdate 마다 velocity.x 에 곱함.
        /// </summary>
        [Tooltip("넉백 감속 비율. 권장: 0.75~0.85.")]
        [Range(0.5f, 0.99f)]
        [SerializeField] public float knockbackDecay = 0.8f;

        /// <summary>
        /// 피격 무적 시간 (초).
        /// 이 시간 동안 추가 피격 무시.
        /// </summary>
        [Tooltip("피격 무적 시간 (초). 권장: 0.2~0.5.")]
        [Range(0.05f, 2.0f)]
        [SerializeField] public float iFrameDuration = 0.3f;

        /// <summary>
        /// 피격 플래시 깜빡임 간격 (초).
        /// </summary>
        [Tooltip("피격 플래시 간격. 권장: 0.05~0.1.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] public float hitFlashInterval = 0.07f;

        // ──────────────────────────────────────────
        // 이동
        // ──────────────────────────────────────────

        [Header("── 이동 ──────────────────────")]

        /// <summary> 순찰 이동 속도 (units/s). </summary>
        [Tooltip("순찰 이동 속도. 권장: 1.5~3.0.")]
        [Min(0f)]
        [SerializeField] public float patrolSpeed = 2f;

        /// <summary> 추격 이동 속도 (units/s). </summary>
        [Tooltip("추격 이동 속도. 권장: 3.0~5.0.")]
        [Min(0f)]
        [SerializeField] public float chaseSpeed = 3.5f;

        /// <summary> 랜덤 정지 최소 시간 (초). </summary>
        [Tooltip("랜덤 정지 최소 시간.")]
        [Min(0.1f)]
        [SerializeField] public float idleTimeMin = 1.0f;

        /// <summary> 랜덤 정지 최대 시간 (초). </summary>
        [Tooltip("랜덤 정지 최대 시간.")]
        [Min(0.1f)]
        [SerializeField] public float idleTimeMax = 3.0f;

        /// <summary>
        /// 방향 전환 시 정지 확률 (0~1).
        /// 0 = 항상 즉시 전환. 1 = 항상 멈춤.
        /// </summary>
        [Tooltip("방향 전환 정지 확률. 0=없음 / 1=항상.")]
        [Range(0f, 1f)]
        [SerializeField] public float idleChance = 0.3f;

        // ──────────────────────────────────────────
        // 감지
        // ──────────────────────────────────────────

        [Header("── 감지 ──────────────────────")]

        /// <summary>
        /// 순찰 중 플레이어 직선 감지 거리.
        /// 이 거리 내에 플레이어가 있으면 Chase 진입.
        /// </summary>
        [Tooltip("순찰 직선 감지 거리. 권장: 4~8.")]
        [Min(0.1f)]
        [SerializeField] public float patrolSightRange = 6f;

        /// <summary>
        /// 추격 유지 범위 반지름.
        /// 이 범위를 벗어나면 Patrol 복귀.
        /// </summary>
        [Tooltip("추격 유지 범위 반지름. 권장: 8~12.")]
        [Min(0.1f)]
        [SerializeField] public float chaseSightRadius = 10f;

        /// <summary>
        /// 특수 공격 발동 감지 범위 반지름.
        /// EnemySensor.CheckChargeRange() 에서 사용.
        /// Knight: 차징 발동 조건.
        /// patrolSightRange 보다 크고 chaseSightRadius 보다 작게 설정.
        /// </summary>
        [Tooltip("특수 공격 발동 감지 범위. 권장: 5~8.")]
        [Min(0.1f)]
        [SerializeField] public float chargeDetectRange = 5f;

        /// <summary> 전방 벽 감지 Raycast 거리. </summary>
        [Tooltip("전방 벽 감지 거리. 권장: 0.5~0.8.")]
        [Min(0.1f)]
        [SerializeField] public float wallCheckDistance = 0.6f;

        /// <summary> 발 앞 낭떠러지 감지 하향 Raycast 거리. </summary>
        [Tooltip("낭떠러지 감지 하향 거리. 권장: 0.8~1.5.")]
        [Min(0.1f)]
        [SerializeField] public float cliffCheckDistance = 1.0f;

        /// <summary>
        /// 낭떠러지 감지 전방 오프셋.
        /// 발 위치에서 이 거리만큼 앞에서 하향 Ray 발사.
        /// </summary>
        [Tooltip("낭떠러지 감지 전방 오프셋. 권장: 0.3~0.5.")]
        [Min(0f)]
        [SerializeField] public float cliffCheckOffset = 0.4f;

        // ──────────────────────────────────────────
        // AI
        // ──────────────────────────────────────────

        [Header("── 근접 공격 (Dash 봉인 시 대체 공격) ──────────────────────")]

        /// <summary>
        /// 근접 1타 피해량.
        /// Dash 봉인 중 차징 대신 사용하는 근접 공격.
        /// 차징보다 약하게 설정 권장.
        /// </summary>
        [Tooltip("근접 1타 피해량. Dash 봉인 시 대체 공격. 권장: 8~15.")]
        [Min(0f)]
        [SerializeField] public float meleeAttackDamage = 10f;

        /// <summary>
        /// 근접 1타 쿨타임 (초).
        /// </summary>
        [Tooltip("근접 공격 쿨타임 (초). 권장: 1.5~3.0.")]
        [Min(0.1f)]
        [SerializeField] public float meleeAttackCooldown = 2.0f;

        /// <summary>
        /// 근접 공격 사정거리 (units).
        /// 기사 위치 기준 전방 이 범위 안에 플레이어가 있으면 공격.
        /// </summary>
        [Tooltip("근접 공격 사정거리. 권장: 1.5~2.5.")]
        [Min(0.1f)]
        [SerializeField] public float meleeAttackRange = 2.0f;

        [Header("── AI ──────────────────────")]

        /// <summary>
        /// Groggy 지속 시간 (초).
        /// 돌진 벽 충돌 또는 봉인 취소 후 진입.
        /// 이 시간 동안 완전 정지 — 플레이어 공략 타이밍.
        /// </summary>
        [Tooltip("그로기 지속 시간. 플레이어 공략 타이밍. 권장: 2.0~3.5.")]
        [Min(0.5f)]
        [SerializeField] public float groggyDuration = 2.5f;

        /// <summary>
        /// Chase 중 방향 전환 쿨타임 (초).
        /// 이 시간이 지나야 UpdateChaseDirection() 에서 방향 전환 가능.
        /// 플레이어가 적 등 뒤를 노릴 수 있는 시간을 확보.
        /// Groggy 종료 시 TurnTowardPlayer() 는 이 쿨타임을 무시하고 즉시 전환.
        /// 권장: 1.5~3.0
        /// </summary>
        [Tooltip("Chase 중 방향 전환 쿨타임 (초). 플레이어 등 뒤 공략 시간 확보. 권장: 1.5~3.0.")]
        [Min(0f)]
        [SerializeField] public float flipCooldown = 2.0f;

        // ──────────────────────────────────────────
        // 레이어
        // ──────────────────────────────────────────

        [Header("── 레이어 ──────────────────────")]

        /// <summary>
        /// 플레이어 탐지 레이어.
        /// EnemySensor Raycast / OverlapCircle 전용.
        /// </summary>
        [Tooltip("플레이어 탐지 레이어. Player 레이어 선택.")]
        [SerializeField] public LayerMask playerLayer;

        /// <summary>
        /// 지형 레이어.
        /// EnemySensor 벽/낭떠러지 감지 + ChargeAttack 벽 충돌 감지.
        /// </summary>
        [Tooltip("지형 레이어. Ground + Wall 레이어 선택.")]
        [SerializeField] public LayerMask groundLayer;

        /// <summary>
        /// 적 공격이 플레이어를 감지하는 레이어.
        /// EnemyKnightChargeAttack.HitPlayer() 등에서 사용.
        ///
        /// [playerLayer 와의 차이]
        ///   playerLayer    : EnemySensor 탐지 전용
        ///   attackHitLayer : 적 공격 히트 판정 전용
        /// </summary>
        [Tooltip("적 공격의 플레이어 감지 레이어. Player 레이어 선택.")]
        [SerializeField] public LayerMask attackHitLayer;
    }
}