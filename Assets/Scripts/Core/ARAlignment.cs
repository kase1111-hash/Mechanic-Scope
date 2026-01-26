using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace MechanicScope.Core
{
    /// <summary>
    /// Handles AR session management, 3D model alignment, and part detection via raycasting.
    /// </summary>
    public class ARAlignment : MonoBehaviour
    {
        [Header("AR Components")]
        [SerializeField] private ARSession arSession;
        [SerializeField] private ARSessionOrigin arSessionOrigin;
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private Camera arCamera;

        [Header("Alignment Settings")]
        [SerializeField] private float rotationSpeed = 0.5f;
        [SerializeField] private float scaleSpeed = 0.01f;
        [SerializeField] private float translateSpeed = 0.002f;
        [SerializeField] private float minScale = 0.1f;
        [SerializeField] private float maxScale = 3.0f;

        [Header("State")]
        [SerializeField] private GameObject currentEngineModel;

        // Events
        public event Action<bool> OnTrackingStateChanged;
        public event Action<Pose> OnModelPoseUpdated;
        public event Action<string> OnPartTapped;
        public event Action OnAlignmentLocked;
        public event Action OnAlignmentUnlocked;

        // Properties
        public bool IsTracking { get; private set; }
        public Pose CurrentModelPose => currentEngineModel != null
            ? new Pose(currentEngineModel.transform.position, currentEngineModel.transform.rotation)
            : Pose.identity;
        public float AlignmentConfidence => IsTracking ? 1.0f : 0.0f;
        public bool IsAlignmentLocked { get; private set; }
        public GameObject CurrentModel => currentEngineModel;

        // Private state
        private AlignmentState currentState = AlignmentState.Uninitialized;
        private Vector2 previousTouchPosition;
        private float previousPinchDistance;
        private bool isRotating;
        private bool isScaling;
        private bool isTranslating;
        private string currentEngineId;

        private static readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();

        public enum AlignmentState
        {
            Uninitialized,
            Loading,
            Aligning,
            Locked,
            Paused
        }

        public AlignmentState CurrentState => currentState;

        private void Awake()
        {
            if (arCamera == null)
            {
                arCamera = Camera.main;
                if (arCamera == null)
                {
                    Debug.LogError("ARAlignment: No AR camera assigned and Camera.main is null. AR features will not work correctly.");
                }
            }
        }

        private void Start()
        {
            if (arSession != null)
            {
                ARSession.stateChanged += OnARSessionStateChanged;
            }
        }

        private void OnDestroy()
        {
            ARSession.stateChanged -= OnARSessionStateChanged;
        }

        private void Update()
        {
            if (currentState == AlignmentState.Paused || currentState == AlignmentState.Uninitialized)
                return;

            UpdateTrackingState();

            if (currentEngineModel != null && !IsAlignmentLocked)
            {
                HandleTouchInput();
            }
            else if (currentEngineModel != null && IsAlignmentLocked)
            {
                HandlePartTapInput();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && currentState != AlignmentState.Uninitialized)
            {
                currentState = AlignmentState.Paused;
            }
            else if (!pauseStatus && currentState == AlignmentState.Paused)
            {
                currentState = IsAlignmentLocked ? AlignmentState.Locked : AlignmentState.Aligning;
            }
        }

        private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            bool wasTracking = IsTracking;
            IsTracking = args.state == ARSessionState.SessionTracking;

            if (wasTracking != IsTracking)
            {
                OnTrackingStateChanged?.Invoke(IsTracking);
            }
        }

        private void UpdateTrackingState()
        {
            if (arSession == null) return;

            bool newTrackingState = ARSession.state == ARSessionState.SessionTracking;
            if (newTrackingState != IsTracking)
            {
                IsTracking = newTrackingState;
                OnTrackingStateChanged?.Invoke(IsTracking);
            }
        }

        /// <summary>
        /// Loads an engine model from the given GameObject (instantiated externally).
        /// </summary>
        public void SetEngineModel(GameObject model, string engineId)
        {
            if (currentEngineModel != null)
            {
                Destroy(currentEngineModel);
            }

            currentState = AlignmentState.Loading;
            currentEngineModel = model;
            currentEngineId = engineId;

            if (model != null)
            {
                // Position model in front of camera initially
                if (arCamera != null)
                {
                    model.transform.position = arCamera.transform.position + arCamera.transform.forward * 0.5f;
                    model.transform.rotation = Quaternion.identity;
                }

                // Generate mesh colliders for part detection
                GenerateMeshColliders(model);

                // Load saved alignment if exists
                LoadSavedAlignment(engineId);

                currentState = AlignmentState.Aligning;
                IsAlignmentLocked = false;
            }
        }

        /// <summary>
        /// Generates mesh colliders on all mesh renderers for raycast detection.
        /// </summary>
        private void GenerateMeshColliders(GameObject model)
        {
            if (model == null) return;

            MeshFilter[] meshFilters = model.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                if (meshFilter == null) continue;
                if (meshFilter.sharedMesh == null)
                {
                    Debug.LogWarning($"MeshFilter on '{meshFilter.gameObject.name}' has no mesh assigned, skipping collider generation.");
                    continue;
                }
                if (meshFilter.GetComponent<Collider>() == null)
                {
                    MeshCollider collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = meshFilter.sharedMesh;
                    collider.convex = false;
                }
            }
        }

        /// <summary>
        /// Manually set the model's pose.
        /// </summary>
        public void SetManualAlignment(Pose pose)
        {
            if (currentEngineModel != null && !IsAlignmentLocked)
            {
                currentEngineModel.transform.SetPositionAndRotation(pose.position, pose.rotation);
                OnModelPoseUpdated?.Invoke(pose);
            }
        }

        /// <summary>
        /// Reset alignment to default position.
        /// </summary>
        public void ResetAlignment()
        {
            if (currentEngineModel != null && arCamera != null)
            {
                currentEngineModel.transform.position = arCamera.transform.position + arCamera.transform.forward * 0.5f;
                currentEngineModel.transform.rotation = Quaternion.identity;
                currentEngineModel.transform.localScale = Vector3.one;
                IsAlignmentLocked = false;
                currentState = AlignmentState.Aligning;
                OnAlignmentUnlocked?.Invoke();
            }
        }

        /// <summary>
        /// Lock alignment to prevent accidental changes.
        /// </summary>
        public void LockAlignment()
        {
            if (currentEngineModel != null)
            {
                IsAlignmentLocked = true;
                currentState = AlignmentState.Locked;
                SaveAlignment(currentEngineId);
                OnAlignmentLocked?.Invoke();
            }
        }

        /// <summary>
        /// Unlock alignment to allow adjustments.
        /// </summary>
        public void UnlockAlignment()
        {
            IsAlignmentLocked = false;
            currentState = AlignmentState.Aligning;
            OnAlignmentUnlocked?.Invoke();
        }

        /// <summary>
        /// Convert screen position to a point on the model.
        /// </summary>
        public Vector3 ScreenToModelPoint(Vector2 screenPosition)
        {
            if (arCamera == null) return Vector3.zero;

            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                return hit.point;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Get the part name at a screen position via raycast.
        /// </summary>
        public string GetPartAtScreenPosition(Vector2 screenPosition)
        {
            if (arCamera == null || currentEngineModel == null) return null;

            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Check if hit object is part of our engine model
                if (hit.transform.IsChildOf(currentEngineModel.transform) || hit.transform == currentEngineModel.transform)
                {
                    return hit.transform.name;
                }
            }
            return null;
        }

        private void HandleTouchInput()
        {
            int touchCount = Input.touchCount;

            if (touchCount == 0)
            {
                isRotating = false;
                isScaling = false;
                isTranslating = false;
                return;
            }

            if (touchCount == 1)
            {
                HandleSingleTouch(Input.GetTouch(0));
            }
            else if (touchCount == 2)
            {
                HandleTwoFingerTouch(Input.GetTouch(0), Input.GetTouch(1));
            }
        }

        private void HandleSingleTouch(Touch touch)
        {
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    previousTouchPosition = touch.position;
                    isRotating = true;
                    break;

                case TouchPhase.Moved:
                    if (isRotating && currentEngineModel != null)
                    {
                        Vector2 delta = touch.position - previousTouchPosition;

                        // Rotate around Y axis (horizontal drag) and X axis (vertical drag)
                        float rotationX = -delta.y * rotationSpeed;
                        float rotationY = delta.x * rotationSpeed;

                        currentEngineModel.transform.Rotate(arCamera.transform.up, rotationY, Space.World);
                        currentEngineModel.transform.Rotate(arCamera.transform.right, rotationX, Space.World);

                        previousTouchPosition = touch.position;
                        OnModelPoseUpdated?.Invoke(CurrentModelPose);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isRotating = false;
                    break;
            }
        }

        private void HandleTwoFingerTouch(Touch touch0, Touch touch1)
        {
            if (currentEngineModel == null) return;

            // Calculate pinch distance
            float currentPinchDistance = Vector2.Distance(touch0.position, touch1.position);
            Vector2 centerPoint = (touch0.position + touch1.position) / 2f;

            if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                previousPinchDistance = currentPinchDistance;
                previousTouchPosition = centerPoint;
                isScaling = true;
                isTranslating = true;
                return;
            }

            if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
            {
                // Pinch to scale
                if (isScaling)
                {
                    float pinchDelta = currentPinchDistance - previousPinchDistance;
                    float scaleFactor = 1f + (pinchDelta * scaleSpeed);

                    Vector3 newScale = currentEngineModel.transform.localScale * scaleFactor;
                    newScale.x = Mathf.Clamp(newScale.x, minScale, maxScale);
                    newScale.y = Mathf.Clamp(newScale.y, minScale, maxScale);
                    newScale.z = Mathf.Clamp(newScale.z, minScale, maxScale);

                    currentEngineModel.transform.localScale = newScale;
                    previousPinchDistance = currentPinchDistance;
                }

                // Two-finger drag to translate
                if (isTranslating)
                {
                    Vector2 dragDelta = centerPoint - previousTouchPosition;
                    Vector3 translation = new Vector3(dragDelta.x, dragDelta.y, 0) * translateSpeed;
                    translation = arCamera.transform.TransformDirection(translation);
                    currentEngineModel.transform.position += translation;
                    previousTouchPosition = centerPoint;
                }

                OnModelPoseUpdated?.Invoke(CurrentModelPose);
            }

            if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended)
            {
                isScaling = false;
                isTranslating = false;
            }
        }

        private void HandlePartTapInput()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Ended && touch.deltaTime < 0.2f)
                {
                    string partName = GetPartAtScreenPosition(touch.position);
                    if (!string.IsNullOrEmpty(partName))
                    {
                        OnPartTapped?.Invoke(partName);
                    }
                }
            }

            // Mouse support for editor testing
            #if UNITY_EDITOR
            if (Input.GetMouseButtonUp(0))
            {
                string partName = GetPartAtScreenPosition(Input.mousePosition);
                if (!string.IsNullOrEmpty(partName))
                {
                    OnPartTapped?.Invoke(partName);
                }
            }
            #endif
        }

        private void SaveAlignment(string engineId)
        {
            if (currentEngineModel == null || string.IsNullOrEmpty(engineId)) return;

            Vector3 pos = currentEngineModel.transform.position;
            Quaternion rot = currentEngineModel.transform.rotation;
            Vector3 scale = currentEngineModel.transform.localScale;

            PlayerPrefs.SetFloat($"{engineId}_pos_x", pos.x);
            PlayerPrefs.SetFloat($"{engineId}_pos_y", pos.y);
            PlayerPrefs.SetFloat($"{engineId}_pos_z", pos.z);
            PlayerPrefs.SetFloat($"{engineId}_rot_x", rot.x);
            PlayerPrefs.SetFloat($"{engineId}_rot_y", rot.y);
            PlayerPrefs.SetFloat($"{engineId}_rot_z", rot.z);
            PlayerPrefs.SetFloat($"{engineId}_rot_w", rot.w);
            PlayerPrefs.SetFloat($"{engineId}_scale_x", scale.x);
            PlayerPrefs.SetFloat($"{engineId}_scale_y", scale.y);
            PlayerPrefs.SetFloat($"{engineId}_scale_z", scale.z);
            PlayerPrefs.Save();
        }

        private void LoadSavedAlignment(string engineId)
        {
            if (currentEngineModel == null || string.IsNullOrEmpty(engineId)) return;

            if (!PlayerPrefs.HasKey($"{engineId}_pos_x")) return;

            Vector3 pos = new Vector3(
                PlayerPrefs.GetFloat($"{engineId}_pos_x"),
                PlayerPrefs.GetFloat($"{engineId}_pos_y"),
                PlayerPrefs.GetFloat($"{engineId}_pos_z")
            );

            Quaternion rot = new Quaternion(
                PlayerPrefs.GetFloat($"{engineId}_rot_x"),
                PlayerPrefs.GetFloat($"{engineId}_rot_y"),
                PlayerPrefs.GetFloat($"{engineId}_rot_z"),
                PlayerPrefs.GetFloat($"{engineId}_rot_w")
            );

            Vector3 scale = new Vector3(
                PlayerPrefs.GetFloat($"{engineId}_scale_x"),
                PlayerPrefs.GetFloat($"{engineId}_scale_y"),
                PlayerPrefs.GetFloat($"{engineId}_scale_z")
            );

            currentEngineModel.transform.position = pos;
            currentEngineModel.transform.rotation = rot;
            currentEngineModel.transform.localScale = scale;
        }

        /// <summary>
        /// Clear saved alignment for an engine.
        /// </summary>
        public void ClearSavedAlignment(string engineId)
        {
            string[] keys = { "pos_x", "pos_y", "pos_z", "rot_x", "rot_y", "rot_z", "rot_w", "scale_x", "scale_y", "scale_z" };
            foreach (string key in keys)
            {
                PlayerPrefs.DeleteKey($"{engineId}_{key}");
            }
            PlayerPrefs.Save();
        }
    }
}
