// ============================================================
// EnemyKnight.cs  v2.0
// 기사형 적 — 리모델링 (콜라이더 레이어 기반 정면/후면 판단)
//
// [v2.0 리모델링 변경]
//
//   ① IsFrontalAttack() 제거
//       기존: DamageInfo.Direction 방향 벡터로 정면/후면 판단.
//             dot(facingDir, attackDir) < 0 = 정면 공격.
//             → Flip 연동 복잡 + Lock localPosition 이 반전 안 되는 버그.
//
//       변경: 콜라이더 레이어 자체가 정면/후면을 정의.
//             ShieldCollider (EnemyShield 레이어) → 방패.
//               PlayerWeaponHitboxManager 가 이 레이어 감지 시 아무것도 안 함.
//             LockCollider (EnemyLock 레이어) → 자물쇠.
//               PlayerWeaponHitboxManager 가 직접 LockComponent.TakeDamage() 호출.
//             Enemy 레이어 → 본체.
//               TakeDamage() 호출 → 자물쇠 해제 여부만 판단.
//
//       결과: EnemyKnight.TakeDamage() 에서 방향 계산 완전 제거.
//             Lock 전부 해제 여부만 확인하면 됨.
//
//   ② TakeDamage public new → public override
//       EnemyBase v2.0 의 virtual TakeDamage 를 override.
//       IDamageable 참조로 호출해도 반드시 이 구현이 실행됨.
//       (기존 'new' 방식은 인터페이스 참조에서 EnemyBase 가 호출되는 버그)
//
//   ③ ShieldCollider Flip 연동 추가
//       Shield 오브젝트의 BoxCollider2D localPosition.x 를
//       EnemyAI.OnFlipped 이벤트로 반전.
//       기사가 방향을 바꾸면 방패도 항상 정면에 위치.
//       → ShieldCollider 가 있는 방향 = 정면 (공격 감지 안 됨).
//       → ShieldCollider 가 없는 방향 = 후방 (LockCollider 위치).
//
//   ④ Guard 봉인 (EnemySealComponent) 유지
//       Guard 봉인 활성 시: Lock 미해제여도 본체 TakeDamage 허용.
//       (방패 기능을 봉인하는 개념)
//
// [전투 구조]
//   PlayerWeaponHitboxManager.CheckHit()
//     EnemyShield 레이어 감지 → 아무것도 안 함 (방패 차단)
//     EnemyLock 레이어 감지   → LockComponent.TakeDamage() 직접 호출
//     Enemy 레이어 감지       → EnemyKnight.TakeDamage() 호출
//                               → Lock 전부 해제? → 본체 피격
//                               → Guard 봉인?    → 본체 피격
//                               → 그 외          → 무시 (방패가 이미 ShieldCollider로 막음)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 적. EnemyBase 상속. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [TakeDamage 흐름 — v2.0]
    ///
    ///   모든 자물쇠 해제 완료
    ///     → base.TakeDamage() (정상 체력 감소 + 사망 가능)
    ///
    ///   Guard 봉인 활성 (자물쇠 미해제)
    ///     → base.TakeDamage() (방패 무시, 체력 감소)
    ///
    ///   그 외 (자물쇠 미해제, Guard 봉인 없음)
    ///     → 아무것도 안 함
    ///     (이 경우는 PlayerWeaponHitboxManager 가 방패(ShieldCollider)를
    ///      통과해서 Enemy 레이어에 닿은 상황 → 물리적으로 일어나면 안 됨)
    ///     → 방어 목적의 추가 차단 레이어로 사용
    ///
    /// [ShieldCollider 역할]
    ///   isTrigger = OFF → 물리 충돌로 플레이어가 방패를 통과하지 못함.
    ///   EnemyShield 레이어 → PlayerWeaponHitboxManager 감지에서 제외.
    ///   Flip 시 localPosition.x 반전 → 항상 기사 정면에 위치.
    ///
    /// [LockCollider 역할]
    ///   isTrigger = ON → PlayerWeaponHitboxManager 가 감지 가능.
    ///   EnemyLock 레이어 → 감지 시 LockComponent.TakeDamage() 직접 호출.
    ///   Flip 시 localPosition.x 반전 (LockComponent v2.0 이 자체 처리).
    ///   → 항상 기사 후방에 위치.
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
        /// Inspector 에서 순서대로 연결. 순서 = 피격 우선순위.
        /// 미연결 시 Awake 에서 GetComponentsInChildren 으로 자동 수집.
        /// </summary>
        [Tooltip("자물쇠 리스트. 순서 = 피격 우선순위. 미연결 시 자동 수집.")]
        [SerializeField] private List<LockComponent> _locks = new List<LockComponent>();

        [Header("── 방패 콜라이더 ──────────────────────")]

        /// <summary>
        /// 방패 콜라이더 (ShieldCollider 자식 오브젝트의 Collider2D).
        /// Layer = EnemyShield.
        /// isTrigger = OFF (물리 충돌로 플레이어 통과 차단).
        /// Flip 시 localPosition.x 반전 → 항상 기사 정면에 위치.
        /// 미연결 시 Flip 연동 없음 (방패 물리 차단만 동작).
        /// </summary>
        [Tooltip("ShieldCollider 자식 오브젝트의 Collider2D. " +
                 "EnemyShield 레이어. isTrigger=OFF. 미연결 시 Flip 연동 없음.")]
        [SerializeField] private Collider2D _shieldCollider;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private EnemySealComponent _sealComponent;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 해제된 자물쇠 수. </summary>
        private int _unlockedCount;

        /// <summary>
        /// 모든 자물쇠 해제 여부.
        /// true → base.TakeDamage() 정상 처리.
        /// </summary>
        private bool _isAllLocksUnlocked;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();

            _sealComponent = GetComponent<EnemySealComponent>();

            // Inspector 미연결 시 자동 수집
            if (_locks.Count == 0)
                _locks.AddRange(GetComponentsInChildren<LockComponent>());

            if (_locks.Count == 0)
                Debug.LogWarning("[EnemyKnight] LockComponent 가 없습니다.");
        }

        private void Start()
        {
            // LockComponent 이벤트 구독
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
        /// 기사형 피격 처리. (v2.0)
        ///
        /// [방향 계산 없음 — v2.0 설계 변경]
        ///   정면/후면 판단을 코드가 아닌 콜라이더 레이어로 처리.
        ///   PlayerWeaponHitboxManager 가 이미 레이어로 분기:
        ///     EnemyShield → 아무것도 안 함 (이 TakeDamage 호출 안 됨)
        ///     EnemyLock   → LockComponent.TakeDamage() 직접 호출 (이 TakeDamage 호출 안 됨)
        ///     Enemy       → 이 TakeDamage() 호출
        ///
        ///   Enemy 레이어를 직접 맞을 수 있는 경우:
        ///     ① Lock 전부 해제 후 어떤 방향에서든 공격 가능
        ///     ② Guard 봉인 활성 시 방패 무시하고 본체 직접 공격
        ///     ③ 예외 상황 (방패 콜라이더 미연결 등)
        ///
        ///   [분기]
        ///     Lock 전부 해제 → base.TakeDamage() (체력 감소 + 사망 가능)
        ///     Guard 봉인 활성 → base.TakeDamage() (방패 무시)
        ///     그 외 → 무시 (방패 콜라이더가 통과를 막아야 하는 상황)
        /// </summary>
        public override void TakeDamage(DamageInfo info)
        {
            // ① 모든 자물쇠 해제 → 정상 피격
            if (_isAllLocksUnlocked)
            {
                base.TakeDamage(info);
                return;
            }

            // ② Guard 봉인 활성 → 방패 무시 피격
            bool guardSealed = _sealComponent != null
                && _sealComponent.IsSealedAction(SealType.Guard);

            if (guardSealed)
            {
                Debug.Log("[EnemyKnight] Guard 봉인 → 방패 무시 피격");
                base.TakeDamage(info);
                return;
            }

            // ③ 그 외 → 무시
            // (방패 콜라이더가 정상 작동하면 이 경로는 거의 실행되지 않음)
            Debug.Log("[EnemyKnight] 본체 직접 공격 감지 → 무시 (자물쇠 미해제)");
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

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
        // 해제 조건
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 전부 해제 조건 확인.
        /// 이 메서드만 수정하면 해제 조건 변경 가능.
        /// </summary>
        private bool CheckAllUnlocked()
        {
            if (_locks.Count == 0) return true;
            return _unlockedCount >= _locks.Count;
        }

        /// <summary>
        /// 모든 자물쇠 해제 시 처리.
        /// 색상 변경으로 약점 노출 피드백.
        /// </summary>
        private void OnAllLocksUnlocked()
        {
            Debug.Log("[EnemyKnight] 모든 자물쇠 해제 → 약점 노출!");
            if (_spriteRenderer != null)
                _spriteRenderer.color = new Color(1f, 0.4f, 0.4f, 1f);
        }

        // ══════════════════════════════════════════════════════
        // EnemyBase override
        // ══════════════════════════════════════════════════════

        protected override void OnDamaged(DamageInfo info) { }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 기사 전체 리셋. 테스트 / 리스폰 시 호출.
        /// </summary>
        public override void ResetEnemy()
        {
            base.ResetEnemy();

            _unlockedCount = 0;
            _isAllLocksUnlocked = false;

            foreach (var lock_ in _locks)
                lock_?.ResetLock();
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

#if UNITY_EDITOR
            UnityEditor.Handles.color = _isAllLocksUnlocked ? Color.red : Color.blue;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.2f,
                _isAllLocksUnlocked
                    ? "★ 약점 노출"
                    : $"자물쇠 {_unlockedCount}/{_locks.Count}");
#endif
        }
    }
}