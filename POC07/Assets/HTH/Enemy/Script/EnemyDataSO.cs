// ============================================================
// EnemyDataSO.cs  v2.1
// 적 수치 설정 ScriptableObject — attackHitLayer + 차징 수치 추가
//
// [v2.1 변경]
//   attackHitLayer 추가
//     - EnemyKnightAttack.CheckHit() 에서 플레이어 감지 레이어.
//     - 기존: EnemyDataSO.playerLayer (이동/감지용) 을 공격 판정에도 사용.
//     - 변경: attackHitLayer 를 공격 판정 전용으로 분리.
//     - 설정값: Player 레이어 선택.
//     - [배경] playerLayer 는 EnemySensor 의 플레이어 탐지용.
//       공격 히트박스 감지는 별도 레이어로 명확히 분리.
//
//   차징 돌진 수치 섹션 추가 (EnemyKnightChargeAttack 용)
//     - chargeWindupTime  : 돌진 준비(경고) 시간
//     - chargeSpeed       : 돌진 속도
//     - chargeDuration    : 돌진 지속 시간
//     - chargeDamage      : 돌진 피해량
//     - chargeKnockback   : 돌진 넉백 배율
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
    /// 적 전 타입 수치 통합 ScriptableObject. (v2.1)
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
        // 차징 돌진 (EnemyKnightChargeAttack 용)
        // ──────────────────────────────────────────

        [Header("── 차징 돌진 (Knight 전용) ──────────────────────")]

        /// <summary>
        /// 돌진 준비(경고) 시간 (초).
        /// 이 시간 동안 적이 멈추고 경고 모션 / 이펙트 재생.
        /// </summary>
        [Tooltip("돌진 전 준비 시간 (초). 경고 모션 재생 구간. 권장: 0.5~1.0.")]
        [Min(0f)]
        [SerializeField] public float chargeWindupTime = 0.6f;

        /// <summary>
        /// 돌진 속도 (units/s).
        /// 일반 추격 속도보다 훨씬 빨라야 위협감이 있음.
        /// </summary>
        [Tooltip("돌진 속도 (units/s). 권장: 10~18.")]
        [Min(1f)]
        [SerializeField] public float chargeSpeed = 14f;

        /// <summary>
        /// 돌진 최대 지속 시간 (초).
        /// 벽/낭떠러지 충돌 or 이 시간 초과 시 돌진 종료.
        /// </summary>
        [Tooltip("돌진 최대 지속 시간 (초). 권장: 0.6~1.2.")]
        [Min(0.1f)]
        [SerializeField] public float chargeDuration = 0.8f;

        /// <summary>
        /// 돌진 피해량.
        /// 일반 공격보다 크게 설정 권장 (리스크 큰 공격).
        /// </summary>
        [Tooltip("돌진 피해량. 권장: attackDamage × 1.5~2.0.")]
        [Min(0f)]
        [SerializeField] public float chargeDamage = 25f;

        /// <summary>
        /// 돌진 넉백 배율.
        /// 일반 knockbackForce 에 이 값을 곱해 강한 넉백 부여.
        /// </summary>
        [Tooltip("돌진 넉백 배율. knockbackForce × 이 값. 권장: 1.5~2.5.")]
        [Min(0f)]
        [SerializeField] public float chargeKnockbackMultiplier = 2.0f;

        /// <summary>
        /// 돌진 쿨타임 (초).
        /// 일반 공격 쿨타임보다 길게 설정.
        /// </summary>
        [Tooltip("돌진 쿨타임 (초). 권장: 4~8.")]
        [Min(0.1f)]
        [SerializeField] public float chargeCooldown = 5.0f;

        // ──────────────────────────────────────────
        // 그로기 (EnemyAI.Groggy 상태 전용)
        // ──────────────────────────────────────────

        [Header("── 그로기 ──────────────────────")]

        /// <summary>
        /// 그로기 지속 시간 (초).
        /// 돌진 벽 충돌 or 봉인으로 돌진 취소 시 진입.
        /// 이 시간 동안 완전 정지 — 플레이어가 Lock 을 공격할 타이밍.
        /// 권장: 2.0~3.5
        /// </summary>
        [Tooltip("그로기 지속 시간 (초). 돌진 충돌/취소 후 완전 정지 구간. 권장: 2.0~3.5.")]
        [Min(0.5f)]
        [SerializeField] public float groggyDuration = 2.5f;

        /// <summary>
        /// 차징 발동 감지 범위 반경 (units). (v1.1 추가)
        /// EnemySensor.CheckChargeRange() 에서 사용.
        /// attackRange 보다 크고 chaseSightRadius 보다 작게 설정.
        /// 이 범위 안에 플레이어가 있고 차징 쿨타임이 끝나면 차징 공격 선택.
        /// </summary>
        [Tooltip("차징 발동 감지 범위. attackRange < 이 값 < chaseSightRadius. 권장: 4~7.")]
        [Min(0.1f)]
        [SerializeField] public float chargeDetectRange = 5.0f;

        // ──────────────────────────────────────────
        // 레이어
        // ──────────────────────────────────────────

        [Header("── 레이어 ──────────────────────")]

        /// <summary>
        /// 플레이어 탐지 레이어.
        /// EnemySensor 의 Raycast / OverlapCircle 감지 대상.
        /// 설정값: Player 레이어.
        /// </summary>
        [Tooltip("플레이어 탐지 레이어. EnemySensor 전용. Player 레이어 선택.")]
        [SerializeField] public LayerMask playerLayer;

        /// <summary>
        /// 지형 레이어.
        /// EnemySensor 의 벽/낭떠러지/지면 감지 대상.
        /// 설정값: Ground 레이어.
        /// </summary>
        [Tooltip("지형 레이어. EnemySensor 감지용. Ground 레이어 선택.")]
        [SerializeField] public LayerMask groundLayer;

        /// <summary>
        /// 공격 히트박스 감지 레이어. (v2.1 추가)
        /// EnemyKnightAttack.CheckHit() 에서 플레이어 콜라이더 감지.
        /// 설정값: Player 레이어.
        ///
        /// [playerLayer 와의 차이]
        ///   playerLayer   : EnemySensor 전용 — Raycast 탐지
        ///   attackHitLayer: 공격 히트박스 전용 — OverlapCollider 감지
        ///   Physics 2D Matrix: EnemyAttackHit ↔ Player 충돌 ON 필요.
        /// </summary>
        [Tooltip("공격 히트박스 감지 레이어. Player 레이어 선택. " +
                 "Physics 2D Matrix 에서 EnemyAttackHit ↔ Player 충돌 ON 필요.")]
        [SerializeField] public LayerMask attackHitLayer;
    }
}