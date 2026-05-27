// ============================================================
// WeaponAnimator.cs  v1.0
// 무기 Animator 파라미터 동기화 + WeaponMover 연동
//
// [역할]
//   PlayerWeaponBase(RustyKeyWeapon 등) 의 이벤트를 구독하여
//   1. Player Animator 에 Attack Layer Trigger 발행
//   2. WeaponMover.PlaySwing() 호출 → Weapon 오브젝트 스윙 이동
//
// [Animator 파라미터 — Attack Layer]
//   Trigger: AttackCombo1 / AttackCombo2 / AttackCombo3 / AirAttack
//   (스프라이트 완성 후 클립 연결. 지금은 구조만 세팅)
//
// [AnimatorOverrideController 스왑]
//   WeaponKeyController 가 열쇠 교체 시
//   keyData.overrideController 를 Player Animator 에 적용.
//   (스프라이트 완성 후 활성화 — 지금은 null 이면 스킵)
//
// [Hierarchy 위치]
//   Player
//   ├── [Animator]         Player.controller 연결
//   ├── [WeaponAnimator]   이 컴포넌트 — Player 루트에 부착
//   └── Weapon
//         ├── [WeaponMover]
//         └── [RustyKeyWeapon]
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 무기 Animator 동기화 + WeaponMover 연동 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [이벤트 구독 흐름]
    ///   RustyKeyWeapon.OnCombo1Started   → HandleCombo1()
    ///   RustyKeyWeapon.OnCombo2Started   → HandleCombo2()
    ///   RustyKeyWeapon.OnCombo3Started   → HandleCombo3()
    ///   RustyKeyWeapon.OnAirAttackStarted→ HandleAirAttack()
    ///   RustyKeyWeapon.OnComboReset      → HandleComboReset()
    ///
    /// [각 핸들러 처리]
    ///   Animator.SetTrigger()  → Attack Layer 상태 전환
    ///   WeaponMover.PlaySwing()→ Weapon 오브젝트 스윙 이동
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PlayerWeaponAnimator : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 컴포넌트 연결 ──────────────────────")]

        /// <summary>
        /// WeaponMover 참조.
        /// Weapon 자식 오브젝트에서 자동 탐색.
        /// </summary>
        [Tooltip("WeaponMover. 미연결 시 자동 탐색.")]
        [SerializeField] private PlayerWeaponMover _weaponMover;

        // ──────────────────────────────────────────
        // Animator 해시 캐싱
        // ──────────────────────────────────────────

        // Attack Layer Trigger 파라미터
        // Player.controller 에 동일 이름으로 파라미터 추가 필요.
        private static readonly int _hashAttackCombo1 = Animator.StringToHash("AttackCombo1");
        private static readonly int _hashAttackCombo2 = Animator.StringToHash("AttackCombo2");
        private static readonly int _hashAttackCombo3 = Animator.StringToHash("AttackCombo3");
        private static readonly int _hashAirAttack = Animator.StringToHash("AirAttack");

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Animator _animator;

        /// <summary>
        /// 현재 구독 중인 무기 컴포넌트.
        /// SetWeapon() 으로 교체.
        /// </summary>
        private PlayerWeaponBase _currentWeapon;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _animator = GetComponent<Animator>();

            if (_weaponMover == null)
                _weaponMover = GetComponentInChildren<PlayerWeaponMover>();

            if (_weaponMover == null)
                Debug.LogWarning("[WeaponAnimator] WeaponMover 를 찾을 수 없습니다.");
        }

        private void OnDestroy()
        {
            UnsubscribeWeapon(_currentWeapon);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 무기 컴포넌트 교체 및 이벤트 재구독.
        /// WeaponKeyController.ActivateWeapon() 에서 호출.
        ///
        /// [호출 시점]
        ///   열쇠 교체 → WeaponKeyController → SetWeapon(newWeapon)
        /// </summary>
        public void SetWeapon(PlayerWeaponBase newWeapon)
        {
            // 기존 무기 구독 해제
            UnsubscribeWeapon(_currentWeapon);

            _currentWeapon = newWeapon;

            // 새 무기 구독
            SubscribeWeapon(_currentWeapon);
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 구독 / 해제
        // ══════════════════════════════════════════════════════

        private void SubscribeWeapon(PlayerWeaponBase weapon)
        {
            if (weapon == null) return;

            // RustyKeyWeapon 이벤트 구독
            if (weapon is RustyKeyWeapon rusty)
            {
                rusty.OnCombo1Started += HandleCombo1;
                rusty.OnCombo2Started += HandleCombo2;
                rusty.OnCombo3Started += HandleCombo3;
                rusty.OnAirAttackStarted += HandleAirAttack;
                rusty.OnComboReset += HandleComboReset;
            }

            // 추후 HookKeyWeapon 등 추가 시 여기에 else if 로 확장
        }

        private void UnsubscribeWeapon(PlayerWeaponBase weapon)
        {
            if (weapon == null) return;

            if (weapon is RustyKeyWeapon rusty)
            {
                rusty.OnCombo1Started -= HandleCombo1;
                rusty.OnCombo2Started -= HandleCombo2;
                rusty.OnCombo3Started -= HandleCombo3;
                rusty.OnAirAttackStarted -= HandleAirAttack;
                rusty.OnComboReset -= HandleComboReset;
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Combo1 시작 — Trigger 발행 + 스윙 이동.
        /// </summary>
        private void HandleCombo1()
        {
            _animator.SetTrigger(_hashAttackCombo1);
            _weaponMover?.PlaySwing(AttackType.Combo1);
        }

        /// <summary>
        /// Combo2 시작 — Trigger 발행 + 스윙 이동.
        /// </summary>
        private void HandleCombo2()
        {
            _animator.SetTrigger(_hashAttackCombo2);
            _weaponMover?.PlaySwing(AttackType.Combo2);
        }

        /// <summary>
        /// Combo3(피니셔) 시작 — Trigger 발행 + 스윙 이동.
        /// </summary>
        private void HandleCombo3()
        {
            _animator.SetTrigger(_hashAttackCombo3);
            _weaponMover?.PlaySwing(AttackType.Combo3);
        }

        /// <summary>
        /// 공중 공격 시작 — Trigger 발행 + 스윙 이동(아래).
        /// </summary>
        private void HandleAirAttack()
        {
            _animator.SetTrigger(_hashAirAttack);
            _weaponMover?.PlaySwing(AttackType.AirAttack);
        }

        /// <summary>
        /// 콤보 리셋 — 진행 중인 스윙 즉시 취소 + 원점 복귀.
        /// </summary>
        private void HandleComboReset()
        {
            _weaponMover?.CancelSwing();
        }
    }
}