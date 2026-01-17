using System;
using UnityEngine;
using UnityEngine.UI;

namespace MechanicScope.Core
{
    /// <summary>
    /// Provides visual guides to help users align 3D models with real-world objects.
    /// Shows overlays, guides, and alignment indicators.
    /// </summary>
    public class AlignmentGuides : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ARAlignment arAlignment;
        [SerializeField] private Camera arCamera;

        [Header("Guide Prefabs")]
        [SerializeField] private GameObject centerCrosshairPrefab;
        [SerializeField] private GameObject boundingBoxPrefab;
        [SerializeField] private GameObject axisIndicatorPrefab;
        [SerializeField] private LineRenderer alignmentLinePrefab;

        [Header("UI Elements")]
        [SerializeField] private RectTransform guideOverlay;
        [SerializeField] private Image[] cornerBrackets;
        [SerializeField] private Image centerDot;
        [SerializeField] private RectTransform compassRing;
        [SerializeField] private Text instructionText;

        [Header("Colors")]
        [SerializeField] private Color guideColor = new Color(1f, 0.42f, 0.21f, 0.8f);
        [SerializeField] private Color alignedColor = new Color(0.3f, 0.69f, 0.31f, 0.8f);
        [SerializeField] private Color warningColor = new Color(0.96f, 0.76f, 0.05f, 0.8f);

        [Header("Settings")]
        [SerializeField] private bool showCenterCrosshair = true;
        [SerializeField] private bool showBoundingBox = true;
        [SerializeField] private bool showAxisIndicator = true;
        [SerializeField] private bool showAlignmentFeedback = true;
        [SerializeField] private float alignmentThreshold = 0.05f;

        // Events
        public event Action OnAlignmentGood;
        public event Action OnAlignmentPoor;

        // State
        private GameObject centerCrosshair;
        private GameObject boundingBox;
        private GameObject axisIndicator;
        private LineRenderer[] alignmentLines;
        private bool guidesVisible;
        private AlignmentQuality currentQuality = AlignmentQuality.Unknown;

        public enum AlignmentQuality
        {
            Unknown,
            Poor,
            Fair,
            Good,
            Excellent
        }

        private void Start()
        {
            if (arCamera == null)
            {
                arCamera = Camera.main;
            }

            CreateGuideElements();

            if (arAlignment != null)
            {
                arAlignment.OnModelPoseUpdated += OnModelPoseUpdated;
                arAlignment.OnAlignmentLocked += OnAlignmentLocked;
                arAlignment.OnAlignmentUnlocked += OnAlignmentUnlocked;
            }
        }

        private void OnDestroy()
        {
            if (arAlignment != null)
            {
                arAlignment.OnModelPoseUpdated -= OnModelPoseUpdated;
                arAlignment.OnAlignmentLocked -= OnAlignmentLocked;
                arAlignment.OnAlignmentUnlocked -= OnAlignmentUnlocked;
            }

            DestroyGuideElements();
        }

        private void Update()
        {
            if (guidesVisible && arAlignment != null && arAlignment.CurrentModel != null)
            {
                UpdateGuidePositions();
                UpdateAlignmentFeedback();
            }
        }

        private void CreateGuideElements()
        {
            // Create center crosshair
            if (showCenterCrosshair && centerCrosshairPrefab != null)
            {
                centerCrosshair = Instantiate(centerCrosshairPrefab, transform);
            }
            else if (showCenterCrosshair)
            {
                centerCrosshair = CreateDefaultCrosshair();
            }

            // Create bounding box
            if (showBoundingBox && boundingBoxPrefab != null)
            {
                boundingBox = Instantiate(boundingBoxPrefab, transform);
            }
            else if (showBoundingBox)
            {
                boundingBox = CreateDefaultBoundingBox();
            }

            // Create axis indicator
            if (showAxisIndicator && axisIndicatorPrefab != null)
            {
                axisIndicator = Instantiate(axisIndicatorPrefab, transform);
            }
            else if (showAxisIndicator)
            {
                axisIndicator = CreateDefaultAxisIndicator();
            }

            // Initially hide all guides
            SetGuidesActive(false);
        }

        private void DestroyGuideElements()
        {
            if (centerCrosshair != null) Destroy(centerCrosshair);
            if (boundingBox != null) Destroy(boundingBox);
            if (axisIndicator != null) Destroy(axisIndicator);

            if (alignmentLines != null)
            {
                foreach (var line in alignmentLines)
                {
                    if (line != null) Destroy(line.gameObject);
                }
            }
        }

        private GameObject CreateDefaultCrosshair()
        {
            GameObject crosshair = new GameObject("Crosshair");
            crosshair.transform.SetParent(transform);

            // Create horizontal and vertical lines
            for (int i = 0; i < 2; i++)
            {
                GameObject line = new GameObject($"Line{i}");
                line.transform.SetParent(crosshair.transform);

                LineRenderer lr = line.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = lr.endColor = guideColor;
                lr.startWidth = lr.endWidth = 0.002f;
                lr.positionCount = 2;
                lr.useWorldSpace = false;

                float size = 0.02f;
                if (i == 0)
                {
                    lr.SetPosition(0, new Vector3(-size, 0, 0));
                    lr.SetPosition(1, new Vector3(size, 0, 0));
                }
                else
                {
                    lr.SetPosition(0, new Vector3(0, -size, 0));
                    lr.SetPosition(1, new Vector3(0, size, 0));
                }
            }

            return crosshair;
        }

        private GameObject CreateDefaultBoundingBox()
        {
            GameObject box = new GameObject("BoundingBox");
            box.transform.SetParent(transform);

            // Create 12 edges of a cube
            LineRenderer[] edges = new LineRenderer[12];

            for (int i = 0; i < 12; i++)
            {
                GameObject edge = new GameObject($"Edge{i}");
                edge.transform.SetParent(box.transform);

                LineRenderer lr = edge.AddComponent<LineRenderer>();
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = lr.endColor = guideColor;
                lr.startWidth = lr.endWidth = 0.001f;
                lr.positionCount = 2;
                lr.useWorldSpace = true;

                edges[i] = lr;
            }

            return box;
        }

        private GameObject CreateDefaultAxisIndicator()
        {
            GameObject indicator = new GameObject("AxisIndicator");
            indicator.transform.SetParent(transform);

            // X axis (red)
            CreateAxisLine(indicator.transform, Vector3.right, Color.red, "X");
            // Y axis (green)
            CreateAxisLine(indicator.transform, Vector3.up, Color.green, "Y");
            // Z axis (blue)
            CreateAxisLine(indicator.transform, Vector3.forward, Color.blue, "Z");

            return indicator;
        }

        private void CreateAxisLine(Transform parent, Vector3 direction, Color color, string name)
        {
            GameObject line = new GameObject($"Axis_{name}");
            line.transform.SetParent(parent);

            LineRenderer lr = line.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = color;
            lr.startWidth = 0.003f;
            lr.endWidth = 0.001f;
            lr.positionCount = 2;
            lr.useWorldSpace = false;

            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, direction * 0.1f);
        }

        /// <summary>
        /// Shows alignment guides.
        /// </summary>
        public void ShowGuides()
        {
            guidesVisible = true;
            SetGuidesActive(true);
            UpdateInstruction("Align the model with your engine");
        }

        /// <summary>
        /// Hides alignment guides.
        /// </summary>
        public void HideGuides()
        {
            guidesVisible = false;
            SetGuidesActive(false);
        }

        private void SetGuidesActive(bool active)
        {
            if (centerCrosshair != null) centerCrosshair.SetActive(active && showCenterCrosshair);
            if (boundingBox != null) boundingBox.SetActive(active && showBoundingBox);
            if (axisIndicator != null) axisIndicator.SetActive(active && showAxisIndicator);

            if (guideOverlay != null) guideOverlay.gameObject.SetActive(active);
        }

        private void UpdateGuidePositions()
        {
            if (arAlignment?.CurrentModel == null) return;

            Transform model = arAlignment.CurrentModel.transform;

            // Update crosshair to center of model
            if (centerCrosshair != null)
            {
                centerCrosshair.transform.position = model.position;
                centerCrosshair.transform.LookAt(arCamera.transform);
            }

            // Update axis indicator
            if (axisIndicator != null)
            {
                axisIndicator.transform.position = model.position;
                axisIndicator.transform.rotation = model.rotation;
            }

            // Update bounding box
            if (boundingBox != null)
            {
                UpdateBoundingBox(model);
            }
        }

        private void UpdateBoundingBox(Transform model)
        {
            // Calculate bounds of the model
            Bounds bounds = CalculateModelBounds(model.gameObject);

            // Get the 8 corners of the bounding box
            Vector3[] corners = new Vector3[8];
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            corners[0] = new Vector3(min.x, min.y, min.z);
            corners[1] = new Vector3(max.x, min.y, min.z);
            corners[2] = new Vector3(max.x, min.y, max.z);
            corners[3] = new Vector3(min.x, min.y, max.z);
            corners[4] = new Vector3(min.x, max.y, min.z);
            corners[5] = new Vector3(max.x, max.y, min.z);
            corners[6] = new Vector3(max.x, max.y, max.z);
            corners[7] = new Vector3(min.x, max.y, max.z);

            // Update the 12 edge lines
            LineRenderer[] edges = boundingBox.GetComponentsInChildren<LineRenderer>();
            if (edges.Length >= 12)
            {
                // Bottom face
                edges[0].SetPositions(new[] { corners[0], corners[1] });
                edges[1].SetPositions(new[] { corners[1], corners[2] });
                edges[2].SetPositions(new[] { corners[2], corners[3] });
                edges[3].SetPositions(new[] { corners[3], corners[0] });

                // Top face
                edges[4].SetPositions(new[] { corners[4], corners[5] });
                edges[5].SetPositions(new[] { corners[5], corners[6] });
                edges[6].SetPositions(new[] { corners[6], corners[7] });
                edges[7].SetPositions(new[] { corners[7], corners[4] });

                // Vertical edges
                edges[8].SetPositions(new[] { corners[0], corners[4] });
                edges[9].SetPositions(new[] { corners[1], corners[5] });
                edges[10].SetPositions(new[] { corners[2], corners[6] });
                edges[11].SetPositions(new[] { corners[3], corners[7] });
            }
        }

        private Bounds CalculateModelBounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(model.transform.position, Vector3.one * 0.1f);
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }

        private void UpdateAlignmentFeedback()
        {
            if (!showAlignmentFeedback) return;

            AlignmentQuality quality = CalculateAlignmentQuality();

            if (quality != currentQuality)
            {
                currentQuality = quality;
                UpdateGuideColors(quality);
                UpdateInstructionForQuality(quality);

                if (quality == AlignmentQuality.Good || quality == AlignmentQuality.Excellent)
                {
                    OnAlignmentGood?.Invoke();
                }
                else if (quality == AlignmentQuality.Poor)
                {
                    OnAlignmentPoor?.Invoke();
                }
            }
        }

        private AlignmentQuality CalculateAlignmentQuality()
        {
            if (arAlignment?.CurrentModel == null || arCamera == null)
            {
                return AlignmentQuality.Unknown;
            }

            Transform model = arAlignment.CurrentModel.transform;

            // Check distance from camera
            float distance = Vector3.Distance(model.position, arCamera.transform.position);
            if (distance < 0.2f || distance > 2f)
            {
                return AlignmentQuality.Poor;
            }

            // Check if model is in view
            Vector3 screenPoint = arCamera.WorldToViewportPoint(model.position);
            if (screenPoint.x < 0.1f || screenPoint.x > 0.9f ||
                screenPoint.y < 0.1f || screenPoint.y > 0.9f ||
                screenPoint.z < 0)
            {
                return AlignmentQuality.Poor;
            }

            // Check tracking quality
            if (!arAlignment.IsTracking)
            {
                return AlignmentQuality.Fair;
            }

            // If all checks pass
            if (screenPoint.x > 0.3f && screenPoint.x < 0.7f &&
                screenPoint.y > 0.3f && screenPoint.y < 0.7f &&
                distance > 0.3f && distance < 1f)
            {
                return AlignmentQuality.Excellent;
            }

            return AlignmentQuality.Good;
        }

        private void UpdateGuideColors(AlignmentQuality quality)
        {
            Color color = quality switch
            {
                AlignmentQuality.Excellent => alignedColor,
                AlignmentQuality.Good => alignedColor,
                AlignmentQuality.Fair => warningColor,
                AlignmentQuality.Poor => guideColor,
                _ => guideColor
            };

            // Update all line renderers
            LineRenderer[] lines = GetComponentsInChildren<LineRenderer>();
            foreach (var line in lines)
            {
                // Skip axis indicator colors
                if (line.transform.parent?.name == "AxisIndicator") continue;

                line.startColor = line.endColor = color;
            }

            // Update UI elements
            if (cornerBrackets != null)
            {
                foreach (var bracket in cornerBrackets)
                {
                    if (bracket != null) bracket.color = color;
                }
            }

            if (centerDot != null)
            {
                centerDot.color = color;
            }
        }

        private void UpdateInstructionForQuality(AlignmentQuality quality)
        {
            string instruction = quality switch
            {
                AlignmentQuality.Excellent => "Perfect alignment! Tap Lock when ready.",
                AlignmentQuality.Good => "Good alignment. Fine-tune if needed.",
                AlignmentQuality.Fair => "Move closer and center the model.",
                AlignmentQuality.Poor => "Model not visible. Point camera at engine.",
                _ => "Align the model with your engine."
            };

            UpdateInstruction(instruction);
        }

        private void UpdateInstruction(string text)
        {
            if (instructionText != null)
            {
                instructionText.text = text;
            }
        }

        private void OnModelPoseUpdated(Pose pose)
        {
            // Guide positions are updated in Update()
        }

        private void OnAlignmentLocked()
        {
            HideGuides();
        }

        private void OnAlignmentUnlocked()
        {
            ShowGuides();
        }

        /// <summary>
        /// Gets the current alignment quality.
        /// </summary>
        public AlignmentQuality GetAlignmentQuality()
        {
            return currentQuality;
        }

        /// <summary>
        /// Sets whether specific guide types are visible.
        /// </summary>
        public void SetGuideVisibility(bool crosshair, bool bounding, bool axis)
        {
            showCenterCrosshair = crosshair;
            showBoundingBox = bounding;
            showAxisIndicator = axis;

            if (guidesVisible)
            {
                SetGuidesActive(true);
            }
        }
    }
}
