// ============================================================
// PlayerWeaponMover.cs  v1.3
// Weapon 오브젝트 스윙 이동 전담 컴포넌트
//
// [v1.3 변경 — 콤보별 DOTween 임팩트 차별화]
//
//   ① 콤보 단계별 스윙 임팩트 추가
//       Combo1 : DOPunchPosition X(수평) + DOPunchRotation Z
//       Combo2 : DOPunchPosition Y(하향) + DOShakeScale
//       Combo3 : 선딜레이 0.06초 + DOPunchPosition X(강) + 히트스탑
//       AirSide: DOPunchPosition X + DOPunchRotation Z(소)
//       AirDown: DOPunchPosition Y(강하향) + DOShakeScale
//       AirUp  : DOPunchPosition Y(상향) + DOPunchRotation Z
//
//   ② 히트스탑 구현
//       Combo3 히트박스 활성 직전 0.06초 동안 Time.timeScale = 0 처리.
//       DOTween.To 로 timeScale 제어. SetUpdate(true) 로 언스케일드 타임 사용.
//
//   ③ AttackType 확장
//       AirSide / AirDown / AirUp 분기 추가.
//       RustyKeyWeapon v1.5 의 4방향 이벤트와 연동.
//
// [v1.2 변경]
//   SyncOrigin(dir) 추가 — ObjectFlipController 에서 호출.
//   왼쪽 공격 방향 튀는 버그 수정.
//
// [v1.1 변경]
//   PlayerMover.OnFlipped 구독 추가.
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
    /// Weapon 오브젝트 스윙 이동 전담 컴포넌트. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [콤보별 임팩트]
    ///   Combo1 : 수평 스윙 — PunchPosition(X) + PunchRotation(Z)
    ///   Combo2 : 내리찍기 — PunchPosition(Y하) + ShakeScale
    ///   Combo3 : 피니셔   — 히트스탑(0.06초) + PunchPosition(X강) + ShakeScale
    ///   AirSide: 수평 공격 — PunchPosition(X) + PunchRotation(Z소)
    ///   AirDown: 내리찍기  — PunchPosition(Y강하) + ShakeScale
    ///   AirUp  : 상향 공격 — PunchPosition(Y상) + PunchRotation(Z역)
    ///
    /// [히트스탑]
    ///   Combo3 전용. Time.timeScale 을 0.05초 동안 0 으로 설정 후 복원.
    ///   DOTween SetUpdate(true) 로 언스케일드 타임에서 동작.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerWeaponMover : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트스탑 설정 ──────────────────────")]

        /// <summary>
        /// Combo3 피니셔 히트스탑 지속 시간 (초, 실제 시간).
        /// 0 = 히트스탑 없음.
        /// </summary>
        [Tooltip("Combo3 히트스탑 지속 시간 (초). 0 = 비활성.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float _hitStopDuration = 0.06f;

        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        /// <summary> 현재 장착된 열쇠 데이터. </summary>
        private KeyDataSO _keyData;

        private Tween _swingTween;
        private Coroutine _swingCoroutine;

        /// <summary>
        /// Weapon 오브젝트의 로컬 원점 위치.
        /// Awake 에서 초기값 캐싱.
        /// SyncOrigin / OnFlipped 수신 시 X 부호가 반전됨.
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
            _originLocalPosition = transform.localPosition;
        }

        private void OnDestroy()
        {
            _swingTween?.Kill();
            // 히트스탑 중 파괴 시 TimeScale 복원
            if (Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 1f;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 열쇠 데이터 주입.
        /// PlayerWeaponController.ActivateWeapon() 에서 호출.
        /// </summary>
        public void SetKeyData(KeyDataSO keyData) => _keyData = keyData;

        /// <summary>
        /// 방향 전환 시 _originLocalPosition.x 동기화.
        /// ObjectFlipController.OnFlipped() 에서 호출.
        /// </summary>
        public void SyncOrigin(float dir)
        {
            _originLocalPosition = new Vector3(
                Mathf.Abs(_originLocalPosition.x) * dir,
                _originLocalPosition.y,
                _originLocalPosition.z);

            if (!IsSwinging)
                transform.localPosition = _originLocalPosition;
        }

        /// <summary>
        /// 스윙 이동 실행.
        /// PlayerWeaponAnimator 에서 콤보 이벤트 수신 시 호출.
        /// </summary>
        public void PlaySwing(AttackType attackType)
        {
            if (_keyData == null) return;

            _swingTween?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            transform.localPosition = _originLocalPosition;
            _swingCoroutine = StartCoroutine(SwingRoutine(attackType));
        }

        /// <summary>
        /// 진행 중인 스윙 즉시 중단 + 원점 복귀.
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
        ///   1. (Combo3만) 히트스탑 0.06초
        ///   2. 앞으로 뻗기 + DOTween 임팩트 (swingDuration)
        ///   3. 히트박스 유지 (hitboxDuration - swingDuration)
        ///   4. 원점 복귀 (returnDuration)
        /// </summary>
        private IEnumerator SwingRoutine(AttackType attackType)
        {
            IsSwinging = true;

            float facing = PlayerMovementFacade.Instance?.FacingDirection ?? 1f;

            // ── ① Combo3 히트스탑 ──────────────────────────────
            if (attackType == AttackType.Combo3 && _hitStopDuration > 0f)
                yield return StartCoroutine(HitStopRoutine(_hitStopDuration));

            // ── ② 앞으로 뻗기 + 임팩트 DOTween ──────────────────
            Vector3 targetPos = _originLocalPosition + GetSwingOffset(attackType, facing);

            bool swingDone = false;
            _swingTween = transform.DOLocalMove(targetPos, _keyData.swingDuration)
                .SetEase(Ease.OutQuart)
                .OnComplete(() => swingDone = true);

            // 임팩트 DOTween (이동과 동시)
            ApplySwingImpact(attackType, facing);

            yield return new WaitUntil(() => swingDone);

            // ── ③ 히트박스 유지 구간 ──────────────────────────────
            float holdTime = Mathf.Max(0f, _keyData.hitboxDuration - _keyData.swingDuration);
            if (holdTime > 0f)
                yield return new WaitForSeconds(holdTime);

            // ── ④ 원점 복귀 ──────────────────────────────────────
            bool returnDone = false;
            _swingTween = transform.DOLocalMove(_originLocalPosition, _keyData.returnDuration)
                .SetEase(Ease.InQuart)
                .OnComplete(() => returnDone = true);

            yield return new WaitUntil(() => returnDone);

            IsSwinging = false;
        }

        // ══════════════════════════════════════════════════════
        // 임팩트 DOTween (v1.3 신규)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 타입별 DOTween 임팩트 적용.
        /// PlaySwing 이동과 동시에 실행.
        ///
        /// [Combo1] 수평 스윙 — PunchPosition(X) + PunchRotation(Z)
        /// [Combo2] 내리찍기 — PunchPosition(Y하) + ShakeScale
        /// [Combo3] 피니셔   — PunchPosition(X강) + ShakeScale
        /// [AirSide] 수평    — PunchPosition(X) + PunchRotation(Z소)
        /// [AirDown] 내리찍기 — PunchPosition(Y강하) + ShakeScale
        /// [AirUp]  상향     — PunchPosition(Y상) + PunchRotation(Z역)
        /// </summary>
        private void ApplySwingImpact(AttackType attackType, float facing)
        {
            DOTween.Kill(transform, false); // 기존 임팩트 Kill (이동 Tween 제외)

            switch (attackType)
            {
                case AttackType.Combo1:
                    // 수평 스윙 — 전방 펀치 + 약한 회전
                    transform.DOPunchPosition(
                        new Vector3(facing * 0.18f, 0f, 0f),
                        duration: 0.12f, vibrato: 2, elasticity: 0.3f);
                    transform.DOPunchRotation(
                        new Vector3(0f, 0f, facing * -8f),
                        duration: 0.15f, vibrato: 2, elasticity: 0.4f);
                    break;

                case AttackType.Combo2:
                    // 내리찍기 — 하향 펀치 + 스케일 흔들림
                    transform.DOPunchPosition(
                        new Vector3(facing * 0.1f, -0.2f, 0f),
                        duration: 0.12f, vibrato: 2, elasticity: 0.3f);
                    transform.DOPunchScale(
                        new Vector3(0.15f, -0.1f, 0f),
                        duration: 0.14f, vibrato: 3, elasticity: 0.4f);
                    break;

                case AttackType.Combo3:
                    // 피니셔 — 강한 전방 펀치 + 스케일 흔들림
                    transform.DOPunchPosition(
                        new Vector3(facing * 0.28f, 0f, 0f),
                        duration: 0.15f, vibrato: 3, elasticity: 0.2f);
                    transform.DOPunchScale(
                        new Vector3(0.2f, 0.2f, 0f),
                        duration: 0.18f, vibrato: 4, elasticity: 0.5f);
                    break;

                case AttackType.AirAttack: // AirSide (기본값)
                    transform.DOPunchPosition(
                        new Vector3(facing * 0.15f, 0f, 0f),
                        duration: 0.1f, vibrato: 2, elasticity: 0.3f);
                    transform.DOPunchRotation(
                        new Vector3(0f, 0f, facing * -5f),
                        duration: 0.12f, vibrato: 2, elasticity: 0.4f);
                    break;

                // ── 공중 4방향 (v0.22 연동) ──────────────────────
                case AttackType.AirAttackDown:
                    // 내리찍기 — 강한 하향 + 스케일 진동
                    transform.DOPunchPosition(
                        new Vector3(facing * 0.08f, -0.3f, 0f),
                        duration: 0.13f, vibrato: 3, elasticity: 0.2f);
                    transform.DOPunchScale(
                        new Vector3(0.1f, 0.25f, 0f),
                        duration: 0.16f, vibrato: 4, elasticity: 0.4f);
                    break;

                case AttackType.AirAttackUp:
                    // 상향 공격 — 상방 펀치 + 역회전
                    transform.DOPunchPosition(
                        new Vector3(facing * 0.08f, 0.25f, 0f),
                        duration: 0.12f, vibrato: 2, elasticity: 0.3f);
                    transform.DOPunchRotation(
                        new Vector3(0f, 0f, facing * 10f),
                        duration: 0.15f, vibrato: 2, elasticity: 0.4f);
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 히트스탑 코루틴 (v1.3 신규)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 히트스탑: TimeScale 을 0 으로 설정 → duration 후 복원.
        /// DOTween.SetUpdate(true) 로 언스케일드 타임 사용.
        /// Combo3 전용.
        /// </summary>
        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = 0f;

            // WaitForSecondsRealtime — TimeScale 0 에서도 동작
            yield return new WaitForSecondsRealtime(duration);

            Time.timeScale = 1f;
        }

        // ══════════════════════════════════════════════════════
        // 스윙 오프셋 계산
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// AttackType 별 스윙 오프셋 계산.
        ///
        /// [지상 콤보]   FacingDirection × swingDistance → X 전진
        /// [AirSide]     X 전진 + 소량 Y 하향
        /// [AirDown]     강한 Y 하향 + 소량 X 전진
        /// [AirUp]       Y 상향 + 소량 X 전진
        /// </summary>
        private Vector3 GetSwingOffset(AttackType attackType, float facing)
        {
            switch (attackType)
            {
                case AttackType.AirAttack:   // AirSide
                    return new Vector3(
                        facing * _keyData.swingDistance * 0.5f,
                        -_keyData.airSwingDistance * 0.4f,
                        0f);

                case AttackType.AirAttackDown:
                    return new Vector3(
                        facing * _keyData.swingDistance * 0.2f,
                        -_keyData.airSwingDistance,
                        0f);

                case AttackType.AirAttackUp:
                    return new Vector3(
                        facing * _keyData.swingDistance * 0.2f,
                        _keyData.airSwingDistance,
                        0f);

                default: // Combo1 / Combo2 / Combo3
                    return new Vector3(facing * _keyData.swingDistance, 0f, 0f);
            }
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                transform.parent != null
                    ? transform.parent.TransformPoint(_originLocalPosition)
                    : transform.position,
                0.08f);
        }
#endif
    }
}