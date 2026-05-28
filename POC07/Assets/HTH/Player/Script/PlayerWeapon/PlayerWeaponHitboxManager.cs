// ============================================================
// PlayerWeaponHitboxManager.cs  v1.1
// 플레이어 무기 히트박스 관리 컴포넌트
//
// [v1.1 변경 — 히트박스 좌우 반전 처리]
//
//   [문제]
//     PlayerMover.SpriteRenderer.flipX 로 스프라이트를 반전할 때
//     Weapon.localPosition.x 는 PlayerWeaponMover 가 반전하지만
//     Hitbox_* 오브젝트의 BoxCollider2D.offset.x 는 그대로 유지됨.
//     → 왼쪽 방향에서 히트박스 판정이 오른쪽에 남아있는 버그.
//
//   [원인]
//     SpriteRenderer.flipX 는 렌더링만 뒤집고
//     Collider2D 의 월드 위치 계산에는 영향을 주지 않음.
//     Collider2D 는 localPosition + offset 을 그대로 월드 좌표로 변환.
//
//   [해결]
//     PlayerMover.OnFlipped 구독 추가.
//     방향 전환 시 _hitboxes 배열의 각 BoxCollider2D.offset.x 부호 반전.
//     → 판정 위치가 항상 캐릭터의 앞쪽에 위치.
//
//   [offset.x 반전 vs localPosition.x 반전]
//     offset.x 를 반전하는 이유:
//       offset 은 해당 콜라이더 오브젝트 기준 상대 위치.
//       localPosition 을 반전하면 씬 배치 의도와 어긋날 수 있음.
//       offset 반전이 판정 영역만 정확하게 대칭 이동시킴.
//
//   [초기 offset 캐싱]
//     Awake 에서 _originalOffsets 배열에 각 박스의 원본 offset 저장.
//     FlipHitboxes(dir) 에서 |offset.x| * dir 로 계산.
//     절댓값 사용으로 여러 번 반전해도 누적 오류 없음.
//
// [v1.0 역할 유지]
//   콤보 단계별 히트박스(Collider2D) 활성/비활성.
//   히트 감지 시 OnHit 이벤트 발행.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 무기 히트박스 관리 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [히트박스 좌우 반전 흐름]
    ///   PlayerMover.FlipSprite()
    ///     → OnFlipped(newDir) 이벤트
    ///       → FlipHitboxes(newDir)
    ///           → 각 BoxCollider2D.offset.x = |origOffset.x| * newDir
    ///
    /// [초기화 흐름]
    ///   Awake()
    ///     → CacheOriginalOffsets()
    ///         → _originalOffsets[i] = _hitboxes[i] as BoxCollider2D 의 offset 저장
    ///     → DisableAllHitboxes()
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerWeaponHitboxManager : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 히트박스 인덱스 상수
        // ──────────────────────────────────────────

        /// <summary> Combo1 히트박스 인덱스. </summary>
        public const int HitboxCombo1 = 0;

        /// <summary> Combo2 히트박스 인덱스. </summary>
        public const int HitboxCombo2 = 1;

        /// <summary> Combo3 히트박스 인덱스. </summary>
        public const int HitboxCombo3 = 2;

        /// <summary> 공중 공격 히트박스 인덱스. </summary>
        public const int HitboxAirAttack = 3;

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트박스 콜라이더 연결 ──────────────────────")]

        /// <summary>
        /// 콤보별 히트박스 Collider2D 배열.
        /// 인덱스: 0=Combo1, 1=Combo2, 2=Combo3, 3=AirAttack.
        /// Inspector 에서 순서대로 연결.
        /// </summary>
        [Tooltip("콤보별 히트박스. 인덱스 0=Combo1, 1=Combo2, 2=Combo3, 3=AirAttack.")]
        [SerializeField] private Collider2D[] _hitboxes;

        [Header("── 감지 설정 ──────────────────────")]

        /// <summary>
        /// 히트 판정 LayerMask.
        /// 적(Enemy) 레이어를 포함한 레이어만 감지.
        /// </summary>
        [Tooltip("히트 판정 레이어. Enemy 레이어 선택.")]
        [SerializeField] private LayerMask _hitLayer;

        // ──────────────────────────────────────────
        // 히트박스 원본 offset 캐시 (v1.1)
        // ──────────────────────────────────────────

        /// <summary>
        /// 각 BoxCollider2D 의 초기 offset 을 캐싱.
        /// Awake 에서 한 번만 저장.
        /// FlipHitboxes() 에서 절댓값 + 방향 곱으로 정확한 반전 계산에 사용.
        ///
        /// [왜 절댓값인가?]
        ///   여러 번 반전 시 offset.x = -offset.x 를 반복하면 정상.
        ///   하지만 offset.x = |originalOffset.x| * dir 방식은
        ///   어떤 순서로 호출해도 항상 올바른 값을 보장.
        /// </summary>
        private Vector3[] _HitBoxPosition;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 활성화된 히트박스 인덱스. -1 = 비활성. </summary>
        private int _activeHitboxIndex = -1;

        /// <summary>
        /// 현재 타격에서 이미 맞은 오브젝트 목록.
        /// 히트박스 활성 기간 동안 중복 피격 방지.
        /// DisableAllHitboxes() 호출 시 초기화.
        /// </summary>
        private readonly HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();

        /// <summary> 현재 활성 히트박스에 전달할 데미지 정보. </summary>
        private DamageInfo _currentDamageInfo;

        /// <summary> OverlapCollider 결과 임시 버퍼. GC 방지용 필드. </summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// IDamageable 에 히트 발생 시 발행.
        /// 파라미터: (피격된 IDamageable, DamageInfo).
        /// </summary>
        public event Action<IDamageable, DamageInfo> OnHit;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화 — offset 캐싱 후 모든 히트박스 비활성화.
        /// </summary>
        private void Awake()
        {
            if (_hitboxes == null || _hitboxes.Length == 0)
            {
                Debug.LogWarning("[PlayerWeaponHitboxManager] 히트박스가 연결되지 않았습니다.");
                return;
            }

            CacheOriginalOffsets();
            DisableAllHitboxes();
        }

        /// <summary>
        /// Start 에서 PlayerMover.OnFlipped 구독.
        /// Awake 순서 보장을 위해 Start 사용.
        /// </summary>
        private void Start()
        {
            var mover = GetComponentInParent<PlayerMover>();

            if (mover != null)
            {
                mover.OnFlipped += FlipHitboxes;
            }
            else
            {
                Debug.LogWarning("[PlayerWeaponHitboxManager] 부모에서 PlayerMover 를 " +
                                 "찾을 수 없습니다. 히트박스 좌우 반전이 비활성화됩니다.");
            }
        }

        /// <summary>
        /// 이벤트 구독 해제.
        /// </summary>
        private void OnDestroy()
        {
            var mover = GetComponentInParent<PlayerMover>();
            if (mover != null)
                mover.OnFlipped -= FlipHitboxes;
        }

        /// <summary>
        /// 활성화된 히트박스가 있는 경우 매 프레임 히트 감지.
        /// </summary>
        private void Update()
        {
            if (_activeHitboxIndex < 0) return;
            CheckHit(_hitboxes[_activeHitboxIndex]);
        }

        // ══════════════════════════════════════════════════════
        // 히트박스 반전 (v1.1 신규)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 모든 히트박스의 BoxCollider2D.offset.x 를 방향에 맞게 반전.
        /// PlayerMover.OnFlipped 이벤트 수신 시 호출.
        ///
        /// [동작]
        ///   각 BoxCollider2D 의 offset.x = Abs(originalOffset.x) * newDir
        ///   → 1(오른쪽) : 원본 offset 그대로
        ///   → -1(왼쪽)  : offset.x 부호 반전
        ///
        /// [BoxCollider2D 가 아닌 경우]
        ///   CircleCollider2D 등 다른 타입도 offset 을 가짐.
        ///   현재는 BoxCollider2D 만 처리.
        ///   추후 CircleCollider2D 지원 필요 시 else if 분기 추가.
        /// </summary>
        /// <param name="newDir">새 방향. 1 = 오른쪽, -1 = 왼쪽.</param>
        private void FlipHitboxes(float newDir)
        {
            if (_hitboxes == null) return;
            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i] == null && _HitBoxPosition[i] == null) continue;

                if (_hitboxes[i] is BoxCollider2D box)
                {
                    Debug.Log($"newDir: {newDir} HitBox{_HitBoxPosition[i].x} {box}");
                    _HitBoxPosition[i] = new Vector3(Mathf.Abs(_HitBoxPosition[i].x) * newDir,
                        _HitBoxPosition[i].y, _HitBoxPosition[i].z);
                    box.transform.localPosition = _HitBoxPosition[i];
                }
            }
        }

        /// <summary>
        /// 각 BoxCollider2D 의 초기 offset 을 _originalOffsets 배열에 캐싱.
        /// Awake 에서 1회 호출.
        /// </summary>
        private void CacheOriginalOffsets()
        {
            _HitBoxPosition = new Vector3[_hitboxes.Length];

            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i] is BoxCollider2D box)
                    _HitBoxPosition[i] = box.gameObject.transform.localPosition;
                else
                    Debug.Log($"[PlayerWeaponHitBoxManager] HitBox를 찾을 수 없습니다. {_HitBoxPosition[i]}");
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — RustyKeyWeapon 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지정 인덱스의 히트박스를 활성화한다.
        /// 이전 히트박스는 자동으로 비활성화.
        /// </summary>
        /// <param name="hitboxIndex">활성화할 히트박스 인덱스</param>
        /// <param name="damageInfo">이 히트에서 전달할 데미지 정보</param>
        public void EnableHitbox(int hitboxIndex, DamageInfo damageInfo)
        {
            if (!IsValidIndex(hitboxIndex)) return;

            DisableAllHitboxes();

            _activeHitboxIndex = hitboxIndex;
            _currentDamageInfo = damageInfo;
            _hitboxes[hitboxIndex].enabled = true;
        }

        /// <summary>
        /// 모든 히트박스를 비활성화하고 히트 대상 목록을 초기화한다.
        /// 공격 모션 종료 시 호출.
        /// </summary>
        public void DisableAllHitboxes()
        {
            if (_hitboxes == null) return;

            foreach (var hb in _hitboxes)
                if (hb != null) hb.enabled = false;

            _activeHitboxIndex = -1;
            _hitTargets.Clear();
        }

        // ══════════════════════════════════════════════════════
        // 히트 감지 — 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// OverlapCollider 로 히트박스와 겹치는 IDamageable 을 감지하고
        /// 아직 맞지 않은 대상에게 OnHit 이벤트 발행.
        /// </summary>
        private void CheckHit(Collider2D hitbox)
        {
            _overlapBuffer.Clear();

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_hitLayer);
            filter.useTriggers = true;

            int count = hitbox.Overlap(filter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _overlapBuffer[i];

                if (_hitTargets.Contains(col)) continue;

                if (col.TryGetComponent<IDamageable>(out var damageable))
                {
                    _hitTargets.Add(col);
                    damageable.TakeDamage(_currentDamageInfo);
                    OnHit?.Invoke(damageable, _currentDamageInfo);
                }
            }
        }

        /// <summary>
        /// 히트박스 인덱스 유효성 검사.
        /// </summary>
        private bool IsValidIndex(int index)
        {
            if (_hitboxes == null || index < 0 || index >= _hitboxes.Length)
            {
                Debug.LogWarning($"[PlayerWeaponHitboxManager] 유효하지 않은 인덱스: {index}");
                return false;
            }
            return true;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_hitboxes == null) return;

            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i] == null) continue;

                Gizmos.color = (i == _activeHitboxIndex)
                    ? new Color(1f, 0.2f, 0.2f, 0.6f)
                    : new Color(0.5f, 0.5f, 0.5f, 0.2f);

                if (_hitboxes[i] is BoxCollider2D box)
                {
                    // offset 이 반전된 현재 실제 위치로 Gizmos 표시
                    Gizmos.DrawCube(
                        (Vector2)box.transform.position + box.offset,
                        box.size);
                }
            }
        }
    }
}