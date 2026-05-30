// ============================================================
// PlayerWeaponMover.cs  v1.4
// Weapon 오브젝트 스윙 이동 전담 컴포넌트
//
// [v1.4 변경 — 절대 좌표 3단계 Sequence + 히트박스 타이밍 제어]
//
//   ① 이동 방식 변경
//       기존: _originLocalPosition 기준 ±미세 이동 (깔짝)
//       변경: Player 기준 절대 로컬 좌표 — 신체 전체를 가로지르는 큰 궤적
//
//       [Combo1 횡베기]
//         시작: 후방 어깨 (-1.0, 0.3)
//         타격: 전방 허리 (+1.2, 0.0)
//         복귀: 원점
//
//       [Combo2 내리찍기]
//         시작: 머리 위  (0.3, 1.5)
//         타격: 발 아래  (0.5, -1.0)
//         복귀: 원점
//
//       [Combo3 찌르기]
//         히트스탑
//         시작: 후방    (-0.6, 0.2)
//         타격: 전방 끝  (+1.5, 0.0)
//         복귀: 원점
//
//       [AirSide]  후방위 → 전방하향  큰 수평 호
//       [AirDown]  머리위 → 발아래    최대 높이 차
//       [AirUp]    발아래 → 머리위    역방향 퍼올리기
//
//   ② 히트박스 타이밍 제어 (RustyKeyWeapon 에서 이전)
//       DOTween Sequence.InsertCallback() 으로 타격 직전 EnableHitbox()
//       타격 완료 후 DisableAllHitboxes()
//       → 무기가 타격 위치 도달 시점에만 히트박스 활성
//
//   ③ Z축 회전 — 2D 사이드뷰 칼날 호 표현
//       facing 방향에 따라 부호 자동 반전
//
//   ④ 카메라 흔들림 — Combo3/AirDown 타격 시 CameraShake
//
// [v1.3 변경]
//   콤보별 DOPunch + 히트스탑
//
// [v1.2 변경]
//   SyncOrigin(dir) 추가
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
    /// [이동 방식]
    ///   Player 기준 절대 로컬 좌표로 Weapon 이동.
    ///   신체 전체를 가로지르는 큰 궤적 표현.
    ///   facing 방향에 따라 X 좌표 부호 자동 반전.
    ///
    /// [히트박스 타이밍]
    ///   백스윙 완료 시점 → EnableHitbox()
    ///   타격 이동 완료 시점 → DisableAllHitboxes()
    ///   → 무기가 타격 위치 도달할 때만 히트박스 활성
    ///
    /// [3단계 Sequence]
    ///   ① 시작위치로 빠르게 이동 (백스윙/준비 0.07~0.08초)
    ///   ② 타격위치로 이동 + Z회전 (0.10~0.12초)
    ///   ③ 원점 복귀 + Z=0° (0.12~0.18초)
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerWeaponMover : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트스탑 설정 ──────────────────────")]

        [Tooltip("Combo3 히트스탑 지속 시간 (초). 0 = 비활성.")]
        [Range(0f, 0.2f)]
        [SerializeField] private float _hitStopDuration = 0.06f;

        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        private KeyDataSO _keyData;
        private PlayerWeaponHitboxManager _hitboxManager;
        private Sequence _swingSequence;
        private Coroutine _swingCoroutine;
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
        /// 히트박스 매니저 주입.
        /// PlayerWeaponController.ActivateWeapon() 에서 호출.
        /// </summary>
        public void SetHitboxManager(PlayerWeaponHitboxManager manager)
            => _hitboxManager = manager;

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
        /// <param name="attackType">공격 타입</param>
        /// <param name="hitboxIndex">활성화할 히트박스 인덱스</param>
        /// <param name="damageInfo">피해 정보</param>
        public void PlaySwing(AttackType attackType, int hitboxIndex, DamageInfo damageInfo)
        {
            if (_keyData == null) return;

            _swingSequence?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            _hitboxManager?.DisableAllHitboxes();

            transform.localPosition = _originLocalPosition;
            transform.localRotation = Quaternion.identity;

            _swingCoroutine = StartCoroutine(
                SwingRoutine(attackType, hitboxIndex, damageInfo));
        }

        /// <summary> 스윙 즉시 중단 + 원점 복귀. </summary>
        public void CancelSwing()
        {
            _swingSequence?.Kill();
            if (_swingCoroutine != null) StopCoroutine(_swingCoroutine);

            _hitboxManager?.DisableAllHitboxes();
            IsSwinging = false;
            transform.localPosition = _originLocalPosition;
            transform.localRotation = Quaternion.identity;
        }

        // ══════════════════════════════════════════════════════
        // 스윙 코루틴
        // ══════════════════════════════════════════════════════

        private IEnumerator SwingRoutine(
            AttackType attackType,
            int hitboxIndex,
            DamageInfo damageInfo)
        {
            IsSwinging = true;

            float facing = PlayerMovementFacade.Instance?.FacingDirection ?? 1f;

            // Combo3 히트스탑
            if (attackType == AttackType.Combo3 && _hitStopDuration > 0f)
                yield return StartCoroutine(HitStopRoutine(_hitStopDuration));

            // Sequence 빌드 + 실행
            bool done = false;
            _swingSequence = BuildSwingSequence(
                attackType, facing, hitboxIndex, damageInfo);
            _swingSequence.OnComplete(() => done = true);
            _swingSequence.Play();

            yield return new WaitUntil(() => done);

            IsSwinging = false;
        }

        // ══════════════════════════════════════════════════════
        // Sequence 빌더 — 절대 좌표 3단계
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 타입별 DOTween Sequence 생성.
        ///
        /// [좌표 기준]
        ///   Player 의 로컬 좌표. Weapon 은 Player 의 자식.
        ///   facing = +1 (오른쪽) 기준. facing = -1 이면 X 부호 반전.
        ///
        /// [히트박스 타이밍]
        ///   InsertCallback(백스윙 시간) → EnableHitbox()    타격 직전 활성
        ///   InsertCallback(백+타격 시간) → DisableAllHitboxes() 타격 완료 후 비활성
        ///
        /// [단축 변수]
        ///   f  = facing 부호
        ///   bD = backswingDuration
        ///   sD = swingDuration (타격 이동 시간)
        ///   rD = returnDuration
        /// </summary>
        private Sequence BuildSwingSequence(
            AttackType attackType,
            float facing,
            int hitboxIndex,
            DamageInfo damageInfo)
        {
            Sequence seq = DOTween.Sequence();

            float f = facing;
            float bD = _keyData.backswingDuration;
            float sD = _keyData.swingDuration;
            float rD = _keyData.returnDuration;
            float rot1 = _keyData.combo1RotationZ;
            float rot2 = _keyData.combo2RotationZ;
            float airRot = _keyData.airAttackRotationZ;

            Vector3 origin = _originLocalPosition;

            // 히트박스 콜백 공통
            float hitEnableTime = 0f; // 백스윙 완료 시점
            float hitDisableTime = 0f; // 타격 완료 시점

            switch (attackType)
            {
                // ── Combo1 — 가로 횡베기 ──────────────────────────
                case AttackType.Combo1:
                    {
                        // 시작: 후방 어깨 높이 / 타격: 전방 허리 높이
                        Vector3 startPos = new Vector3(f * -_keyData.backswingDistance * 2f, 0.3f, 0f);
                        Vector3 attackPos = new Vector3(f * _keyData.combo1AttackDistance, 0.0f, 0f);

                        // ① 시작위치로 빠르게 이동 + Z 역회전
                        seq.Append(transform.DOLocalMove(startPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, f * rot1 * 0.5f), bD)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + sD;

                        // ② 타격: 전방 허리로 크게 스윙 + Z 하향 회전
                        seq.Append(transform.DOLocalMove(attackPos, sD)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -f * rot1), sD)
                            .SetEase(Ease.InOutCubic));

                        // ③ 복귀
                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, rD)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── Combo2 — 내리찍기 ──────────────────────────────
                case AttackType.Combo2:
                    {
                        // 시작: 머리 위 / 타격: 발 아래
                        Vector3 startPos = new Vector3(f * 0.3f, 1.5f, 0f);
                        Vector3 attackPos = new Vector3(f * 0.5f, -_keyData.combo2AttackDistanceY, 0f);

                        // ① 머리 위로 들어올림 + Z 역회전 (칼날 뒤로 젖힘)
                        seq.Append(transform.DOLocalMove(startPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -f * rot2 * 0.8f), bD)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + sD;

                        // ② 발 아래로 강하게 내리침 + Z 전방 회전
                        seq.Append(transform.DOLocalMove(attackPos, sD)
                            .SetEase(Ease.InCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, f * rot2), sD)
                            .SetEase(Ease.InCubic));

                        // ③ 복귀 (OutBounce — 찍힌 느낌)
                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutBounce));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, rD)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── Combo3 — 찌르기 피니셔 ────────────────────────
                case AttackType.Combo3:
                    {
                        // 시작: 후방 당김 / 타격: 전방 최대 사거리
                        Vector3 startPos = new Vector3(f * -0.6f, 0.2f, 0f);
                        Vector3 attackPos = new Vector3(f * _keyData.combo3AttackDistance, 0.0f, 0f);

                        // ① 후방으로 크게 당김 (회전 없음 — 직선 찌르기)
                        seq.Append(transform.DOLocalMove(startPos, bD)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + sD * 0.8f;

                        // ② 강하게 전방 찌르기 (InExpo — 폭발적 가속)
                        seq.Append(transform.DOLocalMove(attackPos, sD * 0.8f)
                            .SetEase(Ease.InExpo));
                        seq.Join(transform.DOPunchScale(
                            Vector3.one * 0.3f, 0.12f, vibrato: 5, elasticity: 0.4f));

                        // 카메라 흔들림
                        seq.InsertCallback(bD + sD * 0.8f,
                            () => CameraShake.Shake(CameraShake.Preset.Heavy));

                        // ③ 복귀 (OutExpo)
                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutExpo));
                        break;
                    }

                // ── AirSide — 공중 수평 횡베기 ────────────────────
                case AttackType.AirAttack:
                    {
                        Vector3 startPos = new Vector3(f * -0.8f, 0.4f, 0f);
                        Vector3 attackPos = new Vector3(f * 1.1f, -0.15f, 0f);

                        seq.Append(transform.DOLocalMove(startPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, f * rot1 * 0.4f), bD)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + sD * 1.1f;

                        seq.Append(transform.DOLocalMove(attackPos, sD * 1.1f)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -f * airRot * 0.8f), sD * 1.1f)
                            .SetEase(Ease.InOutCubic));

                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, rD)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── AirDown — 공중 내리찍기 ───────────────────────
                case AttackType.AirAttackDown:
                    {
                        // 시작: 머리 위 최대 / 타격: 발 아래 최대
                        Vector3 startPos = new Vector3(f * 0.2f, 1.8f, 0f);
                        Vector3 attackPos = new Vector3(f * 0.4f, -1.5f, 0f);

                        // ① 머리 위로 크게 들어올림 + 큰 Z 역회전
                        seq.Append(transform.DOLocalMove(startPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -f * airRot * 0.8f), bD)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + sD;

                        // ② 발 아래로 강하게 내리침 + 큰 아크 회전
                        seq.Append(transform.DOLocalMove(attackPos, sD)
                            .SetEase(Ease.InCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, f * airRot), sD)
                            .SetEase(Ease.InCubic));

                        // 카메라 흔들림
                        seq.InsertCallback(bD + sD,
                            () => CameraShake.Shake(CameraShake.Preset.Medium));

                        // ③ 복귀
                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, rD)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── AirUp — 공중 상향 퍼올리기 ────────────────────
                case AttackType.AirAttackUp:
                    {
                        // 시작: 발 아래 / 타격: 머리 위
                        Vector3 startPos = new Vector3(f * 0.3f, -1.2f, 0f);
                        Vector3 attackPos = new Vector3(f * -0.2f, 1.8f, 0f);

                        // ① 발 아래로 낮춤 + Z 하향 기울기
                        seq.Append(transform.DOLocalMove(startPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, f * airRot * 0.6f), bD)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + sD * 1.1f;

                        // ② 머리 위로 크게 퍼올림 + Z 역방향 큰 회전
                        seq.Append(transform.DOLocalMove(attackPos, sD * 1.1f)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, -f * airRot), sD * 1.1f)
                            .SetEase(Ease.InOutCubic));

                        // ③ 복귀
                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(Vector3.zero, rD)
                            .SetEase(Ease.OutQuart));
                        break;
                    }
            }

            // ── 히트박스 타이밍 InsertCallback ────────────────────
            if (_hitboxManager != null)
            {
                // 백스윙 완료 시점 → EnableHitbox
                seq.InsertCallback(hitEnableTime,
                    () => _hitboxManager.EnableHitbox(hitboxIndex, damageInfo));

                // 타격 완료 시점 → DisableAllHitboxes
                seq.InsertCallback(hitDisableTime,
                    () => _hitboxManager.DisableAllHitboxes());
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
        }
#endif
    }
}