// ============================================================
// PlayerWeaponMover.cs  v1.5
// Weapon 오브젝트 스윙 이동 전담 컴포넌트
//
// [v1.5 변경 — Pivot/원점 기반 절대 좌표 재설계]
//
//   Weapon 원점: localPosition = (1, 0, 0) — 플레이어 오른손 위치
//   Key_0 Pivot: x=0.25 (손잡이), 열쇠 날이 +X 방향으로 뻗음
//
//   이전 v1.4 의 문제:
//     백스윙/타격 좌표가 Weapon 실제 위치(1,0,0) 를 고려하지 않은
//     절반은 절대/절반은 상대 혼용 구조 → 의도한 자세가 나오지 않음
//
//   v1.5 변경:
//     KeyDataSO 에 콤보별 백스윙/타격 위치를 Vector2 로 직접 지정
//     모든 좌표 = Player 로컬 기준 절대값 (오른쪽 facing 기준)
//     facing = -1 일 때 X 좌표만 부호 반전
//     Z회전도 KeyDataSO 에서 직접 지정 (백스윙/타격 각각)
//
//   [좌표 직관 규칙]
//     (1, 0) = 플레이어 오른손 (Weapon 원점)
//     (1, 1.5) = 플레이어 머리 위
//     (1, -1.5) = 플레이어 발 아래
//     (2.2, 0) = 전방 최대 사거리 (찌르기 끝)
//     (0.2, 0) = 손목 수축 위치
//
//   [Z회전 직관 규칙]
//     0°   = 열쇠 날이 오른쪽 수평 (기본 자세)
//     +90° = 열쇠 날이 위를 향함
//     -90° = 열쇠 날이 아래를 향함
//     +30° = 약간 날이 위 (횡베기 준비)
//     -40° = 날이 아래로 내려오며 베기
//
// [v1.4 변경]
//   절대 좌표 3단계 Sequence + 히트박스 InsertCallback
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
    /// Weapon 오브젝트 스윙 이동 전담 컴포넌트. (v1.5)
    ///
    /// ────────────────────────────────────────────────────
    /// [동작 방식]
    ///   1. PlaySwing() 호출 시 Weapon 을 현재 원점으로 스냅
    ///   2. 백스윙 위치로 빠르게 이동 + Z회전 시작
    ///   3. 타격 위치로 이동 + Z회전 완료 → 히트박스 활성
    ///   4. 복귀: 원점 + Z=0° → 히트박스 비활성
    ///
    /// [좌표 기준]
    ///   Player 로컬 절대 좌표. facing=+1 기준.
    ///   facing=-1 이면 X 좌표만 부호 반전, Y 그대로.
    ///   Weapon 원점 = (1, 0, 0).
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerWeaponMover : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트스탑 설정 ──────────────────────")]

        [Tooltip("Combo3 히트스탑 지속 시간 (초, 실제 시간). 0 = 비활성.")]
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

        /// <summary> 히트박스 매니저 주입. </summary>
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

            bool done = false;
            _swingSequence = BuildSwingSequence(attackType, facing, hitboxIndex, damageInfo);
            _swingSequence.OnComplete(() => done = true);
            _swingSequence.Play();

            yield return new WaitUntil(() => done);

            IsSwinging = false;
        }

        // ══════════════════════════════════════════════════════
        // Sequence 빌더
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 타입별 DOTween Sequence.
        ///
        /// [좌표 적용 규칙]
        ///   KeyDataSO 의 Vector2 위치값은 오른쪽(facing=+1) 기준.
        ///   facing=-1 일 때 FlipX(pos) 로 X 부호만 반전.
        ///   Y 는 항상 그대로.
        ///
        /// [Z회전 적용 규칙]
        ///   facing=+1: 그대로 적용
        ///   facing=-1: 부호 반전 (대칭)
        ///
        /// [히트박스 타이밍]
        ///   InsertCallback(백스윙 완료) → EnableHitbox
        ///   InsertCallback(타격 완료)   → DisableAllHitboxes
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
            float aD = _keyData.attackDuration;
            float rD = _keyData.returnDuration;

            Vector3 origin = _originLocalPosition;

            float hitEnableTime = 0f;
            float hitDisableTime = 0f;

            switch (attackType)
            {
                // ── Combo1 — 가로 횡베기 ──────────────────────
                case AttackType.Combo1:
                    {
                        Vector3 backPos = FlipX(_keyData.combo1BackPos, f);
                        Vector3 attackPos = FlipX(_keyData.combo1AttackPos, f);
                        float rotBack = f * _keyData.combo1RotBack;
                        float rotAtk = f * _keyData.combo1RotAtk;

                        seq.Append(transform.DOLocalMove(backPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotBack), bD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + aD;

                        seq.Append(transform.DOLocalMove(attackPos, aD)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotAtk), aD, RotateMode.Fast)
                            .SetEase(Ease.InOutCubic));

                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            Vector3.zero, rD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── Combo2 — 내리찍기 ──────────────────────────
                case AttackType.Combo2:
                    {
                        Vector3 backPos = FlipX(_keyData.combo2BackPos, f);
                        Vector3 attackPos = FlipX(_keyData.combo2AttackPos, f);
                        float rotBack = f * _keyData.combo2RotBack;
                        float rotAtk = f * _keyData.combo2RotAtk;

                        seq.Append(transform.DOLocalMove(backPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotBack), bD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + aD;

                        seq.Append(transform.DOLocalMove(attackPos, aD)
                            .SetEase(Ease.InCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotAtk), aD, RotateMode.Fast)
                            .SetEase(Ease.InCubic));

                        // OutBounce — 내리찍힌 느낌
                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutBounce));
                        seq.Join(transform.DOLocalRotate(
                            Vector3.zero, rD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── Combo3 — 찌르기 피니셔 ────────────────────
                case AttackType.Combo3:
                    {
                        Vector3 backPos = FlipX(_keyData.combo3BackPos, f);
                        Vector3 attackPos = FlipX(_keyData.combo3AttackPos, f);
                        // 찌르기는 Z회전 없음 — 수평 직선

                        seq.Append(transform.DOLocalMove(backPos, bD)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + aD;

                        // InExpo — 폭발적 가속
                        seq.Append(transform.DOLocalMove(attackPos, aD)
                            .SetEase(Ease.InExpo));
                        seq.Join(transform.DOPunchScale(
                            Vector3.one * 0.25f, 0.1f, vibrato: 5, elasticity: 0.3f));

                        // 카메라 흔들림
                        seq.InsertCallback(bD + aD,
                            () => CameraShake.Shake(CameraShake.Preset.Heavy));

                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutExpo));
                        break;
                    }

                // ── AirSide — 공중 수평 횡베기 ────────────────
                case AttackType.AirAttack:
                    {
                        Vector3 backPos = FlipX(_keyData.airSideBackPos, f);
                        Vector3 attackPos = FlipX(_keyData.airSideAttackPos, f);
                        float rotBack = f * _keyData.airSideRotBack;
                        float rotAtk = f * _keyData.airSideRotAtk;

                        seq.Append(transform.DOLocalMove(backPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotBack), bD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + aD;

                        seq.Append(transform.DOLocalMove(attackPos, aD)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotAtk), aD, RotateMode.Fast)
                            .SetEase(Ease.InOutCubic));

                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            Vector3.zero, rD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── AirDown — 공중 내리찍기 ───────────────────
                case AttackType.AirAttackDown:
                    {
                        Vector3 backPos = FlipX(_keyData.airDownBackPos, f);
                        Vector3 attackPos = FlipX(_keyData.airDownAttackPos, f);
                        float rotBack = f * _keyData.airDownRotBack;
                        float rotAtk = f * _keyData.airDownRotAtk;

                        seq.Append(transform.DOLocalMove(backPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotBack), bD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + aD;

                        seq.Append(transform.DOLocalMove(attackPos, aD)
                            .SetEase(Ease.InCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotAtk), aD, RotateMode.Fast)
                            .SetEase(Ease.InCubic));

                        seq.InsertCallback(bD + aD,
                            () => CameraShake.Shake(CameraShake.Preset.Medium));

                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            Vector3.zero, rD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));
                        break;
                    }

                // ── AirUp — 공중 상향 퍼올리기 ────────────────
                case AttackType.AirAttackUp:
                    {
                        Vector3 backPos = FlipX(_keyData.airUpBackPos, f);
                        Vector3 attackPos = FlipX(_keyData.airUpAttackPos, f);
                        float rotBack = f * _keyData.airUpRotBack;
                        float rotAtk = f * _keyData.airUpRotAtk;

                        seq.Append(transform.DOLocalMove(backPos, bD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotBack), bD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));

                        hitEnableTime = bD;
                        hitDisableTime = bD + aD;

                        seq.Append(transform.DOLocalMove(attackPos, aD)
                            .SetEase(Ease.InOutCubic));
                        seq.Join(transform.DOLocalRotate(
                            new Vector3(0f, 0f, rotAtk), aD, RotateMode.Fast)
                            .SetEase(Ease.InOutCubic));

                        seq.Append(transform.DOLocalMove(origin, rD)
                            .SetEase(Ease.OutQuart));
                        seq.Join(transform.DOLocalRotate(
                            Vector3.zero, rD, RotateMode.Fast)
                            .SetEase(Ease.OutQuart));
                        break;
                    }
            }

            // ── 히트박스 타이밍 InsertCallback ────────────────
            if (_hitboxManager != null)
            {
                seq.InsertCallback(hitEnableTime,
                    () => _hitboxManager.EnableHitbox(hitboxIndex, damageInfo));
                seq.InsertCallback(hitDisableTime,
                    () => _hitboxManager.DisableAllHitboxes());
            }

            return seq;
        }

        // ══════════════════════════════════════════════════════
        // 보조
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// facing 방향에 따라 X 부호 반전.
        /// Y 는 항상 그대로 유지.
        /// facing=+1: 그대로 / facing=-1: X 반전
        /// </summary>
        private static Vector3 FlipX(Vector2 pos, float facing)
            => new Vector3(pos.x * facing, pos.y, 0f);

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
            if (!Application.isPlaying || _keyData == null) return;

            float f = PlayerMovementFacade.Instance?.FacingDirection ?? 1f;

            // 현재 공격 상태 표시
            Gizmos.color = IsSwinging ? Color.red : Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.08f);

            // 원점 표시
            if (transform.parent != null)
            {
                Vector3 origin = transform.parent.TransformPoint(_originLocalPosition);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(origin, 0.05f);

                // Combo2 위치 미리보기
                Vector3 back = transform.parent.TransformPoint(
                    FlipX(_keyData.combo2BackPos, f));
                Vector3 attack = transform.parent.TransformPoint(
                    FlipX(_keyData.combo2AttackPos, f));
                Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                Gizmos.DrawLine(back, attack);
                Gizmos.DrawWireSphere(back, 0.05f);
                Gizmos.DrawWireSphere(attack, 0.05f);
            }
        }
#endif
    }
}