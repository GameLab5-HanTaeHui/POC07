// ============================================================
// KeyInventorySO.cs  v1.0
// 플레이어 보유 열쇠 목록 관리 ScriptableObject
//
// [역할]
//   플레이어가 보유한 열쇠 목록과 현재 장착 열쇠를 관리.
//   인게임 획득(AcquireKey) 및 수동 교체(EquipKey) API 제공.
//   SO 이므로 씬 전환 후에도 데이터 유지.
//   (단, Play Mode 종료 시 초기화 — 런타임 세이브는 별도 구현 필요)
//
// [생성 방법]
//   Project 창 우클릭 → Create → KEY → Key Inventory
//
// [WeaponKeyController 와의 관계]
//   이 SO 는 "데이터" 만 가짐.
//   실제 무기 컴포넌트 교체는 WeaponKeyController 가 담당.
//   EquipKey() 호출 시 OnKeyEquipped 이벤트 발행 →
//   WeaponKeyController 가 구독하여 처리.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 보유 열쇠 목록 관리 ScriptableObject. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [외부 사용 예시]
    ///   // 인게임 획득
    ///   keyInventory.AcquireKey(rustyKeyData);
    ///
    ///   // UI 에서 수동 교체
    ///   keyInventory.EquipKey(1);
    ///
    ///   // 현재 장착 열쇠 조회
    ///   KeyDataSO current = keyInventory.EquippedKey;
    /// ────────────────────────────────────────────────────
    /// </summary>
    [CreateAssetMenu(
        fileName = "KeyInventory",
        menuName = "KEY/Key Inventory",
        order = 1)]
    public class KeyInventoryDataSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // Inspector — 초기 보유 열쇠
        // ──────────────────────────────────────────

        [Header("── 초기 보유 열쇠 ──────────────────────")]

        /// <summary>
        /// 게임 시작 시 기본으로 보유하는 열쇠 목록.
        /// Inspector 에서 녹슨 열쇠 등 시작 무기를 등록.
        /// </summary>
        [Tooltip("게임 시작 시 기본 보유 열쇠. 녹슨 열쇠 등 등록.")]
        [SerializeField] private List<KeyDataSO> _defaultKeys = new List<KeyDataSO>();

        // ──────────────────────────────────────────
        // 런타임 상태 (플레이 중 변경)
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 보유 중인 열쇠 목록.
        /// 런타임에 AcquireKey() 로 추가됨.
        /// </summary>
        private List<KeyDataSO> _ownedKeys = new List<KeyDataSO>();

        /// <summary>
        /// 현재 장착 중인 열쇠 인덱스 (_ownedKeys 기준).
        /// </summary>
        private int _equippedIndex = 0;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 새 열쇠 획득 시 발행. 파라미터: 획득한 KeyDataSO.
        /// UI 인벤토리 슬롯 추가 등에서 구독.
        /// </summary>
        public event Action<KeyDataSO> OnKeyAcquired;

        /// <summary>
        /// 열쇠 장착(교체) 시 발행. 파라미터: 새로 장착된 KeyDataSO.
        /// WeaponKeyController 가 구독하여 무기 컴포넌트 교체 처리.
        /// </summary>
        public event Action<KeyDataSO> OnKeyEquipped;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 장착 중인 열쇠 데이터. 없으면 null.
        /// </summary>
        public KeyDataSO EquippedKey =>
            (_ownedKeys.Count > 0) ? _ownedKeys[_equippedIndex] : null;

        /// <summary>
        /// 보유 열쇠 목록 읽기 전용.
        /// </summary>
        public IReadOnlyList<KeyDataSO> OwnedKeys => _ownedKeys;

        /// <summary>
        /// 보유 열쇠 수.
        /// </summary>
        public int KeyCount => _ownedKeys.Count;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 런타임 상태를 초기화하고 기본 열쇠를 세팅한다.
        /// WeaponKeyController.Start() 에서 호출.
        ///
        /// [왜 OnEnable 이 아닌 수동 호출인가?]
        ///   SO 의 OnEnable 은 에디터 로드 시에도 호출됨.
        ///   런타임 초기화는 게임 시작 시점에 명시적으로 호출하는 게 안전.
        /// </summary>
        public void Initialize()
        {
            _ownedKeys.Clear();
            _equippedIndex = 0;

            foreach (var key in _defaultKeys)
            {
                if (key != null)
                    _ownedKeys.Add(key);
            }

            // 초기 장착 열쇠 이벤트 발행
            if (EquippedKey != null)
                OnKeyEquipped?.Invoke(EquippedKey);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 새 열쇠를 획득한다.
        /// 이미 보유 중인 열쇠면 중복 추가하지 않는다.
        ///
        /// [호출 위치]
        ///   드롭 아이템 획득, 보상 지급 등 인게임 획득 이벤트에서 호출.
        /// </summary>
        /// <param name="keyData">획득할 열쇠 데이터</param>
        public void AcquireKey(KeyDataSO keyData)
        {
            if (keyData == null)
            {
                Debug.LogWarning("[KeyInventorySO] null 열쇠를 획득하려 했습니다.");
                return;
            }

            // 중복 획득 방지
            if (_ownedKeys.Contains(keyData))
            {
                Debug.Log($"[KeyInventorySO] 이미 보유 중: {keyData.keyName}");
                return;
            }

            _ownedKeys.Add(keyData);
            OnKeyAcquired?.Invoke(keyData);

            Debug.Log($"[KeyInventorySO] 열쇠 획득: {keyData.keyName} (총 {_ownedKeys.Count}개)");
        }

        /// <summary>
        /// 지정 인덱스의 열쇠를 장착한다.
        /// 인덱스가 유효하지 않으면 무시.
        ///
        /// [호출 위치]
        ///   UI 인벤토리에서 플레이어가 열쇠 슬롯을 선택할 때.
        /// </summary>
        /// <param name="index">장착할 열쇠 인덱스 (_ownedKeys 기준)</param>
        public void EquipKey(int index)
        {
            if (_ownedKeys.Count == 0)
            {
                Debug.LogWarning("[KeyInventorySO] 보유한 열쇠가 없습니다.");
                return;
            }

            if (index < 0 || index >= _ownedKeys.Count)
            {
                Debug.LogWarning($"[KeyInventorySO] 유효하지 않은 인덱스: {index}");
                return;
            }

            if (_equippedIndex == index)
            {
                Debug.Log($"[KeyInventorySO] 이미 장착 중: {_ownedKeys[index].keyName}");
                return;
            }

            _equippedIndex = index;
            OnKeyEquipped?.Invoke(EquippedKey);

            Debug.Log($"[KeyInventorySO] 열쇠 장착: {EquippedKey.keyName}");
        }

        /// <summary>
        /// 다음 열쇠로 순환 교체한다.
        /// 마지막 열쇠에서 호출하면 첫 번째 열쇠로 돌아감.
        ///
        /// [호출 위치]
        ///   단축키(탭 등)로 빠르게 순환 교체할 때.
        /// </summary>
        public void EquipNextKey()
        {
            if (_ownedKeys.Count <= 1) return;
            EquipKey((_equippedIndex + 1) % _ownedKeys.Count);
        }

        /// <summary>
        /// 이전 열쇠로 순환 교체한다.
        /// </summary>
        public void EquipPrevKey()
        {
            if (_ownedKeys.Count <= 1) return;
            int prev = (_equippedIndex - 1 + _ownedKeys.Count) % _ownedKeys.Count;
            EquipKey(prev);
        }

        /// <summary>
        /// 특정 타입의 열쇠를 보유 중인지 확인한다.
        /// </summary>
        /// <param name="type">확인할 열쇠 타입</param>
        public bool HasKey(KeyType type)
        {
            foreach (var key in _ownedKeys)
                if (key.keyType == type) return true;
            return false;
        }
    }
}