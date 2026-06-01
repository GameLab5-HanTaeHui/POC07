// ============================================================
// BossPartComponent.cs  v1.3
// 보스 부위 컴포넌트 — 봉인 상태 + 약점 노출 관리
//
// [v1.3 변경 — 색상 피드백 추가]
//
//   [추가]
//     _partSpriteRenderer : 부위 SpriteRenderer (자식 자동 탐색)
//     _lockedColor        : 잠금 상태 색상 (기본 파란색)
//     _unlockedColor      : 해제 상태 색상 (기본 붉은색)
//     RefreshColor()      : 상태 변화 시 색상 즉시 갱신
//
//   [색상 적용 시점]
//     Initialize()         → 잠금 색상 적용
//     HandleLockUnlocked() → 해제 색상 적용
//     ReLock()             → 잠금 색상 복귀
//     비활성 Phase         → 색상 변경 없음 (원본 유지)
//
// [v1.2 변경 — SpeedMultiplier 방향 수정]
//
//   [기존 v1.0/v1.1 문제]
//     HandleLockUnlocked() 에서 ApplySpeedMultiplier(_sealedSpeedMultiplier) 호출
//     → 자물쇠가 해제될 때 패턴이 느려짐 (기획과 반대)
//     Initialize() 에서 ResetSpeedMultiplier() 호출
//     → Phase 시작 시 봉인 상태임에도 패턴 속도 정상 (기획과 반대)
//     ReLock() 에서 ResetSpeedMultiplier() 호출
//     → 재잠금 시 패턴이 빨라짐 (기획과 반대)
//
//   [기획 의도]
//     봉인 상태 (Locked)   : 패턴 느림 — _sealedSpeedMultiplier 적용
//     해제 상태 (Unlocked) : 패턴 빠름 — 1.0 복귀 (위험 증가)
//     재잠금 시            : 패턴 다시 느려짐 — _sealedSpeedMultiplier 복귀
//
//   [수정 내용]
//     Initialize()         : 활성 부위 봉인 상태 시작
//                            → ApplySpeedMultiplier(_sealedSpeedMultiplier) 호출
//                              (봉인 상태로 Phase 시작이므로 즉시 느려짐 적용)
//     HandleLockUnlocked() : 해제 시 패턴 빠름 적용
//                            → ResetSpeedMultiplier() 호출 (1.0 복귀)
//     ReLock()             : 재잠금 시 패턴 느림 복귀
//                            → ApplySpeedMultiplier(_sealedSpeedMultiplier) 호출
//
// [역할]
//   보스의 각 부위(팔/검/방패/코어 등)에 부착.
//   LockComponent 와 연동하여 잠금/해제 상태 관리.
//   팔 봉인 시 연결된 패턴의 속도 배율 조정.
//   Phase 전환 시 Initialize() 로 초기화.
//
// [A키 홀드 처형과의 연동]
//   BossExecutionHandler 가 그로기 상태에서
//   이 컴포넌트의 ExecutionRange 범위 내 플레이어를 감지.
//   A키 홀드 완료 → ReLock() or ForceUnlock() 실행.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 보스 부위 컴포넌트. (v1.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [상태와 패턴 속도 관계]
    ///   Locked   : 봉인 상태 → 패턴 느림 (_sealedSpeedMultiplier)
    ///   Unlocked : 해제 상태 → 패턴 빠름 (1.0 복귀, 위험 증가)
    ///
    /// [Phase별 활성화]
    ///   _activePhases 에 등록된 Phase 에서만 활성.
    ///   비활성 Phase 에서는 Collider 비활성 + 피격 무시.
    ///   비활성 부위는 패턴 속도 배율 영향 없음 (1.0 유지).
    ///
    /// [SpeedMultiplier 흐름]
    ///   Initialize()         → 활성 부위 : ApplySpeedMultiplier (봉인 시작)
    ///                          비활성 부위: ResetSpeedMultiplier (영향 없음)
    ///   HandleLockUnlocked() → ResetSpeedMultiplier (해제 → 빨라짐)
    ///   ReLock()             → ApplySpeedMultiplier (재잠금 → 느려짐)
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class BossPartComponent : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 부위 설정 ──────────────────────")]

        /// <summary>
        /// 부위 타입. BossCoreLock / BossKnightAI 에서 타입으로 식별.
        /// </summary>
        [Tooltip("부위 타입.")]
        [SerializeField] private BossPartType _partType;

        /// <summary>
        /// 이 부위가 활성화되는 Phase 목록.
        /// 등록되지 않은 Phase 에서는 Collider 비활성.
        /// </summary>
        [Tooltip("이 부위가 활성화되는 Phase. 비활성 Phase 에서는 피격 무시.")]
        [SerializeField] private List<BossPhase> _activePhases = new();

        [Header("── 자물쇠 연결 ──────────────────────")]

        /// <summary>
        /// 이 부위의 자물쇠 LockComponent.
        /// 미연결 시 자동 탐색 (자식 오브젝트).
        /// </summary>
        [Tooltip("자물쇠 LockComponent. 미연결 시 자식에서 자동 탐색.")]
        [SerializeField] private LockComponent _lockComponent;

        /// <summary>
        /// 자물쇠 콜라이더 (LockComponent 와 같은 오브젝트의 Collider2D).
        /// Phase 비활성 시 이 Collider 를 비활성.
        /// </summary>
        [Tooltip("자물쇠 콜라이더. Phase 비활성 시 비활성화.")]
        [SerializeField] private Collider2D _lockCollider;

        [Header("── 패턴 속도 영향 ──────────────────────")]

        /// <summary>
        /// 이 부위가 봉인(Locked) 상태일 때 느려지는 패턴 목록.
        /// 봉인 → 패턴 느림 / 해제 → 1.0 복귀.
        /// </summary>
        [Tooltip("봉인 시 속도 영향받는 패턴 목록.")]
        [SerializeField] private List<BossPatternBase> _affectedPatterns = new();

        /// <summary>
        /// 봉인(Locked) 시 패턴 속도 배율.
        /// 1.0 = 정상속도. 2.0 = 2배 느림.
        /// 해제(Unlocked) 시에는 항상 1.0 으로 복귀.
        /// </summary>
        [Tooltip("봉인 시 패턴 속도 배율. 1.0=정상 / 2.0=2배 느림.")]
        [Min(1.0f)]
        [SerializeField] private float _sealedSpeedMultiplier = 1.5f;

        [Header("── 색상 피드백 ──────────────────────")]

        /// <summary>
        /// 부위 SpriteRenderer.
        /// 잠금/해제 상태에 따라 색상 변경.
        /// 미연결 시 자식에서 자동 탐색.
        /// </summary>
        [Tooltip("부위 SpriteRenderer. 미연결 시 자식에서 자동 탐색.")]
        [SerializeField] private SpriteRenderer _partSpriteRenderer;

        /// <summary>
        /// 잠금(Locked) 상태 색상.
        /// 기본값: 파란색 — 봉인 상태 시각화.
        /// </summary>
        [Tooltip("잠금 상태 색상. 기본: 파란색.")]
        [SerializeField] private Color _lockedColor = new Color(0.3f, 0.5f, 1.0f, 1.0f);

        /// <summary>
        /// 해제(Unlocked) 상태 색상.
        /// 기본값: 붉은색 — 해제(위험) 상태 시각화.
        /// </summary>
        [Tooltip("해제 상태 색상. 기본: 붉은색.")]
        [SerializeField] private Color _unlockedColor = new Color(1.0f, 0.3f, 0.3f, 1.0f);

        [Header("── 처형 설정 ──────────────────────")]

        /// <summary>
        /// 처형 가능 범위 반지름.
        /// BossExecutionHandler 가 플레이어와의 거리 체크에 사용.
        /// 0 = DataSO.executionRange 기본값 사용.
        /// </summary>
        [Tooltip("처형 가능 범위. 0 = DataSO 기본값 사용.")]
        [Min(0f)]
        [SerializeField] private float _executionRangeOverride = 0f;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 자물쇠 해제 여부. false = 봉인(Locked). </summary>
        private bool _isUnlocked;

        /// <summary> 현재 Phase 에서 활성화된 부위인지 여부. </summary>
        private bool _isActiveInCurrentPhase;

        /// <summary> 현재 Phase. </summary>
        private BossPhase _currentPhase;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 자물쇠 해제 완료 시 발행.
        /// BossKnight.HandlePartUnlocked() 가 구독.
        /// BossCoreLock.CheckCoreActivation() 호출 트리거.
        /// </summary>
        public event Action<BossPartType> OnPartUnlocked;

        /// <summary>
        /// 재잠금 완료 시 발행.
        /// BossCoreLock.CheckCoreActivation() 호출 트리거.
        /// </summary>
        public event Action<BossPartType> OnPartReLocked;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 부위 타입. </summary>
        public BossPartType PartType => _partType;

        /// <summary> 자물쇠 해제 여부. </summary>
        public bool IsUnlocked => _isUnlocked;

        /// <summary> 자물쇠 봉인 여부. </summary>
        public bool IsLocked => !_isUnlocked;

        /// <summary> 현재 Phase 에서 활성화된 부위인지 여부. </summary>
        public bool IsActive => _isActiveInCurrentPhase;

        /// <summary>
        /// 처형 가능 범위.
        /// _executionRangeOverride 가 0 이면 dataDefault 사용.
        /// </summary>
        public float ExecutionRange(float dataDefault)
            => _executionRangeOverride > 0f ? _executionRangeOverride : dataDefault;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_lockComponent == null)
                _lockComponent = GetComponentInChildren<LockComponent>();

            if (_lockCollider == null && _lockComponent != null)
                _lockCollider = _lockComponent.GetComponent<Collider2D>();

            // 색상 피드백용 SpriteRenderer 자동 탐색
            if (_partSpriteRenderer == null)
                _partSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Start()
        {
            if (_lockComponent != null)
                _lockComponent.OnLockUnlocked += HandleLockUnlocked;
        }

        private void OnDestroy()
        {
            if (_lockComponent != null)
                _lockComponent.OnLockUnlocked -= HandleLockUnlocked;
        }

        // ══════════════════════════════════════════════════════
        // Phase 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Phase 전환 시 초기화. BossKnight.InitializePhase() 에서 호출.
        ///
        /// [v1.2 수정]
        ///   활성 부위 → 봉인 상태로 시작 → ApplySpeedMultiplier 호출
        ///   비활성 부위 → ResetSpeedMultiplier 호출 (영향 없음)
        ///
        /// [Phase 시작 시 봉인 상태가 맞는 이유]
        ///   기획: Phase 전환 시 자물쇠 전부 초기화 (봉인 상태로 복귀)
        ///   → 활성 부위는 봉인(Locked) 상태로 시작해야 함
        ///   → 봉인 상태 = 패턴 느림이므로 SpeedMultiplier 적용
        /// </summary>
        public void Initialize(BossPhase phase)
        {
            _currentPhase = phase;
            _isActiveInCurrentPhase = _activePhases.Contains(phase);
            _isUnlocked = false;

            // 자물쇠 초기화 (봉인 상태로 리셋)
            _lockComponent?.ResetLock();

            // 자물쇠 콜라이더 활성/비활성
            if (_lockCollider != null)
                _lockCollider.enabled = _isActiveInCurrentPhase;

            // ★ v1.2 수정: 활성 부위 = 봉인 상태 시작 → 패턴 느림 적용
            //              비활성 부위 = 영향 없음 → 1.0 복귀
            if (_isActiveInCurrentPhase)
                ApplySpeedMultiplier(_sealedSpeedMultiplier);
            else
                ResetSpeedMultiplier();

            // 색상 피드백 — 활성 부위만 적용
            if (_isActiveInCurrentPhase)
                RefreshColor();

            Debug.Log($"[BossPartComponent] {_partType} 초기화 — " +
                      $"Phase:{phase} 활성:{_isActiveInCurrentPhase} " +
                      $"배율:{(_isActiveInCurrentPhase ? _sealedSpeedMultiplier : 1.0f)}");
        }

        /// <summary>
        /// 현재 Phase 에서 이 부위가 활성인지 확인.
        /// BossKnight.IsAllLocksCleared() 에서 사용.
        /// </summary>
        public bool IsCurrentPhaseActive(BossPhase phase)
            => _activePhases.Contains(phase);

        // ══════════════════════════════════════════════════════
        // 잠금 / 해제
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// LockComponent.OnLockUnlocked 이벤트 수신.
        ///
        /// [v1.2 수정]
        ///   해제 = 패턴 빠름 → ResetSpeedMultiplier() (1.0 복귀)
        ///   기획: 자물쇠 해제할수록 패턴 위험도 증가
        /// </summary>
        private void HandleLockUnlocked()
        {
            if (!_isActiveInCurrentPhase) return;

            _isUnlocked = true;

            // ★ v1.2 수정: 해제 → 패턴 1.0 복귀 (빨라짐 = 위험 증가)
            ResetSpeedMultiplier();

            // 색상 피드백: 해제 = 붉은색
            RefreshColor();

            OnPartUnlocked?.Invoke(_partType);

            Debug.Log($"[BossPartComponent] {_partType} 자물쇠 해제 → 패턴 속도 1.0 복귀 (위험 증가)");
        }

        /// <summary>
        /// 재잠금. A키 홀드 처형 완료 시 BossExecutionHandler 에서 호출.
        ///
        /// [v1.2 수정]
        ///   재잠금 = 패턴 느림 복귀 → ApplySpeedMultiplier(_sealedSpeedMultiplier)
        ///   기획: 재봉인 시 패턴 속도/시전 시간 원래대로 복귀
        /// </summary>
        public void ReLock()
        {
            if (!_isUnlocked) return;

            _isUnlocked = false;
            _lockComponent?.ResetLock();

            // ★ v1.2 수정: 재잠금 → 패턴 느림 복귀
            ApplySpeedMultiplier(_sealedSpeedMultiplier);

            // 색상 피드백: 재잠금 = 파란색
            RefreshColor();

            OnPartReLocked?.Invoke(_partType);

            Debug.Log($"[BossPartComponent] {_partType} 재잠금 → 패턴 속도 {_sealedSpeedMultiplier} 복귀");
        }

        /// <summary>
        /// 직접 해제. A키 홀드 처형(잠긴 부위) 시 BossExecutionHandler 에서 호출.
        /// HandleLockUnlocked 가 OnLockUnlocked 이벤트로 자동 호출됨.
        /// </summary>
        public void ForceUnlock()
        {
            if (_isUnlocked) return;
            _lockComponent?.ForceUnlock();
        }

        // ══════════════════════════════════════════════════════
        // 패턴 속도 배율
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 연결된 패턴에 속도 배율 적용.
        /// 봉인(Locked) 상태 시 호출.
        /// </summary>
        private void ApplySpeedMultiplier(float multiplier)
        {
            foreach (var pattern in _affectedPatterns)
                pattern?.SetSpeedMultiplier(multiplier);
        }

        /// <summary>
        /// 연결된 패턴 속도 배율을 1.0 으로 리셋.
        /// 해제(Unlocked) 상태 시 호출.
        /// </summary>
        private void ResetSpeedMultiplier()
        {
            foreach (var pattern in _affectedPatterns)
                pattern?.SetSpeedMultiplier(1.0f);
        }

        // ══════════════════════════════════════════════════════
        // 색상 피드백
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 잠금/해제 상태에 맞게 색상 갱신.
        ///   잠금(Locked)   → _lockedColor   (기본 파란색)
        ///   해제(Unlocked) → _unlockedColor (기본 붉은색)
        /// </summary>
        private void RefreshColor()
        {
            if (_partSpriteRenderer == null) return;
            _partSpriteRenderer.color = _isUnlocked ? _unlockedColor : _lockedColor;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isUnlocked
                ? new Color(1f, 0.3f, 0.3f, 0.5f)   // 붉은색 = 해제
                : new Color(0.3f, 0.5f, 1f, 0.5f);   // 파란색 = 잠금

            Gizmos.DrawWireSphere(transform.position, 0.4f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                $"{_partType} " +
                $"{(_isUnlocked ? "[해제🔴] 속도:1.0" : $"[잠금🔵] 속도:{_sealedSpeedMultiplier}")} " +
                $"{(_isActiveInCurrentPhase ? "" : "[비활성]")}");
#endif
        }
    }
}