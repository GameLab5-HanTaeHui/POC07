// ============================================================
// TestBossDataSO.cs  v1.1
// 테스트 보스 전용 수치 ScriptableObject
//
// [v1.1 변경 — 처형 분리 수치 추가]
//   executionHoldThreshold : A키 홀드 최소 유지 시간 추가
//                            단타 공격 (performed 즉시) 과 홀드 처형 분리
//   executionCooldown      : 처형 완료 후 재발동 대기 시간 추가
//                            연속 처형 버그 방지
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 전용 수치 ScriptableObject. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [생성 방법]
    ///   Assets 우클릭 → Create → KEY → TestBossDataSO
    ///
    /// [연결]
    ///   TestBossCore._data 에 Inspector 연결.
    /// ────────────────────────────────────────────────────
    /// </summary>
    [CreateAssetMenu(menuName = "KEY/TestBossDataSO", fileName = "TestBossDataSO")]
    public class TestBossDataSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // HP
        // ──────────────────────────────────────────

        [Header("── HP ──────────────────────")]

        /// <summary>
        /// 보스 최대 체력.
        /// 딜타임 중 피격으로 감소. 0 → 처치.
        /// </summary>
        [Tooltip("보스 최대 체력.")]
        [Min(1f)]
        public float maxHp = 300f;

        /// <summary>
        /// 딜타임 중 한 번 공격에 받는 데미지.
        /// 실제 운용에서는 플레이어 공격력으로 대체.
        /// </summary>
        [Tooltip("딜타임 중 공격 한 방 데미지.")]
        [Min(1f)]
        public float dilTimeDamagePerHit = 30f;

        // ──────────────────────────────────────────
        // A키 홀드 처형
        // ──────────────────────────────────────────

        [Header("── A키 홀드 처형 ──────────────────────")]

        /// <summary>
        /// 처형 판정 A키 최소 홀드 시간 (초). [v1.1 추가]
        ///
        /// [역할]
        ///   A키 단타 (콤보 공격) 와 A키 홀드 (처형) 를 분리하는 임계값.
        ///   이 시간 미만 홀드 → 처형 불발 (단타 공격만 나감)
        ///   이 시간 이상 홀드 + 부위 범위 내 → 처형 발동
        ///
        /// [권장값]
        ///   0.3 ~ 0.6초. 너무 짧으면 공격 도중 처형 발동.
        ///   너무 길면 처형 타이밍이 불편함.
        /// </summary>
        [Tooltip("처형 판정 A키 최소 홀드 시간. 단타 공격과 홀드 처형 분리. 권장: 0.3~0.6초.")]
        [Range(0.1f, 2.0f)]
        public float executionHoldThreshold = 0.5f;

        /// <summary>
        /// 처형 완료 후 재발동 대기 시간 (초). [v1.1 추가]
        ///
        /// [역할]
        ///   처형 완료 직후 A키를 계속 누르고 있을 때 즉시 재발동되는 버그 방지.
        ///   이 시간 동안 처형 감지 루프 정지.
        ///   + A키를 한 번 뗀 것도 확인해야 재감지 허용 (_mustReleaseKey).
        ///
        /// [권장값]
        ///   0.5 ~ 1.0초. 처형 연출 체감 시간에 맞게 조정.
        /// </summary>
        [Tooltip("처형 완료 후 재발동 대기 시간. 연속 처형 버그 방지. 권장: 0.5~1.0초.")]
        [Range(0.1f, 3.0f)]
        public float executionCooldown = 0.7f;

        /// <summary>
        /// 처형 발동에 필요한 A키 홀드 시간 (초). ← 이동 시작까지의 추가 딜레이용
        /// 현재 v1.1 에서는 executionHoldThreshold 가 이 역할을 대체.
        /// 하위 호환 유지를 위해 보존.
        /// </summary>
        [Tooltip("처형 이동 시작 홀드 시간 (레거시). executionHoldThreshold 로 대체됨.")]
        [Range(0.1f, 3.0f)]
        public float executionHoldTime = 0.8f;

        /// <summary>
        /// 처형 이동 속도 (units/s).
        /// 플레이어가 부위 위치로 자동 이동할 때 사용.
        /// </summary>
        [Tooltip("처형 이동 속도 (units/s). 권장: 10~20.")]
        [Min(1f)]
        public float executionMoveSpeed = 14f;

        /// <summary>
        /// 처형 이동 완료 판정 거리 (units).
        /// 부위와의 거리가 이 값 이하가 되면 도착으로 판정.
        /// </summary>
        [Tooltip("처형 도착 판정 거리. 권장: 0.3~0.6.")]
        [Min(0.1f)]
        public float executionArrivalDistance = 0.4f;

        /// <summary>
        /// 부위 처형 감지 범위 (units).
        /// 플레이어가 이 범위 내에 있어야 처형 입력이 인식됨.
        /// </summary>
        [Tooltip("부위 처형 감지 범위. 권장: 2.0~4.0.")]
        [Min(0.5f)]
        public float executionDetectRange = 3.0f;

        // ──────────────────────────────────────────
        // 그로기
        // ──────────────────────────────────────────

        [Header("── 그로기 ──────────────────────")]

        /// <summary>
        /// 그로기 지속 시간 (초).
        /// 그로기 중 A키 홀드 처형 가능.
        /// </summary>
        [Tooltip("그로기 지속 시간 (초). 권장: 3.0~5.0.")]
        [Range(1.0f, 10.0f)]
        public float groggyDuration = 4.0f;

        // ──────────────────────────────────────────
        // 딜타임
        // ──────────────────────────────────────────

        [Header("── 딜타임 ──────────────────────")]

        /// <summary>
        /// 딜타임 지속 시간 (초).
        /// 코어 처형 완료 후 이 시간 동안 집중 공격 가능.
        /// </summary>
        [Tooltip("딜타임 지속 시간 (초). 권장: 5.0~10.0.")]
        [Range(1.0f, 30.0f)]
        public float dilTimeDuration = 7.0f;

        // ──────────────────────────────────────────
        // 넉백
        // ──────────────────────────────────────────

        [Header("── 넉백 ──────────────────────")]

        /// <summary>
        /// 피격 시 넉백 힘.
        /// 0 이면 넉백 없음.
        /// </summary>
        [Tooltip("피격 넉백 힘. 0 = 넉백 없음.")]
        [Min(0f)]
        public float knockbackForce = 2.0f;

        /// <summary>
        /// 넉백 감속 계수 (FixedUpdate 마다 곱함).
        /// 낮을수록 빠르게 멈춤.
        /// </summary>
        [Tooltip("넉백 감속 계수. 권장: 0.7~0.9.")]
        [Range(0.5f, 0.99f)]
        public float knockbackDecay = 0.8f;

        // ──────────────────────────────────────────
        // 무적 프레임
        // ──────────────────────────────────────────

        [Header("── 무적 프레임 ──────────────────────")]

        /// <summary>
        /// 피격 후 무적 시간 (초).
        /// 이 시간 동안 추가 피격 무시.
        /// </summary>
        [Tooltip("피격 후 무적 시간 (초).")]
        [Range(0f, 3.0f)]
        public float iFrameDuration = 0.3f;

        // ──────────────────────────────────────────
        // 색상 피드백
        // ──────────────────────────────────────────

        [Header("── 색상 피드백 ──────────────────────")]

        /// <summary>
        /// 팔 해제(Unlocked) 상태 색상.
        /// 플레이어가 봉인해야 하는 상태 — 위험 신호.
        /// </summary>
        [Tooltip("팔 해제 상태 색상. 기본: 붉은색.")]
        public Color armUnlockedColor = new Color(1f, 0.3f, 0.3f, 1f);

        /// <summary>
        /// 팔 봉인(Locked) 상태 색상.
        /// 처형으로 봉인 완료 — 안전 신호.
        /// </summary>
        [Tooltip("팔 봉인 상태 색상. 기본: 파란색.")]
        public Color armLockedColor = new Color(0.3f, 0.5f, 1.0f, 1f);

        /// <summary>
        /// 코어 활성 상태 색상.
        /// 양팔 봉인 완료 → 코어 노출.
        /// </summary>
        [Tooltip("코어 활성 색상. 기본: 노란색.")]
        public Color coreActiveColor = new Color(1f, 0.9f, 0.2f, 1f);

        /// <summary>
        /// 딜타임 중 보스 본체 색상.
        /// 딜타임 진입 피드백.
        /// </summary>
        [Tooltip("딜타임 중 보스 색상. 기본: 주황색.")]
        public Color dilTimeBodyColor = new Color(1f, 0.5f, 0.1f, 1f);
    }
}