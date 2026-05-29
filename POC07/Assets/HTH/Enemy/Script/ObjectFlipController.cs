// ============================================================
// ObjectFlipController.cs  v1.2
// 자식 오브젝트 좌우 반전 일괄 관리 컴포넌트
//
// [v1.2 변경]
//   PlayerWeaponMover.SyncOrigin(dir) 호출 추가.
//   방향 전환 시 _originLocalPosition.x 도 동기화.
//   이를 통해 PlaySwing() 에서 _originLocalPosition 스냅 시
//   올바른 방향으로 스냅되어 왼쪽 공격 튀는 버그 수정.
//
// [v1.1 변경]
//   SpriteRenderer flipX 반전 기능 추가.
//   _spriteRenderers 리스트 추가 — Inspector 에서 드래그 연결.
//   OnFlipped 수신 시 localPosition.x 반전과 동시에
//   _spriteRenderers 전체 flipX = (dir < 0) 처리.
//
// [v1.0]
//   localPosition.x 일괄 반전.
//   _flipTargets / _invertList / _originalAbsX 캐시 구조.
//
// [역할]
//   SpriteRenderer.flipX 로 스프라이트를 반전할 때
//   자식 오브젝트의 localPosition.x 는 자동으로 반전되지 않는 문제 해결.
//   localPosition.x 반전 + SpriteRenderer.flipX 모두 일괄 처리.
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
    /// 자식 오브젝트 좌우 반전 일괄 관리 컴포넌트. (v1.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [Inspector 사용 예시 — Player.Weapon]
    ///   _flipSourceType   = PlayerMover
    ///   _flipTargets[0]   = Weapon Transform
    ///   _flipTargets[1~4] = HitBox01~04 Transform
    ///   _flipTargets[5]   = FirePoint Transform
    ///   _spriteRenderers[0] = Weapon SpriteRenderer  ← v1.1 추가
    ///
    /// [Inspector 사용 예시 — Enemy_Knight]
    ///   _flipSourceType   = EnemyAI
    ///   _flipTargets[0]   = ShieldCollider  _invertList[0]=false (정면)
    ///   _flipTargets[1]   = Lock            _invertList[1]=true  (후방)
    ///   _flipTargets[2]   = ChargeHitbox    _invertList[2]=false (정면)
    ///   _spriteRenderers[0] = SpriteRenderer (선택)
    ///
    /// [_invertList 설명]
    ///   false : dir × +originalX (정면 방향 — 히트박스, 방패)
    ///   true  : dir × -originalX (후방 방향 — 자물쇠)
    ///
    /// [_spriteRenderers 설명]
    ///   방향 전환 시 flipX = (dir < 0) 으로 일괄 적용.
    ///   연결하지 않으면 localPosition.x 반전만 수행.
    ///   Weapon 스프라이트처럼 부모와 별도로 flipX 를 관리해야 하는
    ///   자식 SpriteRenderer 를 여기에 등록.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class ObjectFlipController : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 이벤트 소스 열거형
        // ──────────────────────────────────────────

        public enum FlipSourceType
        {
            /// <summary> PlayerMover.OnFlipped 구독. Player 오브젝트에 사용. </summary>
            PlayerMover,
            /// <summary> EnemyAI.OnFlipped 구독. Enemy 오브젝트에 사용. </summary>
            EnemyAI,
            /// <summary> 두 소스 모두 구독. </summary>
            Both,
        }

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 이벤트 소스 ──────────────────────")]

        /// <summary>
        /// OnFlipped 이벤트 소스 선택.
        /// Player = PlayerMover / Enemy = EnemyAI.
        /// </summary>
        [Tooltip("OnFlipped 이벤트 소스. Player = PlayerMover / Enemy = EnemyAI.")]
        [SerializeField] private FlipSourceType _flipSourceType = FlipSourceType.EnemyAI;

        [Header("── localPosition.x 반전 대상 ──────────────────────")]

        /// <summary>
        /// 방향 전환 시 localPosition.x 를 반전할 Transform 목록.
        /// 순서는 _invertList 와 1:1 대응.
        /// </summary>
        [Tooltip("반전 대상 Transform 목록. 순서는 _invertList 와 대응.")]
        [SerializeField] private List<Transform> _flipTargets = new List<Transform>();

        /// <summary>
        /// 각 Transform 의 반전 방향 반대 여부.
        /// false : dir × +originalX (정면 방향 — 히트박스, 방패)
        /// true  : dir × -originalX (후방 방향 — 자물쇠)
        /// </summary>
        [Tooltip("true = 후방(자물쇠 등) / false = 정면(히트박스, 방패 등).")]
        [SerializeField] private List<bool> _invertList = new List<bool>();

        [Header("── SpriteRenderer flipX 반전 대상 ──────────────────────")]

        /// <summary>
        /// 방향 전환 시 flipX 를 반전할 SpriteRenderer 목록.
        /// 연결한 모든 SpriteRenderer 에 flipX = (dir &lt; 0) 적용.
        /// 연결하지 않으면 SpriteRenderer 반전 없이 localPosition.x 만 처리.
        ///
        /// [사용 예]
        ///   Weapon 오브젝트의 SpriteRenderer — 무기 스프라이트 방향 동기화
        ///   자식 캐릭터 파츠 SpriteRenderer 등
        /// </summary>
        [Tooltip("flipX 반전 대상 SpriteRenderer 목록. 방향 전환 시 flipX = (dir < 0) 적용.")]
        [SerializeField] private List<SpriteRenderer> _spriteRenderers = new List<SpriteRenderer>();

        // ──────────────────────────────────────────
        // 캐시
        // ──────────────────────────────────────────

        /// <summary>
        /// 각 Transform 의 초기 localPosition.x 절댓값.
        /// Awake 에서 캐싱. 방향 반전 시 이 값 × dir 로 계산 → 누적 오류 없음.
        /// </summary>
        private float[] _originalAbsX;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private PlayerMover _playerMover;
        private PlayerWeaponMover _weaponMover;
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
        /// _flipTargets 의 초기 localPosition.x 절댓값 캐싱.
        /// </summary>
        private void CacheOriginalPositions()
        {
            _originalAbsX = new float[_flipTargets.Count];
            for (int i = 0; i < _flipTargets.Count; i++)
            {
                if (_flipTargets[i] != null)
                    _originalAbsX[i] = Mathf.Abs(_flipTargets[i].localPosition.x);
            }
        }

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

                // PlayerWeaponMover — _originLocalPosition 동기화 대상
                _weaponMover = GetComponentInParent<PlayerWeaponMover>();
                if (_weaponMover == null)
                    _weaponMover = GetComponentInChildren<PlayerWeaponMover>();
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
        /// OnFlipped 이벤트 수신.
        /// ① _flipTargets 전체 localPosition.x 일괄 반전.
        /// ② _spriteRenderers 전체 flipX 반전.
        ///
        /// [localPosition.x 계산 공식]
        ///   invert=false : +originalAbsX × dir  (정면 방향)
        ///   invert=true  : -originalAbsX × dir  (후방 방향)
        ///
        /// [flipX 공식]
        ///   flipX = (dir &lt; 0)  → 왼쪽이면 true, 오른쪽이면 false
        /// </summary>
        private void OnFlipped(float dir)
        {
            // ── ① localPosition.x 반전 ──────────────────────
            for (int i = 0; i < _flipTargets.Count; i++)
            {
                if (_flipTargets[i] == null) continue;

                bool invert = (i < _invertList.Count) && _invertList[i];
                float sign = invert ? -1f : 1f;
                Vector3 pos = _flipTargets[i].localPosition;

                _flipTargets[i].localPosition = new Vector3(
                    _originalAbsX[i] * dir * sign,
                    pos.y,
                    pos.z);
            }

            // ── ② PlayerWeaponMover 원점 동기화 ─────────────────
            // PlaySwing() 이 _originLocalPosition 으로 스냅하기 전에
            // 현재 방향에 맞는 올바른 원점을 동기화.
            _weaponMover?.SyncOrigin(dir);

            // ── ③ SpriteRenderer flipX 반전 ──────────────────
            bool flipped = dir < 0f;
            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                if (_spriteRenderers[i] != null)
                    _spriteRenderers[i].flipX = flipped;
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 런타임에 localPosition.x 반전 대상 추가.
        /// </summary>
        public void AddFlipTarget(Transform target, bool invert = false)
        {
            if (target == null) return;

            Array.Resize(ref _originalAbsX, _originalAbsX.Length + 1);
            _originalAbsX[_originalAbsX.Length - 1] = Mathf.Abs(target.localPosition.x);
            _flipTargets.Add(target);
            _invertList.Add(invert);
        }

        /// <summary>
        /// 런타임에 localPosition.x 반전 대상 제거.
        /// </summary>
        public void RemoveFlipTarget(Transform target)
        {
            int idx = _flipTargets.IndexOf(target);
            if (idx < 0) return;

            _flipTargets.RemoveAt(idx);
            if (idx < _invertList.Count) _invertList.RemoveAt(idx);

            var newArr = new float[_originalAbsX.Length - 1];
            for (int i = 0, j = 0; i < _originalAbsX.Length; i++)
            {
                if (i == idx) continue;
                newArr[j++] = _originalAbsX[i];
            }
            _originalAbsX = newArr;
        }

        /// <summary>
        /// 런타임에 SpriteRenderer 반전 대상 추가.
        /// </summary>
        public void AddSpriteRenderer(SpriteRenderer sr)
        {
            if (sr != null && !_spriteRenderers.Contains(sr))
                _spriteRenderers.Add(sr);
        }

        /// <summary>
        /// 런타임에 SpriteRenderer 반전 대상 제거.
        /// </summary>
        public void RemoveSpriteRenderer(SpriteRenderer sr)
        {
            _spriteRenderers.Remove(sr);
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
                    ? new Color(1f, 0.4f, 0f, 0.7f)
                    : new Color(0f, 0.7f, 1f, 0.7f);

                Gizmos.DrawWireSphere(_flipTargets[i].position, 0.15f);

                UnityEditor.Handles.Label(
                    _flipTargets[i].position + Vector3.up * 0.25f,
                    $"[Flip] {_flipTargets[i].name}" +
                    (invert ? " (후방)" : " (정면)"));
            }

            // SpriteRenderer 등록 표시
            foreach (var sr in _spriteRenderers)
            {
                if (sr == null) continue;
                Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.6f);
                Gizmos.DrawWireSphere(sr.transform.position, 0.2f);
                UnityEditor.Handles.Label(
                    sr.transform.position + Vector3.up * 0.45f,
                    $"[flipX] {sr.name}");
            }
        }
#endif
    }
}