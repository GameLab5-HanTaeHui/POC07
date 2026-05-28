// ============================================================
// EnemyKnight.cs  v1.4
// 기사형 적 — 자물쇠 List 확장
//
// [v1.4 변경 — LockComponent 단일 → List]
//   _backLock (단일) → _locks List<LockComponent> 로 변경.
//   Inspector 에서 여러 자물쇠 연결 가능.
//   미연결 시 Awake 에서 GetComponentsInChildren 으로 자동 수집.
//
//   [해제 조건 — 전부 해제 (기본값)]
//     _unlockedCount == _locks.Count 일 때 약점 노출.
//     추후 속성 자물쇠 / 부위별 조건으로 확장 가능한 구조.
//
//   [후면 공격 처리]
//     잠긴 자물쇠(_locks 中 IsUnlocked == false)를 순서대로 탐색.
//     첫 번째 잠긴 자물쇠에 TakeDamage 전달.
//     모두 해제 상태면 후면 공격도 EnemyBase 정상 피격.
//
//   [이벤트 구독]
//     Start() 에서 _locks 전체 순회하여 각각 OnLockUnlocked / OnLockHit 구독.
//     OnDestroy() 에서 전체 구독 해제.
//
// [v1.3 변경]
//   방어 로직 명확화 — 정면/후면/봉인 분기.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 적. EnemyBase 상속. (v1.4)
    ///
    /// ────────────────────────────────────────────────────
    /// [전투 흐름]
    ///   자물쇠 전부 해제 전 → 정면 공격 무효 / 후면 → 자물쇠 피격
    ///   자물쇠 전부 해제 후 → 모든 방향 정상 피격
    ///   Guard 봉인 활성    → 자물쇠 미해제여도 방패 무시 피격
    ///
    /// [자물쇠 여러 개 처리]
    ///   후면 공격 → 첫 번째 잠긴 자물쇠에 TakeDamage 전달
    ///   모든 자물쇠 해제 → _isAllLocksUnlocked = true → 약점 노출
    ///
    /// [추후 확장 포인트]
    ///   - 속성 자물쇠 (특정 공격 유형만 해제)
    ///   - 부위별 자물쇠 (다리/팔/머리 → 부위별 효과)
    ///   - 일부 해제 조건 (n개 중 m개 해제 시 약점 노출)
    ///   → CheckAllUnlocked() 메서드 하나만 수정하면 됨
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemyKnight : EnemyBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 자물쇠 연결 ──────────────────────")]

        /// <summary>
        /// 자물쇠 LockComponent 리스트.
        /// Inspector 에서 드래그 연결. 순서 = 피격 우선순위.
        /// 미연결 시 Awake 에서 GetComponentsInChildren 으로 자동 수집.
        ///
        /// [추후 속성 자물쇠 추가 시]
        ///   이 리스트에 새 LockComponent 를 드래그 추가만 하면 됨.
        /// </summary>
        [Tooltip("자물쇠 LockComponent 리스트. 순서 = 피격 우선순위. " +
                 "미연결 시 자동 수집.")]
        [SerializeField] private List<LockComponent> _locks = new List<LockComponent>();

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private EnemyAI _enemyAI;
        private EnemySealComponent _sealComponent;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재까지 해제된 자물쇠 수.
        /// OnLockUnlocked 이벤트 수신 시 증가.
        /// </summary>
        private int _unlockedCount;

        /// <summary>
        /// 모든 자물쇠 해제 여부.
        /// true = 방패 해제 → EnemyBase 정상 피격.
        /// false = 방패 활성 → 정면 공격 무효, 후면 → 자물쇠 피격.
        /// </summary>
        private bool _isAllLocksUnlocked;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();

            _enemyAI = GetComponent<EnemyAI>();
            _sealComponent = GetComponent<EnemySealComponent>();

            // Inspector 미연결 시 자동 수집
            if (_locks.Count == 0)
            {
                var found = GetComponentsInChildren<LockComponent>();
                _locks.AddRange(found);
            }

            if (_locks.Count == 0)
                Debug.LogWarning("[EnemyKnight] LockComponent 가 없습니다. " +
                                 "자물쇠 없이 시작합니다.");
        }

        private void Start()
        {
            foreach (var lock_ in _locks)
            {
                if (lock_ == null) continue;
                lock_.OnLockUnlocked += HandleLockUnlocked;
                lock_.OnLockHit += HandleLockHit;
            }
        }

        private void OnDestroy()
        {
            foreach (var lock_ in _locks)
            {
                if (lock_ == null) continue;
                lock_.OnLockUnlocked -= HandleLockUnlocked;
                lock_.OnLockHit -= HandleLockHit;
            }
        }

        // ══════════════════════════════════════════════════════
        // IDamageable override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 기사형 피격 처리. (v1.4)
        ///
        /// [분기 흐름]
        ///   ① 모든 자물쇠 해제 완료
        ///      → EnemyBase.TakeDamage() 정상 처리
        ///
        ///   ② 자물쇠 미해제 + Guard 봉인 활성
        ///      → 방패 무시 → EnemyBase.TakeDamage()
        ///
        ///   ③ 자물쇠 미해제 + Guard 봉인 없음 + 정면 공격
        ///      → 방패 완전 무효 (반응 없음)
        ///
        ///   ④ 자물쇠 미해제 + Guard 봉인 없음 + 후면 공격
        ///      → 첫 번째 잠긴 자물쇠에 TakeDamage 전달
        ///         자물쇠 없으면 EnemyBase 정상 피격
        /// </summary>
        public new void TakeDamage(DamageInfo info)
        {
            // ① 모든 자물쇠 해제 완료 → 정상 피격
            if (_isAllLocksUnlocked)
            {
                base.TakeDamage(info);
                return;
            }

            // ② Guard 봉인 활성 → 방패 무시
            bool guardSealed = _sealComponent != null
                && _sealComponent.IsSealedAction(SealType.Guard);

            if (guardSealed)
            {
                Debug.Log("[EnemyKnight] Guard 봉인 활성 → 방패 무시 피격!");
                base.TakeDamage(info);
                return;
            }

            // ③ 정면 공격 → 방패 완전 무효
            if (IsFrontalAttack(info.Direction))
            {
                Debug.Log("[EnemyKnight] 정면 방패 → 공격 무효");
                return;
            }

            // ④ 후면 공격 → 첫 번째 잠긴 자물쇠에 전달
            LockComponent targetLock = GetFirstLockedLock();

            if (targetLock != null)
            {
                Debug.Log($"[EnemyKnight] 후면 공격 → 자물쇠 피격 " +
                          $"({_locks.IndexOf(targetLock) + 1}/{_locks.Count})");
                targetLock.TakeDamage(info);
            }
            else
            {
                // 자물쇠 없는 상태 — 후면 공격 정상 피격
                Debug.Log("[EnemyKnight] 후면 공격 → 자물쇠 없음, 정상 피격");
                base.TakeDamage(info);
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 개별 자물쇠 해제 완료 수신.
        /// 전부 해제되면 _isAllLocksUnlocked = true → 약점 노출.
        /// </summary>
        private void HandleLockUnlocked()
        {
            _unlockedCount++;
            Debug.Log($"[EnemyKnight] 자물쇠 해제 {_unlockedCount}/{_locks.Count}");

            if (CheckAllUnlocked())
            {
                _isAllLocksUnlocked = true;
                OnAllLocksUnlocked();
            }
        }

        private void HandleLockHit(int current, int required)
        {
            Debug.Log($"[EnemyKnight] 자물쇠 피격 {current}/{required}");
        }

        // ══════════════════════════════════════════════════════
        // 해제 조건 판별
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 전부 해제 조건 확인.
        ///
        /// [추후 확장 포인트]
        ///   - 일부 해제 조건: _unlockedCount >= requiredCount
        ///   - 속성 조건: 특정 타입 자물쇠만 해제 여부 체크
        ///   이 메서드만 수정하면 전체 해제 조건 변경 가능.
        /// </summary>
        private bool CheckAllUnlocked()
        {
            if (_locks.Count == 0) return true;
            return _unlockedCount >= _locks.Count;
        }

        /// <summary>
        /// 모든 자물쇠 해제 완료 처리.
        /// 색상 변경으로 약점 노출 피드백.
        /// </summary>
        private void OnAllLocksUnlocked()
        {
            Debug.Log("[EnemyKnight] 모든 자물쇠 해제 → 약점 노출!");

            if (_spriteRenderer != null)
                _spriteRenderer.color = new Color(1f, 0.4f, 0.4f, 1f);
        }

        // ══════════════════════════════════════════════════════
        // 보조
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 리스트에서 아직 잠긴 첫 번째 자물쇠 반환.
        /// 순서 = Inspector 에서 설정한 우선순위.
        /// 모두 해제됐으면 null 반환.
        /// </summary>
        private LockComponent GetFirstLockedLock()
        {
            foreach (var lock_ in _locks)
            {
                if (lock_ != null && !lock_.IsUnlocked)
                    return lock_;
            }
            return null;
        }

        /// <summary>
        /// 공격 방향이 기사 정면과 반대인지 판단.
        /// dot(기사방향, 공격방향) &lt; 0 → 정면 공격.
        /// </summary>
        private bool IsFrontalAttack(Vector2 attackDir)
        {
            float facingDir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            return facingDir * attackDir.x < 0f;
        }

        // ══════════════════════════════════════════════════════
        // EnemyBase override
        // ══════════════════════════════════════════════════════

        protected override void OnDamaged(DamageInfo info) { }
    }
}