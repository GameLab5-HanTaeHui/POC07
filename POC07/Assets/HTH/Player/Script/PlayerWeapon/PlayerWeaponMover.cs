// ============================================================
// WeaponMover.cs  v1.0
// Weapon 오브젝트 스윙 이동 전담 컴포넌트
//
// [역할]
//   공격 시 Weapon 오브젝트(로컬 Transform)를
//   DOTween 으로 앞으로 뻗었다 원점으로 복귀시킴.
//   히트박스가 Weapon 의 자식이므로 자동으로 같이 이동.
//
// [이동 흐름]
//   PlaySwing() 호출
//     1. 진행 중인 Tween 즉시 Kill + 원점 복귀
//     2. 앞으로 뻗기  : localPosition → offset  (Ease.OutQuart, swingDuration)
//     3. 히트박스 구간 유지 (hitboxDuration)
//     4. 원점 복귀    : localPosition → Vector3.zero (Ease.InQuart, returnDuration)
//
// [좌우 방향 처리]
//   PlayerMovementFacade.FacingDirection 으로 X 오프셋 부호 결정.
//   공중 공격(AirAttack)은 Y 음수 방향으로 이동.
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
    /// Weapon 오브젝트 스윙 이동 전담 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [WeaponAnimator 에서의 호출]
    ///   // 지상 콤보
    ///   _weaponMover.PlaySwing(AttackType.Combo1);
    ///
    ///   // 공중 공격
    ///   _weaponMover.PlaySwing(AttackType.AirAttack);
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
        /// 새 공격 시작 시 Kill() 후 재시작.
        /// </summary>
        private Tween _swingTween;

        /// <summary>
        /// 진행 중인 스윙 시퀀스 코루틴.
        /// </summary>
        private Coroutine _swingCoroutine;

        /// <summary>
        /// Weapon 오브젝트의 로컬 원점 위치.
        /// Awake 에서 캐싱. 복귀 목표 위치.
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

        private void Awake()
        {
            // 씬 배치 시 초기 로컬 위치를 원점으로 캐싱
            _originLocalPosition = transform.localPosition;
        }

        private void OnDestroy()
        {
            _swingTween?.Kill();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 열쇠 데이터 주입.
        /// WeaponKeyController.ActivateWeapon() 에서 호출.
        /// </summary>
        public void SetKeyData(KeyDataSO keyData)
        {
            _keyData = keyData;
        }

        /// <summary>
        /// 스윙 이동 실행.
        /// WeaponAnimator 에서 콤보 이벤트 수신 시 호출.
        ///
        /// [AttackType 별 이동 방향]
        ///   Combo1~3    : FacingDirection(X) 앞으로 swingDistance
        ///   AirAttack   : 아래(Y 음수) 로 airSwingDistance
        /// </summary>
        /// <param name="attackType">공격 유형 — 이동 방향 결정</param>
        public void PlaySwing(AttackType attackType)
        {
            if (_keyData == null) return;

            // 진행 중인 스윙 즉시 중단
            _swingTween?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            // 즉시 원점으로 스냅 후 새 스윙 시작
            transform.localPosition = _originLocalPosition;
            _swingCoroutine = StartCoroutine(SwingRoutine(attackType));
        }

        /// <summary>
        /// 진행 중인 스윙을 즉시 중단하고 원점으로 복귀.
        /// 콤보 리셋 / 무기 교체 시 호출.
        /// </summary>
        public void CancelSwing()
        {
            _swingTween?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            IsSwinging = false;

            // 원점으로 즉시 복귀
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
        ///   2. 히트박스 유지 (hitboxDuration — 뻗은 상태 유지)
        ///   3. 원점 복귀    (returnDuration, Ease.InQuart)
        /// </summary>
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

            // ② 히트박스 유지 구간 (hitboxDuration 동안 뻗은 상태)
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
        ///   Y 음수 방향으로 airSwingDistance (내리찍기)
        ///   + 소량 X 전진으로 입체감 추가
        /// </summary>
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
                    // Combo1 / Combo2 / Combo3 모두 동일 거리
                    return new Vector3(facing * _keyData.swingDistance, 0f, 0f);
            }
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            // 원점 위치 표시
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                transform.parent != null
                    ? transform.parent.TransformPoint(_originLocalPosition)
                    : transform.position,
                0.08f);
        }
    }
}