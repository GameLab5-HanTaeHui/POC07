// ============================================================
// TestBossArmPart.cs  v1.0
// 테스트 보스 팔 부위 컴포넌트
//
// [역할]
//   핵심 플레이 루프의 "팔 봉인" 단계를 담당.
//   팔 1개당 1개의 컴포넌트를 부착 (Arm_L / Arm_R).
//
// [상태]
//   IsUnlocked = true  : 팔이 해제(풀린) 상태 — 붉은색 (시작 상태)
//   IsUnlocked = false : 팔이 봉인(잠긴) 상태 — 파란색
//
// [전환]
//   ReLock()       : 해제 → 봉인 (A키 홀드 처형으로 호출됨)
//   ForceUnlock()  : 봉인 → 해제 (딜타임 종료 시 TestBossCore 에서 호출됨)
//
// [이벤트]
//   OnReLocked  : 봉인 완료 시 발행 → TestBossCore 에서 구독
//   OnUnlocked  : 해제 완료 시 발행 → TestBossCore 에서 구독
//
// [처형 가능 조건]
//   IsUnlocked == true (해제 상태) 일 때만 처형 가능
//   봉인 상태 팔은 처형 불가
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 팔 부위 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [팔 부위 역할]
    ///   - 시작 시 해제(붉은색) 상태
    ///   - 플레이어가 A키 홀드 처형으로 봉인(파란색)
    ///   - 양팔 동시 봉인 → TestBossCore.CheckCoreActivation() 호출
    ///   - 딜타임 종료 시 ForceUnlock() → 다시 해제(붉은색)
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossArmPart : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 부위 식별 ──────────────────────")]

        /// <summary>
        /// 팔 타입 식별자.
        /// TestBossCore 에서 ArmL / ArmR 구분에 사용.
        /// </summary>
        [Tooltip("팔 타입. ArmL 또는 ArmR 설정.")]
        [SerializeField] private TestBossPartType _partType = TestBossPartType.ArmL;

        [Header("── 컴포넌트 연결 ──────────────────────")]

        /// <summary>
        /// 부위 색상 피드백용 SpriteRenderer.
        /// 미연결 시 자식에서 자동 탐색.
        /// </summary>
        [Tooltip("색상 피드백용 SpriteRenderer. 미연결 시 자식에서 자동 탐색.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("── 처형 범위 오버라이드 ──────────────────────")]

        /// <summary>
        /// 처형 감지 범위 오버라이드.
        /// 0 이하면 TestBossDataSO.executionDetectRange 사용.
        /// </summary>
        [Tooltip("처형 감지 범위 오버라이드. 0 이하 = DataSO 값 사용.")]
        [Min(0f)]
        [SerializeField] private float _executionRangeOverride = 0f;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 팔 해제 여부. true = 해제(붉은), false = 봉인(파란). </summary>
        private bool _isUnlocked = true;

        /// <summary> DataSO 참조. TestBossCore 에서 Initialize() 로 주입. </summary>
        private TestBossDataSO _data;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// ReLock() 완료 시 발행.
        /// TestBossCore.CheckCoreActivation() 호출 트리거.
        /// 파라미터: 봉인된 팔 타입.
        /// </summary>
        public event Action<TestBossPartType> OnReLocked;

        /// <summary>
        /// ForceUnlock() 완료 시 발행.
        /// TestBossCore.CheckCoreActivation() 호출 트리거.
        /// 파라미터: 해제된 팔 타입.
        /// </summary>
        public event Action<TestBossPartType> OnUnlocked;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 팔 타입. </summary>
        public TestBossPartType PartType => _partType;

        /// <summary> 팔 해제 상태 여부. </summary>
        public bool IsUnlocked => _isUnlocked;

        /// <summary> 팔 봉인 상태 여부. </summary>
        public bool IsLocked => !_isUnlocked;

        /// <summary>
        /// 처형 감지 범위.
        /// _executionRangeOverride 가 0 이하면 DataSO 기본값 사용.
        /// </summary>
        public float ExecutionRange => (_executionRangeOverride > 0f)
            ? _executionRangeOverride
            : (_data != null ? _data.executionDetectRange : 3.0f);

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // SpriteRenderer 자동 탐색
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화. TestBossCore.Start() 에서 호출.
        /// DataSO 주입 + 해제 상태(시작)로 초기화.
        ///
        /// [핵심 기획]
        ///   팔은 항상 해제(붉은색) 상태로 시작.
        ///   플레이어가 처형으로 봉인해야 코어 활성.
        /// </summary>
        /// <param name="data">TestBossDataSO 참조.</param>
        public void Initialize(TestBossDataSO data)
        {
            _data = data;

            // 항상 해제 상태로 시작
            _isUnlocked = true;
            RefreshColor();

            Debug.Log($"[TestBossArmPart] {_partType} 초기화 — 해제(붉은색) 상태 시작");
        }

        // ══════════════════════════════════════════════════════
        // 봉인 / 해제 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 팔 봉인 (ReLock).
        /// TestBossExecution 에서 처형 완료 시 호출.
        ///
        /// [조건]
        ///   이미 봉인 상태면 무시.
        ///   해제 상태일 때만 처형 대상이 되므로 정상 흐름에서는 항상 해제 상태에서 호출됨.
        /// </summary>
        public void ReLock()
        {
            if (!_isUnlocked) return;

            _isUnlocked = false;
            RefreshColor();

            OnReLocked?.Invoke(_partType);

            Debug.Log($"[TestBossArmPart] {_partType} 봉인 완료 (파란색) → OnReLocked 발행");
        }

        /// <summary>
        /// 팔 강제 해제 (ForceUnlock).
        /// TestBossCore.ExitDilTime() 에서 딜타임 종료 시 호출.
        ///
        /// [기획]
        ///   딜타임 종료 → 양팔 강제 해제 → 루프 반복.
        ///   이미 해제 상태면 무시.
        /// </summary>
        public void ForceUnlock()
        {
            if (_isUnlocked) return;

            _isUnlocked = true;
            RefreshColor();

            OnUnlocked?.Invoke(_partType);

            Debug.Log($"[TestBossArmPart] {_partType} 강제 해제 (붉은색) → OnUnlocked 발행");
        }

        // ══════════════════════════════════════════════════════
        // 색상 피드백
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 상태에 맞게 색상 즉시 갱신.
        /// 해제 = 붉은색 / 봉인 = 파란색.
        /// </summary>
        private void RefreshColor()
        {
            if (_spriteRenderer == null || _data == null) return;

            _spriteRenderer.color = _isUnlocked
                ? _data.armUnlockedColor
                : _data.armLockedColor;
        }

        /// <summary>
        /// 봉인 상태 색상 복구 (외부 API).
        /// 패턴 DOTween 연출이 팔 색상을 변경한 뒤
        /// Recovery 종료 또는 Interrupt 후 호출하여 봉인 색상으로 복구.
        ///
        /// [호출처]
        ///   TestBossPattern_PunchDown — OnRecovery 완료 / Interrupt 후
        ///   TestBossPattern_PunchShot — OnRecovery 완료 / Interrupt 후
        /// </summary>
        public void RestoreArmColor()
        {
            RefreshColor();
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            // 처형 감지 범위 시각화
            float range = (_data != null) ? ExecutionRange : 3.0f;

            Gizmos.color = _isUnlocked
                ? new Color(1f, 0.3f, 0.3f, 0.3f)
                : new Color(0.3f, 0.5f, 1.0f, 0.3f);

            Gizmos.DrawWireSphere(transform.position, range);

#if UNITY_EDITOR
            UnityEditor.Handles.color = _isUnlocked ? Color.red : Color.blue;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                $"{_partType} [{(_isUnlocked ? "해제" : "봉인")}]");
#endif
        }
    }

    // ──────────────────────────────────────────
    // 팔 타입 열거형
    // ──────────────────────────────────────────

    /// <summary>
    /// 테스트 보스 팔 부위 타입.
    /// TestBossCore 에서 L/R 구분에 사용.
    /// </summary>
    public enum TestBossPartType
    {
        /// <summary> 왼팔. </summary>
        ArmL,

        /// <summary> 오른팔. </summary>
        ArmR,

        /// <summary> 코어 (양팔 봉인 후 활성). </summary>
        Core,
    }
}