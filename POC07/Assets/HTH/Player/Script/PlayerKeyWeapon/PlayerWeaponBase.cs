// ============================================================
// PlayerWeaponBase.cs  v1.2
// 플레이어 무기 추상 베이스 클래스
//
// [v1.2 변경]
//   IsReadyToFire 가상 프로퍼티 추가.
//   HandleAttackInput() 의 _keyData == null 체크를
//   IsReadyToFire 로 교체.
//
//   [변경 이유]
//     SealKeyWeapon 은 KeyDataSO 를 사용하지 않음.
//     기존 코드에서 _keyData == null 이면 공격 입력을 무시하므로
//     SealKeyWeapon 의 Attack() 이 절대 호출되지 않는 문제.
//
//   [IsReadyToFire 동작]
//     PlayerWeaponBase 기본값: _keyData != null  (기존 동작 유지)
//     SealKeyWeapon override: _sealData != null  (봉인 데이터 체크)
//     → 하위 클래스가 각자의 준비 조건을 정의.
//
// [v1.1 변경]
//   SetKeyData(KeyDataSO) 추가.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 무기 추상 베이스 클래스. (v1.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [상속 시 반드시 구현]
    ///   Attack()       : 지상 공격
    ///   AirAttack()    : 공중 공격
    ///   ComboReset()   : 콤보 초기화
    ///
    /// [선택 override]
    ///   IsReadyToFire  : 공격 가능 조건 (기본: _keyData != null)
    ///                    SealKeyWeapon 처럼 KeyDataSO 를 쓰지 않는 경우 override.
    ///
    /// [데이터 주입 흐름]
    ///   일반 열쇠: WeaponKeyController → SetKeyData(KeyDataSO)
    ///   봉인 열쇠: WeaponKeyController → (SealKeyWeapon)SetSealData(SealDataSO)
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
        /// SealKeyWeapon 은 이 필드 대신 _sealData 사용.
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

        /// <summary>
        /// 공격 입력을 처리할 준비가 됐는지 여부.
        ///
        /// [기본 구현]
        ///   _keyData != null — KeyDataSO 가 주입된 상태만 허용.
        ///   기존 RustyKeyWeapon 등 일반 무기는 이 조건으로 충분.
        ///
        /// [override 예시 — SealKeyWeapon]
        ///   protected override bool IsReadyToFire => _sealData != null;
        ///   → KeyDataSO 없이도 SealDataSO 가 있으면 발사 허용.
        /// </summary>
        protected virtual bool IsReadyToFire => _keyData != null;

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
        /// 컴포넌트 활성화 시 구독 시작.
        /// WeaponKeyController 가 enabled = true 로 바꿀 때 실행.
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
        /// SealKeyWeapon 은 이 메서드를 호출받지 않고
        /// SetSealData() 를 통해 별도 주입.
        /// </summary>
        /// <param name="keyData">장착할 열쇠 데이터</param>
        public void SetKeyData(KeyDataSO keyData)
        {
            _keyData = keyData;
            OnKeyDataSet(keyData);
        }

        /// <summary>
        /// SetKeyData 호출 후 하위 클래스에서 추가 처리.
        /// </summary>
        protected virtual void OnKeyDataSet(KeyDataSO keyData) { }

        /// <summary> 무기 장착 시 호출. 필요 시 override. </summary>
        public virtual void OnEquip() { }

        /// <summary> 무기 해제 시 호출. 필요 시 override. </summary>
        public virtual void OnUnequip() { ComboReset(); }

        /// <summary> 공격 차단. </summary>
        public void BlockAttack() => _attackBlocked = true;

        /// <summary> 공격 차단 해제. </summary>
        public void UnblockAttack() => _attackBlocked = false;

        // ══════════════════════════════════════════════════════
        // 입력 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// InputManager.OnAttack 수신 시 호출.
        /// 차단 체크 후 지상/공중 분기.
        ///
        /// [v1.2 변경]
        ///   _keyData == null 체크 → IsReadyToFire 체크로 교체.
        ///   하위 클래스가 각자의 준비 조건을 정의할 수 있게 됨.
        ///   SealKeyWeapon 은 _sealData != null 로 override.
        /// </summary>
        private void HandleAttackInput()
        {
            if (_attackBlocked) return;

            if (!IsReadyToFire)
            {
                Debug.LogWarning($"[{GetType().Name}] 무기 데이터가 주입되지 않았습니다.");
                return;
            }

            bool isGrounded = PlayerMovementFacade.Instance?.IsGrounded ?? true;

            if (isGrounded) Attack();
            else AirAttack();
        }

        // ══════════════════════════════════════════════════════
        // 추상 메서드
        // ══════════════════════════════════════════════════════

        /// <summary> 지상 공격. 하위 클래스에서 구현. </summary>
        protected abstract void Attack();

        /// <summary> 공중 공격. 하위 클래스에서 구현. </summary>
        protected abstract void AirAttack();

        /// <summary> 상태 초기화. 타이머 만료, 무기 교체 시 호출. </summary>
        public abstract void ComboReset();
    }
}