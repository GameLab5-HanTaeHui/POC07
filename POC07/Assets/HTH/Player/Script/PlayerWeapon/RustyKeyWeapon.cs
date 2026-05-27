// ============================================================
// RustyKeyWeapon.cs  v1.3
// 녹슨 열쇠 무기 — Animator 직접 폴링 방식
//
// [v1.2 → v1.3 핵심 변경]
//
//   [v1.2 의 문제]
//     코드가 자체 elapsed 타이머로 comboWindowStart 를 계산.
//     코드 타이밍이 Animator ExitTime 보다 빠르면 AttackCombo2 Trigger
//     가 선발행되어 큐에 쌓임 → ExitTime 도달 즉시 클릭 없이 전환됨.
//
//   [v1.3 해결 — Animator.GetCurrentAnimatorStateInfo 직접 폴링]
//     Animator 의 실제 normalizedTime 을 매 프레임 읽음.
//     코드와 Animator 가 완전히 같은 진행률 기준을 공유.
//     Trigger 는 다음 단계 ExecuteCombo 시작 시(FireComboEvent)에만 발행.
//     comboWindowStartRatio 구간에 버퍼 없으면 Trigger 절대 발행 안 함.
//     → Animator 큐에 미소비 Trigger 가 쌓이는 현상 원천 차단.
//
// [Animator 참조]
//   Weapon 은 Player 의 자식 → GetComponentInParent<Animator>() 로 취득.
//
// [공격 상태 해시]
//   _attackStateHashes 배열에 PlayerAttack01/02/03 해시 캐싱.
//   normalizedTime 폴링 전 현재 상태 검증에 사용.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 녹슨 열쇠 무기. 3단 콤보 + 공중 내리찍기. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [콤보 흐름]
    ///   클릭 → Attack()
    ///     공격 중 아님 → ExecuteCombo(0) 시작
    ///     공격 중      → _inputBuffered = true (Trigger 발행 안 함)
    ///
    ///   ExecuteCombo 내부 (매 프레임 Animator normalizedTime 폴링):
    ///     normalizedTime >= hitboxStartRatio  → 히트박스 ON
    ///     normalizedTime >= hitboxEndRatio    → 히트박스 OFF
    ///     normalizedTime >= comboWindowStart
    ///       && _inputBuffered && !isFinal     → 다음 ExecuteCombo 시작
    ///                                           (이때 Trigger 발행)
    ///     normalizedTime >= 1.0               → ComboReset
    ///
    /// [Trigger 발행 보장]
    ///   Trigger 는 ExecuteCombo 시작 직후 FireComboEvent 에서만 발행.
    ///   버퍼 없이 comboWindowStart 를 지나면 Trigger 발행 없이 클립 종료 대기.
    ///   → 클릭 없이 다음 콤보로 넘어가는 버그 완전 차단.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class RustyKeyWeapon : PlayerWeaponBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 컴포넌트 연결 ──────────────────────")]

        /// <summary>
        /// 히트박스 관리 컴포넌트.
        /// 같은 오브젝트 or 자식에 부착.
        /// </summary>
        [Tooltip("PlayerWeaponHitboxManager. 필수 연결.")]
        [SerializeField] private PlayerWeaponHitboxManager _hitboxManager;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// Player 루트의 Animator.
        /// normalizedTime 폴링에 사용.
        /// Weapon 은 Player 의 자식이므로 GetComponentInParent 로 취득.
        /// </summary>
        private Animator _animator;

        // ──────────────────────────────────────────
        // Animator 상태 해시 캐시
        // ──────────────────────────────────────────

        /// <summary>
        /// 공격 상태 이름 해시 배열.
        /// GetCurrentAnimatorStateInfo 결과와 비교하여 현재 상태 검증.
        /// 인덱스 0=PlayerAttack01, 1=PlayerAttack02, 2=PlayerAttack03.
        ///
        /// [shortNameHash vs fullPathHash]
        ///   shortNameHash 는 상태 이름만으로 계산 (레이어 경로 무시).
        ///   Base Layer 에 상태가 있으면 shortNameHash 로 충분.
        /// </summary>
        private static readonly int[] _attackStateHashes = new int[]
        {
            Animator.StringToHash("PlayerAttack01"),
            Animator.StringToHash("PlayerAttack02"),
            Animator.StringToHash("PlayerAttack03"),
        };

        // ──────────────────────────────────────────
        // 내부 상태 — 콤보
        // ──────────────────────────────────────────

        /// <summary> 현재 실행 중인 콤보 단계. 0-based. </summary>
        private int _currentStep;

        /// <summary> 현재 실행 중인 콤보 코루틴. </summary>
        private Coroutine _comboCoroutine;

        /// <summary>
        /// 입력 버퍼 플래그.
        /// 공격 중 클릭 시 true. 콤보당 1회만.
        /// comboWindowStartRatio 이후 버퍼 확인 → 있으면 다음 콤보.
        /// </summary>
        private bool _inputBuffered;

        /// <summary> 입력 2단 방지</summary>
        private int _lastAttackInputFrame = -1;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary> Combo1 시작 시 발행. MovementAnimator → AttackCombo1 Trigger. </summary>
        public event System.Action OnCombo1Started;

        /// <summary> Combo2 시작 시 발행. MovementAnimator → AttackCombo2 Trigger. </summary>
        public event System.Action OnCombo2Started;

        /// <summary> Combo3 시작 시 발행. MovementAnimator → AttackCombo3 Trigger. </summary>
        public event System.Action OnCombo3Started;

        /// <summary> 공중 공격 시작 시 발행. MovementAnimator → AirAttack Trigger. </summary>
        public event System.Action OnAirAttackStarted;

        /// <summary> 콤보 리셋 시 발행. PlayerWeaponAnimator → 스윙 취소. </summary>
        public event System.Action OnComboReset;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_hitboxManager == null)
                _hitboxManager = GetComponentInChildren<PlayerWeaponHitboxManager>();

            if (_hitboxManager == null)
                Debug.LogError("[RustyKeyWeapon] PlayerWeaponHitboxManager 가 없습니다.");

            // Weapon 오브젝트는 Player 의 자식 → 부모 방향으로 Animator 탐색
            _animator = GetComponentInParent<Animator>();

            if (_animator == null)
                Debug.LogWarning("[RustyKeyWeapon] 부모에서 Animator 를 찾을 수 없습니다. " +
                                 "normalizedTime 폴링이 비활성화됩니다.");
        }

        protected override void Start()
        {
            base.Start();
            ComboReset();
        }

        // ══════════════════════════════════════════════════════
        // PlayerWeaponBase override
        // ══════════════════════════════════════════════════════

        protected override void OnKeyDataSet(KeyDataSO keyData)
        {
            ComboReset();
        }

        /// <summary>
        /// 지상 공격 입력 처리.
        ///
        /// [중요] 이 함수에서 Trigger 를 직접 발행하지 않음.
        ///   Trigger 는 ExecuteCombo 시작 시 FireComboEvent 에서만 발행.
        ///   공격 중 클릭은 버퍼에만 저장 → 코루틴이 적절한 타이밍에 소비.
        /// </summary>
        protected override void Attack()
        {
            if (_keyData == null) return;
            if (_lastAttackInputFrame == Time.frameCount) return;

            _lastAttackInputFrame = Time.frameCount;

            if (!_isAttacking)
            {
                _currentStep = 0;
                _comboCoroutine = StartCoroutine(ExecuteCombo(_currentStep));
            }
            else
            {
                _inputBuffered = true;
            }
        }

        protected override void AirAttack()
        {
            if (_isAttacking) return;
            if (_keyData == null) return;

            if (_comboCoroutine != null)
                StopCoroutine(_comboCoroutine);

            _comboCoroutine = StartCoroutine(ExecuteAirAttack());
        }

        /// <summary>
        /// 콤보 완전 초기화.
        /// </summary>
        public override void ComboReset()
        {
            if (_comboCoroutine != null)
            {
                StopCoroutine(_comboCoroutine);
                _comboCoroutine = null;
            }

            _currentStep = 0;
            _inputBuffered = false;
            _isAttacking = false;

            _hitboxManager?.DisableAllHitboxes();
            OnComboReset?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 콤보 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지상 콤보 단계 실행 코루틴.
        ///
        /// [핵심: Animator.normalizedTime 직접 폴링]
        ///   자체 elapsed 타이머 대신 Animator 의 실제 진행률을 읽음.
        ///   코드와 Animator 가 완전히 같은 기준 사용.
        ///
        /// [Trigger 발행 시점]
        ///   FireComboEvent(step) 를 ExecuteCombo 시작 직후 1회만 호출.
        ///   comboWindowStart 이후에도 버퍼 없으면 Trigger 발행 없이 대기.
        ///   버퍼 있을 때만 다음 ExecuteCombo 를 시작하여 거기서 Trigger 발행.
        /// </summary>
        private IEnumerator ExecuteCombo(int step)
        {
            _isAttacking = true;
            _inputBuffered = false;

            // ① Trigger 발행 — Animator 전환 시작
            FireComboEvent(step);

            // Animator 가 전환을 처리할 최소 1프레임 대기
            yield return null;

            float comboWindowRatio = _keyData.comboWindowStartRatio;
            float hitboxStartRatio = _keyData.hitboxStartRatio;
            float hitboxEndRatio = _keyData.hitboxEndRatio;
            bool hitboxOn = false;
            bool windowReached = false;

            // ② 매 프레임 Animator normalizedTime 폴링
            while (true)
            {
                float nt = GetNormalizedTime(step);

                // 히트박스 ON
                if (!hitboxOn && nt >= hitboxStartRatio)
                {
                    hitboxOn = true;
                    float dmg = _keyData.baseDamage * _keyData.GetComboMultiplier(step);
                    DamageInfo info = BuildDamageInfo(dmg, StepToAttackType(step));
                    _hitboxManager.EnableHitbox(step, info);
                }

                // 히트박스 OFF
                if (hitboxOn && nt >= hitboxEndRatio)
                {
                    hitboxOn = false;
                    _hitboxManager.DisableAllHitboxes();
                }

                // 콤보 창 구간 진입 감지
                if (!windowReached && nt >= comboWindowRatio)
                    windowReached = true;

                if (windowReached)
                {
                    bool isFinal = (step >= _keyData.comboCount - 1);

                    if (_inputBuffered && !isFinal)
                    {
                        // 버퍼 소비 → 다음 단계 시작 (Trigger 는 거기서 발행)
                        _currentStep = step + 1;
                        _comboCoroutine = StartCoroutine(ExecuteCombo(_currentStep));
                        yield break;
                    }

                    // 버퍼 없음 or 피니셔 → 클립 끝까지 대기
                    if (nt >= 1.0f)
                        break;
                }

                yield return null;
            }

            // ③ 클립 종료 정리
            _hitboxManager.DisableAllHitboxes();
            ComboReset();
        }

        /// <summary>
        /// 공중 내리찍기 실행. hitboxDuration 기준 (공중 전용).
        /// </summary>
        private IEnumerator ExecuteAirAttack()
        {
            _isAttacking = true;
            OnAirAttackStarted?.Invoke();

            float dmg = _keyData.baseDamage * _keyData.airAttackMultiplier;
            DamageInfo info = BuildDamageInfo(dmg, AttackType.AirAttack);
            _hitboxManager.EnableHitbox(PlayerWeaponHitboxManager.HitboxAirAttack, info);

            yield return new WaitForSeconds(_keyData.hitboxDuration);

            _hitboxManager.DisableAllHitboxes();
            ComboReset();
        }

        // ══════════════════════════════════════════════════════
        // Animator 폴링
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 Animator 공격 상태의 normalizedTime 을 반환.
        ///
        /// [정상 케이스]
        ///   Animator 가 해당 step 의 Attack 상태를 재생 중이면
        ///   Clamp01(normalizedTime) 반환.
        ///
        /// [전환 중(IsInTransition)]
        ///   다음 상태(destination)를 확인. 목표 Attack 상태로 가는 중이면 0 반환.
        ///   (전환이 완료되지 않은 상태이므로 아직 시작 안 된 것으로 간주)
        ///
        /// [상태 불일치 — 이미 클립이 끝났거나 다른 상태로 넘어간 경우]
        ///   1.0 반환 → while 루프 즉시 종료 유도.
        ///
        /// [Layer 인덱스 0 가정]
        ///   공격 상태가 Base Layer(0)에 있다고 가정.
        ///   Attack Layer(1) 를 분리해서 사용하는 경우 인덱스 수정 필요.
        /// </summary>
        private float GetNormalizedTime(int step)
        {
            if (_animator == null) return 0f;

            int expectedHash = (step < _attackStateHashes.Length)
                ? _attackStateHashes[step] : -1;

            // 현재 상태 확인
            AnimatorStateInfo cur = _animator.GetCurrentAnimatorStateInfo(0);
            if (expectedHash != -1 && cur.shortNameHash == expectedHash)
                return Mathf.Clamp01(cur.normalizedTime);

            // 전환 중이면 다음 상태 확인
            if (_animator.IsInTransition(0))
            {
                AnimatorStateInfo next = _animator.GetNextAnimatorStateInfo(0);
                if (expectedHash != -1 && next.shortNameHash == expectedHash)
                    return 0f; // 아직 전환 완료 전
            }

            // 상태 불일치 → 클립 끝으로 간주
            return 1.0f;
        }

        // ══════════════════════════════════════════════════════
        // 보조
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 콤보 단계별 이벤트 발행.
        /// [호출 위치] ExecuteCombo 시작 직후 1회만. 절대 다른 곳에서 호출 금지.
        /// </summary>
        private void FireComboEvent(int step)
        {
            switch (step)
            {
                case 0: OnCombo1Started?.Invoke(); Debug.Log($"[RustyKeyWeapon] 콤보 1단계"); break;
                case 1: OnCombo2Started?.Invoke(); Debug.Log($"[RustyKeyWeapon] 콤보 2단계"); break;
                case 2: OnCombo3Started?.Invoke(); Debug.Log($"[RustyKeyWeapon] 콤보 3단계"); break;
                default:
                    Debug.LogWarning($"[RustyKeyWeapon] 정의되지 않은 콤보 단계: {step}");
                    break;
            }
        }

        private AttackType StepToAttackType(int step)
        {
            switch (step)
            {
                case 0: return AttackType.Combo1;
                case 1: return AttackType.Combo2;
                default: return AttackType.Combo3;
            }
        }

        private DamageInfo BuildDamageInfo(float amount, AttackType attackType)
        {
            float facing = PlayerMovementFacade.Instance?.FacingDirection ?? 1f;
            Vector2 attackDir = new Vector2(facing, 0f);

            if (attackType == AttackType.AirAttack)
                attackDir = new Vector2(facing * 0.5f, -1f).normalized;

            return new DamageInfo(
                attackerPosition: transform.position,
                amount: amount,
                direction: attackDir,
                attackType: attackType
            );
        }
    }
}