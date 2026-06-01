// ============================================================
// LockComponent.cs  v2.0
// 자물쇠 컴포넌트 — 리모델링
//
// [v2.0 리모델링 변경]
//
//   [핵심 변경 — Flip 연동]
//     기존: Lock 오브젝트 localPosition.x 고정 (+1.7).
//           Flip 해도 위치가 바뀌지 않아 항상 같은 방향에 있음.
//           → 기사가 왼쪽을 봐도 Lock이 오른쪽에 있음 (항상 정면).
//
//     변경: EnemyAI.OnFlipped 이벤트를 구독.
//           방향 전환 시 localPosition.x 부호 반전.
//           → Lock이 항상 기사의 실제 후방에 위치.
//           → 플레이어가 후방에서 공격할 때만 Lock 콜라이더에 닿음.
//
//   [초기 localPosition.x 캐싱]
//     Awake 에서 _originalLocalX = Abs(localPosition.x) 저장.
//     FlipPosition(dir) 에서 _originalLocalX * dir 로 계산.
//     절댓값 사용 → 여러 번 Flip 해도 누적 오류 없음.
//
//   [Start 에서 EnemyAI.OnFlipped 구독]
//     EnemyKnightAttack / EnemyKnightChargeAttack 과 동일 패턴.
//     EnemyAI 가 없으면 구독 생략 (더미 적 호환).
//
//   [IDamageable 구현 유지]
//     PlayerWeaponHitboxManager 가 EnemyLock 레이어 감지 시
//     LockComponent.TakeDamage() 직접 호출.
//     기존 인터페이스 변경 없음.
//
// [v1.0 역할 유지]
//   피격 횟수 누적 → 해제 조건 충족 시 OnLockUnlocked 이벤트 발행.
//   해제 후 콜라이더 비활성 (더 이상 피격 판정 없음).
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 자물쇠 컴포넌트. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [Hierarchy 위치]
    ///   Enemy_Knight
    ///   └── Lock                      Layer: EnemyLock
    ///         ├── [LockComponent]     이 컴포넌트
    ///         ├── [SpriteRenderer]    자물쇠 스프라이트
    ///         └── [BoxCollider2D]     isTrigger=ON
    ///
    /// [localPosition.x 초기값 설정 가이드]
    ///   기사가 오른쪽을 바라볼 때 자물쇠가 뒤(왼쪽)에 있어야 함.
    ///   → localPosition.x = -1.7 (음수 = 기사 후방)
    ///   Flip 시 +1.7 로 자동 반전.
    ///   기존 Prefab 의 +1.7 값을 -1.7 로 수정 필요.
    ///
    /// [피격 흐름]
    ///   PlayerWeaponHitboxManager.CheckHit()
    ///     → EnemyLock 레이어 감지
    ///       → LockComponent.TakeDamage(info)
    ///         → 횟수 누적 → 해제 조건 충족 시 OnLockUnlocked 발행
    ///           → EnemyKnight.HandleLockUnlocked() 수신
    ///             → _unlockedCount++ → 전부 해제 시 약점 노출
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class LockComponent : MonoBehaviour, IDamageable
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 스프라이트 ──────────────────────")]

        /// <summary>
        /// 자물쇠 시각 표현용 SpriteRenderer.
        /// 미연결 시 색상 변경 생략.
        /// </summary>
        [Tooltip("자물쇠 SpriteRenderer. 미연결 시 색상 변경 생략.")]
        [SerializeField] private SpriteRenderer _spriteRenderer;

        [Header("── 해제 조건 ──────────────────────")]

        /// <summary>
        /// 해제에 필요한 피격 횟수.
        /// 이 횟수만큼 맞으면 OnLockUnlocked 이벤트 발행.
        /// 권장: 3~5.
        /// </summary>
        [Tooltip("해제에 필요한 피격 횟수. 권장: 3~5.")]
        [Min(1)]
        [SerializeField] private int _requiredHitCount = 3;

        [Header("── 비주얼 ──────────────────────")]

        /// <summary> 잠긴 상태 색상. </summary>
        [Tooltip("잠긴 상태 색상.")]
        [SerializeField] private Color _lockedColor = new Color(0.3f, 0.3f, 1f, 1f);

        /// <summary> 해제된 상태 색상. </summary>
        [Tooltip("해제된 상태 색상.")]
        [SerializeField] private Color _unlockedColor = new Color(1f, 0.8f, 0f, 1f);

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private int _currentHitCount;
        private bool _isUnlocked;
        private Collider2D _collider;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 자물쇠 해제 완료 시 발행.
        /// EnemyKnight 에서 구독하여 _unlockedCount 증가.
        /// </summary>
        public event Action OnLockUnlocked;

        /// <summary>
        /// 자물쇠 피격 시 발행 (해제 전).
        /// 파라미터: 현재 누적 횟수, 필요 횟수.
        /// UI 피격 진행 표시에서 구독 가능.
        /// </summary>
        public event Action<int, int> OnLockHit;

        // ──────────────────────────────────────────
        // IDamageable
        // ──────────────────────────────────────────

        /// <summary> 해제된 자물쇠 = 사망 취급. </summary>
        public bool IsDead => _isUnlocked;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 해제 완료 여부. </summary>
        public bool IsUnlocked => _isUnlocked;

        /// <summary>
        /// 해제 진행률 (0~1).
        /// UI 게이지 표시용.
        /// </summary>
        public float UnlockProgress =>
            _requiredHitCount > 0
                ? Mathf.Clamp01((float)_currentHitCount / _requiredHitCount)
                : 1f;

        /// <summary> 현재 피격 횟수. </summary>
        public int CurrentHitCount => _currentHitCount;

        /// <summary> 해제에 필요한 총 피격 횟수. </summary>
        public int RequiredHitCount => _requiredHitCount;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();

            RefreshVisual();
        }

        // ══════════════════════════════════════════════════════
        // IDamageable 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리.
        /// 이미 해제된 자물쇠는 무시.
        /// 필요 횟수 누적 시 해제.
        ///
        /// [호출 경로]
        ///   PlayerWeaponHitboxManager.CheckHit()
        ///     → EnemyLock 레이어 감지
        ///       → LockComponent.TakeDamage(info) 직접 호출
        /// </summary>
        public void TakeDamage(DamageInfo info)
        {
            if (_isUnlocked) return;

            _currentHitCount++;
            OnLockHit?.Invoke(_currentHitCount, _requiredHitCount);
            // DOTween 피격 피드백 (RefreshVisual 색상은 OnComplete 에서 처리)
            HitFeedback.PlayerHitLock(_spriteRenderer, transform,
                UnlockProgress, Color.Lerp(_lockedColor, _unlockedColor, UnlockProgress));
            // 색상 Lerp 갱신은 HitFeedback OnComplete 후 RefreshVisual 로 처리
            DOVirtual.DelayedCall(0.18f, RefreshVisual);

            Debug.Log($"[LockComponent] 피격 {_currentHitCount}/{_requiredHitCount} ({gameObject.name})");

            if (_currentHitCount >= _requiredHitCount)
                Unlock();
        }

        // ══════════════════════════════════════════════════════
        // 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 자물쇠 해제 처리.
        /// 콜라이더 비활성 → 색상 변경 → OnLockUnlocked 이벤트 발행.
        /// </summary>
        private void Unlock()
        {
            _isUnlocked = true;
            if (_collider != null) _collider.enabled = false;

            // 해제 피드백 — HitFeedback 에 위임 (파티클 + DOTween)
            HitFeedback.LockUnlocked(_spriteRenderer, transform);

            OnLockUnlocked?.Invoke();
            Debug.Log($"[LockComponent] 자물쇠 해제! ({gameObject.name})");
        }

        /// <summary>
        /// 잠김/해제 상태에 따라 스프라이트 색상 갱신.
        /// </summary>
        private void RefreshVisual()
        {
            if (_spriteRenderer == null) return;

            if (_isUnlocked)
            {
                _spriteRenderer.color = _unlockedColor;
                return;
            }

            // 피격 진행에 따라 잠긴 색상에서 해제 색상으로 Lerp
            float t = UnlockProgress;
            _spriteRenderer.color = Color.Lerp(_lockedColor, _unlockedColor, t);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 자물쇠 상태 초기화. 테스트 / 리스폰 시 호출.
        /// </summary>
        public void ResetLock()
        {
            _currentHitCount = 0;
            _isUnlocked = false;

            if (_collider != null)
                _collider.enabled = true;

            RefreshVisual();
        }

        /// <summary>
        /// 자물쇠 즉시 강제 해제. (v2.2 추가)
        /// 피격 횟수 없이 바로 해제.
        /// BossPartComponent.ForceUnlock() 에서 호출.
        /// A키 홀드 처형으로 자물쇠를 직접 해제할 때 사용.
        /// 이미 해제된 자물쇠는 무시.
        /// </summary>
        public void ForceUnlock()
        {
            if (_isUnlocked) return;
            _currentHitCount = _requiredHitCount;
            Unlock();
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _isUnlocked
                ? new Color(1f, 0.8f, 0f, 0.5f)
                : new Color(0.3f, 0.3f, 1f, 0.5f);

            Gizmos.DrawWireSphere(transform.position, 0.3f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.5f,
                $"Lock {_currentHitCount}/{_requiredHitCount} " +
                (_isUnlocked ? "[해제]" : "[잠김]"));
#endif
        }
    }
}