// ============================================================
// PlayerWeaponBase.cs  v1.1
// 플레이어 무기 추상 베이스 클래스
//
// [v1.1 변경]
//   SetKeyData(KeyDataSO) 추가.
//   WeaponKeyController 가 무기 활성화 시 KeyDataSO 를 주입.
//   하위 클래스는 _keyData 를 참조하여 수치 사용.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 무기 추상 베이스 클래스. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [상속 시 반드시 구현]
    ///   Attack()     : 지상 공격
    ///   AirAttack()  : 공중 공격
    ///   ComboReset() : 콤보 초기화
    ///
    /// [데이터 주입 흐름]
    ///   WeaponKeyController.ActivateWeapon()
    ///     → SetKeyData(KeyDataSO) 호출
    ///       → _keyData 에 저장
    ///         → Attack() 등에서 _keyData.baseDamage 등 참조
    /// ────────────────────────────────────────────────────
    /// </summary>
    public abstract class PlayerWeaponBase : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 데이터
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 장착된 열쇠 데이터 SO.
        /// WeaponKeyController.SetKeyData() 로 주입.
        /// 하위 클래스에서 수치 참조 시 사용.
        /// </summary>
        protected KeyDataSO _keyData;

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 무기 기본 설정 ──────────────────────")]

        /// <summary>
        /// 공격 불가 상태 플래그.
        /// 외부(스턴, UI 등)에서 공격을 막을 때 true 로 설정.
        /// </summary>
        [Tooltip("공격 불가 상태. true = 공격 입력 무시.")]
        [SerializeField] private bool _attackBlocked;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 공격 중(모션 재생 중) 여부.
        /// 하위 클래스에서 공격 시작/종료 시 갱신.
        /// </summary>
        protected bool _isAttacking;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 공격 중 여부. </summary>
        public bool IsAttacking => _isAttacking;

        /// <summary> 공격 차단 여부. </summary>
        public bool IsAttackBlocked => _attackBlocked;

        /// <summary> 현재 장착된 열쇠 데이터. </summary>
        public KeyDataSO KeyData => _keyData;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// InputManager 이벤트 구독.
        /// WeaponKeyController 가 enabled = true 로 설정 후
        /// MonoBehaviour Start 가 호출되는 타이밍에 실행.
        /// </summary>
        protected virtual void Start()
        {
            if (InputManager.Instance == null)
            {
                Debug.LogError("[PlayerWeaponBase] InputManager 가 없습니다.");
                return;
            }

            InputManager.Instance.OnAttack += HandleAttackInput;
        }

        /// <summary>
        /// 이벤트 구독 해제.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnAttack -= HandleAttackInput;
        }

        /// <summary>
        /// 컴포넌트 활성화 시 호출.
        /// WeaponKeyController 가 enabled = true 로 바꿀 때 실행.
        /// Start 이후에는 OnEnable/OnDisable 로 구독 관리.
        /// </summary>
        protected virtual void OnEnable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnAttack += HandleAttackInput;
        }

        /// <summary>
        /// 컴포넌트 비활성화 시 구독 해제.
        /// WeaponKeyController 가 enabled = false 로 바꿀 때 실행.
        /// </summary>
        protected virtual void OnDisable()
        {
            if (InputManager.Instance != null)
                InputManager.Instance.OnAttack -= HandleAttackInput;

            ComboReset();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — WeaponKeyController 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 열쇠 데이터를 주입한다.
        /// WeaponKeyController.ActivateWeapon() 에서 활성화 직전 호출.
        /// </summary>
        /// <param name="keyData">장착할 열쇠 데이터</param>
        public void SetKeyData(KeyDataSO keyData)
        {
            _keyData = keyData;
            OnKeyDataSet(keyData);
        }

        /// <summary>
        /// SetKeyData 호출 후 하위 클래스에서 추가 처리가 필요하면 override.
        /// 예: 콤보 카운트 갱신, 히트박스 크기 조정 등.
        /// </summary>
        /// <param name="keyData">주입된 열쇠 데이터</param>
        protected virtual void OnKeyDataSet(KeyDataSO keyData) { }

        // ══════════════════════════════════════════════════════
        // 입력 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// InputManager.OnAttack 수신 시 호출.
        /// 차단 체크 후 지상/공중 분기.
        /// </summary>
        private void HandleAttackInput()
        {
            if (_attackBlocked) return;
            if (_keyData == null)
            {
                Debug.LogWarning("[PlayerWeaponBase] KeyData 가 주입되지 않았습니다.");
                return;
            }

            bool isGrounded = PlayerMovementFacade.Instance?.IsGrounded ?? true;

            if (isGrounded) Attack();
            else AirAttack();
        }

        // ══════════════════════════════════════════════════════
        // 추상 메서드
        // ══════════════════════════════════════════════════════

        /// <summary> 지상 공격. 하위 클래스에서 콤보 로직 구현. </summary>
        protected abstract void Attack();

        /// <summary> 공중 공격. 하위 클래스에서 공중 모션 구현. </summary>
        protected abstract void AirAttack();

        /// <summary> 콤보 초기화. 타이머 만료, 무기 교체 시 호출. </summary>
        public abstract void ComboReset();

        // ══════════════════════════════════════════════════════
        // 가상 메서드
        // ══════════════════════════════════════════════════════

        /// <summary> 무기 장착 시 호출. 필요 시 override. </summary>
        public virtual void OnEquip() { }

        /// <summary> 무기 해제 시 호출. 필요 시 override. </summary>
        public virtual void OnUnequip() { ComboReset(); }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary> 공격 차단. 스턴, UI 오픈 등에서 호출. </summary>
        public void BlockAttack() => _attackBlocked = true;

        /// <summary> 공격 차단 해제. </summary>
        public void UnblockAttack() => _attackBlocked = false;
    }
}