using System.Collections;
using UnityEngine;

namespace BreathCasino.Gameplay
{
    public class BCLeverMechanism : MonoBehaviour
    {
        [SerializeField] private Transform leverRoot;
        [SerializeField] private Vector3 pullAxis = new(-1f, 0f, 0f);
        [SerializeField] private float clickPullAngle = 18f;
        [SerializeField] private float holdPullAngle = 34f;
        [SerializeField] private float clickDuration = 0.18f;
        [SerializeField] private float returnDuration = 0.16f;

        private Quaternion _restRotation;
        private Coroutine _animationRoutine;

        private void Awake()
        {
            if (leverRoot == null)
            {
                leverRoot = transform;
            }

            _restRotation = leverRoot.localRotation;
        }

        public void PlayPull()
        {
            RestartRoutine(AnimateClick());
        }

        public void SetPullAxis(Vector3 axis)
        {
            if (axis.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            pullAxis = axis.normalized;
        }

        public void AutoConfigurePullAxisTowards(Transform target)
        {
            if (leverRoot == null || target == null)
            {
                return;
            }

            Vector3 worldDirection = target.position - leverRoot.position;
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 localDirection = leverRoot.InverseTransformDirection(worldDirection.normalized);
            localDirection.y *= 0.25f;

            if (Mathf.Abs(localDirection.z) >= Mathf.Abs(localDirection.x))
            {
                SetPullAxis(new Vector3(0f, 0f, Mathf.Sign(Mathf.Approximately(localDirection.z, 0f) ? -1f : localDirection.z)));
                return;
            }

            SetPullAxis(new Vector3(Mathf.Sign(Mathf.Approximately(localDirection.x, 0f) ? -1f : localDirection.x), 0f, 0f));
        }

        public void SetHoldProgress(float progress01)
        {
            if (leverRoot == null)
            {
                return;
            }

            float angle = Mathf.Lerp(0f, holdPullAngle, Mathf.Clamp01(progress01));
            leverRoot.localRotation = _restRotation * Quaternion.Euler(pullAxis.normalized * angle);
        }

        public void CancelHold()
        {
            RestartRoutine(AnimateReturn());
        }

        public void CompleteHold()
        {
            RestartRoutine(AnimateHoldComplete());
        }

        private void RestartRoutine(IEnumerator routine)
        {
            if (_animationRoutine != null)
            {
                StopCoroutine(_animationRoutine);
            }

            _animationRoutine = StartCoroutine(routine);
        }

        private IEnumerator AnimateClick()
        {
            yield return AnimateToAngle(clickPullAngle, clickDuration);
            yield return AnimateToAngle(0f, returnDuration);
            _animationRoutine = null;
        }

        private IEnumerator AnimateHoldComplete()
        {
            yield return AnimateToAngle(holdPullAngle, 0.08f);
            yield return new WaitForSeconds(0.05f);
            yield return AnimateToAngle(0f, returnDuration);
            _animationRoutine = null;
        }

        private IEnumerator AnimateReturn()
        {
            yield return AnimateToAngle(0f, returnDuration);
            _animationRoutine = null;
        }

        private IEnumerator AnimateToAngle(float angle, float duration)
        {
            if (leverRoot == null)
            {
                yield break;
            }

            Quaternion start = leverRoot.localRotation;
            Quaternion target = _restRotation * Quaternion.Euler(pullAxis.normalized * angle);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(duration, 0.01f));
                float eased = t * t * (3f - 2f * t);
                leverRoot.localRotation = Quaternion.Slerp(start, target, eased);
                yield return null;
            }

            leverRoot.localRotation = target;
        }
    }
}
