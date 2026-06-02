// ============================================================
// ObjectFlipController.cs  v1.5
// 자식 오브젝트 좌우 반전 일괄 관리 컴포넌트
//
// [v1.3 변경 — TestBossAI 소스 추가]
//
//   FlipSourceType 에 TestBossAI 추가.
//   SubscribeEvents / UnsubscribeEvents 에 TestBossAI 분기 추가.
//   TestBoss 에서도 ObjectFlipController 를 사용 가능.
//
//   [TestBoss 사용법]
//     TestBoss 루트 오브젝트에 ObjectFlipController 부착.
//     _flipSourceType = TestBossAI 선택.
//     _flipTargets = Arm_L, Arm_R 등 반전할 Transform 연결.
//     _spriteRenderers = 반전할 SpriteRenderer 연결 (선택).
//
// [v1.2 변경]
//   PlayerWeaponMover.SyncOrigin(dir) 호출 추가.
//
// [v1.1 변경]
//   SpriteRenderer flipX 반전 기능 추가.
//
// [v1.0]
//   localPosition.x 일괄 반전.
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
    /// 자식 오브젝트 좌우 반전 일괄 관리 컴포넌트. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [FlipSourceType]
    ///   PlayerMover  : Player 오브젝트에 사용
    ///   EnemyAI      : Enemy 오브젝트에 사용
    ///   TestBossAI   : TestBoss 오브젝트에 사용 ← v1.3 추가
    ///   Both         : PlayerMover + EnemyAI 동시 구독
    ///
    /// [TestBoss 사용 예시]
    ///   _flipSourceType   = TestBossAI
    ///   _flipTargets[0]   = Arm_L Transform   _invertList[0]=false
    ///   _flipTargets[1]   = Arm_R Transform   _invertList[1]=false
    ///   _spriteRenderers[0] = 루트 SpriteRenderer (선택)
    ///
    /// [_invertList 설명]
    ///   false : dir × +originalX (정면 방향)
    ///   true  : dir × -originalX (후방 방향)
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
            /// <summary> TestBossAI.OnFlipped 구독. TestBoss 오브젝트에 사용. ← v1.3 추가 </summary>
            TestBossAI,
        }

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 이벤트 소스 ──────────────────────")]

        [Tooltip("OnFlipped 이벤트 소스.\nPlayerMover=플레이어 / EnemyAI=일반적 / TestBossAI=테스트보스.")]
        [SerializeField] private FlipSourceType _flipSourceType = FlipSourceType.EnemyAI;

        [Header("── localPosition.x 반전 대상 ──────────────────────")]

        [Tooltip("반전 대상 Transform 목록. 순서는 _invertList 와 대응.")]
        [SerializeField] private List<Transform> _flipTargets = new List<Transform>();

        [Tooltip("true = 후방(자물쇠 등) / false = 정면(히트박스, 방패 등).")]
        [SerializeField] private List<bool> _invertList = new List<bool>();

        [Header("── SpriteRenderer flipX 반전 대상 ──────────────────────")]

        [Tooltip("flipX 반전 대상 반대로 적용 시키기.")]
        [SerializeField] private bool _spriteFlipX;
        [Tooltip("flipX 반전 대상 SpriteRenderer 목록. 방향 전환 시 flipX = (dir < 0) 적용.")]
        [SerializeField] private List<SpriteRenderer> _spriteRenderers = new List<SpriteRenderer>();

        // ──────────────────────────────────────────
        // 캐시
        // ──────────────────────────────────────────

        private float[] _originalAbsX;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private PlayerMover _playerMover;
        private PlayerWeaponMover _weaponMover;
        private EnemyAI _enemyAI;

        /// <summary> v1.3 추가 — TestBossAI 참조. </summary>
        private TestBossAI _testBossAI;

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
        /// 각 Target 의 localPosition.x 를 부호 포함 실제 값으로 캐싱.
        ///
        /// [v1.5 수정]
        ///   기존: Mathf.Abs 로 절댓값만 저장
        ///     Arm_L(-2), Arm_R(+2) 모두 2 로 캐싱됨
        ///     반전 시 2 * dir * 1 → 둘 다 -2 로 겹침
        ///   수정: 부호 포함 그대로 저장
        ///     Arm_L = -2, Arm_R = +2
        ///     반전 시 originalX * -1 → Arm_L: +2, Arm_R: -2 (정확한 대칭)
        /// </summary>
        private void CacheOriginalPositions()
        {
            _originalAbsX = new float[_flipTargets.Count];
            for (int i = 0; i < _flipTargets.Count; i++)
            {
                if (_flipTargets[i] != null)
                    _originalAbsX[i] = _flipTargets[i].localPosition.x; // ★ v1.5 Mathf.Abs 제거
            }
        }

        private void SubscribeEvents()
        {
            if (_flipSourceType == FlipSourceType.PlayerMover
                || _flipSourceType == FlipSourceType.Both)
            {
                _playerMover = GetComponentInParent<PlayerMover>();
                if (_playerMover != null)
                    _playerMover.OnFlipped += HandleFlipped;
                else
                    Debug.LogWarning("[ObjectFlipController] PlayerMover 를 찾을 수 없습니다.");

                _weaponMover = GetComponentInParent<PlayerWeaponMover>();
                if (_weaponMover == null)
                    _weaponMover = GetComponentInChildren<PlayerWeaponMover>();
            }

            if (_flipSourceType == FlipSourceType.EnemyAI
                || _flipSourceType == FlipSourceType.Both)
            {
                _enemyAI = GetComponentInParent<EnemyAI>();
                if (_enemyAI != null)
                    _enemyAI.OnFlipped += HandleFlipped;
                else
                    Debug.LogWarning("[ObjectFlipController] EnemyAI 를 찾을 수 없습니다.");
            }

            // ★ v1.3: TestBossAI 구독
            if (_flipSourceType == FlipSourceType.TestBossAI)
            {
                _testBossAI = GetComponentInParent<TestBossAI>();
                if (_testBossAI != null)
                    _testBossAI.OnFlipped += HandleFlipped;
                else
                    Debug.LogWarning("[ObjectFlipController] TestBossAI 를 찾을 수 없습니다.");
            }
        }

        private void UnsubscribeEvents()
        {
            if (_playerMover != null) _playerMover.OnFlipped -= HandleFlipped;
            if (_enemyAI != null) _enemyAI.OnFlipped -= HandleFlipped;
            // ★ v1.3: TestBossAI 구독 해제
            if (_testBossAI != null) _testBossAI.OnFlipped -= HandleFlipped;
        }

        // ══════════════════════════════════════════════════════
        // 반전 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// OnFlipped 이벤트 수신.
        /// ① _flipTargets 전체 localPosition.x 일괄 반전.
        /// ② PlayerWeaponMover 원점 동기화 (Player 전용).
        /// ③ _spriteRenderers 전체 flipX 반전.
        /// ④ TestBossPattern SyncOrigin 호출 (v1.4 추가)
        /// </summary>
        private void HandleFlipped(float dir)
        {
            // ① localPosition.x 반전
            for (int i = 0; i < _flipTargets.Count; i++)
            {
                if (_flipTargets[i] == null) continue;

                bool invert = (i < _invertList.Count) && _invertList[i];
                float sign = invert ? -1f : 1f;
                Vector3 pos = _flipTargets[i].localPosition;

                // ★ v1.5: originalX 는 부호 포함 실제 값
                //   dir=+1(오른쪽): originalX 그대로 사용 (초기 상태 복원)
                //   dir=-1(왼쪽):  originalX * -1 로 대칭 이동
                //   invert=true:   부호 추가 반전 (후방 자물쇠 등)
                _flipTargets[i].localPosition = new Vector3(
                    _originalAbsX[i] * dir * sign,
                    pos.y,
                    pos.z);
            }

            // ② PlayerWeaponMover 원점 동기화 (Player 전용)
            _weaponMover?.SyncOrigin(dir);

            // ③ SpriteRenderer flipX 반전
            bool flipped;
            if (_spriteFlipX) flipped = dir > 0f;
            else flipped = dir < 0f;

            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                if (_spriteRenderers[i] != null)
                    _spriteRenderers[i].flipX = flipped;
            }

            // ④ TestBossPattern SyncOrigin — 반전 후 팔 원점 재캐싱
            //    패턴이 반전된 좌표를 원위치로 인식하게 함
            if (_flipSourceType == FlipSourceType.TestBossAI)
            {
                var patterns = GetComponentsInChildren<TestBossPatternBase>(true);
                foreach (var p in patterns)
                {
                    if (p is TestBossPattern_PunchDown pd) pd.SyncOrigin(dir);
                    else if (p is TestBossPattern_PunchShot ps) ps.SyncOrigin(dir);
                }
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary> 런타임에 localPosition.x 반전 대상 추가. </summary>
        public void AddFlipTarget(Transform target, bool invert = false)
        {
            if (target == null) return;
            Array.Resize(ref _originalAbsX, _originalAbsX.Length + 1);
            _originalAbsX[_originalAbsX.Length - 1] = target.localPosition.x; // v1.5 Mathf.Abs 제거
            _flipTargets.Add(target);
            _invertList.Add(invert);
        }

        /// <summary> 런타임에 localPosition.x 반전 대상 제거. </summary>
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

        /// <summary> 런타임에 SpriteRenderer 반전 대상 추가. </summary>
        public void AddSpriteRenderer(SpriteRenderer sr)
        {
            if (sr != null && !_spriteRenderers.Contains(sr))
                _spriteRenderers.Add(sr);
        }

        /// <summary> 런타임에 SpriteRenderer 반전 대상 제거. </summary>
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
                    (invert ? " [후방]" : " [정면]"));
            }
        }
#endif
    }
}