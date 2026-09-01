using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Softens selected wall renderers only when they sit between the gameplay
/// camera and the player. Station and conversation views always restore the
/// walls to full opacity.
///
/// The assigned wall materials must use a transparent-capable shader/material
/// setup (for URP Lit, set Surface Type to Transparent).
/// </summary>
[DisallowMultipleComponent]
public class CameraWallFader : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Transform target;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private ConversationController conversation;
    [Tooltip("Only these renderers are allowed to fade.")]
    [SerializeField] private Renderer[] occluders;

    [Header("Fade")]
    [Tooltip("The ray aims above the target pivot so the wall fades around the character's body.")]
    [SerializeField] private float targetHeight = 1f;
    [Range(0.05f, 1f)]
    [SerializeField] private float fadedAlpha = 0.3f;
    [Min(0.01f)]
    [SerializeField] private float fadeSeconds = 0.2f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private readonly Dictionary<Renderer, WallState> walls =
        new Dictionary<Renderer, WallState>();
    private readonly HashSet<Renderer> obstructing =
        new HashSet<Renderer>();

    private MaterialPropertyBlock propertyBlock;

    private sealed class WallState
    {
        public int colorProperty;
        public Color originalColor;
        public float currentAlpha;
    }

    private void Awake()
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        propertyBlock = new MaterialPropertyBlock();
        CacheWalls();
    }

    private void LateUpdate()
    {
        obstructing.Clear();

        if (ShouldCheckForObstructions())
            FindObstructingWalls();

        foreach (KeyValuePair<Renderer, WallState> pair in walls)
        {
            Renderer wall = pair.Key;
            WallState state = pair.Value;
            if (wall == null) continue;

            float solidAlpha = state.originalColor.a;
            float goal = obstructing.Contains(wall)
                ? solidAlpha * fadedAlpha
                : solidAlpha;

            float distance = Mathf.Abs(solidAlpha - solidAlpha * fadedAlpha);
            float speed = distance / Mathf.Max(0.01f, fadeSeconds);
            state.currentAlpha = Mathf.MoveTowards(
                state.currentAlpha,
                goal,
                speed * Time.deltaTime);

            ApplyAlpha(wall, state, state.currentAlpha);
        }
    }

    private bool ShouldCheckForObstructions()
    {
        if (gameplayCamera == null || target == null) return false;
        if (interactor != null && interactor.IsAtStation) return false;
        if (conversation != null && conversation.InConversation) return false;
        return true;
    }

    private void FindObstructingWalls()
    {
        Vector3 origin = gameplayCamera.transform.position;
        Vector3 targetPoint = target.position + Vector3.up * targetHeight;
        Vector3 offset = targetPoint - origin;
        float distance = offset.magnitude;
        if (distance <= Mathf.Epsilon) return;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            offset / distance,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.collider.transform;

            foreach (Renderer wall in walls.Keys)
            {
                if (wall == null) continue;

                Transform wallTransform = wall.transform;
                if (hitTransform == wallTransform ||
                    hitTransform.IsChildOf(wallTransform) ||
                    wallTransform.IsChildOf(hitTransform))
                {
                    obstructing.Add(wall);
                }
            }
        }
    }

    private void CacheWalls()
    {
        walls.Clear();
        if (occluders == null) return;

        foreach (Renderer wall in occluders)
        {
            if (wall == null || walls.ContainsKey(wall)) continue;

            Material material = wall.sharedMaterial;
            if (material == null)
            {
                Debug.LogWarning(
                    "CameraWallFader: " + wall.name + " has no material.",
                    wall);
                continue;
            }

            int property = 0;
            if (material.HasProperty(BaseColorId)) property = BaseColorId;
            else if (material.HasProperty(ColorId)) property = ColorId;

            if (property == 0)
            {
                Debug.LogWarning(
                    "CameraWallFader: " + material.name +
                    " has no _BaseColor or _Color property.",
                    wall);
                continue;
            }

            Color original = material.GetColor(property);
            walls.Add(wall, new WallState
            {
                colorProperty = property,
                originalColor = original,
                currentAlpha = original.a
            });

            if (material.HasProperty("_Surface") &&
                material.GetFloat("_Surface") < 0.5f)
            {
                Debug.LogWarning(
                    "CameraWallFader: " + material.name +
                    " is Opaque. Duplicate it and set Surface Type to " +
                    "Transparent before testing the fade.",
                    wall);
            }
        }
    }

    private void ApplyAlpha(Renderer wall, WallState state, float alpha)
    {
        wall.GetPropertyBlock(propertyBlock);

        Color color = state.originalColor;
        color.a = alpha;
        propertyBlock.SetColor(state.colorProperty, color);

        wall.SetPropertyBlock(propertyBlock);
        propertyBlock.Clear();
    }

    private void OnDisable()
    {
        RestoreWalls();
    }

    private void OnDestroy()
    {
        RestoreWalls();
    }

    private void RestoreWalls()
    {
        if (propertyBlock == null) return;

        foreach (KeyValuePair<Renderer, WallState> pair in walls)
        {
            if (pair.Key == null) continue;
            ApplyAlpha(pair.Key, pair.Value, pair.Value.originalColor.a);
        }
    }

    private void OnValidate()
    {
        fadedAlpha = Mathf.Clamp(fadedAlpha, 0.05f, 1f);
        fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
        targetHeight = Mathf.Max(0f, targetHeight);
    }
}
