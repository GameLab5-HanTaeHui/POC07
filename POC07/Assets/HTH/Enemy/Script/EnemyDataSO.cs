// ============================================================
// EnemyDataSO.cs  v2.0
// 적 수치 설정 ScriptableObject — 전 타입 통합
//
// [v2.0 변경]
//   KnightDataSO 제거 → 이 파일 하나로 모든 적 수치 통합.
//   EnemyType enum 추가 — EnemyAI 가 타입별 행동 분기에 사용.
//   섹션 구성:
//     공용  : 체력 / 넉백 / iFrame  (모든 적 공통)
//     이동  : 순찰속도 / 추격속도 / Idle 설정
//     감지  : Raycast / OverlapCircle 범위
//     공격  : 데미지 / 쿨타임 / 지속시간
//     레이어: playerLayer / groundLayer
//
// [생성 방법]
//   Project 창 우클릭 → Create → KEY → Enemy Data
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    // ──────────────────────────────────────────
    // 적 타입 열거형
    // ──────────────────────────────────────────

    /// <summary>
    /// 적 캐릭터 타입.
    /// EnemyDataSO.enemyType 에 설정 후
    /// EnemyAI 에서 switch 분기에 사용.
    ///
    /// [새 적 추가 방법]
    ///   1. 여기에 항목 추가
    ///   2. EnemyAI 의 OnPatrolMove / OnChaseMove / OnEnterAttack switch 에 케이스 추가
    ///   3. EnemyBase 상속 피격 클래스 작성
    ///   4. EnemyAttackBase 상속 공격 클래스 작성 (모션이 다른 경우)
    /// </summary>
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
    /// 적 전 타입 수치 통합 ScriptableObject. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [사용 흐름]
    ///   1. Project 에서 EnemyData 에셋 생성
    ///   2. enemyType 설정 (Knight, Drone 등)
    ///   3. EnemyBase._settings 에 연결
    ///   4. EnemyAI 가 enemyType 으로 행동 분기
    /// ────────────────────────────────────────────────────
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnemyData",
        menuName = "KEY/Enemy Data",
        order = 10)]
    public class EnemyDataSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 기본 정보
        // ──────────────────────────────────────────

        [Header("── 기본 정보 ──────────────────────")]

        /// <summary>
        /// 적 이름. 디버그 / UI 용.
        /// </summary>
        [Tooltip("적 이름. 디버그 및 UI 표시용.")]
        [SerializeField] public string enemyName = "적";

        /// <summary>
        /// 적 타입.
        /// EnemyAI 의 행동 분기 기준.
        /// </summary>
        [Tooltip("적 타입. EnemyAI 행동 분기에 사용.")]
        [SerializeField] public EnemyType enemyType = EnemyType.Dummy;

        // ──────────────────────────────────────────
        // 체력
        // ──────────────────────────────────────────

        [Header("── 체력 ──────────────────────")]

        /// <summary>
        /// 최대 체력.
        /// 더미 타입은 사망하지 않으므로 시각 확인용.
        /// </summary>
        [Tooltip("최대 체력.")]
        [Min(1f)]
        [SerializeField] public float maxHp = 100f;

        // ──────────────────────────────────────────
        // 피격 반응 (공용)
        // ──────────────────────────────────────────

        [Header("── 피격 반응 (공용) ──────────────────────")]

        /// <summary>
        /// 넉백 초기 속도.
        /// EnemyBase.KnockbackRoutine 에서 velocity.x 에 적용.
        /// 0 = 넉백 없음.
        /// </summary>
        [Tooltip("넉백 초기 속도. 0 = 없음. 권장: 4~10.")]
        [Min(0f)]
        [SerializeField] public float knockbackForce = 6f;

        /// <summary>
        /// 넉백 감속 비율. 매 FixedUpdate 마다 velocity.x 에 곱함.
        /// 0.7 = 빠른 감속 / 0.95 = 느린 감속.
        /// </summary>
        [Tooltip("넉백 감속 비율. 매 프레임 velocity *= 이 값. 권장: 0.75~0.85.")]
        [Range(0.5f, 0.99f)]
        [SerializeField] public float knockbackDecay = 0.8f;

        /// <summary>
        /// 피격 무적 시간 (초).
        /// </summary>
        [Tooltip("피격 무적 시간 (초). 권장: 0.2~0.5.")]
        [Range(0.1f, 2.0f)]
        [SerializeField] public float iFrameDuration = 0.3f;

        /// <summary>
        /// 피격 플래시 깜빡임 간격 (초).
        /// </summary>
        [Tooltip("피격 플래시 깜빡임 간격. 권장: 0.05~0.1.")]
        [Range(0.02f, 0.2f)]
        [SerializeField] public float hitFlashInterval = 0.07f;

        // ──────────────────────────────────────────
        // 이동
        // ──────────────────────────────────────────

        [Header("── 이동 ──────────────────────")]

        /// <summary>
        /// 순찰 이동 속도 (units/s).
        /// Dummy 타입에서는 사용 안 함.
        /// </summary>
        [Tooltip("순찰 이동 속도. 더미 타입 무관. 권장: 1.5~3.0.")]
        [Min(0f)]
        [SerializeField] public float patrolSpeed = 2f;

        /// <summary>
        /// 추격 이동 속도 (units/s).
        /// </summary>
        [Tooltip("추격 이동 속도. 권장: 3.0~5.0.")]
        [Min(0f)]
        [SerializeField] public float chaseSpeed = 3.5f;

        /// <summary>
        /// 순찰 중 랜덤 정지 최소 대기 시간 (초).
        /// </summary>
        [Tooltip("랜덤 정지 최소 시간 (초).")]
        [Min(0.1f)]
        [SerializeField] public float idleTimeMin = 1.0f;

        /// <summary>
        /// 순찰 중 랜덤 정지 최대 대기 시간 (초).
        /// </summary>
        [Tooltip("랜덤 정지 최대 시간 (초).")]
        [Min(0.1f)]
        [SerializeField] public float idleTimeMax = 3.0f;

        /// <summary>
        /// 방향 전환 시 Idle 상태 진입 확률 (0~1).
        /// </summary>
        [Tooltip("방향 전환 시 정지 확률. 0=없음 / 1=항상.")]
        [Range(0f, 1f)]
        [SerializeField] public float idleChance = 0.4f;

        // ──────────────────────────────────────────
        // 감지 범위
        // ──────────────────────────────────────────

        [Header("── 감지 범위 ──────────────────────")]

        /// <summary>
        /// 순찰 중 전방 직선 감지 거리.
        /// </summary>
        [Tooltip("순찰 Raycast 거리. 권장: 5~8.")]
        [Min(0f)]
        [SerializeField] public float patrolSightRange = 6f;

        /// <summary>
        /// 추격 원형 감지 반경.
        /// 이 반경을 벗어나면 Patrol 복귀.
        /// </summary>
        [Tooltip("추격 OverlapCircle 반경. 권장: 8~12.")]
        [Min(0f)]
        [SerializeField] public float chaseSightRadius = 10f;

        /// <summary>
        /// 공격 사정거리 반경.
        /// </summary>
        [Tooltip("공격 사정거리. 권장: 1.0~2.0.")]
        [Min(0f)]
        [SerializeField] public float attackRange = 1.5f;

        /// <summary>
        /// 전방 벽 감지 Raycast 거리.
        /// </summary>
        [Tooltip("벽 감지 Ray 거리. 권장: 0.5~1.0.")]
        [Min(0f)]
        [SerializeField] public float wallCheckDistance = 0.6f;

        /// <summary>
        /// 낭떠러지 감지 하향 Ray 거리.
        /// </summary>
        [Tooltip("낭떠러지 하향 Ray 거리. 권장: 0.5~1.5.")]
        [Min(0f)]
        [SerializeField] public float cliffCheckDistance = 1.0f;

        /// <summary>
        /// 낭떠러지 Ray 시작 X 오프셋 (발 앞쪽).
        /// </summary>
        [Tooltip("낭떠러지 Ray 시작 오프셋. 권장: 0.3~0.6.")]
        [Min(0f)]
        [SerializeField] public float cliffCheckOffset = 0.4f;

        // ──────────────────────────────────────────
        // 공격
        // ──────────────────────────────────────────

        [Header("── 공격 ──────────────────────")]

        /// <summary>
        /// 공격 데미지.
        /// </summary>
        [Tooltip("공격 데미지.")]
        [Min(0f)]
        [SerializeField] public float attackDamage = 15f;

        /// <summary>
        /// 공격 쿨타임 (초).
        /// </summary>
        [Tooltip("공격 쿨타임 (초). 권장: 1.5~3.0.")]
        [Min(0.1f)]
        [SerializeField] public float attackCooldown = 2f;

        /// <summary>
        /// 공격 모션 지속 시간 (초). 히트박스 활성 유지 시간.
        /// </summary>
        [Tooltip("공격 지속 시간 (초). 권장: 0.2~0.5.")]
        [Range(0.1f, 1.0f)]
        [SerializeField] public float attackDuration = 0.3f;

        // ──────────────────────────────────────────
        // 레이어 마스크
        // ──────────────────────────────────────────

        [Header("── 레이어 마스크 ──────────────────────")]

        /// <summary>
        /// 플레이어 감지 레이어. Raycast / OverlapCircle 대상.
        /// </summary>
        [Tooltip("Player 레이어. 필수 설정.")]
        [SerializeField] public LayerMask playerLayer;

        /// <summary>
        /// 지면 레이어. 벽 / 낭떠러지 감지 대상.
        /// </summary>
        [Tooltip("Ground 레이어. 필수 설정.")]
        [SerializeField] public LayerMask groundLayer;
    }
}