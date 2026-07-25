// AR Foundation / AR Subsystems stand-ins for the headless test harness.
//
// AR cannot run without a device, so these types exist purely so ARAlignment and AppInitializer can
// be compile-checked. Every tracking API reports "not available", which is the honest answer here.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.XR.ARSubsystems
{
    public enum TrackingState { None, Limited, Tracking }

    public enum NotTrackingReason
    {
        None, Initializing, Relocalizing, InsufficientLight,
        InsufficientFeatures, ExcessiveMotion, Unsupported, CameraUnavailable
    }

    [Flags]
    public enum TrackableType
    {
        None = 0,
        PlaneWithinPolygon = 1,
        PlaneWithinBounds = 2,
        PlaneWithinInfinity = 4,
        PlaneEstimated = 8,
        Planes = PlaneWithinPolygon | PlaneWithinBounds | PlaneWithinInfinity | PlaneEstimated,
        FeaturePoint = 16,
        Image = 32,
        Face = 64,
        All = ~0
    }

    public enum PlaneAlignment { None, HorizontalUp, HorizontalDown, Vertical, NotAxisAligned }
    public enum PlaneClassification { None, Wall, Floor, Ceiling, Table, Seat, Door, Window }

    public struct TrackableId
    {
        public static TrackableId invalidId => default;
    }
}

namespace UnityEngine.XR.ARFoundation
{
    using UnityEngine.XR.ARSubsystems;

    public enum ARSessionState
    {
        None, Unsupported, CheckingAvailability, NeedsInstall,
        Installing, Ready, SessionInitializing, SessionTracking
    }

    public struct ARSessionStateChangedEventArgs
    {
        public ARSessionState state { get; }
        public ARSessionStateChangedEventArgs(ARSessionState state) { this.state = state; }
    }

    public class ARSession : MonoBehaviour
    {
        public static ARSessionState state { get; set; } = ARSessionState.None;
        public static NotTrackingReason notTrackingReason => NotTrackingReason.Unsupported;
        public static event Action<ARSessionStateChangedEventArgs> stateChanged;

        public bool matchFrameRate { get; set; }
        public void Reset() { }
    }

    public class ARSessionOrigin : MonoBehaviour
    {
        public Camera camera { get; set; }
        public Transform trackablesParent => transform;
        public void MakeContentAppearAt(Transform content, Vector3 position) { }
        public void MakeContentAppearAt(Transform content, Vector3 position, Quaternion rotation) { }
    }

    public class XROrigin : MonoBehaviour
    {
        public Camera Camera { get; set; }
        public Transform TrackablesParent => transform;
    }

    public struct ARRaycastHit
    {
        public Pose pose { get; set; }
        public float distance { get; set; }
        public TrackableType hitType { get; set; }
        public TrackableId trackableId { get; set; }
    }

    public class ARRaycastManager : MonoBehaviour
    {
        public bool Raycast(Vector2 screenPoint, List<ARRaycastHit> hitResults,
                            TrackableType trackableTypes = TrackableType.All)
        {
            hitResults?.Clear();
            return false;
        }

        public bool Raycast(Ray ray, List<ARRaycastHit> hitResults,
                            TrackableType trackableTypes = TrackableType.All)
        {
            hitResults?.Clear();
            return false;
        }
    }

    public class ARTrackable : MonoBehaviour
    {
        public TrackingState trackingState => TrackingState.None;
    }

    public class ARPlane : ARTrackable
    {
        public PlaneAlignment alignment => PlaneAlignment.None;
        public PlaneClassification classification => PlaneClassification.None;
        public Vector3 center => Vector3.zero;
        public Vector2 size => Vector2.zero;
    }

    public class ARPlanesChangedEventArgs
    {
        public List<ARPlane> added { get; set; } = new List<ARPlane>();
        public List<ARPlane> updated { get; set; } = new List<ARPlane>();
        public List<ARPlane> removed { get; set; } = new List<ARPlane>();
    }

    public class ARPlaneManager : MonoBehaviour
    {
        public event Action<ARPlanesChangedEventArgs> planesChanged;
        public bool enabled { get; set; } = true;
        public GameObject planePrefab { get; set; }
        public IEnumerable<ARPlane> trackables => new List<ARPlane>();
        public void SetTrackablesActive(bool active) { }
    }

    public class ARAnchor : ARTrackable { }

    public class ARAnchorManager : MonoBehaviour
    {
        public ARAnchor AddAnchor(Pose pose) => null;
        public bool RemoveAnchor(ARAnchor anchor) => false;
    }

    public class ARCameraManager : MonoBehaviour
    {
        public bool autoFocusEnabled { get; set; }
    }
}
