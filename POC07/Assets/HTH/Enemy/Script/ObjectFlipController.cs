// ============================================================
// ObjectFlipController.cs  v1.0
// 자식 오브젝트 좌우 반전 일괄 관리 컴포넌트
//
// [역할]
//   SpriteRenderer.flipX 로 스프라이트를 반전할 때
//   자식 오브젝트의 localPosition.x 는 World 좌표 체계로 인해
//   자동으로 반전되지 않는 문제를 해결.
//
// [문제 배경]
//   Unity 에서 부모의 SpriteRenderer.flipX = true 를 설정해도
//   자식 오브젝트의 Transform.localPosition 은 그대로 유지됨.
//   → 히트박스, 방패, 자물쇠 등이 항상 같은 월드 방향에 고정됨.
//
//   기존: 각 스크립트(EnemyKnightChargeAttack, LockComponent, EnemyKnight 등)가
//         각자 OnFlipped 이벤트를 구독하고 _originalLocalX 를 캐싱.
//         같은 패턴 코드가 여러 곳에 중복.
//
//   변경: ObjectFlipController 하나에 반전 대상 Transform 을 List 로 등록.
//         PlayerMover.OnFlipped 또는 EnemyAI.OnFlipped 를 구독.
//         방향 전환 시 List 의 모든 오브젝트 localPosition.x 를 일괄 반전.
//         각 스크립트에서 Flip 관련 코드를 제거할 수 있음.
//
// [사용 방법]
//   ① Player 오브젝트 or Enemy_Knight 루트에 ObjectFlipController 부착.
//   ② Inspector 에서 _flipTargets 리스트에 반전할 오브젝트의 Transform 연결.
//   ③ Player 의 경우: _flipSourceType = PlayerMover
//      Enemy 의 경우: _flipSourceType = EnemyAI
//
//   ④ 기존 스크립트에서 OnFlipped 구독 및 FlipHitbox() 코드 제거 가능.
//      (ObjectFlipController 가 대신 처리하므로)
//
// [방향 반전 규칙]
//   각 Transform 의 초기 localPosition.x 절댓값을 Awake 에서 캐싱.
//   방향 전환 시: localPosition.x = originalAbsX * newDir
//   → 여러 번 반전해도 누적 오류 없음.
//
// [Player vs Enemy 분리]
//   _flipSourceType 으로 이벤트 소스 선택.
//   PlayerMover.OnFlipped 또는 EnemyAI.OnFlipped 중 하나 구독.
//   두 소스를 동시에 구독할 수도 있음 (_flipBothSources = true).
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
    /// 자식 오브젝트 좌우 반전 일괄 관리 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [Inspector 사용 예시 — Enemy_Knight]
    ///   _flipSourceType = EnemyAI
    ///   _flipTargets:
    ///     [0] ShieldCollider Transform  (dir × +originalX = 정면)
    ///     [1] Lock Transform            (dir × -originalX = 후방) ← _invertList[1] = true
    ///     [2] ChargeHitbox Transform    (dir × +originalX)
    ///
    /// [Inspector 사용 예시 — Player.Weapon]
    ///   _flipSourceType = PlayerMover
    ///   _flipTargets:
    ///     [0] Weapon Transform          (dir × +originalX)
    ///     [1] Hitbox_Combo1 Transform
    ///     [2] Hitbox_Combo2 Transform
    ///     ...
    ///
    /// [_invertList 설명]
    ///   기본값: dir × +originalX (오브젝트가 정면 방향으로 이동)
    ///   invert=true: dir × -originalX (오브젝트가 후방 방향으로 이동)
    ///   자물쇠(Lock)처럼 항상 후방에 있어야 하는 오브젝트에 사용.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class ObjectFlipController : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 이벤트 소스 열거형
        // ──────────────────────────────────────────

        public enum FlipSourceType
        {
            /// <summary>
            /// PlayerMover.OnFlipped 구독.
            /// Player 오브젝트에 사용.
            /// </summary>
            PlayerMover,

            /// <summary>
            /// EnemyAI.OnFlipped 구독.
            /// Enemy 오브젝트에 사용.
            /// </summary>
            EnemyAI,

            /// <summary>
            /// 두 소스 모두 구독.
            /// 필요 시 사용.
            /// </summary>
            Both,
        }

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 이벤트 소스 ──────────────────────")]

        /// <summary>
        /// 어느 컴포넌트의 OnFlipped 이벤트를 구독할지 선택.
        /// Player 오브젝트 → PlayerMover.
        /// Enemy 오브젝트 → EnemyAI.
        /// </summary>
        [Tooltip("OnFlipped 이벤트 소스. Player = PlayerMover / Enemy = EnemyAI.")]
        [SerializeField] private FlipSourceType _flipSourceType = FlipSourceType.EnemyAI;

        [Header("── 반전 대상 오브젝트 ──────────────────────")]

        /// <summary>
        /// 방향 전환 시 localPosition.x 를 반전할 Transform 목록.
        /// Inspector 에서 드래그로 추가.
        /// 순서는 _invertList 와 1:1 대응.
        /// </summary>
        [Tooltip("반전 대상 Transform 목록. 순서는 InvertList 와 대응.")]
        [SerializeField] private List<Transform> _flipTargets = new List<Transform>();

        /// <summary>
        /// 각 Transform 의 반전 방향 반대 여부.
        /// false : dir × +originalX (정면 방향 — 히트박스, 방패 등)
        /// true  : dir × -originalX (후방 방향 — 자물쇠 등)
        ///
        /// [리스트 크기]
        ///   _flipTargets 와 같은 인덱스로 대응.
        ///   _invertList 의 크기가 _flipTargets 보다 작으면
        ///   나머지는 false (정면 방향) 로 처리.
        /// </summary>
        [Tooltip("각 Transform 의 반전 방향 반대 여부. true = 후방(자물쇠 등).")]
        [SerializeField] private List<bool> _invertList = new List<bool>();

        // ──────────────────────────────────────────
        // 캐시 — 초기 localPosition.x 절댓값
        // ──────────────────────────────────────────

        /// <summary>
        /// 각 Transform 의 초기 localPosition.x 절댓값.
        /// Awake 에서 캐싱.
        /// 방향 반전 시 이 값 × dir 로 계산 → 누적 오류 없음.
        /// </summary>
        private float[] _originalAbsX;

        // ──────────────────────────────────────────
        // 이벤트 소스 참조
        // ──────────────────────────────────────────

        private PlayerMover _playerMover;
        private EnemyAI _enemyAI;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            CacheOriginalPositions();
        }

        private void Start()
        {
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 각 Transform 의 초기 localPosition.x 절댓값 캐싱.
        /// Awake 에서 1회 호출.
        /// </summary>
        private void CacheOriginalPositions()
        {
            _originalAbsX = new float[_flipTargets.Count];

            for (int i = 0; i < _flipTargets.Count; i++)
            {
                if (_flipTargets[i] == null)
                {
                    _originalAbsX[i] = 0f;
                    Debug.LogWarning($"[ObjectFlipController] _flipTargets[{i}] 가 null 입니다.");
                    continue;
                }

                _originalAbsX[i] = Mathf.Abs(_flipTargets[i].localPosition.x);
            }
        }

        /// <summary>
        /// _flipSourceType 에 따라 이벤트 구독.
        /// Start 에서 호출 (Awake 순서 보장).
        /// </summary>
        private void SubscribeEvents()
        {
            if (_flipSourceType == FlipSourceType.PlayerMover
                || _flipSourceType == FlipSourceType.Both)
            {
                _playerMover = GetComponentInParent<PlayerMover>();
                if (_playerMover != null)
                    _playerMover.OnFlipped += OnFlipped;
                else
                    Debug.LogWarning("[ObjectFlipController] PlayerMover 를 찾을 수 없습니다.");
            }

            if (_flipSourceType == FlipSourceType.EnemyAI
                || _flipSourceType == FlipSourceType.Both)
            {
                _enemyAI = GetComponentInParent<EnemyAI>();
                if (_enemyAI != null)
                    _enemyAI.OnFlipped += OnFlipped;
                else
                    Debug.LogWarning("[ObjectFlipController] EnemyAI 를 찾을 수 없습니다.");
            }
        }

        private void UnsubscribeEvents()
        {
            if (_playerMover != null) _playerMover.OnFlipped -= OnFlipped;
            if (_enemyAI != null) _enemyAI.OnFlipped -= OnFlipped;
        }

        // ══════════════════════════════════════════════════════
        // 반전 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// OnFlipped 이벤트 수신 → _flipTargets 전체 일괄 반전.
        ///
        /// [계산 공식]
        ///   invert = false : localPosition.x = +originalAbsX * dir  (정면)
        ///   invert = true  : localPosition.x = -originalAbsX * dir  (후방)
        ///
        /// [예시]
        ///   dir = +1 (오른쪽), invert = false → x = +originalAbsX (오른쪽 = 정면)
        ///   dir = -1 (왼쪽),  invert = false → x = -originalAbsX (왼쪽  = 정면)
        ///   dir = +1 (오른쪽), invert = true  → x = -originalAbsX (왼쪽  = 후방)
        ///   dir = -1 (왼쪽),  invert = true  → x = +originalAbsX (오른쪽 = 후방)
        /// </summary>
        private void OnFlipped(float dir)
        {
            for (int i = 0; i < _flipTargets.Count; i++)
            {
                if (_flipTargets[i] == null) continue;

                // _invertList 범위 초과 시 기본값 false 사용
                bool invert = (i < _invertList.Count) && _invertList[i];

                float sign = invert ? -1f : 1f;
                Vector3 pos = _flipTargets[i].localPosition;

                _flipTargets[i].localPosition = new Vector3(
                    _originalAbsX[i] * dir * sign,
                    pos.y,
                    pos.z);
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 런타임에 반전 대상을 추가.
        /// 동적으로 생성된 오브젝트 등에 사용.
        /// </summary>
        /// <param name="target">추가할 Transform</param>
        /// <param name="invert">후방 방향 여부 (자물쇠 등)</param>
        public void AddFlipTarget(Transform target, bool invert = false)
        {
            if (target == null) return;

            // 배열 크기 재할당
            Array.Resize(ref _originalAbsX, _originalAbsX.Length + 1);
            _originalAbsX[_originalAbsX.Length - 1] = Mathf.Abs(target.localPosition.x);

            _flipTargets.Add(target);
            _invertList.Add(invert);
        }

        /// <summary>
        /// 런타임에 반전 대상 제거.
        /// </summary>
        /// <param name="target">제거할 Transform</param>
        public void RemoveFlipTarget(Transform target)
        {
            int idx = _flipTargets.IndexOf(target);
            if (idx < 0) return;

            _flipTargets.RemoveAt(idx);
            if (idx < _invertList.Count) _invertList.RemoveAt(idx);

            // 배열 재구성
            var newArr = new float[_originalAbsX.Length - 1];
            for (int i = 0, j = 0; i < _originalAbsX.Length; i++)
            {
                if (i == idx) continue;
                newArr[j++] = _originalAbsX[i];
            }
            _originalAbsX = newArr;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            for (int i = 0; i < _flipTargets.Count; i++)
            {
                if (_flipTargets[i] == null) continue;

                bool invert = (i < _invertList.Count) && _invertList[i];
                Gizmos.color = invert
                    ? new Color(1f, 0.4f, 0f, 0.7f)   // 주황 = 후방 (자물쇠)
                    : new Color(0f, 0.7f, 1f, 0.7f);   // 파랑 = 정면 (히트박스, 방패)

                Gizmos.DrawWireSphere(_flipTargets[i].position, 0.15f);

                UnityEditor.Handles.Label(
                    _flipTargets[i].position + Vector3.up * 0.25f,
                    $"[Flip] {_flipTargets[i].name}" +
                    (invert ? " (후방)" : " (정면)"));
            }
        }
#endif
    }
}