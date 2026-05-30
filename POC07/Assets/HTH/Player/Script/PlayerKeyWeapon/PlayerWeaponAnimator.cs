// ============================================================
// PlayerWeaponAnimator.cs  v1.2
// 무기 스윙 이동 연동 컴포넌트
//
// [v1.2 변경]
//   공중 4방향 이벤트 구독 추가 (v0.22 연동).
//   OnAirAttackSide / OnAirAttackDown / OnAirAttackUp 구독.
//   방향별 PlaySwing(AttackType) 호출.
//
// [v1.1 변경]
//   Animator Trigger 발행 제거 → MovementAnimator 로 이전.
//
// [역할 분리]
//   MovementAnimator    : 모든 Animator Trigger/Bool/Float 담당
//   PlayerWeaponAnimator: Weapon 오브젝트 스윙 이동(PlayerWeaponMover) 담당
//
// [이벤트 구독 흐름 — v1.2]
//   RustyKeyWeapon.OnCombo1Started    → PlaySwing(Combo1)
//   RustyKeyWeapon.OnCombo2Started    → PlaySwing(Combo2)
//   RustyKeyWeapon.OnCombo3Started    → PlaySwing(Combo3)
//   RustyKeyWeapon.OnAirAttackStarted → PlaySwing(AirAttack)   ← 하위 호환
//   RustyKeyWeapon.OnAirAttackSide    → PlaySwing(AirAttack)   ← 수평
//   RustyKeyWeapon.OnAirAttackDown    → PlaySwing(AirAttackDown) ← 하향
//   RustyKeyWeapon.OnAirAttackUp      → PlaySwing(AirAttackUp)   ← 상향
//   RustyKeyWeapon.OnComboReset       → CancelSwing()
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 무기 스윙 이동 연동 컴포넌트. (v1.2)
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
                rusty.OnAirAttackStarted += HandleAirAttackSide; // 하위 호환
                rusty.OnAirAttackSide += HandleAirAttackSide;
                rusty.OnAirAttackDown += HandleAirAttackDown;
                rusty.OnAirAttackUp += HandleAirAttackUp;
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
                rusty.OnAirAttackStarted -= HandleAirAttackSide;
                rusty.OnAirAttackSide -= HandleAirAttackSide;
                rusty.OnAirAttackDown -= HandleAirAttackDown;
                rusty.OnAirAttackUp -= HandleAirAttackUp;
                rusty.OnComboReset -= HandleComboReset;
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary> Combo1 — 수평 스윙 이동. </summary>
        private void HandleCombo1() => _weaponMover?.PlaySwing(AttackType.Combo1);

        /// <summary> Combo2 — 내리찍기 이동. </summary>
        private void HandleCombo2() => _weaponMover?.PlaySwing(AttackType.Combo2);

        /// <summary> Combo3 — 피니셔 이동 + 히트스탑. </summary>
        private void HandleCombo3() => _weaponMover?.PlaySwing(AttackType.Combo3);

        /// <summary> 공중 수평 공격 이동. </summary>
        private void HandleAirAttackSide() => _weaponMover?.PlaySwing(AttackType.AirAttack);

        /// <summary> 공중 하향 내리찍기 이동. </summary>
        private void HandleAirAttackDown() => _weaponMover?.PlaySwing(AttackType.AirAttackDown);

        /// <summary> 공중 상향 공격 이동. </summary>
        private void HandleAirAttackUp() => _weaponMover?.PlaySwing(AttackType.AirAttackUp);

        /// <summary> 콤보 리셋 → 스윙 취소 + 원점 복귀. </summary>
        private void HandleComboReset() => _weaponMover?.CancelSwing();
    }
}