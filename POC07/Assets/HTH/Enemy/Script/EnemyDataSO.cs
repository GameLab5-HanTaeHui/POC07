// ============================================================
// EnemyDataSO.cs  v2.2
// 적 수치 설정 ScriptableObject
//
// [v2.2 변경]
//   stateTransitionDelay 추가.
//     EnemyAI 상태전환 딜레이 (Chase↔Attack).
//     클수록 적이 둔하게 반응. 0 = 즉각 전환.
//
// [v2.1 변경]
//   attackHitLayer 추가 — 공격 히트박스 감지 전용 레이어.
//   차징 돌진 수치 섹션 추가 (EnemyKnightChargeAttack 용).
//   chargeWindupTime / chargeSpeed / chargeDuration /
//   chargeDamage / chargeKnockbackMultiplier / chargeCooldown / chargeDetectRange
//
// [v2.0 변경]
//   KnightDataSO 제거 → 전 타입 수치 통합.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    public enum EnemyType
    {
        /// <summary> 자물쇠 없는 정지 더미. </summary>
        Dummy,
        /// <summary> 자물쇠 있는 정지 더미. </summary>
        DummyLocked,
        /// <summary> 기사형 — 순찰/추격/공격, 정면 방패 + 등 뒤 자물쇠. </summary>
        Knight,
        /// <summary> 드론형 — 공중 이동, 회전 자물쇠. (추후) </summary>
        Drone,
        /// <summary> 골렘형 — 순서 해제 다중 자물쇠. (추후) </summary>
        Golem,
    }

    /// <summary>
    /// 적 전 타입 수치 통합 ScriptableObject. (v2.2)
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "KEY/Enemy Data", order = 10)]
    public class EnemyDataSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 기본 정보
        // ──────────────────────────────────────────

        [Header("── 기본 정보 ──────────────────────")]
        [Tooltip("적 이름. 디버그 및 UI 표시용.")]
        [SerializeField] public string enemyName = "적";

        [Tooltip("적 타입. EnemyAI 행동 분기에 사용.")]
        [SerializeField] public EnemyType enemyType = EnemyType.Dummy;

        // ──────────────────────────────────────────
        // 체력
        // ──────────────────────────────────────────

        [Header("── 체력 ──────────────────────")]
        [Tooltip("최대 체력.")]
        [Min(1f)]
        [SerializeField] public float maxHp = 100f;

        // ──────────────────────────────────────────
        // 피격 반응 (공용)
        // ──────────────────────────────────────────

        [Header("── 피격 반응 (공용) ──────────────────────")]
        [Tooltip("넉백 초기 속도. 0 = 없음. 권장: 4~10.")]
        [Min(0f)]
        [SerializeField] public float knockbackForce = 6f;

        [Tooltip("넉백 감속 비율. 매 프레임 velocity *= 이 값. 권장: 0.75~0.85.")]
        [Range(0.5f, 0.99f)]
        [SerializeField] public float knockbackDecay = 0.8f;

        [Tooltip("피격 무적 시간 (초). 권장: 0.2~0.5.")]
        [Range(0.1f, 2.0f)]
        [SerializeField] public float iFrameDuration = 0.3f;

        [Tooltip("피격 플래시 깜빡임 간격. 권장: 0.05~0.1.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] public float hitFlashInterval = 0.07f;

        // ──────────────────────────────────────────
        // 이동
        // ──────────────────────────────────────────

        [Header("── 이동 ──────────────────────")]
        [Tooltip("순찰 이동 속도. 권장: 1.5~3.0.")]
        [Min(0f)]
        [SerializeField] public float patrolSpeed = 2f;

        [Tooltip("추격 이동 속도. 권장: 3.0~5.0.")]
        [Min(0f)]
        [SerializeField] public float chaseSpeed = 3.5f;

        [Tooltip("랜덤 정지 최소 시간 (초).")]
        [Min(0.1f)]
        [SerializeField] public float idleTimeMin = 1.0f;

        [Tooltip("랜덤 정지 최대 시간 (초).")]
        [Min(0.1f)]
        [SerializeField] public float idleTimeMax = 3.0f;

        [Tooltip("방향 전환 시 정지 확률. 0=없음 / 1=항상.")]
        [Range(0f, 1f)]
        [SerializeField] public float idleChance = 0.4f;

        // ──────────────────────────────────────────
        // 감지
        // ──────────────────────────────────────────

        [Header("── 감지 ──────────────────────")]
        [Tooltip("순찰 직선 감지 거리 (units).")]
        [Min(0.1f)]
        [SerializeField] public float patrolSightRange = 6f;

        [Tooltip("추격 유지 범위 반지름 (units).")]
        [Min(0.1f)]
        [SerializeField] public float chaseSightRadius = 10f;

        [Tooltip("공격 사정거리 반지름 (units).")]
        [Min(0.1f)]
        [SerializeField] public float attackRange = 1.5f;

        [Tooltip("전방 벽 감지 거리 (units).")]
        [Min(0.1f)]
        [SerializeField] public float wallCheckDistance = 0.6f;

        [Tooltip("발 앞 낭떠러지 감지 하향 거리 (units).")]
        [Min(0.1f)]
        [SerializeField] public float cliffCheckDistance = 1.0f;

        [Tooltip("낭떠러지 감지 발 앞 오프셋 (units).")]
        [Min(0f)]
        [SerializeField] public float cliffCheckOffset = 0.4f;

        // ──────────────────────────────────────────
        // 공격 공통
        // ──────────────────────────────────────────

        [Header("── 공격 공통 ──────────────────────")]
        [Tooltip("근접 공격 데미지.")]
        [Min(0f)]
        [SerializeField] public float attackDamage = 15f;

        [Tooltip("공격 쿨타임 (초).")]
        [Min(0.1f)]
        [SerializeField] public float attackCooldown = 2.0f;

        [Tooltip("히트박스 활성 지속 시간 (초).")]
        [Min(0.05f)]
        [SerializeField] public float attackDuration = 0.3f;

        // ──────────────────────────────────────────
        // 차징 돌진 (EnemyKnightChargeAttack 용) — v2.1
        // ──────────────────────────────────────────

        [Header("── 차징 돌진 (Knight 전용) ──────────────────────")]

        /// <summary> 돌진 준비(경고) 시간 (초). </summary>
        [Tooltip("돌진 전 준비 시간 (초). 경고 모션 재생 구간. 권장: 0.5~1.0.")]
        [Min(0f)]
        [SerializeField] public float chargeWindupTime = 0.6f;

        /// <summary> 돌진 속도 (units/s). </summary>
        [Tooltip("돌진 속도 (units/s). 권장: 10~18.")]
        [Min(1f)]
        [SerializeField] public float chargeSpeed = 14f;

        /// <summary> 돌진 최대 지속 시간 (초). </summary>
        [Tooltip("돌진 최대 지속 시간 (초). 권장: 0.6~1.2.")]
        [Min(0.1f)]
        [SerializeField] public float chargeDuration = 0.8f;

        /// <summary> 돌진 피해량. </summary>
        [Tooltip("돌진 피해량. 권장: attackDamage × 1.5~2.0.")]
        [Min(0f)]
        [SerializeField] public float chargeDamage = 25f;

        /// <summary> 돌진 넉백 배율. knockbackForce × 이 값. </summary>
        [Tooltip("돌진 넉백 배율. knockbackForce × 이 값. 권장: 1.5~2.5.")]
        [Min(0f)]
        [SerializeField] public float chargeKnockbackMultiplier = 2.0f;

        /// <summary> 돌진 쿨타임 (초). </summary>
        [Tooltip("돌진 쿨타임 (초). 권장: 4~8.")]
        [Min(0.1f)]
        [SerializeField] public float chargeCooldown = 5.0f;

        /// <summary>
        /// 차징 발동 감지 범위 (units).
        /// attackRange < 이 값 < chaseSightRadius.
        /// </summary>
        [Tooltip("차징 발동 감지 범위. attackRange < 이 값 < chaseSightRadius. 권장: 4~7.")]
        [Min(0.1f)]
        [SerializeField] public float chargeDetectRange = 5.0f;

        // ──────────────────────────────────────────
        // AI 상태전환 — v2.2
        // ──────────────────────────────────────────

        [Header("── AI 상태전환 ──────────────────────")]

        /// <summary>
        /// Chase → Attack / Attack → Chase 전환 딜레이 (초).
        /// 클수록 적이 둔하게 반응. 0 = 즉각 전환.
        /// Patrol ↔ Idle 전환에는 적용하지 않음.
        /// </summary>
        [Tooltip("Chase↔Attack 전환 딜레이 (초). 클수록 둔하게 반응. 0 = 즉각 전환. 권장: 0.3~0.8.")]
        [Range(0f, 2f)]
        [SerializeField] public float stateTransitionDelay = 0.4f;

        // ──────────────────────────────────────────
        // 레이어
        // ──────────────────────────────────────────

        [Header("── 레이어 ──────────────────────")]

        /// <summary> 플레이어 탐지 레이어. EnemySensor 전용. </summary>
        [Tooltip("플레이어 탐지 레이어. EnemySensor 전용. Player 레이어 선택.")]
        [SerializeField] public LayerMask playerLayer;

        /// <summary> 지형 레이어. EnemySensor 벽/낭떠러지/지면 감지. </summary>
        [Tooltip("지형 레이어. EnemySensor 감지용. Ground 레이어 선택.")]
        [SerializeField] public LayerMask groundLayer;

        /// <summary>
        /// 공격 히트박스 감지 레이어. (v2.1)
        /// EnemyKnightAttack.CheckHit() / ChargeAttack.CheckChargeHitPlayer() 전용.
        /// Physics 2D Matrix: EnemyAttackHit ↔ Player 충돌 ON 필요.
        /// </summary>
        [Tooltip("공격 히트박스 감지 레이어. Player 레이어 선택. " +
                 "Physics 2D Matrix EnemyAttackHit ↔ Player 충돌 ON 필요.")]
        [SerializeField] public LayerMask attackHitLayer;
    }
}