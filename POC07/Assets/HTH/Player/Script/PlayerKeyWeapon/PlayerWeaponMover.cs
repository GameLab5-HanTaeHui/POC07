// ============================================================
// PlayerWeaponMover.cs  v1.1
// Weapon 오브젝트 스윙 이동 전담 컴포넌트
//
// [v1.1 변경]
//   ① PlayerMover.OnFlipped 구독 추가
//       - 방향 전환 시 _originLocalPosition.x 부호 반전
//       - 스윙 진행 중이면 CancelSwing() 으로 즉시 중단 후 위치 보정
//       - 이로써 왼쪽을 바라볼 때도 무기가 항상 캐릭터 앞쪽에 위치
//
// [역할]
//   공격 시 Weapon 오브젝트(로컬 Transform)를
//   DOTween 으로 앞으로 뻗었다 원점으로 복귀시킴.
//   히트박스가 Weapon 의 자식이므로 자동으로 같이 이동.
//
// [이동 흐름]
//   PlaySwing() 호출
//     1. 진행 중인 스윙 즉시 Kill + 원점 복귀
//     2. 앞으로 뻗기  : localPosition → offset  (Ease.OutQuart, swingDuration)
//     3. 히트박스 구간 유지 (hitboxDuration - swingDuration)
//     4. 원점 복귀    : localPosition → _originLocalPosition (Ease.InQuart, returnDuration)
//
// [좌우 방향 처리]
//   PlayerMover.OnFlipped 수신 시 _originLocalPosition.x 부호 반전.
//   PlaySwing 의 GetSwingOffset 은 FacingDirection 으로 X 부호 결정.
//   두 값이 항상 일치하므로 스윙 방향과 원점 위치가 동기화됨.
//
// [Hierarchy 위치]
//   Player
//   └── Weapon          ← 이 오브젝트에 부착
//         └── Hitbox_*  ← 자동으로 같이 이동
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// Weapon 오브젝트 스윙 이동 전담 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [WeaponAnimator 에서의 호출]
    ///   _weaponMover.PlaySwing(AttackType.Combo1);
    ///   _weaponMover.PlaySwing(AttackType.AirAttack);
    ///
    /// [PlayerMover.OnFlipped 구독 흐름]
    ///   PlayerMover.FlipSprite() → OnFlipped(newDir)
    ///     → HandleFlipped(newDir)
    ///         → _originLocalPosition.x = |x| * newDir
    ///         → 스윙 중이면 CancelSwing() 으로 즉시 원점 복귀
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerWeaponMover : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 장착된 열쇠 데이터.
        /// WeaponKeyController 가 열쇠 교체 시 SetKeyData() 로 주입.
        /// </summary>
        private KeyDataSO _keyData;

        /// <summary>
        /// 진행 중인 스윙 Tween.
        /// 새 공격 시작 또는 방향 전환 시 Kill() 후 재시작.
        /// </summary>
        private Tween _swingTween;

        /// <summary>
        /// 진행 중인 스윙 시퀀스 코루틴.
        /// </summary>
        private Coroutine _swingCoroutine;

        /// <summary>
        /// 무기 오브젝트 스프라이트
        /// </summary>
        private SpriteRenderer _spriteRenderer;

        /// <summary>
        /// Weapon 오브젝트의 로컬 원점 위치.
        /// Awake 에서 초기값 캐싱.
        /// OnFlipped 수신 시 X 부호가 반전됨.
        ///
        /// [왜 X 만 바꾸는가?]
        ///   Weapon 은 Player 의 자식이므로 localPosition.x 의 부호가
        ///   캐릭터 기준 좌/우를 결정함.
        ///   PlayerMover 가 SpriteRenderer.flipX 로 스프라이트를 반전하므로
        ///   Weapon 의 로컬 X 좌표도 같이 반전해야 시각적으로 앞쪽에 위치.
        /// </summary>
        private Vector3 _originLocalPosition;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 스윙 중 여부. </summary>
        public bool IsSwinging { get; private set; }

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 씬 배치 시 초기 로컬 위치를 원점으로 캐싱.
        /// PlayerMover.OnFlipped 구독 시작.
        /// </summary>
        private void Awake()
        {
            _originLocalPosition = transform.localPosition;
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Start 에서 PlayerMover 이벤트 구독.
        /// Awake 순서 보장을 위해 Start 사용.
        /// </summary>
        private void Start()
        {
            var mover = GetComponentInParent<PlayerMover>();

            if (mover != null)
            {
                mover.OnFlipped += HandleFlipped;
            }
            else
            {
                Debug.LogWarning("[PlayerWeaponMover] 부모에서 PlayerMover 를 찾을 수 없습니다. " +
                                 "좌우 Weapon 동기화가 비활성화됩니다.");
            }
        }

        /// <summary>
        /// 이벤트 구독 해제 및 진행 중인 Tween Kill.
        /// </summary>
        private void OnDestroy()
        {
            _swingTween?.Kill();

            var mover = GetComponentInParent<PlayerMover>();
            if (mover != null)
                mover.OnFlipped -= HandleFlipped;
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// PlayerMover.OnFlipped 수신 핸들러.
        /// 방향 전환 시 _originLocalPosition.x 를 반전하고
        /// 스윙 진행 중이면 즉시 취소 후 새 원점으로 이동.
        ///
        /// [처리 흐름]
        ///   1. _originLocalPosition.x 를 newDir 부호로 교정
        ///   2. 스윙 중이면 CancelSwing() → localPosition = _originLocalPosition
        ///   3. 스윙 중이 아니면 localPosition 만 즉시 교정
        /// </summary>
        /// <param name="newDir">새 방향. 1 = 오른쪽, -1 = 왼쪽.</param>
        private void HandleFlipped(float newDir)
        {
            // X 부호만 반전 (Y, Z 는 유지)
            _originLocalPosition = new Vector3(Mathf.Abs(_originLocalPosition.x) * newDir,
                _originLocalPosition.y, _originLocalPosition.z);
            // 스프라이트 반전
            _spriteRenderer.flipX = newDir > 0 ? false : true;

            if (IsSwinging)
            {
                // 스윙 중이면 즉시 취소하고 새 원점으로 복귀
                CancelSwing();
            }
            else
            {
                // 스윙 중이 아니면 현재 위치만 즉시 보정
                transform.localPosition = _originLocalPosition;
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 열쇠 데이터 주입.
        /// WeaponKeyController.ActivateWeapon() 에서 호출.
        /// </summary>
        /// <param name="keyData">장착된 열쇠 데이터</param>
        public void SetKeyData(KeyDataSO keyData)
        {
            _keyData = keyData;
        }

        /// <summary>
        /// 스윙 이동 실행.
        /// PlayerWeaponAnimator 에서 콤보 이벤트 수신 시 호출.
        ///
        /// [AttackType 별 이동 방향]
        ///   Combo1 ~ Combo3 : FacingDirection(X) 앞으로 swingDistance
        ///   AirAttack       : 아래(Y 음수) + 소량 X 전진
        /// </summary>
        /// <param name="attackType">공격 유형 — 이동 방향 결정</param>
        public void PlaySwing(AttackType attackType)
        {
            if (_keyData == null) return;

            // 진행 중인 스윙 즉시 중단
            _swingTween?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            // 원점으로 즉시 스냅 후 새 스윙 시작
            transform.localPosition = _originLocalPosition;
            _swingCoroutine = StartCoroutine(SwingRoutine(attackType));
        }

        /// <summary>
        /// 진행 중인 스윙을 즉시 중단하고 원점으로 복귀.
        /// 콤보 리셋 / 무기 교체 / 방향 전환 시 호출.
        /// </summary>
        public void CancelSwing()
        {
            _swingTween?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            IsSwinging = false;
            transform.localPosition = _originLocalPosition;
        }

        // ══════════════════════════════════════════════════════
        // 스윙 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 스윙 시퀀스 코루틴.
        ///
        /// [흐름]
        ///   1. 앞으로 뻗기  (swingDuration, Ease.OutQuart)
        ///   2. 히트박스 유지 (hitboxDuration - swingDuration)
        ///   3. 원점 복귀    (returnDuration, Ease.InQuart)
        /// </summary>
        /// <param name="attackType">공격 유형</param>
        private IEnumerator SwingRoutine(AttackType attackType)
        {
            IsSwinging = true;

            Vector3 swingOffset = GetSwingOffset(attackType);
            Vector3 targetPos = _originLocalPosition + swingOffset;

            // ① 앞으로 뻗기
            bool swingDone = false;
            _swingTween = transform.DOLocalMove(targetPos, _keyData.swingDuration)
                .SetEase(Ease.OutQuart)
                .OnComplete(() => swingDone = true);

            yield return new WaitUntil(() => swingDone);

            // ② 히트박스 유지 구간
            float holdTime = Mathf.Max(0f, _keyData.hitboxDuration - _keyData.swingDuration);
            if (holdTime > 0f)
                yield return new WaitForSeconds(holdTime);

            // ③ 원점 복귀
            bool returnDone = false;
            _swingTween = transform.DOLocalMove(_originLocalPosition, _keyData.returnDuration)
                .SetEase(Ease.InQuart)
                .OnComplete(() => returnDone = true);

            yield return new WaitUntil(() => returnDone);

            IsSwinging = false;
        }

        // ══════════════════════════════════════════════════════
        // 보조
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// AttackType 별 스윙 오프셋 계산.
        ///
        /// [지상 콤보]
        ///   FacingDirection × swingDistance → X 방향 앞으로
        ///
        /// [공중 공격]
        ///   Y 음수(아래) + 소량 X 전진
        /// </summary>
        /// <param name="attackType">공격 유형</param>
        /// <returns>원점 기준 로컬 오프셋</returns>
        private Vector3 GetSwingOffset(AttackType attackType)
        {
            float facing = PlayerMovementFacade.Instance?.FacingDirection ?? 1f;

            switch (attackType)
            {
                case AttackType.AirAttack:
                    return new Vector3(
                        facing * _keyData.swingDistance * 0.3f,
                        -_keyData.airSwingDistance,
                        0f);

                default:
                    // Combo1 / Combo2 / Combo3 동일 방향, 동일 거리
                    return new Vector3(facing * _keyData.swingDistance, 0f, 0f);
            }
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            // 현재 원점 위치 표시
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                transform.parent != null
                    ? transform.parent.TransformPoint(_originLocalPosition)
                    : transform.position,
                0.08f);
        }
    }
}