// ============================================================
// PlayerWeaponAnimator.cs  v1.1
// 무기 스윙 이동 연동 컴포넌트
//
// [v1.1 변경]
//   Animator Trigger 발행 제거 → MovementAnimator 로 이전.
//   이 컴포넌트는 PlayerWeaponMover 스윙 이동만 담당.
//
// [역할 분리]
//   MovementAnimator    : 모든 Animator Trigger/Bool/Float 담당
//   PlayerWeaponAnimator: Weapon 오브젝트 스윙 이동(PlayerWeaponMover) 담당
//
// [이벤트 구독 흐름]
//   RustyKeyWeapon.OnCombo1Started    → PlayerWeaponMover.PlaySwing(Combo1)
//   RustyKeyWeapon.OnCombo2Started    → PlayerWeaponMover.PlaySwing(Combo2)
//   RustyKeyWeapon.OnCombo3Started    → PlayerWeaponMover.PlaySwing(Combo3)
//   RustyKeyWeapon.OnAirAttackStarted → PlayerWeaponMover.PlaySwing(AirAttack)
//   RustyKeyWeapon.OnComboReset       → PlayerWeaponMover.CancelSwing()
//
// [Hierarchy 위치]
//   Player (루트에 부착)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 무기 스윙 이동 연동 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// Animator Trigger 는 MovementAnimator 가 담당.
    /// 이 컴포넌트는 PlayerWeaponMover 호출만 담당.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerWeaponAnimator : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 컴포넌트 연결 ──────────────────────")]

        /// <summary>
        /// PlayerWeaponMover 참조.
        /// 미연결 시 Awake 에서 자동 탐색.
        /// </summary>
        [Tooltip("PlayerWeaponMover. 미연결 시 자동 탐색.")]
        [SerializeField] private PlayerWeaponMover _weaponMover;

        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        /// <summary> 현재 구독 중인 무기 컴포넌트. </summary>
        private PlayerWeaponBase _currentWeapon;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_weaponMover == null)
                _weaponMover = GetComponentInChildren<PlayerWeaponMover>();

            if (_weaponMover == null)
                Debug.LogWarning("[PlayerWeaponAnimator] PlayerWeaponMover 를 찾을 수 없습니다.");
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
        /// PlayerWeaponController.ActivateWeapon() 에서 호출.
        /// </summary>
        public void SetWeapon(PlayerWeaponBase newWeapon)
        {
            UnsubscribeWeapon(_currentWeapon);
            _currentWeapon = newWeapon;
            SubscribeWeapon(_currentWeapon);
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 구독 / 해제
        // ══════════════════════════════════════════════════════

        private void SubscribeWeapon(PlayerWeaponBase weapon)
        {
            if (weapon is RustyKeyWeapon rusty)
            {
                rusty.OnCombo1Started += HandleCombo1;
                rusty.OnCombo2Started += HandleCombo2;
                rusty.OnCombo3Started += HandleCombo3;
                rusty.OnAirAttackStarted += HandleAirAttack;
                rusty.OnComboReset += HandleComboReset;
            }
        }

        private void UnsubscribeWeapon(PlayerWeaponBase weapon)
        {
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
        // 이벤트 핸들러 — 스윙 이동만 처리
        // ══════════════════════════════════════════════════════

        /// <summary> Combo1 시작 → 스윙 이동. </summary>
        private void HandleCombo1() => _weaponMover?.PlaySwing(AttackType.Combo1);

        /// <summary> Combo2 시작 → 스윙 이동. </summary>
        private void HandleCombo2() => _weaponMover?.PlaySwing(AttackType.Combo2);

        /// <summary> Combo3 시작 → 스윙 이동. </summary>
        private void HandleCombo3() => _weaponMover?.PlaySwing(AttackType.Combo3);

        /// <summary> 공중 공격 시작 → 아래 스윙 이동. </summary>
        private void HandleAirAttack() => _weaponMover?.PlaySwing(AttackType.AirAttack);

        /// <summary> 콤보 리셋 → 스윙 취소 + 원점 복귀. </summary>
        private void HandleComboReset() => _weaponMover?.CancelSwing();
    }
}