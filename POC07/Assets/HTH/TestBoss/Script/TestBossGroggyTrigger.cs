// ============================================================
// TestBossGroggyTrigger.cs  v1.0
// 테스트 보스 그로기 진입 트리거 (테스트 전용)
//
// [역할]
//   실제 패턴 시스템 없이 그로기 상태를 강제로 유도하는
//   테스트 전용 유틸리티 컴포넌트.
//
// [사용법]
//   TestBoss 루트 오브젝트 또는 별도 오브젝트에 부착.
//   Inspector 에서 TriggerKey 설정 (기본: F 키).
//   플레이 중 해당 키 입력 → 그로기 진입.
//
//   또는 Unity Editor 의 Inspector 에서
//   TriggerGroggyNow 버튼(Context Menu) 클릭으로도 호출 가능.
//
// [검증 항목]
//   ① 그로기 진입 확인
//   ② A키 홀드 → 팔 봉인 처형 확인 (붉은색 → 파란색)
//   ③ 양팔 봉인 → 코어 활성 확인
//   ④ A키 홀드 → 코어 처형 → 딜타임 진입 확인
//   ⑤ 딜타임 종료 → 양팔 해제 + 충격파 확인
//   ⑥ 루프 반복 확인
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;
using UnityEngine.InputSystem;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 그로기 강제 진입 트리거. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [연결]
    ///   _testBossCore : TestBossCore 컴포넌트 연결 필수.
    ///   TestBoss 루트 오브젝트에 함께 부착하거나
    ///   별도 오브젝트에 부착 후 Inspector 연결.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossGroggyTrigger : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 연결 ──────────────────────")]

        /// <summary>
        /// TestBossCore 참조.
        /// 미연결 시 GetComponent 로 자동 탐색.
        /// </summary>
        [Tooltip("TestBossCore 참조. 미연결 시 자동 탐색.")]
        [SerializeField] private TestBossCore _testBossCore;

        [Header("── 트리거 키 설정 ──────────────────────")]

        /// <summary>
        /// 그로기 강제 진입 키.
        /// 기본값: F 키.
        /// </summary>
        [Tooltip("그로기 강제 진입 키. 기본: F.")]
        [SerializeField] private Key _triggerKey = Key.F;

        /// <summary>
        /// 그로기 지속 시간 오버라이드.
        /// 0 이하면 TestBossDataSO 기본값 사용.
        /// </summary>
        [Tooltip("그로기 지속 시간 오버라이드. 0 이하 = DataSO 기본값.")]
        [Min(0f)]
        [SerializeField] private float _groogyDurationOverride = 0f;

        [Header("── 딜타임 직접 트리거 ──────────────────────")]

        /// <summary>
        /// 딜타임 직접 강제 진입 키.
        /// 핵심 루프 후반 구간만 검증할 때 사용.
        /// 기본값: G 키.
        /// </summary>
        [Tooltip("딜타임 강제 진입 키. 기본: G.")]
        [SerializeField] private Key _dilTimeTriggerKey = Key.G;

        // ──────────────────────────────────────────
        // Unity 라이프사이클
        // ──────────────────────────────────────────

        private void Awake()
        {
            if (_testBossCore == null)
                _testBossCore = GetComponent<TestBossCore>();

            if (_testBossCore == null)
                Debug.LogError("[TestBossGroggyTrigger] TestBossCore 를 찾을 수 없습니다.");
        }

        private void Update()
        {
            if (_testBossCore == null) return;

            // 그로기 강제 진입
            if (Keyboard.current[_triggerKey].wasPressedThisFrame)
            {
                TriggerGroggyNow();
            }

            // 딜타임 강제 진입
            if (Keyboard.current[_dilTimeTriggerKey].wasPressedThisFrame)
            {
                TriggerDilTimeNow();
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API / Context Menu
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 그로기 강제 진입.
        /// Inspector Context Menu 또는 키 입력으로 호출.
        /// </summary>
        [ContextMenu("그로기 강제 진입")]
        public void TriggerGroggyNow()
        {
            if (_testBossCore == null)
            {
                Debug.LogWarning("[TestBossGroggyTrigger] TestBossCore 미연결.");
                return;
            }

            _testBossCore.EnterGroggy(_groogyDurationOverride > 0f ? _groogyDurationOverride : -1f);
            Debug.Log("[TestBossGroggyTrigger] 그로기 강제 진입 실행");
        }

        /// <summary>
        /// 딜타임 강제 진입.
        /// Inspector Context Menu 또는 키 입력으로 호출.
        /// 루프 후반 (딜타임 ~ 종료) 구간만 검증할 때 사용.
        /// </summary>
        [ContextMenu("딜타임 강제 진입")]
        public void TriggerDilTimeNow()
        {
            if (_testBossCore == null)
            {
                Debug.LogWarning("[TestBossGroggyTrigger] TestBossCore 미연결.");
                return;
            }

            _testBossCore.EnterDilTime();
            Debug.Log("[TestBossGroggyTrigger] 딜타임 강제 진입 실행");
        }

        /// <summary>
        /// 보스 완전 리셋.
        /// </summary>
        [ContextMenu("보스 리셋")]
        public void ResetBossNow()
        {
            _testBossCore?.ResetBoss();
        }
    }
}