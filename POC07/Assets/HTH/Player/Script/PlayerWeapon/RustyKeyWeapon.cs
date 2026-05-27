// ============================================================
// RustyKeyWeapon.cs  v1.1
// 녹슨 열쇠 무기 — KeyDataSO 수치 연동
//
// [v1.1 변경]
//   하드코딩된 수치 제거 → 모두 _keyData(KeyDataSO) 에서 읽음.
//   OnKeyDataSet() override — 데이터 주입 시 최대 콤보 수 갱신.
//   Inspector 수치 필드 제거 (KeyDataSO 로 통합).
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 녹슨 열쇠 무기. 3단 콤보 + 공중 내리찍기. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [수치 출처]
    ///   모든 전투 수치는 _keyData(KeyDataSO) 에서 읽음.
    ///   Inspector 에서 직접 수정 불가 — KeyDataSO 에셋에서 수정.
    ///
    /// [Inspector 필수 연결]
    ///   _hitboxManager : PlayerWeaponHitboxManager
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
        // 내부 상태 — 콤보
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 콤보 단계. 0 = 대기.
        /// Attack() 호출 시 이 값으로 다음 단계 결정.
        /// </summary>
        private int _comboIndex;

        /// <summary>
        /// 콤보 윈도우 타이머.
        /// 양수인 동안 다음 콤보 입력 허용.
        /// 만료 + 공격 중 아닐 때 → ComboReset().
        /// </summary>
        private float _comboWindowTimer;

        /// <summary>
        /// 현재 실행 중인 콤보 코루틴.
        /// ComboReset() 시 StopCoroutine 에 사용.
        /// </summary>
        private Coroutine _comboCoroutine;

        // ──────────────────────────────────────────
        // 이벤트 — 추후 WeaponAnimator 구독
        // ──────────────────────────────────────────

        /// <summary> Combo1 시작 시 발행. 추후 WeaponAnimator 구독. </summary>
        public event System.Action OnCombo1Started;

        /// <summary> Combo2 시작 시 발행. </summary>
        public event System.Action OnCombo2Started;

        /// <summary> Combo3(피니셔) 시작 시 발행. </summary>
        public event System.Action OnCombo3Started;

        /// <summary> 공중 공격 시작 시 발행. </summary>
        public event System.Action OnAirAttackStarted;

        /// <summary> 콤보 리셋 시 발행. </summary>
        public event System.Action OnComboReset;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// HitboxManager 자동 취득.
        /// </summary>
        private void Awake()
        {
            if (_hitboxManager == null)
                _hitboxManager = GetComponentInChildren<PlayerWeaponHitboxManager>();

            if (_hitboxManager == null)
                Debug.LogError("[RustyKeyWeapon] PlayerWeaponHitboxManager 가 없습니다.");
        }

        /// <summary>
        /// base.Start() 호출 후 초기화.
        /// </summary>
        protected override void Start()
        {
            base.Start();
            ComboReset();
        }

        /// <summary>
        /// 콤보 윈도우 타이머 감소 및 만료 처리.
        /// </summary>
        private void Update()
        {
            if (_comboWindowTimer <= 0f) return;

            _comboWindowTimer -= Time.deltaTime;

            if (_comboWindowTimer <= 0f && !_isAttacking)
                ComboReset();
        }

        // ══════════════════════════════════════════════════════
        // PlayerWeaponBase override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// KeyDataSO 주입 시 호출.
        /// 콤보 리셋 후 새 데이터 기반으로 초기화.
        /// </summary>
        /// <param name="keyData">주입된 열쇠 데이터</param>
        protected override void OnKeyDataSet(KeyDataSO keyData)
        {
            ComboReset();
        }

        /// <summary>
        /// 지상 공격. _comboIndex 에 따라 1~3단 분기.
        /// </summary>
        protected override void Attack()
        {
            if (_isAttacking) return;
            if (_keyData == null) return;

            if (_comboCoroutine != null)
                StopCoroutine(_comboCoroutine);

            // 최대 콤보 수는 KeyDataSO.comboCount 기준
            int maxCombo = _keyData.comboCount;
            int step = Mathf.Clamp(_comboIndex, 0, maxCombo - 1);

            _comboCoroutine = StartCoroutine(ExecuteCombo(step));
        }

        /// <summary>
        /// 공중 공격. 콤보 상태 무관 단독 실행.
        /// </summary>
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
            _comboIndex = 0;
            _comboWindowTimer = 0f;
            _isAttacking = false;

            _hitboxManager?.DisableAllHitboxes();
            OnComboReset?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 콤보 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지상 콤보 단계 실행.
        /// step 0 = Combo1, 1 = Combo2, 2 = Combo3(피니셔).
        /// KeyDataSO 의 배율 배열과 hitboxDuration 을 사용.
        /// </summary>
        /// <param name="step">0-based 콤보 단계</param>
        private IEnumerator ExecuteCombo(int step)
        {
            _isAttacking = true;

            // 단계별 이벤트 발행 (추후 WeaponAnimator 구독)
            switch (step)
            {
                case 0: OnCombo1Started?.Invoke(); break;
                case 1: OnCombo2Started?.Invoke(); break;
                case 2: OnCombo3Started?.Invoke(); break;
            }

            // KeyDataSO 에서 수치 읽기
            float damage = _keyData.baseDamage * _keyData.GetComboMultiplier(step);
            float duration = _keyData.hitboxDuration;
            AttackType type = StepToAttackType(step);

            DamageInfo info = BuildDamageInfo(damage, type);
            _hitboxManager.EnableHitbox(step, info);

            yield return new WaitForSeconds(duration);

            _hitboxManager.DisableAllHitboxes();
            _isAttacking = false;

            int maxCombo = _keyData.comboCount;
            bool isFinal = (step >= maxCombo - 1);

            if (isFinal)
            {
                // 피니셔 이후 완전 리셋
                ComboReset();
            }
            else
            {
                // 다음 콤보 대기
                _comboIndex = step + 1;
                _comboWindowTimer = _keyData.comboWindowTime;
            }
        }

        /// <summary>
        /// 공중 내리찍기 실행.
        /// </summary>
        private IEnumerator ExecuteAirAttack()
        {
            _isAttacking = true;
            OnAirAttackStarted?.Invoke();

            float damage = _keyData.baseDamage * _keyData.airAttackMultiplier;
            float duration = _keyData.hitboxDuration;

            DamageInfo info = BuildDamageInfo(damage, AttackType.AirAttack);
            _hitboxManager.EnableHitbox(PlayerWeaponHitboxManager.HitboxAirAttack, info);

            yield return new WaitForSeconds(duration);

            _hitboxManager.DisableAllHitboxes();
            ComboReset();
        }

        // ══════════════════════════════════════════════════════
        // 보조
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 콤보 단계 인덱스를 AttackType 으로 변환.
        /// </summary>
        private AttackType StepToAttackType(int step)
        {
            switch (step)
            {
                case 0: return AttackType.Combo1;
                case 1: return AttackType.Combo2;
                default: return AttackType.Combo3;
            }
        }

        /// <summary>
        /// DamageInfo 빌드.
        /// FacingDirection 을 방향 벡터로 변환.
        /// </summary>
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