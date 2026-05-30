// ============================================================
// PlayerWeaponMover.cs  v1.4
// Weapon 오브젝트 스윙 이동 전담 컴포넌트
//
// [v1.4 변경 — DOTween Sequence 3단계 스윙 + Z축 회전]
//
//   기존: 단순 DOLocalMove(원점→타격→원점) + DOPunch 임팩트
//   변경: 백스윙(준비) → 타격 → 복귀 3단계 시퀀스
//
//   [Combo1 — 가로 횡베기]
//     백스윙: 후방으로 당김 + Z 역회전
//     타격:   전방 X 전진 + Z 하향 회전 (칼날 아래로)
//     복귀:   원점 + Z 0°
//
//   [Combo2 — 내리찍기]
//     백스윙: 위로 들어올림 + Z 역회전
//     타격:   Y 하향 내리침 + Z 전방 회전
//     복귀:   원점 (OutBounce — 찍힌 느낌)
//
//   [Combo3 — 찌르기 피니셔]
//     히트스탑 0.06초
//     백스윙: 후방으로 크게 당김
//     타격:   X 강한 전진 (InExpo) + PunchScale
//     복귀:   원점 (OutExpo)
//
//   [AirSide — 공중 수평 횡베기]
//     Combo1 과 유사 + 약간 하향 궤적
//
//   [AirDown — 공중 내리찍기]
//     백스윙: 위로 크게 들어올림 + 큰 Z 역회전
//     타격:   Y 강하향 + 큰 Z 회전 아크
//     복귀:   원점
//     카메라 흔들림 Medium
//
//   [AirUp — 공중 상향 퍼올리기]
//     백스윙: 아래로 낮춤 + Z 하향 기울기
//     타격:   Y 상향 퍼올림 + Z 역방향 회전
//     복귀:   원점
//
//   DOTween Sequence.Join() 으로 이동+회전 동시 제어.
//   회전은 모두 localRotation Z축만 사용 (2D 사이드뷰).
//
// [v1.3 변경]
//   콤보별 DOPunch 임팩트 + 히트스탑.
//
// [v1.2 변경]
//   SyncOrigin(dir) 추가.
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
    /// Weapon 오브젝트 스윙 이동 전담 컴포넌트. (v1.4)
    ///
    /// ────────────────────────────────────────────────────
    /// [3단계 시퀀스]
    ///   ① 백스윙 (반대 방향으로 당김 / 0.05~0.07초)
    ///   ② 타격   (큰 이동 + Z축 회전 / 0.08~0.12초)
    ///   ③ 복귀   (원점 + Z=0° / 0.12~0.18초)
    ///
    /// [2D 사이드뷰]
    ///   회전 = Z축만 사용. X/Y 이동으로 방향 표현.
    ///   Z+ 회전 = 칼날 왼쪽 기울기 / Z- = 오른쪽 기울기.
    ///   facing 방향에 따라 부호 자동 반전.
    ///
    /// [CameraShake]
    ///   Combo3, AirDown 타격 시 CameraShake 호출.
    ///   CameraShake.IsEnabled = false 이면 무시.
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

        private KeyDataSO _keyData;
        private Sequence _swingSequence;
        private Coroutine _swingCoroutine;

        /// <summary>
        /// Weapon 로컬 원점 위치.
        /// Awake 캐싱. SyncOrigin 으로 갱신.
        /// </summary>
        private Vector3 _originLocalPosition;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

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
            _swingSequence?.Kill();
            if (Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = 1f;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary> 열쇠 데이터 주입. </summary>
        public void SetKeyData(KeyDataSO keyData) => _keyData = keyData;

        /// <summary>
        /// 방향 전환 시 원점 X 동기화.
        /// ObjectFlipController.OnFlipped() 에서 호출.
        /// </summary>
        public void SyncOrigin(float dir)
        {
            _originLocalPosition = new Vector3(
                Mathf.Abs(_originLocalPosition.x) * dir,
                _originLocalPosition.y,
                _originLocalPosition.z);

            if (!IsSwinging)
            {
                transform.localPosition = _originLocalPosition;
                transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 스윙 실행.
        /// PlayerWeaponAnimator 에서 콤보 이벤트 수신 시 호출.
        /// </summary>
        public void PlaySwing(AttackType attackType)
        {
            if (_keyData == null) return;

            _swingSequence?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            transform.localPosition = _originLocalPosition;
            transform.localRotation = Quaternion.identity;

            _swingCoroutine = StartCoroutine(SwingRoutine(attackType));
        }

        /// <summary>
        /// 스윙 즉시 중단 + 원점 복귀.
        /// </summary>
        public void CancelSwing()
        {
            _swingSequence?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            IsSwinging = false;
            transform.localPosition = _originLocalPosition;
            transform.localRotation = Quaternion.identity;
        }

        // ══════════════════════════════════════════════════════
        // 스윙 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator SwingRoutine(AttackType attackType)
        {
            IsSwinging = true;

            float facing = PlayerMovementFacade.Instance?.FacingDirection ?? 1f;

            // ① Combo3 히트스탑
            if (attackType == AttackType.Combo3 && _hitStopDuration > 0f)
                yield return StartCoroutine(HitStopRoutine(_hitStopDuration));

            // ② 3단계 Sequence 실행
            bool done = false;
            _swingSequence = BuildSwingSequence(attackType, facing);
            _swingSequence.OnComplete(() => done = true);
            _swingSequence.Play();

            yield return new WaitUntil(() => done);

            IsSwinging = false;
        }

        // ══════════════════════════════════════════════════════
        // Sequence 빌더 — 공격 타입별 3단계
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 타입별 DOTween Sequence 생성.
        ///
        /// [공통 구조]
        ///   Append: 백스윙 이동 + Join: Z 역회전
        ///   Append: 타격 이동   + Join: Z 타격 회전
        ///   Append: 복귀        + Join: Z 복귀
        ///   (Combo3) Append: PunchScale (착탄 임팩트)
        ///
        /// [2D 사이드뷰 회전 규칙]
        ///   Z+ = 반시계 = 칼날 왼쪽 기울기
        ///   Z- = 시계   = 칼날 오른쪽 기울기
        ///   facing × rotZ 로 방향에 맞게 자동 부호 적용
        /// </summary>
        private Sequence BuildSwingSequence(AttackType attackType, float facing)
        {
            Sequence seq = DOTween.Sequence();

            // 수치 단축 참조
            float bsDist = _keyData.backswingDistance;
            float bsDur = _keyData.backswingDuration;
            float retDur = _keyData.returnDuration;
            float atk1 = _keyData.combo1AttackDistance;
            float rot1 = _keyData.combo1RotationZ;
            float atk2Y = _keyData.combo2AttackDistanceY;
            float rot2 = _keyData.combo2RotationZ;
            float atk3 = _keyData.combo3AttackDistance;
            float airRot = _keyData.airAttackRotationZ;
            float swDur = _keyData.swingDuration;

            Vector3 origin = _originLocalPosition;

            switch (attackType)
            {
                // ── Combo1 — 가로 횡베기 ──────────────────────────
                case AttackType.Combo1:
                    {
                        Vector3 bsPos = origin + new Vector3(-facing * bsDist, 0.05f, 0f);
                        Vector3 atkPos = origin + new Vector3(facing * atk1, -0.1f, 0f);

                        // 백스윙 — 뒤로 당기며 Z 역회전 (칼날 위로)
                        seq.Append(transform.DOLocalMove(bsPos, bsDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, facing * rot1 * 0.5f), bsDur)
                            .SetEase(Ease.OutQuart));

                        // 타격 — 전방 하향으로 크게 스윙 + Z 하향 회전 (칼날 아래)
                        seq.Append(transform.DOLocalMove(atkPos, swDur)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -facing * rot1), swDur)
                            .SetEase(Ease.InOutCubic));

                        // 히트박스 유지
                        float hold1 = Mathf.Max(0f, _keyData.hitboxDuration - swDur - bsDur);
                        if (hold1 > 0f) seq.AppendInterval(hold1);

                        // 복귀
                        seq.Append(transform.DOLocalMove(origin, retDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, retDur)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── Combo2 — 내리찍기 ──────────────────────────────
                case AttackType.Combo2:
                    {
                        Vector3 bsPos = origin + new Vector3(facing * 0.05f, 0.35f, 0f);
                        Vector3 atkPos = origin + new Vector3(facing * 0.15f, -atk2Y, 0f);

                        // 백스윙 — 위로 들어올림 + Z 역회전
                        seq.Append(transform.DOLocalMove(bsPos, bsDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -facing * rot2 * 0.6f), bsDur)
                            .SetEase(Ease.OutQuart));

                        // 타격 — 하향 내리침 + Z 전방 회전
                        seq.Append(transform.DOLocalMove(atkPos, swDur)
                            .SetEase(Ease.InCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, facing * rot2), swDur)
                            .SetEase(Ease.InCubic));

                        float hold2 = Mathf.Max(0f, _keyData.hitboxDuration - swDur - bsDur);
                        if (hold2 > 0f) seq.AppendInterval(hold2);

                        // 복귀 — OutBounce (찍힌 느낌)
                        seq.Append(transform.DOLocalMove(origin, retDur)
                            .SetEase(Ease.OutBounce));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, retDur)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── Combo3 — 찌르기 피니셔 ────────────────────────
                case AttackType.Combo3:
                    {
                        Vector3 bsPos = origin + new Vector3(-facing * bsDist * 1.3f, 0f, 0f);
                        Vector3 atkPos = origin + new Vector3(facing * atk3, 0f, 0f);

                        // 백스윙 — 뒤로 크게 당김 (회전 없음 — 직선 찌르기)
                        seq.Append(transform.DOLocalMove(bsPos, bsDur)
                            .SetEase(Ease.OutQuart));

                        // 타격 — 강하게 전방 찌르기 (InExpo — 폭발적 가속)
                        seq.Append(transform.DOLocalMove(atkPos, swDur * 0.8f)
                            .SetEase(Ease.InExpo));

                        // 착탄 임팩트 스케일 — 타격과 동시
                        seq.Join(transform.DOPunchScale(
                            Vector3.one * 0.3f, 0.12f, vibrato: 5, elasticity: 0.4f));

                        // 카메라 흔들림
                        seq.InsertCallback(bsDur + swDur * 0.8f,
                            () => CameraShake.Shake(CameraShake.Preset.Heavy));

                        float hold3 = Mathf.Max(0f, _keyData.hitboxDuration - swDur - bsDur);
                        if (hold3 > 0f) seq.AppendInterval(hold3);

                        // 복귀 — OutExpo
                        seq.Append(transform.DOLocalMove(origin, retDur)
                            .SetEase(Ease.OutExpo));
                        break;
                    }

                // ── AirSide — 공중 수평 횡베기 ────────────────────
                case AttackType.AirAttack:
                    {
                        Vector3 bsPos = origin + new Vector3(-facing * bsDist * 0.8f, 0.05f, 0f);
                        Vector3 atkPos = origin + new Vector3(facing * atk1 * 0.8f, -0.1f, 0f);

                        seq.Append(transform.DOLocalMove(bsPos, bsDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, facing * rot1 * 0.4f), bsDur)
                            .SetEase(Ease.OutQuart));

                        seq.Append(transform.DOLocalMove(atkPos, swDur * 1.1f)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -facing * airRot * 0.8f), swDur * 1.1f)
                            .SetEase(Ease.InOutCubic));

                        float holdAS = Mathf.Max(0f, _keyData.hitboxDuration - swDur - bsDur);
                        if (holdAS > 0f) seq.AppendInterval(holdAS);

                        seq.Append(transform.DOLocalMove(origin, retDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, retDur)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── AirDown — 공중 내리찍기 ───────────────────────
                case AttackType.AirAttackDown:
                    {
                        Vector3 bsPos = origin + new Vector3(facing * 0.05f, 0.4f, 0f);
                        Vector3 atkPos = origin + new Vector3(facing * 0.1f, -atk2Y * 1.3f, 0f);

                        // 백스윙 — 위로 크게 들어올림 + 큰 Z 역회전
                        seq.Append(transform.DOLocalMove(bsPos, bsDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -facing * airRot * 0.8f), bsDur)
                            .SetEase(Ease.OutQuart));

                        // 타격 — 강하게 Y 하향 + 큰 아크 회전
                        seq.Append(transform.DOLocalMove(atkPos, swDur)
                            .SetEase(Ease.InCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, facing * airRot), swDur)
                            .SetEase(Ease.InCubic));

                        // 카메라 흔들림
                        seq.InsertCallback(bsDur + swDur,
                            () => CameraShake.Shake(CameraShake.Preset.Medium));

                        float holdAD = Mathf.Max(0f, _keyData.hitboxDuration - swDur - bsDur);
                        if (holdAD > 0f) seq.AppendInterval(holdAD);

                        seq.Append(transform.DOLocalMove(origin, retDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, retDur)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── AirUp — 공중 상향 퍼올리기 ────────────────────
                case AttackType.AirAttackUp:
                    {
                        Vector3 bsPos = origin + new Vector3(facing * 0.05f, -0.25f, 0f);
                        Vector3 atkPos = origin + new Vector3(facing * 0.15f, atk2Y * 1.2f, 0f);

                        // 백스윙 — 아래로 낮춤 + Z 하향 기울기
                        seq.Append(transform.DOLocalMove(bsPos, bsDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, facing * airRot * 0.6f), bsDur)
                            .SetEase(Ease.OutQuart));

                        // 타격 — Y 상향 퍼올림 + Z 역방향 큰 회전
                        seq.Append(transform.DOLocalMove(atkPos, swDur * 1.1f)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -facing * airRot), swDur * 1.1f)
                            .SetEase(Ease.InOutCubic));

                        float holdAU = Mathf.Max(0f, _keyData.hitboxDuration - swDur - bsDur);
                        if (holdAU > 0f) seq.AppendInterval(holdAU);

                        seq.Append(transform.DOLocalMove(origin, retDur)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, retDur)
                            .SetEase(Ease.OutQuart));
                        break;
                    }
            }

            return seq;
        }

        // ══════════════════════════════════════════════════════
        // 히트스탑
        // ══════════════════════════════════════════════════════

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = IsSwinging ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.08f);

            // 원점 표시
            if (transform.parent != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(
                    transform.parent.TransformPoint(_originLocalPosition),
                    0.05f);
            }
        }
#endif
    }
}