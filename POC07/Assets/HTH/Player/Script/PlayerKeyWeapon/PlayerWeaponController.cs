// ============================================================
// PlayerWeaponController.cs  v1.5
// 열쇠 교체 핵심 컨트롤러
//
// [v1.5 변경]
//   SealKeyWeapon / SealDataSO 분기 제거.
//   모든 열쇠는 KeyDataSO 하나로 관리.
//   봉인 수치(sealType, sealDuration 등)는 KeyDataSO 에 통합.
//   WeaponEntry.sealData 슬롯 제거.
//   ActivateSealWeapon() 제거.
//   HandleKeyEquipped() Seal 분기 제거 → 모든 열쇠 ActivateWeapon() 통일.
//
// [교체 흐름 — v1.5]
//   KeyInventoryDataSO.EquipKey()
//     → OnKeyEquipped(KeyDataSO) 이벤트
//       → HandleKeyEquipped(keyData)
//           → ActivateWeapon(weapon, keyData)  ← 모든 타입 동일
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 열쇠 교체 핵심 컨트롤러. (v1.4)
    /// </summary>
    public class PlayerWeaponController : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 내부 매핑 데이터 클래스
        // ──────────────────────────────────────────

        /// <summary>
        /// KeyType 과 무기 구현체 MonoBehaviour 를 묶는 매핑 엔트리.
        ///
        /// [v1.4 추가]
        ///   sealData 슬롯 추가.
        ///   keyType = KeyType.Seal 인 엔트리에만 사용.
        ///   일반 열쇠는 keyData(KeyDataSO) 필드를 그대로 사용.
        ///   단, WeaponEntry 에는 keyData 필드가 없음 —
        ///   열쇠 데이터는 KeyInventoryDataSO.EquipKey() 에서 전달받음.
        /// </summary>
        [System.Serializable]
        public class WeaponEntry
        {
            /// <summary> 매핑할 열쇠 타입. </summary>
            [Tooltip("이 엔트리가 대응하는 열쇠 타입.")]
            public KeyType keyType;

            /// <summary>
            /// 대응하는 무기 컴포넌트 (MonoBehaviour 타입).
            /// Inspector 에서 RustyKeyWeapon 등을 드래그 연결.
            /// PlayerWeaponBase 미상속 시 Awake 에서 LogError 출력.
            /// </summary>
            [Tooltip("무기 컴포넌트. 비활성 상태로 미리 부착.")]
            public MonoBehaviour weapon;
        }

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 필수 연결 ──────────────────────")]

        /// <summary> 보유 열쇠 목록 SO. OnKeyEquipped 구독 대상. </summary>
        [Tooltip("KeyInventoryDataSO. 필수 연결.")]
        [SerializeField] private KeyInventoryDataSO _inventory;

        /// <summary> KeyType → 무기 컴포넌트 매핑 테이블. </summary>
        [Tooltip("KeyType - 무기 컴포넌트 매핑. 열쇠 종류만큼 추가.")]
        [SerializeField] private List<WeaponEntry> _weaponEntries = new List<WeaponEntry>();

        [Header("── 선택 연결 ──────────────────────")]

        /// <summary>
        /// Player Animator.
        /// AnimatorOverrideController 스왑에 사용.
        /// 스프라이트 완성 전까지 미연결 가능.
        /// </summary>
        [Tooltip("Player Animator. 스프라이트 완성 후 연결.")]
        [SerializeField] private Animator _animator;

        /// <summary>
        /// MovementAnimator 참조.
        /// 무기 교체 시 Combo Trigger 이벤트 재구독.
        /// </summary>
        [Tooltip("MovementAnimator. 무기 교체 시 Trigger 재구독.")]
        [SerializeField] private MovementAnimator _movementAnimator;

        /// <summary>
        /// PlayerWeaponAnimator 참조.
        /// 무기 교체 시 스윙 이동 이벤트 재구독.
        /// </summary>
        [Tooltip("PlayerWeaponAnimator. 무기 교체 시 스윙 이동 재구독.")]
        [SerializeField] private PlayerWeaponAnimator _weaponAnimator;

        /// <summary>
        /// PlayerWeaponMover 참조.
        /// 무기 교체 시 스윙 수치 갱신.
        /// </summary>
        [Tooltip("PlayerWeaponMover. 무기 교체 시 수치 갱신.")]
        [SerializeField] private PlayerWeaponMover _weaponMover;

        // ──────────────────────────────────────────
        // 외부 상태
        // ──────────────────────────────────────────

        public PlayerWeaponBase CurrentWeapon => _currentWeapon;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// KeyType → PlayerWeaponBase 런타임 딕셔너리.
        /// Awake 에서 _weaponEntries 로부터 빌드.
        /// </summary>
        private readonly Dictionary<KeyType, PlayerWeaponBase> _weaponMap
            = new Dictionary<KeyType, PlayerWeaponBase>();

        /// <summary> 현재 활성화된 무기 컴포넌트. </summary>
        private PlayerWeaponBase _currentWeapon;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            BuildWeaponMap();
        }

        private void Start()
        {
            if (_inventory == null)
            {
                Debug.LogError("[PlayerWeaponController] KeyInventoryDataSO 가 연결되지 않았습니다.");
                return;
            }

            _inventory.OnKeyEquipped += HandleKeyEquipped;
            _inventory.Initialize();
        }

        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnKeyEquipped -= HandleKeyEquipped;
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// KeyInventoryDataSO.OnKeyEquipped 수신.
        /// keyType 에 따라 일반 무기 or 봉인 무기 활성화 분기.
        /// </summary>
        private void HandleKeyEquipped(KeyDataSO keyData)
        {
            if (keyData == null) return;

            DeactivateCurrentWeapon();

            if (!_weaponMap.TryGetValue(keyData.keyType, out var nextWeapon))
            {
                Debug.LogWarning($"[PlayerWeaponController] 매핑 없음: {keyData.keyType}.");
                return;
            }

            // ── 무기 활성화 ──────────────────────
            ActivateWeapon(nextWeapon, keyData);
            TrySwapAnimatorOverride(keyData);

            Debug.Log($"[PlayerWeaponController] 무기 교체: {keyData.keyName}");
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 무기 활성화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 무기 비활성화.
        /// </summary>
        private void DeactivateCurrentWeapon()
        {
            if (_currentWeapon == null) return;
            _currentWeapon.ComboReset();
            _currentWeapon.enabled = false;
            _currentWeapon = null;
        }

        /// <summary>
        /// 일반 열쇠 무기 활성화.
        /// KeyDataSO 를 주입하고 컴포넌트를 활성화.
        /// </summary>
        private void ActivateWeapon(PlayerWeaponBase weapon, KeyDataSO keyData)
        {
            weapon.SetKeyData(keyData);
            weapon.enabled = true;
            _currentWeapon = weapon;

            // MovementAnimator — Combo Trigger 이벤트 재구독
            _movementAnimator?.SetWeapon(weapon);

            // PlayerWeaponAnimator — 스윙 이동 이벤트 재구독
            _weaponAnimator?.SetWeapon(weapon);

            // PlayerWeaponMover — 스윙 수치 갱신
            _weaponMover?.SetKeyData(keyData);
        }

        /// <summary>
        /// AnimatorOverrideController 스왑.
        /// </summary>
        private void TrySwapAnimatorOverride(KeyDataSO keyData)
        {
            if (_animator == null || keyData.overrideController == null) return;
            _animator.runtimeAnimatorController = keyData.overrideController;
            Debug.Log($"[PlayerWeaponController] AnimatorOverride 적용: {keyData.keyName}");
        }

        /// <summary>
        /// WeaponEntry 목록으로 런타임 딕셔너리 빌드.
        /// </summary>
        private void BuildWeaponMap()
        {
            _weaponMap.Clear();

            foreach (var entry in _weaponEntries)
            {
                if (entry.weapon == null)
                {
                    Debug.LogWarning($"[PlayerWeaponController] KeyType.{entry.keyType} weapon 슬롯 비어있음.");
                    continue;
                }

                var weaponBase = entry.weapon as PlayerWeaponBase;
                if (weaponBase == null)
                {
                    Debug.LogError($"[PlayerWeaponController] {entry.weapon.GetType().Name} 은 " +
                                   "PlayerWeaponBase 를 상속하지 않습니다.");
                    continue;
                }

                if (_weaponMap.ContainsKey(entry.keyType))
                {
                    Debug.LogWarning($"[PlayerWeaponController] 중복 KeyType: {entry.keyType}.");
                    continue;
                }

                weaponBase.enabled = false;
                _weaponMap[entry.keyType] = weaponBase;
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary> 다음 열쇠로 순환 교체. </summary>
        public void EquipNextKey() => _inventory?.EquipNextKey();

        /// <summary> 이전 열쇠로 순환 교체. </summary>
        public void EquipPrevKey() => _inventory?.EquipPrevKey();

        /// <summary> 인덱스로 열쇠 직접 교체. </summary>
        public void EquipKey(int index) => _inventory?.EquipKey(index);
    }
}