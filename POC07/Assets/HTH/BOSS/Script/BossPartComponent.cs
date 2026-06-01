// ============================================================
// BossPartComponent.cs  v1.0
// 보스 부위 컴포넌트 — 봉인 상태 + 약점 노출 관리
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
//   A키 홀드 완료 → ReLock() or Unlock() 실행.
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
    /// 보스 부위 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [상태]
    ///   Locked   : 자물쇠 있음. 봉인 상태. LockComponent 피격 누적.
    ///   Unlocked : 자물쇠 해제. 약점 노출. 재잠금 가능.
    ///
    /// [Phase별 활성화]
    ///   _activePhases 에 등록된 Phase 에서만 활성.
    ///   비활성 Phase 에서는 Collider 비활성 + 피격 무시.
    ///
    /// [팔 봉인 효과]
    ///   _affectedPatterns 에 등록된 패턴에
    ///   SetSpeedMultiplier(_sealedSpeedMultiplier) 적용.
    ///   봉인 해제 시 1.0 으로 복귀.
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
        /// 이 부위가 봉인될 때 속도가 느려지는 패턴 목록.
        /// 팔 자물쇠 봉인 → 해당 팔 사용 패턴 느려짐.
        /// </summary>
        [Tooltip("봉인 시 속도 영향받는 패턴 목록.")]
        [SerializeField] private List<BossPatternBase> _affectedPatterns = new();

        /// <summary>
        /// 봉인 시 패턴 속도 배율.
        /// 1.0 = 정상. 2.0 = 2배 느림.
        /// </summary>
        [Tooltip("봉인 시 패턴 속도 배율. 1.0=정상 / 2.0=2배 느림.")]
        [Min(1.0f)]
        [SerializeField] private float _sealedSpeedMultiplier = 1.5f;

        [Header("── 처형 설정 ──────────────────────")]

        /// <summary>
        /// 처형 가능 범위 반지름.
        /// BossExecutionHandler 가 플레이어와의 거리 체크에 사용.
        /// DataSO.executionRange 를 기본값으로, 부위별 override 가능.
        /// </summary>
        [Tooltip("처형 가능 범위. 0 = DataSO 기본값 사용.")]
        [Min(0f)]
        [SerializeField] private float _executionRangeOverride = 0f;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private bool _isUnlocked;
        private bool _isActiveInCurrentPhase;
        private BossPhase _currentPhase;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 자물쇠 해제 완료 시 발행.
        /// BossKnight.HandlePartUnlocked() 가 구독.
        /// </summary>
        public event Action<BossPartType> OnPartUnlocked;

        /// <summary>
        /// 재잠금 완료 시 발행.
        /// </summary>
        public event Action<BossPartType> OnPartReLocked;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        public BossPartType PartType => _partType;
        public bool IsUnlocked => _isUnlocked;
        public bool IsLocked => !_isUnlocked;
        public bool IsActive => _isActiveInCurrentPhase;

        /// <summary>
        /// 처형 가능 범위.
        /// Override 값이 0 이면 DataSO 기본값 사용.
        /// </summary>
        public float ExecutionRange(float dataDefault)
            => _executionRangeOverride > 0f ? _executionRangeOverride : dataDefault;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // LockComponent 자동 탐색
            if (_lockComponent == null)
                _lockComponent = GetComponentInChildren<LockComponent>();

            if (_lockCollider == null && _lockComponent != null)
                _lockCollider = _lockComponent.GetComponent<Collider2D>();
        }

        private void Start()
        {
            // LockComponent 이벤트 구독
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
        /// 자물쇠 초기화 + 활성/비활성 설정 + 속도 배율 리셋.
        /// </summary>
        public void Initialize(BossPhase phase)
        {
            _currentPhase = phase;
            _isActiveInCurrentPhase = _activePhases.Contains(phase);
            _isUnlocked = false;

            // 자물쇠 초기화
            _lockComponent?.ResetLock();

            // 자물쇠 콜라이더 활성/비활성
            if (_lockCollider != null)
                _lockCollider.enabled = _isActiveInCurrentPhase;

            // 패턴 속도 배율 리셋
            ResetSpeedMultiplier();

            Debug.Log($"[BossPartComponent] {_partType} 초기화 — " +
                      $"Phase:{phase} 활성:{_isActiveInCurrentPhase}");
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
        /// LockComponent.OnLockUnlocked 이벤트 수신 시 호출.
        /// 약점 노출 + 패턴 속도 배율 적용.
        /// </summary>
        private void HandleLockUnlocked()
        {
            if (!_isActiveInCurrentPhase) return;

            _isUnlocked = true;

            // 팔 봉인 효과 — 패턴 속도 느려짐
            ApplySpeedMultiplier(_sealedSpeedMultiplier);

            OnPartUnlocked?.Invoke(_partType);

            Debug.Log($"[BossPartComponent] {_partType} 자물쇠 해제 → " +
                      $"패턴 속도 배율: {_sealedSpeedMultiplier}");
        }

        /// <summary>
        /// 재잠금 — A키 홀드 처형으로 자물쇠를 다시 잠금.
        /// BossExecutionHandler 에서 호출.
        /// </summary>
        public void ReLock()
        {
            if (!_isUnlocked) return;

            _isUnlocked = false;
            _lockComponent?.ResetLock();

            // 패턴 속도 배율 복귀
            ResetSpeedMultiplier();

            OnPartReLocked?.Invoke(_partType);

            Debug.Log($"[BossPartComponent] {_partType} 재잠금 완료");
        }

        /// <summary>
        /// 직접 해제 — A키 홀드 처형으로 자물쇠를 직접 해제.
        /// BossExecutionHandler 에서 호출.
        /// </summary>
        public void ForceUnlock()
        {
            if (_isUnlocked) return;
            _lockComponent?.ForceUnlock();
            // HandleLockUnlocked 가 OnLockUnlocked 이벤트로 자동 호출됨
        }

        // ══════════════════════════════════════════════════════
        // 패턴 속도 배율
        // ══════════════════════════════════════════════════════

        private void ApplySpeedMultiplier(float multiplier)
        {
            foreach (var pattern in _affectedPatterns)
                pattern?.SetSpeedMultiplier(multiplier);
        }

        private void ResetSpeedMultiplier()
        {
            foreach (var pattern in _affectedPatterns)
                pattern?.SetSpeedMultiplier(1.0f);
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isUnlocked
                ? new Color(1f, 0.8f, 0f, 0.5f)
                : new Color(0.3f, 0.3f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.6f,
                $"{_partType} {(_isUnlocked ? "[해제]" : "[잠금]")} " +
                $"{(_isActiveInCurrentPhase ? "" : "[비활성]")}");
#endif
        }
    }
}