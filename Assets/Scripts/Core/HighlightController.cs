using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MechanicScope.Core
{
    /// <summary>
    /// Controls shader-based highlighting of engine parts.
    /// Supports smooth transitions, pulsing effects, and multi-part highlighting.
    /// </summary>
    public class HighlightController : MonoBehaviour
    {
        [Header("Materials")]
        [SerializeField] private Shader highlightShader;
        [SerializeField] private Material defaultMaterial;

        [Header("Highlight Colors")]
        [SerializeField] private Color primaryHighlightColor = new Color(1f, 0.42f, 0.21f, 1f); // Safety Orange
        [SerializeField] private Color secondaryHighlightColor = new Color(0.29f, 0.56f, 0.64f, 1f); // Steel Blue
        [SerializeField] private Color completedColor = new Color(0.3f, 0.69f, 0.31f, 1f); // Green

        [Header("Animation Settings")]
        [SerializeField] private float highlightTransitionDuration = 0.3f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMinIntensity = 0.5f;
        [SerializeField] private float outlineWidth = 0.02f;

        // Events
        public event Action<string> OnPartHighlighted;
        public event Action<string> OnPartUnhighlighted;

        // Tracked state
        private Dictionary<string, HighlightState> highlightedParts = new Dictionary<string, HighlightState>();
        private Dictionary<string, Material> originalMaterials = new Dictionary<string, Material>();
        private Dictionary<string, Material> highlightMaterials = new Dictionary<string, Material>();
        private GameObject currentModel;

        // Shader property IDs for performance
        private static readonly int HighlightIntensityId = Shader.PropertyToID("_HighlightIntensity");
        private static readonly int HighlightColorId = Shader.PropertyToID("_HighlightColor");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int PulseMinId = Shader.PropertyToID("_PulseMin");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private class HighlightState
        {
            public string PartId;
            public HighlightType Type;
            public float TargetIntensity;
            public float CurrentIntensity;
            public Coroutine TransitionCoroutine;
            public Renderer Renderer;
            public int MaterialIndex;
        }

        public enum HighlightType
        {
            None,
            Primary,    // Current active step
            Secondary,  // Available steps
            Completed   // Completed steps
        }

        private void Awake()
        {
            // Try to find shader if not assigned
            if (highlightShader == null)
            {
                highlightShader = Shader.Find("MechanicScope/PartHighlight");
            }
        }

        /// <summary>
        /// Registers a model for highlight management.
        /// </summary>
        public void RegisterModel(GameObject model)
        {
            if (currentModel != null)
            {
                ClearAllHighlights();
                CleanupMaterials();
            }

            currentModel = model;
            CacheOriginalMaterials(model);
        }

        /// <summary>
        /// Unregisters the current model and cleans up.
        /// </summary>
        public void UnregisterModel()
        {
            ClearAllHighlights();
            CleanupMaterials();
            currentModel = null;
        }

        private void CacheOriginalMaterials(GameObject model)
        {
            originalMaterials.Clear();

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                string key = GetRendererKey(renderer);
                if (renderer.material != null)
                {
                    originalMaterials[key] = renderer.material;
                }
            }
        }

        private void CleanupMaterials()
        {
            foreach (var material in highlightMaterials.Values)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
            highlightMaterials.Clear();

            // Restore original materials
            if (currentModel != null)
            {
                Renderer[] renderers = currentModel.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    string key = GetRendererKey(renderer);
                    if (originalMaterials.TryGetValue(key, out Material original))
                    {
                        renderer.material = original;
                    }
                }
            }

            originalMaterials.Clear();
        }

        /// <summary>
        /// Highlights a part with the specified type.
        /// </summary>
        public void HighlightPart(string partId, HighlightType type = HighlightType.Primary)
        {
            if (currentModel == null) return;

            // Find the part in the model
            PartIdentifier part = FindPartInModel(partId);
            if (part == null)
            {
                Debug.LogWarning($"Part not found in model: {partId}");
                return;
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer == null) return;

            // Get or create highlight state
            if (!highlightedParts.TryGetValue(partId, out HighlightState state))
            {
                state = new HighlightState
                {
                    PartId = partId,
                    Renderer = renderer,
                    CurrentIntensity = 0f
                };
                highlightedParts[partId] = state;

                // Apply highlight material
                ApplyHighlightMaterial(renderer, partId);
            }

            // Stop any existing transition
            if (state.TransitionCoroutine != null)
            {
                StopCoroutine(state.TransitionCoroutine);
            }

            // Set highlight parameters
            state.Type = type;
            state.TargetIntensity = type == HighlightType.None ? 0f : 1f;

            // Set colors based on type
            Material mat = GetHighlightMaterial(partId);
            if (mat != null)
            {
                Color highlightColor = GetColorForType(type);
                mat.SetColor(HighlightColorId, highlightColor);
                mat.SetColor(OutlineColorId, highlightColor);
                mat.SetFloat(PulseSpeedId, type == HighlightType.Primary ? pulseSpeed : 0f);
                mat.SetFloat(PulseMinId, pulseMinIntensity);
                mat.SetFloat(OutlineWidthId, type == HighlightType.Primary ? outlineWidth : outlineWidth * 0.5f);
            }

            // Start transition
            state.TransitionCoroutine = StartCoroutine(TransitionHighlight(state));

            OnPartHighlighted?.Invoke(partId);
        }

        /// <summary>
        /// Highlights multiple parts at once.
        /// </summary>
        public void HighlightParts(List<string> partIds, HighlightType type = HighlightType.Primary)
        {
            foreach (string partId in partIds)
            {
                HighlightPart(partId, type);
            }
        }

        /// <summary>
        /// Sets the primary highlighted part (with full pulsing effect).
        /// </summary>
        public void SetPrimaryHighlight(string partId)
        {
            // Reduce other primary highlights to secondary
            foreach (var kvp in highlightedParts)
            {
                if (kvp.Value.Type == HighlightType.Primary && kvp.Key != partId)
                {
                    HighlightPart(kvp.Key, HighlightType.Secondary);
                }
            }

            HighlightPart(partId, HighlightType.Primary);
        }

        /// <summary>
        /// Removes highlight from a part.
        /// </summary>
        public void UnhighlightPart(string partId)
        {
            if (!highlightedParts.TryGetValue(partId, out HighlightState state)) return;

            if (state.TransitionCoroutine != null)
            {
                StopCoroutine(state.TransitionCoroutine);
            }

            state.Type = HighlightType.None;
            state.TargetIntensity = 0f;
            state.TransitionCoroutine = StartCoroutine(TransitionHighlight(state, removeOnComplete: true));

            OnPartUnhighlighted?.Invoke(partId);
        }

        /// <summary>
        /// Removes all highlights.
        /// </summary>
        public void ClearAllHighlights()
        {
            List<string> partIds = new List<string>(highlightedParts.Keys);
            foreach (string partId in partIds)
            {
                UnhighlightPart(partId);
            }
        }

        /// <summary>
        /// Marks a part as completed (shows completed color briefly then removes).
        /// </summary>
        public void MarkPartCompleted(string partId, bool keepHighlight = false)
        {
            if (!highlightedParts.TryGetValue(partId, out HighlightState state))
            {
                HighlightPart(partId, HighlightType.Completed);
                state = highlightedParts[partId];
            }
            else
            {
                state.Type = HighlightType.Completed;
                Material mat = GetHighlightMaterial(partId);
                if (mat != null)
                {
                    mat.SetColor(HighlightColorId, completedColor);
                    mat.SetColor(OutlineColorId, completedColor);
                    mat.SetFloat(PulseSpeedId, 0f);
                }
            }

            if (!keepHighlight)
            {
                // Remove highlight after brief flash
                StartCoroutine(DelayedUnhighlight(partId, 0.5f));
            }
        }

        private IEnumerator DelayedUnhighlight(string partId, float delay)
        {
            yield return new WaitForSeconds(delay);
            UnhighlightPart(partId);
        }

        private IEnumerator TransitionHighlight(HighlightState state, bool removeOnComplete = false)
        {
            float startIntensity = state.CurrentIntensity;
            float elapsed = 0f;

            Material mat = GetHighlightMaterial(state.PartId);

            while (elapsed < highlightTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / highlightTransitionDuration;
                t = EaseInOutQuad(t);

                state.CurrentIntensity = Mathf.Lerp(startIntensity, state.TargetIntensity, t);

                if (mat != null)
                {
                    mat.SetFloat(HighlightIntensityId, state.CurrentIntensity);
                }

                yield return null;
            }

            state.CurrentIntensity = state.TargetIntensity;
            if (mat != null)
            {
                mat.SetFloat(HighlightIntensityId, state.CurrentIntensity);
            }

            if (removeOnComplete && state.TargetIntensity == 0f)
            {
                // Restore original material
                RestoreOriginalMaterial(state.PartId);
                highlightedParts.Remove(state.PartId);
            }

            state.TransitionCoroutine = null;
        }

        private void ApplyHighlightMaterial(Renderer renderer, string partId)
        {
            string key = GetRendererKey(renderer);

            // Create highlight material if needed
            if (!highlightMaterials.ContainsKey(partId))
            {
                Material highlightMat;

                if (highlightShader != null)
                {
                    highlightMat = new Material(highlightShader);

                    // Copy base color from original if available
                    if (originalMaterials.TryGetValue(key, out Material original) && original.HasProperty("_BaseColor"))
                    {
                        highlightMat.SetColor(BaseColorId, original.GetColor("_BaseColor"));
                    }
                    else if (originalMaterials.TryGetValue(key, out Material origColor) && origColor.HasProperty("_Color"))
                    {
                        highlightMat.SetColor(BaseColorId, origColor.GetColor("_Color"));
                    }
                }
                else
                {
                    // Fallback: clone original material and modify
                    highlightMat = new Material(renderer.material);
                }

                highlightMat.SetFloat(HighlightIntensityId, 0f);
                highlightMaterials[partId] = highlightMat;
            }

            renderer.material = highlightMaterials[partId];
        }

        private void RestoreOriginalMaterial(string partId)
        {
            if (!highlightedParts.TryGetValue(partId, out HighlightState state)) return;

            string key = GetRendererKey(state.Renderer);
            if (originalMaterials.TryGetValue(key, out Material original))
            {
                state.Renderer.material = original;
            }

            // Clean up highlight material
            if (highlightMaterials.TryGetValue(partId, out Material highlightMat))
            {
                Destroy(highlightMat);
                highlightMaterials.Remove(partId);
            }
        }

        private Material GetHighlightMaterial(string partId)
        {
            highlightMaterials.TryGetValue(partId, out Material mat);
            return mat;
        }

        private PartIdentifier FindPartInModel(string partId)
        {
            if (currentModel == null) return null;

            PartIdentifier[] parts = currentModel.GetComponentsInChildren<PartIdentifier>();
            foreach (PartIdentifier part in parts)
            {
                if (part.PartId == partId || part.NodeName == partId)
                {
                    return part;
                }
            }

            return null;
        }

        private Color GetColorForType(HighlightType type)
        {
            switch (type)
            {
                case HighlightType.Primary:
                    return primaryHighlightColor;
                case HighlightType.Secondary:
                    return secondaryHighlightColor;
                case HighlightType.Completed:
                    return completedColor;
                default:
                    return Color.white;
            }
        }

        private string GetRendererKey(Renderer renderer)
        {
            return $"{renderer.gameObject.GetInstanceID()}";
        }

        private float EaseInOutQuad(float t)
        {
            return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
        }

        /// <summary>
        /// Gets the current highlight state of a part.
        /// </summary>
        public HighlightType GetPartHighlightType(string partId)
        {
            if (highlightedParts.TryGetValue(partId, out HighlightState state))
            {
                return state.Type;
            }
            return HighlightType.None;
        }

        /// <summary>
        /// Checks if a part is currently highlighted.
        /// </summary>
        public bool IsPartHighlighted(string partId)
        {
            return highlightedParts.ContainsKey(partId) &&
                   highlightedParts[partId].Type != HighlightType.None;
        }

        /// <summary>
        /// Gets all currently highlighted part IDs.
        /// </summary>
        public List<string> GetHighlightedPartIds()
        {
            List<string> result = new List<string>();
            foreach (var kvp in highlightedParts)
            {
                if (kvp.Value.Type != HighlightType.None)
                {
                    result.Add(kvp.Key);
                }
            }
            return result;
        }
    }
}
