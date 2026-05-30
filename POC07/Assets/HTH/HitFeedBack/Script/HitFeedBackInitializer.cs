// ============================================================
// HitFeedbackInitializer.cs  v1.0
// HitFeedback 파티클 Config 주입 컴포넌트
//
// [역할]
//   HitFeedback (static) 에 HitFeedbackConfig 를 주입.
//   씬 시작 시 Awake() 에서 HitFeedback.Init() 호출.
//
// [부착 위치]
//   GameManager 또는 씬 진입 오브젝트에 부착.
//   씬 1개당 1개만 존재.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// HitFeedback 파티클 Config 주입 컴포넌트. (v1.0)
    /// </summary>
    public class HitFeedbackInitializer : MonoBehaviour
    {
        [Header("── Config 연결 ──────────────────────")]

        /// <summary>
        /// 파티클 프리팹 설정 SO.
        /// Assets/KEY/DataSO/HitFeedbackConfig.asset 연결.
        /// </summary>
        [Tooltip("HitFeedbackConfig.asset 연결. 필수.")]
        [SerializeField] private HitFeedbackConfig _config;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogWarning("[HitFeedbackInitializer] HitFeedbackConfig 가 연결되지 않았습니다. " +
                                 "파티클 없이 DOTween 만 동작합니다.");
                return;
            }

            HitFeedback.Init(_config);
            Debug.Log("[HitFeedbackInitializer] HitFeedback 파티클 Config 주입 완료.");
        }
    }
}