// ============================================================
// WeaponKeyController.cs  v1.1
// 열쇠 교체 핵심 컨트롤러
//
// [v1.1 변경]
//   WeaponEntry.weapon 타입 PlayerWeaponBase → 구체 타입별 분리.
//   PlayerWeaponBase 는 추상 클래스이므로 Inspector 에서 직접
//   슬롯 연결 불가 → 구현체(RustyKeyWeapon 등)를 직접 연결.
//   런타임에 PlayerWeaponBase 로 캐스팅하여 사용.
//
// [신규 열쇠 추가 방법]
//   1. KeyType 에 항목 추가
//   2. PlayerWeaponBase 상속 구현체 작성
//   3. Weapon 오브젝트에 컴포넌트 부착 (비활성 상태)
//   4. _weaponEntries 에 새 항목 추가 후 구현체 연결
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 열쇠 교체 핵심 컨트롤러. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [Inspector 연결 방법]
    ///   _weaponEntries 리스트에 열쇠 종류만큼 항목 추가.
    ///   각 항목의 keyType 설정 후 해당 구현체 컴포넌트를 슬롯에 연결.
    ///
    ///   예시:
    ///     [0] keyType = Rusty  / weapon = (RustyKeyWeapon 컴포넌트)
    ///     [1] keyType = Hook   / weapon = (HookKeyWeapon 컴포넌트)
    ///
    /// [Player Hierarchy 위치]
    ///   Player
    ///   └── Weapon
    ///         ├── [WeaponKeyController]   ← 이 컴포넌트
    ///         ├── [RustyKeyWeapon]        비활성 상태로 대기
    ///         ├── [HookKeyWeapon]         비활성 상태로 대기
    ///         └── ...
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class WeaponKeyController : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 내부 매핑 데이터 클래스
        // ──────────────────────────────────────────

        /// <summary>
        /// KeyType 과 무기 구현체 MonoBehaviour 를 묶는 매핑 엔트리.
        ///
        /// [왜 MonoBehaviour 인가?]
        ///   PlayerWeaponBase 는 추상 클래스.
        ///   Unity Inspector 는 추상 클래스 필드에 컴포넌트를 연결할 수 없음.
        ///   MonoBehaviour 로 선언하면 어떤 컴포넌트든 슬롯에 끌어다 놓을 수 있고,
        ///   런타임에 GetComponent / TryGetComponent 로 PlayerWeaponBase 를 얻음.
        ///
        /// [연결 방법]
        ///   Inspector 의 weapon 슬롯에 Weapon 오브젝트의
        ///   RustyKeyWeapon, HookKeyWeapon 등 구현체 컴포넌트를 직접 드래그.
        /// </summary>
        [System.Serializable]
        public class WeaponEntry
        {
            /// <summary> 매핑할 열쇠 타입. </summary>
            [Tooltip("이 엔트리가 대응하는 열쇠 타입.")]
            public KeyType keyType;

            /// <summary>
            /// 대응하는 무기 컴포넌트.
            /// MonoBehaviour 타입으로 선언 — Inspector 에서 구현체를 직접 연결.
            /// 런타임에 PlayerWeaponBase 로 캐스팅하여 사용.
            /// ※ PlayerWeaponBase 를 상속한 컴포넌트만 연결할 것.
            /// </summary>
            [Tooltip("대응하는 무기 컴포넌트 (RustyKeyWeapon 등). 비활성 상태로 미리 부착.")]
            public MonoBehaviour weapon;
        }

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 필수 연결 ──────────────────────")]

        /// <summary>
        /// 플레이어 보유 열쇠 목록 SO.
        /// OnKeyEquipped 이벤트 구독 대상.
        /// </summary>
        [Tooltip("KeyInventoryDataSO. OnKeyEquipped 구독.")]
        [SerializeField] private KeyInventoryDataSO _inventory;

        /// <summary>
        /// KeyType → 무기 컴포넌트 매핑 테이블.
        /// Inspector 에서 열쇠 종류만큼 엔트리 추가.
        /// weapon 슬롯에 해당 구현체 컴포넌트를 드래그하여 연결.
        /// </summary>
        [Tooltip("KeyType - 무기 컴포넌트 매핑. 열쇠 종류만큼 추가.")]
        [SerializeField] private List<WeaponEntry> _weaponEntries = new List<WeaponEntry>();

        [Header("── 선택 연결 ──────────────────────")]

        /// <summary>
        /// Player Animator.
        /// 추후 AnimatorOverrideController 스왑에 사용.
        /// 스프라이트 완성 전까지 미연결 가능.
        /// </summary>
        [Tooltip("Player Animator. 스프라이트 완성 후 연결.")]
        [SerializeField] private Animator _animator;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// KeyType → PlayerWeaponBase 런타임 딕셔너리.
        /// Awake 에서 _weaponEntries 를 캐스팅하여 빌드.
        /// </summary>
        private Dictionary<KeyType, PlayerWeaponBase> _weaponMap
            = new Dictionary<KeyType, PlayerWeaponBase>();

        /// <summary>
        /// 현재 활성화된 무기 컴포넌트.
        /// 교체 시 먼저 비활성.
        /// </summary>
        private PlayerWeaponBase _currentWeapon;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 장착된 무기 컴포넌트. 없으면 null. </summary>
        public PlayerWeaponBase CurrentWeapon => _currentWeapon;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 매핑 딕셔너리 빌드 + 유효성 검사.
        /// 모든 무기 컴포넌트를 비활성 상태로 초기화.
        /// </summary>
        private void Awake()
        {
            if (_inventory == null)
            {
                Debug.LogError("[WeaponKeyController] KeyInventoryDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            BuildWeaponMap();
        }

        /// <summary>
        /// 이벤트 구독 + 인벤토리 초기화.
        /// </summary>
        private void Start()
        {
            _inventory.OnKeyEquipped += HandleKeyEquipped;
            _inventory.Initialize();
        }

        /// <summary>
        /// 이벤트 구독 해제.
        /// </summary>
        private void OnDestroy()
        {
            if (_inventory != null)
                _inventory.OnKeyEquipped -= HandleKeyEquipped;
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// KeyInventoryDataSO.OnKeyEquipped 수신 시 호출.
        /// 기존 무기 비활성 → 새 무기 활성 + 데이터 주입.
        /// </summary>
        /// <param name="keyData">새로 장착할 열쇠 데이터</param>
        private void HandleKeyEquipped(KeyDataSO keyData)
        {
            if (keyData == null)
            {
                Debug.LogWarning("[WeaponKeyController] null 열쇠 장착 시도.");
                return;
            }

            DeactivateCurrentWeapon();

            if (!_weaponMap.TryGetValue(keyData.keyType, out var nextWeapon))
            {
                Debug.LogWarning($"[WeaponKeyController] 매핑 없음: {keyData.keyType}. " +
                                 "_weaponEntries 에 등록하세요.");
                return;
            }

            ActivateWeapon(nextWeapon, keyData);
            TrySwapAnimatorOverride(keyData);

            Debug.Log($"[WeaponKeyController] 무기 교체 완료: {keyData.keyName}");
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 무기 활성/비활성
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 무기를 비활성화하고 콤보를 리셋한다.
        /// </summary>
        private void DeactivateCurrentWeapon()
        {
            if (_currentWeapon == null) return;

            _currentWeapon.ComboReset();
            _currentWeapon.enabled = false;
            _currentWeapon = null;
        }

        /// <summary>
        /// 무기를 활성화하고 KeyDataSO 를 주입한다.
        /// </summary>
        private void ActivateWeapon(PlayerWeaponBase weapon, KeyDataSO keyData)
        {
            weapon.SetKeyData(keyData);
            weapon.enabled = true;
            _currentWeapon = weapon;
        }

        /// <summary>
        /// AnimatorOverrideController 스왑.
        /// overrideController 가 null 이면 스킵.
        /// </summary>
        private void TrySwapAnimatorOverride(KeyDataSO keyData)
        {
            if (_animator == null || keyData.overrideController == null) return;

            _animator.runtimeAnimatorController = keyData.overrideController;
            Debug.Log($"[WeaponKeyController] AnimatorOverride 적용: {keyData.keyName}");
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 매핑 빌드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// _weaponEntries 를 순회하여 딕셔너리 빌드.
        /// MonoBehaviour 를 PlayerWeaponBase 로 캐스팅.
        /// 캐스팅 실패(PlayerWeaponBase 미상속) 시 경고 출력 후 스킵.
        /// </summary>
        private void BuildWeaponMap()
        {
            _weaponMap.Clear();

            foreach (var entry in _weaponEntries)
            {
                if (entry.weapon == null)
                {
                    Debug.LogWarning($"[WeaponKeyController] " +
                                     $"KeyType.{entry.keyType} 의 weapon 슬롯이 비어있습니다.");
                    continue;
                }

                // MonoBehaviour → PlayerWeaponBase 캐스팅
                var weaponBase = entry.weapon as PlayerWeaponBase;
                if (weaponBase == null)
                {
                    Debug.LogError($"[WeaponKeyController] " +
                                   $"KeyType.{entry.keyType} 에 연결된 {entry.weapon.GetType().Name} 은 " +
                                   $"PlayerWeaponBase 를 상속하지 않습니다.");
                    continue;
                }

                if (_weaponMap.ContainsKey(entry.keyType))
                {
                    Debug.LogWarning($"[WeaponKeyController] 중복 KeyType: {entry.keyType}. " +
                                     "첫 번째 엔트리를 사용합니다.");
                    continue;
                }

                // 초기 비활성화
                weaponBase.enabled = false;
                _weaponMap[entry.keyType] = weaponBase;
            }

            Debug.Log($"[WeaponKeyController] 무기 매핑 완료: {_weaponMap.Count}종");
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary> 다음 열쇠로 순환 교체. </summary>
        public void EquipNextKey() => _inventory?.EquipNextKey();

        /// <summary> 이전 열쇠로 순환 교체. </summary>
        public void EquipPrevKey() => _inventory?.EquipPrevKey();

        /// <summary> 인덱스로 열쇠 직접 교체. UI 슬롯 클릭 등에서 호출. </summary>
        public void EquipKey(int index) => _inventory?.EquipKey(index);
    }
}