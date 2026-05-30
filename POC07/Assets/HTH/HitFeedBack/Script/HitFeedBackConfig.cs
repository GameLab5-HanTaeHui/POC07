// ============================================================
// HitFeedbackConfig.cs  v1.0
// 피격 피드백 파티클 프리팹 등록 ScriptableObject
//
// [역할]
//   HitFeedback (static 클래스) 은 파티클 Prefab 을 직접 참조할 수 없음.
//   HitFeedbackConfig.asset 에 Prefab 을 등록하고
//   HitFeedbackInitializer 가 씬 시작 시 HitFeedback.Init() 에 주입.
//
// [등록 파티클 목록]
//   ① fxHitEnemy       : 플레이어 → 적 피격 스파크 (흰+노랑)
//   ② fxHitLock        : 플레이어 → 자물쇠 피격 파티클 (파랑+흰)
//   ③ fxUnlockLock     : 자물쇠 해제 폭발 (금색)
//   ④ fxBlockedShield  : 방패 막힘 파티클 (파랑)
//   ⑤ fxSealApplied    : 봉인 적용 링 이펙트 (파랑+보라)
//
// [사용법]
//   1. Assets/KEY/DataSO/HitFeedbackConfig.asset 생성
//   2. Inspector 에서 각 파티클 Prefab 연결
//   3. HitFeedbackInitializer.cs 를 GameManager 또는 씬 진입 오브젝트에 부착
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 피격 피드백 파티클 프리팹 등록 SO. (v1.0)
    /// </summary>
    [CreateAssetMenu(
        fileName = "HitFeedbackConfig",
        menuName = "KEY/HitFeedback Config",
        order = 20)]
    public class HitFeedbackConfig : ScriptableObject
    {
        [Header("── 피격 파티클 ──────────────────────")]

        /// <summary>
        /// 플레이어 → 적 피격 스파크.
        /// 흰색 + 노란색, 방사형, 0.15초.
        /// </summary>
        [Tooltip("플레이어 → 적 피격 스파크. 흰+노랑 방사형.")]
        [SerializeField] public GameObject fxHitEnemy;

        /// <summary>
        /// 플레이어 → 자물쇠 피격 파티클.
        /// 파란색 + 흰색, 소형, 0.2초.
        /// </summary>
        [Tooltip("플레이어 → 자물쇠 피격. 파랑+흰 소형.")]
        [SerializeField] public GameObject fxHitLock;

        /// <summary>
        /// 자물쇠 해제 폭발 이펙트.
        /// 금색, 방사형, 0.4초. ← 가장 임팩트 큰 이펙트.
        /// </summary>
        [Tooltip("자물쇠 해제 폭발. 금색 방사형. 가장 큰 임팩트.")]
        [SerializeField] public GameObject fxUnlockLock;

        /// <summary>
        /// 방패 막힘 파티클.
        /// 파란색, 0.15초.
        /// </summary>
        [Tooltip("방패 막힘 파티클. 파란색 0.15초.")]
        [SerializeField] public GameObject fxBlockedShield;

        /// <summary>
        /// 봉인 적용 링 이펙트.
        /// 파란색 + 보라색, 원형 방출.
        /// SealComponent.ApplySeal() 에서 호출.
        /// </summary>
        [Tooltip("봉인 적용 링. 파랑+보라 원형 방출.")]
        [SerializeField] public GameObject fxSealApplied;

        [Header("── 스케일 설정 ──────────────────────")]

        /// <summary>
        /// 자물쇠 해제 피격 진행도에 따른 파티클 크기 배율.
        /// 0~1 진행도 × 이 값 = 실제 파티클 스케일.
        /// </summary>
        [Tooltip("자물쇠 피격 진행도 × 이 값 = 파티클 크기.")]
        [Min(0.1f)]
        [SerializeField] public float lockHitScaleMultiplier = 1.5f;
    }
}