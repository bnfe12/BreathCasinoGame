using System;
using UnityEngine;
using BreathCasino.Core;
using BreathCasino.Gameplay;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BreathCasino.Core
{
    public class BCInteractionRaycaster : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private LayerMask interactableLayers = -1;
        [SerializeField] private LayerMask occlusionLayers = Physics.DefaultRaycastLayers;

        private IInteractable _hovered;
        private BCCameraController _cameraController;
        private IHoldInteractable _heldInteractable;
        private float _holdElapsed;
        private bool _holdCompleted;
        private bool _holdStartedOverTarget;

        private void Start()
        {
            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
            }

            _cameraController = FindFirstObjectByType<BCCameraController>();
        }

        private void Update()
        {
            if (raycastCamera == null)
            {
                return;
            }

            Ray ray = raycastCamera.ScreenPointToRay(Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : UnityEngine.Input.mousePosition);
            bool hasHit = TryGetVisibleInteractable(ray, out _, out IInteractable interactable);

            UpdateHoldState(interactable);

            if (hasHit)
            {
                if (interactable != null && interactable.CanInteract)
                {
                    if (_hovered != interactable)
                    {
                        _hovered?.OnHoverExit();
                        _hovered = interactable;
                        _hovered.OnHoverEnter();
                    }

                    if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !IsOverUI())
                    {
                        if (interactable is IHoldInteractable holdInteractable && holdInteractable.CanHold)
                        {
                            _heldInteractable = holdInteractable;
                            _holdElapsed = 0f;
                            _holdCompleted = false;
                            _holdStartedOverTarget = true;
                            _heldInteractable.OnHoldStart();
                        }
                        else
                        {
                            interactable.OnClick();
                        }
                    }
                }
                else
                {
                    ClearHover();
                }
            }
            else
            {
                ClearHover();
            }
        }

        private bool TryGetVisibleInteractable(Ray ray, out RaycastHit resolvedHit, out IInteractable interactable)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, occlusionLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider collider = hit.collider;
                if (collider == null)
                {
                    continue;
                }

                IInteractable candidate = collider.GetComponentInParent<IInteractable>();
                bool onInteractableLayer = ((1 << collider.gameObject.layer) & interactableLayers.value) != 0;

                if (candidate != null && onInteractableLayer)
                {
                    resolvedHit = hit;
                    interactable = candidate;
                    return true;
                }

                if (!collider.isTrigger)
                {
                    break;
                }
            }

            resolvedHit = default;
            interactable = null;
            return false;
        }

        private void ClearHover()
        {
            if (_hovered != null)
            {
                _hovered.OnHoverExit();
                _hovered = null;
            }
        }

        private void UpdateHoldState(IInteractable currentInteractable)
        {
            if (_heldInteractable == null || Mouse.current == null)
            {
                return;
            }

            bool sameTarget = ReferenceEquals(currentInteractable, _heldInteractable);
            bool buttonHeld = Mouse.current.leftButton.isPressed;

            if (buttonHeld && sameTarget && !_holdCompleted)
            {
                _holdElapsed += Time.deltaTime;
                float progress = _holdElapsed / Mathf.Max(_heldInteractable.HoldDuration, 0.01f);
                _heldInteractable.OnHoldProgress(progress);

                if (progress >= 1f)
                {
                    _holdCompleted = true;
                    _heldInteractable.OnHoldComplete();
                }

                return;
            }

            if (!buttonHeld)
            {
                IHoldInteractable completedTarget = _heldInteractable;
                bool completed = _holdCompleted;
                bool shouldClick = !completed && sameTarget && _holdStartedOverTarget && completedTarget.CanInteract;

                if (!completed)
                {
                    completedTarget.OnHoldCancel();
                }

                _heldInteractable = null;
                _holdElapsed = 0f;
                _holdCompleted = false;
                _holdStartedOverTarget = false;

                if (shouldClick && !IsOverUI())
                {
                    completedTarget.OnClick();
                }

                return;
            }

            if (!sameTarget)
            {
                _heldInteractable.OnHoldCancel();
                _heldInteractable = null;
                _holdElapsed = 0f;
                _holdCompleted = false;
                _holdStartedOverTarget = false;
            }
        }

        private static bool IsOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        public void NotifyCardSelectedForPlacement()
        {
            _cameraController?.SwitchToPlayerSlots();
        }
    }
}
