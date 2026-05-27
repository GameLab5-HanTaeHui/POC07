// ============================================================
// PlayerWeaponController.cs  v1.3
// 열쇠 교체 핵심 컨트롤러
//
// [v1.3 변경]
//   WeaponAnimator    → PlayerWeaponAnimator (명칭 변경)
//   WeaponMover       → PlayerWeaponMover    (명칭 변경)
//   MovementAnimator 참조 추가 —
//     무기 교체 시 MovementAnimator.SetWeapon() 도 함께 호출.
//     Trigger 발행(Combo1/2/3/AirAttack)을 MovementAnimator 가 담당하므로
//     무기 이벤트 재구독을 MovementAnimator 에도 알려야 함.
//
// [교체 흐름]
//   KeyInventoryDataSO.EquipKey()
//     → OnKeyEquipped 이벤트
//       → HandleKeyEquipped()
//           → DeactivateCurrentWeapon()
//           → ActivateWeapon()
//               → weapon.SetKeyData()
//               → MovementAnimator.SetWeapon()   ← Trigger 담당
//               → PlayerWeaponAnimator.SetWeapon() ← 스윙 이동 담당
//               → PlayerWeaponMover.SetKeyData()
//               → TrySwapAnimatorOverride()
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 열쇠 교체 핵심 컨트롤러. (v1.3)
    /// </summary>
    public class PlayerWeaponController : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 내부 매핑 데이터 클래스
        // ──────────────────────────────────────────

        /// <summary>
        /// KeyType 과 무기 구현체 MonoBehaviour 를 묶는 매핑 엔트리.
        ///
        /// [Inspector 연결 방법]
        ///   weapon 슬롯에 해당 구현체 컴포넌트를 직접 드래그.
        ///   런타임에 as PlayerWeaponBase 로 캐스팅하여 사용.
        /// </summary>
        [System.Serializable]
        public class WeaponEntry
        {
            /// <summary> 매핑할 열쇠 타입. </summary>
            [Tooltip("이 엔트리가 대응하는 열쇠 타입.")]
            public KeyType keyType;

            /// <summary>
            /// 대응하는 무기 컴포넌트 (MonoBehaviour 타입).
            /// Inspector 에서 RustyKeyWeapon 등 구현체를 드래그 연결.
            /// PlayerWeaponBase 미상속 시 Awake 에서 LogError 출력.
            /// </summary>
            [Tooltip("무기 컴포넌트 (RustyKeyWeapon 등). 비활성 상태로 미리 부착.")]
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
        /// 무기 교체 시 SetWeapon() 호출 → Combo Trigger 이벤트 재구독.
        /// 미연결 시 Awake 에서 자동 탐색.
        /// </summary>
        [Tooltip("MovementAnimator. 미연결 시 자동 탐색.")]
        [SerializeField] private MovementAnimator _movementAnimator;

        /// <summary>
        /// PlayerWeaponAnimator 참조.
        /// 무기 교체 시 SetWeapon() 호출 → 스윙 이동 이벤트 재구독.
        /// 미연결 시 Awake 에서 자동 탐색.
        /// </summary>
        [Tooltip("PlayerWeaponAnimator. 미연결 시 자동 탐색.")]
        [SerializeField] private PlayerWeaponAnimator _weaponAnimator;

        /// <summary>
        /// PlayerWeaponMover 참조.
        /// 무기 교체 시 SetKeyData() 호출 → 스윙 수치 갱신.
        /// 미연결 시 Awake 에서 자동 탐색.
        /// </summary>
        [Tooltip("PlayerWeaponMover. 미연결 시 자동 탐색.")]
        [SerializeField] private PlayerWeaponMover _weaponMover;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private Dictionary<KeyType, PlayerWeaponBase> _weaponMap
            = new Dictionary<KeyType, PlayerWeaponBase>();

        private PlayerWeaponBase _currentWeapon;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 장착된 무기 컴포넌트. 없으면 null. </summary>
        public PlayerWeaponBase CurrentWeapon => _currentWeapon;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_inventory == null)
            {
                Debug.LogError("[PlayerWeaponController] KeyInventoryDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            BuildWeaponMap();

            // 자동 탐색
            if (_movementAnimator == null)
                _movementAnimator = GetComponent<MovementAnimator>();
            if (_weaponAnimator == null)
                _weaponAnimator = GetComponent<PlayerWeaponAnimator>();
            if (_weaponMover == null)
                _weaponMover = GetComponentInChildren<PlayerWeaponMover>();
        }

        private void Start()
        {
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

        private void HandleKeyEquipped(KeyDataSO keyData)
        {
            if (keyData == null)
            {
                Debug.LogWarning("[PlayerWeaponController] null 열쇠 장착 시도.");
                return;
            }

            DeactivateCurrentWeapon();

            if (!_weaponMap.TryGetValue(keyData.keyType, out var nextWeapon))
            {
                Debug.LogWarning($"[PlayerWeaponController] 매핑 없음: {keyData.keyType}.");
                return;
            }

            ActivateWeapon(nextWeapon, keyData);
            TrySwapAnimatorOverride(keyData);

            Debug.Log($"[PlayerWeaponController] 무기 교체: {keyData.keyName}");
        }

        // ══════════════════════════════════════════════════════
        // 내부
        // ══════════════════════════════════════════════════════

        private void DeactivateCurrentWeapon()
        {
            if (_currentWeapon == null) return;
            _currentWeapon.ComboReset();
            _currentWeapon.enabled = false;
            _currentWeapon = null;
        }

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

        private void TrySwapAnimatorOverride(KeyDataSO keyData)
        {
            if (_animator == null || keyData.overrideController == null) return;
            _animator.runtimeAnimatorController = keyData.overrideController;
            Debug.Log($"[PlayerWeaponController] AnimatorOverride 적용: {keyData.keyName}");
        }

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