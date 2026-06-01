// ============================================================
// TestBossCore.cs  v1.0
// 테스트 보스 루트 컴포넌트
//
// [역할]
//   핵심 플레이 루프의 전체 상태를 관리한다.
//
//   ① HP / TakeDamage / 사망
//   ② 그로기 상태 (A키 처형 가능 구간)
//   ③ 코어 활성화 조건 (양팔 봉인 시 Core 오브젝트 표시)
//   ④ 딜타임 진입 / 종료 (코어 처형 후 집중 공격 구간)
//   ⑤ 딜타임 종료 → 양팔 강제 해제 + 충격파 연출
//   ⑥ 루프 반복
//
// [핵심 플레이 루프]
//   팔(Arm_L/R) 해제 상태 시작 (붉은색)
//     ↓ 플레이어 그로기 유도 (TestBossExecution 이 감지)
//   그로기 진입
//     ↓ 플레이어 A키 홀드 → Arm_L 처형 → ReLock (파란색)
//     ↓ 플레이어 A키 홀드 → Arm_R 처형 → ReLock (파란색)
//   양팔 봉인 → CheckCoreActivation() → Core 오브젝트 활성 (노란색)
//     ↓ 플레이어 A키 홀드 → Core 처형 → EnterDilTime()
//   딜타임 진입 (보스 정지, 코어 집중 공격)
//     ↓ 딜타임 종료
//   ExitDilTime() → Arm_L.ForceUnlock() + Arm_R.ForceUnlock() + 충격파
//     ↓ 루프 반복 / HP 0 → 처치
//
// [컴포넌트 구성]
//   TestBossCore        ← 루트 오브젝트에 부착
//   TestBossExecution   ← 루트 오브젝트에 부착 (A키 처형 입력 처리)
//   Arm_L               ← 자식 오브젝트, TestBossArmPart 부착
//   Arm_R               ← 자식 오브젝트, TestBossArmPart 부착
//   Core                ← 자식 오브젝트 (기본 SetActive = false)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 루트 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [TakeDamage 흐름]
    ///   IDamageable.TakeDamage(info)
    ///     → _isInvincible 체크 (iFrame 중 무시)
    ///     → _isDead 체크
    ///     → 딜타임(_isDilTime) 상태만 HP 감소 허용
    ///     → 딜타임 외 피격은 무시
    ///     → HP 0 → Die()
    ///
    /// [그로기 흐름]
    ///   외부에서 EnterGroggy() 호출 (TestBossExecution 또는 테스트용 트리거)
    ///     → OnGroggyEnter 발행 → TestBossExecution 처형 감지 시작
    ///     → groggyDuration 후 OnGroggyExit → 루프 복귀
    ///
    /// [딜타임 흐름]
    ///   TestBossExecution 에서 코어 처형 완료 → EnterDilTime() 호출
    ///     → OnDilTimeEnter 발행 → 색상 변경
    ///     → dilTimeDuration 후 ExitDilTime()
    ///       → Core 비활성 → Arm_L/R ForceUnlock → 충격파
    ///       → OnDilTimeExit 발행
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(TestBossExecution))]
    public class TestBossCore : MonoBehaviour, IDamageable
    {
        // ──────────────────────────────────────────
        // Inspector — 필수 연결
        // ──────────────────────────────────────────

        [Header("── DataSO (필수) ──────────────────────")]

        /// <summary>
        /// 테스트 보스 수치 SO.
        /// ★ Inspector 연결 지점은 이 필드 하나.
        /// </summary>
        [Tooltip("TestBossDataSO. 필수 연결.")]
        [SerializeField] private TestBossDataSO _data;

        [Header("── 팔 부위 연결 (필수) ──────────────────────")]

        /// <summary>
        /// 왼팔 TestBossArmPart.
        /// Inspector 에서 Arm_L 오브젝트 연결.
        /// </summary>
        [Tooltip("왼팔 TestBossArmPart. 필수 연결.")]
        [SerializeField] private TestBossArmPart _armL;

        /// <summary>
        /// 오른팔 TestBossArmPart.
        /// Inspector 에서 Arm_R 오브젝트 연결.
        /// </summary>
        [Tooltip("오른팔 TestBossArmPart. 필수 연결.")]
        [SerializeField] private TestBossArmPart _armR;

        [Header("── 코어 오브젝트 연결 (필수) ──────────────────────")]

        /// <summary>
        /// 코어 GameObject.
        /// 양팔 봉인 시 SetActive(true), 딜타임 종료 시 SetActive(false).
        /// Prefab 에서 기본 SetActive = false 로 설정 필요.
        /// </summary>
        [Tooltip("코어 오브젝트. 기본 SetActive=false 필요.")]
        [SerializeField] private GameObject _coreObject;

        /// <summary>
        /// 코어 SpriteRenderer.
        /// 활성 시 색상 피드백.
        /// 미연결 시 _coreObject 에서 자동 탐색.
        /// </summary>
        [Tooltip("코어 SpriteRenderer. 미연결 시 자동 탐색.")]
        [SerializeField] private SpriteRenderer _coreSpriteRenderer;

        [Header("── 충격파 연결 (선택) ──────────────────────")]

        /// <summary>
        /// 충격파 컴포넌트.
        /// 딜타임 종료 시 호출. 미연결 시 스킵.
        /// </summary>
        [Tooltip("BossShockwave. 미연결 시 충격파 스킵.")]
        [SerializeField] private BossShockwave _shockwave;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;
        private TestBossExecution _execution;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 HP. </summary>
        private float _currentHp;

        /// <summary> 사망 여부. </summary>
        private bool _isDead;

        /// <summary> 무적(iFrame) 여부. </summary>
        private bool _isInvincible;

        /// <summary> 그로기 상태 여부. </summary>
        private bool _isGroggy;

        /// <summary> 딜타임 상태 여부. </summary>
        private bool _isDilTime;

        /// <summary> 코어 활성 여부. </summary>
        private bool _isCoreActive;

        /// <summary> 본체 기본 색상. </summary>
        private Color _defaultBodyColor;

        // ──────────────────────────────────────────
        // 코루틴 핸들
        // ──────────────────────────────────────────

        private Coroutine _groggyCoroutine;
        private Coroutine _dilTimeCoroutine;
        private Coroutine _iFrameCoroutine;
        private Coroutine _knockbackCoroutine;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 그로기 진입 시 발행.
        /// TestBossExecution 이 구독하여 처형 감지 시작.
        /// </summary>
        public event Action OnGroggyEnter;

        /// <summary>
        /// 그로기 종료 시 발행.
        /// TestBossExecution 이 구독하여 처형 감지 중단.
        /// </summary>
        public event Action OnGroggyExit;

        /// <summary> 딜타임 진입 시 발행. </summary>
        public event Action OnDilTimeEnter;

        /// <summary> 딜타임 종료 시 발행. </summary>
        public event Action OnDilTimeExit;

        /// <summary> 코어 활성 시 발행. TestBossExecution 이 구독. </summary>
        public event Action OnCoreActivated;

        /// <summary> 코어 비활성 시 발행. </summary>
        public event Action OnCoreDeactivated;

        /// <summary> 보스 처치 시 발행. </summary>
        public event Action OnDead;

        /// <summary>
        /// 딜타임 중 피격 시 발행.
        /// TestBossFeedback 이 구독하여 흰색 플래시 + 흔들림 연출.
        /// </summary>
        public event Action OnHitFeedback;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 HP. </summary>
        public float CurrentHp => _currentHp;

        /// <summary> 최대 HP. </summary>
        public float MaxHp => _data != null ? _data.maxHp : 1f;

        /// <summary> HP 비율 (0~1). </summary>
        public float HpRatio => MaxHp > 0f ? _currentHp / MaxHp : 0f;

        /// <summary> 사망 여부. IDamageable 구현. </summary>
        public bool IsDead => _isDead;

        /// <summary> 그로기 상태 여부. </summary>
        public bool IsGroggy => _isGroggy;

        /// <summary> 딜타임 상태 여부. </summary>
        public bool IsDilTime => _isDilTime;

        /// <summary> 코어 활성 여부. </summary>
        public bool IsCoreActive => _isCoreActive;

        /// <summary> DataSO 참조. TestBossExecution 에서 사용. </summary>
        public TestBossDataSO Data => _data;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _execution = GetComponent<TestBossExecution>();

            if (_spriteRenderer != null)
                _defaultBodyColor = _spriteRenderer.color;
        }

        private void Start()
        {
            if (_data == null)
            {
                Debug.LogError("[TestBossCore] TestBossDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            // HP 초기화
            _currentHp = _data.maxHp;

            // 코어 오브젝트 비활성 확인
            if (_coreObject != null)
            {
                _coreObject.SetActive(false);

                // 코어 SpriteRenderer 자동 탐색
                if (_coreSpriteRenderer == null)
                    _coreSpriteRenderer = _coreObject.GetComponentInChildren<SpriteRenderer>();
            }

            // 팔 부위 초기화
            if (_armL == null || _armR == null)
            {
                Debug.LogError("[TestBossCore] Arm_L 또는 Arm_R 이 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            _armL.Initialize(_data);
            _armR.Initialize(_data);

            // 팔 이벤트 구독
            _armL.OnReLocked += _ => CheckCoreActivation();
            _armL.OnUnlocked += _ => CheckCoreActivation();
            _armR.OnReLocked += _ => CheckCoreActivation();
            _armR.OnUnlocked += _ => CheckCoreActivation();

            // 처형 핸들러 초기화
            _execution.Initialize(this, _armL, _armR);
            _execution.OnExecutionCompleted += HandleExecutionCompleted;

            // 그로기 이벤트 연결
            OnGroggyEnter += _execution.OnGroggyEnter;
            OnGroggyExit += _execution.OnGroggyExit;

            // 충격파 초기화 (미연결 시 스킵)
            if (_shockwave != null && _data != null)
                _shockwave.Initialize(null); // BossKnightDataSO 없이 수동 관리

            Debug.Log("[TestBossCore] 초기화 완료. 핵심 플레이 루프 준비됨.");
        }

        private void OnDestroy()
        {
            if (_armL != null)
            {
                _armL.OnReLocked -= _ => CheckCoreActivation();
                _armL.OnUnlocked -= _ => CheckCoreActivation();
            }

            if (_armR != null)
            {
                _armR.OnReLocked -= _ => CheckCoreActivation();
                _armR.OnUnlocked -= _ => CheckCoreActivation();
            }

            if (_execution != null)
                _execution.OnExecutionCompleted -= HandleExecutionCompleted;
        }

        // ══════════════════════════════════════════════════════
        // IDamageable 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 피격 처리.
        ///
        /// [중요]
        ///   딜타임(_isDilTime) 상태에서 코어를 공격할 때만 HP 감소.
        ///   나머지 상황은 전부 무시 (핵심 루프 보호).
        ///   iFrame 중 피격도 무시.
        /// </summary>
        /// <param name="info">데미지 정보 구조체.</param>
        public void TakeDamage(DamageInfo info)
        {
            if (_isInvincible || _isDead) return;

            // 딜타임 상태에서만 HP 감소
            if (!_isDilTime)
            {
                Debug.Log("[TestBossCore] 딜타임 외 피격 — 무시");
                return;
            }

            // HP 감소
            _currentHp = Mathf.Max(0f, _currentHp - info.Amount);

            // 넉백
            if (_knockbackCoroutine != null) StopCoroutine(_knockbackCoroutine);
            _knockbackCoroutine = StartCoroutine(KnockbackRoutine(info.Direction));

            // iFrame
            if (_iFrameCoroutine != null) StopCoroutine(_iFrameCoroutine);
            _iFrameCoroutine = StartCoroutine(IFrameRoutine());

            // 피격 피드백 이벤트 발행 → TestBossFeedback 흰색 플래시
            OnHitFeedback?.Invoke();

            Debug.Log($"[TestBossCore] 딜타임 피격: -{info.Amount:F0} / HP {_currentHp:F0}/{MaxHp:F0}");

            // 사망 체크
            if (_currentHp <= 0f)
                Die();
        }

        // ══════════════════════════════════════════════════════
        // 그로기
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 그로기 진입.
        /// 외부(테스트용 트리거 또는 향후 패턴 연결)에서 호출.
        ///
        /// [기획]
        ///   그로기 중 → A키 홀드 처형 가능 → 팔 봉인 → 코어 활성 → 딜타임
        /// </summary>
        /// <param name="duration">그로기 지속 시간. 0 이하면 DataSO 값 사용.</param>
        public void EnterGroggy(float duration = -1f)
        {
            if (_isGroggy || _isDilTime || _isDead) return;

            float t = duration > 0f ? duration : _data.groggyDuration;

            _isGroggy = true;
            _rigid2D.linearVelocity = Vector2.zero;

            OnGroggyEnter?.Invoke();

            if (_groggyCoroutine != null) StopCoroutine(_groggyCoroutine);
            _groggyCoroutine = StartCoroutine(GroggyRoutine(t));

            Debug.Log($"[TestBossCore] 그로기 진입 ({t:F1}초)");
        }

        /// <summary>
        /// 그로기 상태 코루틴.
        /// groggyDuration 후 자동 종료.
        /// </summary>
        private IEnumerator GroggyRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            _groggyCoroutine = null;
            _isGroggy = false;

            OnGroggyExit?.Invoke();

            Debug.Log("[TestBossCore] 그로기 종료 → 루프 복귀");
        }

        // ══════════════════════════════════════════════════════
        // 코어 활성 조건 체크
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 코어 활성화 조건 체크.
        /// 팔 이벤트(OnReLocked / OnUnlocked) 발행 시 자동 호출.
        ///
        /// [조건]
        ///   왼팔 봉인(IsLocked) AND 오른팔 봉인(IsLocked)
        ///   → Core 오브젝트 활성화 (코어 처형 가능)
        ///
        ///   조건 미충족 → Core 비활성화
        /// </summary>
        public void CheckCoreActivation()
        {
            if (_isDilTime) return;

            bool bothLocked = _armL.IsLocked && _armR.IsLocked;

            if (bothLocked && !_isCoreActive)
                ActivateCore();
            else if (!bothLocked && _isCoreActive)
                DeactivateCore();
        }

        // ══════════════════════════════════════════════════════
        // 코어 활성 / 비활성
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 코어 활성화.
        /// 양팔 봉인 시 CheckCoreActivation() 에서 호출.
        /// Core 오브젝트 표시 + OnCoreActivated 이벤트 발행.
        /// </summary>
        private void ActivateCore()
        {
            if (_isCoreActive) return;

            _isCoreActive = true;

            if (_coreObject != null) _coreObject.SetActive(true);

            // 코어 색상 피드백
            if (_coreSpriteRenderer != null && _data != null)
                _coreSpriteRenderer.color = _data.coreActiveColor;

            OnCoreActivated?.Invoke();

            Debug.Log("[TestBossCore] 코어 활성화 — A키 홀드 처형 가능");
        }

        /// <summary>
        /// 코어 비활성화.
        /// CheckCoreActivation() 조건 미충족 또는 ExitDilTime() 에서 호출.
        /// </summary>
        private void DeactivateCore()
        {
            if (!_isCoreActive) return;

            _isCoreActive = false;

            if (_coreObject != null) _coreObject.SetActive(false);

            OnCoreDeactivated?.Invoke();

            Debug.Log("[TestBossCore] 코어 비활성화");
        }

        // ══════════════════════════════════════════════════════
        // 딜타임
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 딜타임 진입.
        /// TestBossExecution 에서 코어 처형 완료 시 호출.
        ///
        /// [흐름]
        ///   딜타임 진입 → 보스 색상 변경 → dilTimeDuration 후 ExitDilTime()
        /// </summary>
        /// <param name="duration">딜타임 지속 시간. 0 이하면 DataSO 값 사용.</param>
        public void EnterDilTime(float duration = -1f)
        {
            if (_isDilTime || _isDead) return;

            float t = duration > 0f ? duration : _data.dilTimeDuration;

            _isDilTime = true;
            _isGroggy = false; // 그로기 상태 해제

            // 그로기 코루틴 중단
            if (_groggyCoroutine != null)
            {
                StopCoroutine(_groggyCoroutine);
                _groggyCoroutine = null;
            }

            // 본체 색상 변경 (딜타임 피드백)
            if (_spriteRenderer != null && _data != null)
                _spriteRenderer.color = _data.dilTimeBodyColor;

            OnDilTimeEnter?.Invoke();

            if (_dilTimeCoroutine != null) StopCoroutine(_dilTimeCoroutine);
            _dilTimeCoroutine = StartCoroutine(DilTimeRoutine(t));

            Debug.Log($"[TestBossCore] 딜타임 진입 ({t:F1}초) — 코어 집중 공격 구간");
        }

        /// <summary>
        /// 딜타임 코루틴.
        /// dilTimeDuration 후 ExitDilTime() 호출.
        /// </summary>
        private IEnumerator DilTimeRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);

            _dilTimeCoroutine = null;
            ExitDilTime();
        }

        /// <summary>
        /// 딜타임 종료 처리.
        /// DilTimeRoutine 완료 후 호출.
        ///
        /// [종료 순서 — 기획서 기준]
        ///   1. DeactivateCore()       → 코어 비활성
        ///   2. Arm_L.ForceUnlock()    → 왼팔 강제 해제 (붉은색)
        ///   3. Arm_R.ForceUnlock()    → 오른팔 강제 해제 (붉은색)
        ///   4. 충격파 발동            → 플레이어 밀침
        ///   5. 본체 색상 복구
        ///   6. OnDilTimeExit 발행     → 루프 복귀
        /// </summary>
        private void ExitDilTime()
        {
            _isDilTime = false;

            // 1. 코어 비활성
            DeactivateCore();

            // 2. 양팔 강제 해제 (붉은색 복귀)
            _armL.ForceUnlock();
            _armR.ForceUnlock();

            // 3. 충격파
            if (_shockwave != null)
                _shockwave.Trigger(transform.position);
            else
                Debug.Log("[TestBossCore] 충격파 스킵 (BossShockwave 미연결)");

            // 4. 본체 색상 복구
            if (_spriteRenderer != null)
                _spriteRenderer.color = _defaultBodyColor;

            OnDilTimeExit?.Invoke();

            Debug.Log("[TestBossCore] 딜타임 종료 → 양팔 해제 + 루프 반복");
        }

        // ══════════════════════════════════════════════════════
        // 처형 완료 이벤트 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// TestBossExecution.OnExecutionCompleted 이벤트 수신.
        /// 처형된 부위 타입에 따라 분기.
        ///
        /// [분기]
        ///   ArmL / ArmR → 해당 팔 ReLock() 호출
        ///   Core        → EnterDilTime() 호출
        /// </summary>
        /// <param name="partType">처형된 부위 타입.</param>
        private void HandleExecutionCompleted(TestBossPartType partType)
        {
            switch (partType)
            {
                case TestBossPartType.ArmL:
                    _armL.ReLock();
                    break;

                case TestBossPartType.ArmR:
                    _armR.ReLock();
                    break;

                case TestBossPartType.Core:
                    EnterDilTime();
                    break;

                default:
                    Debug.LogWarning($"[TestBossCore] 알 수 없는 처형 부위: {partType}");
                    break;
            }
        }

        // ══════════════════════════════════════════════════════
        // 사망
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 보스 처치 처리.
        /// HP 0 이하 시 TakeDamage 에서 호출.
        /// </summary>
        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            StopAllCoroutines();

            if (_rigid2D != null) _rigid2D.linearVelocity = Vector2.zero;
            if (_spriteRenderer != null) _spriteRenderer.color = Color.gray;

            // 코어 / 팔 비활성
            if (_coreObject != null) _coreObject.SetActive(false);

            OnDead?.Invoke();

            Debug.Log("[TestBossCore] 보스 처치!");
        }

        // ══════════════════════════════════════════════════════
        // 코루틴 — 넉백 / iFrame
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 넉백 코루틴.
        /// 딜타임 피격 시 소량 밀림 피드백.
        /// </summary>
        /// <param name="direction">공격 방향 (정규화 벡터).</param>
        private IEnumerator KnockbackRoutine(Vector2 direction)
        {
            if (_data == null || _data.knockbackForce <= 0f) yield break;

            _rigid2D.linearVelocity = new Vector2(
                direction.x * _data.knockbackForce,
                _rigid2D.linearVelocity.y);

            float elapsed = 0f;
            const float maxTime = 0.4f;
            const float threshold = 0.1f;

            while (elapsed < maxTime)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float vx = _rigid2D.linearVelocity.x * _data.knockbackDecay;
                _rigid2D.linearVelocity = new Vector2(vx, _rigid2D.linearVelocity.y);

                if (Mathf.Abs(vx) < threshold) break;
            }

            _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
        }

        /// <summary>
        /// iFrame 코루틴.
        /// 피격 후 무적 시간 동안 추가 피격 무시.
        /// </summary>
        private IEnumerator IFrameRoutine()
        {
            _isInvincible = true;
            yield return new WaitForSeconds(_data != null ? _data.iFrameDuration : 0.3f);
            _isInvincible = false;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 보스 상태 완전 리셋.
        /// 테스트 재시작 시 호출.
        /// </summary>
        public void ResetBoss()
        {
            StopAllCoroutines();

            _currentHp = _data != null ? _data.maxHp : 300f;
            _isDead = false;
            _isInvincible = false;
            _isGroggy = false;
            _isDilTime = false;
            _isCoreActive = false;

            if (_rigid2D != null) _rigid2D.linearVelocity = Vector2.zero;
            if (_spriteRenderer != null) _spriteRenderer.color = _defaultBodyColor;
            if (_coreObject != null) _coreObject.SetActive(false);

            _armL?.Initialize(_data);
            _armR?.Initialize(_data);

            Debug.Log("[TestBossCore] 보스 리셋 완료.");
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.color = _isDead ? Color.gray : Color.red;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"[TestBoss] HP {_currentHp:F0}/{MaxHp:F0}  " +
                $"Groggy:{_isGroggy}  DilTime:{_isDilTime}  Core:{_isCoreActive}");
#endif
        }
    }
}