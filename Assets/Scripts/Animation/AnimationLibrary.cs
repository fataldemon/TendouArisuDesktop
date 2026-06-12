using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AnimationLibrary : MonoBehaviour
{
    public List<AnimationClipData> registry = new List<AnimationClipData>();
    public AnimationClip[] clipReferences = System.Array.Empty<AnimationClip>();
    public BodyEngine bodyEngine;
    public bool allowRootMotion;

    private AnimationClip previewClip;
    private float previewTimer;
    private Animator animator;
    private bool isPreviewing;
    public bool IsPreviewing => isPreviewing;
    private Coroutine previewRoutine;

    private Vector3 savedPos;
    private Quaternion savedRot;
    private Vector3 rootLocalPos;
    private Quaternion rootLocalRot;

    private void RestoreAnimatorState()
    {
        if (animator == null) return;
        animator.transform.position = savedPos;
        animator.transform.rotation = savedRot;
        var root = animator.transform.Find("root");
        if (root != null)
        {
            root.localPosition = rootLocalPos;
            root.localRotation = rootLocalRot;
        }
        animator.enabled = true;
    }

    private void Awake()
    {
        LoadLibrary();
        if (registry.Count == 0)
            ScanAll();
    }

    public void ScanAll()
    {
#if UNITY_EDITOR
        var folders = new[] { "Assets/AnimeGirlIdleAnimations", "Assets/Animvs Game Studio", "Assets/KAWAII_ANIMATIOMS_100", "Assets/Animation", "Assets/ImportedAnimations" };
        var validFolders = new System.Collections.Generic.List<string>();
        foreach (var f in folders)
            if (AssetDatabase.IsValidFolder(f)) validFolders.Add(f);

        if (validFolders.Count == 0) return;
        var guids = AssetDatabase.FindAssets("t:AnimationClip", validFolders.ToArray());
        var existing = new HashSet<string>(registry.Select(r => r.assetPath));

        // Load existing clips from registry to preserve references for build
        var allClips = new List<AnimationClip>();
        foreach (var item in registry)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(item.assetPath);
            if (c != null) allClips.Add(c);
        }

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (existing.Contains(path)) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) continue;
            registry.Add(new AnimationClipData(clip.name, DetectCategory(clip.name, path), clip.length, path));
            allClips.Add(clip);
        }
        clipReferences = allClips.ToArray();
        registry = registry.OrderBy(r => r.category).ThenBy(r => r.name).ToList();
        UnityEditor.EditorUtility.SetDirty(this);
        SaveLibrary();
#endif
    }

#if UNITY_EDITOR
    public void ImportAnimation(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !System.IO.File.Exists(sourcePath)) return;

        string dir = "Assets/ImportedAnimations";
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        string fileName = System.IO.Path.GetFileName(sourcePath);
        string destPath = System.IO.Path.Combine(dir, fileName);

        System.IO.File.Copy(sourcePath, destPath, true);
        UnityEditor.AssetDatabase.ImportAsset(destPath);
        UnityEditor.AssetDatabase.Refresh();

        ScanAll();
    }
#endif

    private string DetectCategory(string clipName, string path)
    {
        string p = path.ToLower();
        if (p.Contains("/idle") || p.Contains("idle")) return "Idle";
        if (p.Contains("/gesture") || p.Contains("gesture")) return "Gesture";
        if (p.Contains("/emote") || p.Contains("react") || p.Contains("layer")) return "Emote";
        if (p.Contains("/combat") || p.Contains("attack") || p.Contains("damage")) return "Combat";
        if (p.Contains("/walk") || p.Contains("/run") || p.Contains("/dash") || p.Contains("/crawl")) return "Move";
        if (p.Contains("/imported")) return "Imported";
        return "Other";
    }

    public List<AnimationClipData> Filter(string category, string search)
    {
        return registry.Where(r =>
            (category == "All" || r.category == category) &&
            (string.IsNullOrEmpty(search) || r.name.ToLower().Contains(search.ToLower()))
        ).ToList();
    }

    public void Preview(AnimationClipData data)
    {
        if (data == null) return;
        AnimationClip? clip = null;
#if UNITY_EDITOR
        clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(data.assetPath);
#else
        foreach (var c in clipReferences)
            if (c.name == data.name) { clip = c; break; }
#endif
        if (clip == null) { Debug.Log("[Preview] clip not found in references: " + data.name + " refs=" + clipReferences.Length); return; }

        if (bodyEngine != null)
            animator = bodyEngine.animator;
        if (animator == null) return;

        if (previewRoutine != null)
        {
            StopCoroutine(previewRoutine);
            previewRoutine = null;
            RestoreAnimatorState();
        }

        var t = animator.transform;
        savedPos = t.position;
        savedRot = t.rotation;
        var rootBone = t.Find("root");
        rootLocalPos = rootBone != null ? rootBone.localPosition : Vector3.zero;
        rootLocalRot = rootBone != null ? rootBone.localRotation : Quaternion.identity;

        previewClip = clip;
        isPreviewing = true;
        previewRoutine = StartCoroutine(PreviewCoroutine(previewClip));
    }

    public void StopPreview()
    {
        if (!isPreviewing) return;
        isPreviewing = false;
        if (previewRoutine != null)
        {
            StopCoroutine(previewRoutine);
            previewRoutine = null;
        }
        RestoreAnimatorState();
        previewClip = null;
    }

    public void PreviewOnce(AnimationClipData data)
    {
        if (data == null) return;
#if UNITY_EDITOR
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(data.assetPath);
        if (clip == null) return;
        Preview(data);
        StartCoroutine(AutoStopRoutine(clip.length + 0.5f));
#endif
    }

    private System.Collections.IEnumerator AutoStopRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        StopPreview();
    }

    private System.Collections.IEnumerator PreviewCoroutine(AnimationClip clip)
    {
        animator.enabled = false;

        var t = animator.transform;
        var rootBone = t.Find("root");
        bool firstFrame = true;
        Vector3 baseSamplePos = Vector3.zero;
        Quaternion baseSampleRot = Quaternion.identity;
        Vector3 baseRootLocalPos = Vector3.zero;
        Quaternion baseRootLocalRot = Quaternion.identity;

        float elapsed = 0f;
        while (isPreviewing)
        {
            clip.SampleAnimation(animator.gameObject, elapsed % clip.length);

            if (firstFrame)
            {
                baseSamplePos = t.position;
                baseSampleRot = t.rotation;
                if (rootBone != null)
                {
                    baseRootLocalPos = rootBone.localPosition;
                    baseRootLocalRot = rootBone.localRotation;
                }
                firstFrame = false;
            }

            if (!allowRootMotion)
            {
                t.position = savedPos;
                t.rotation = savedRot;
                if (rootBone != null)
                {
                    rootBone.localPosition = rootLocalPos;
                    rootBone.localRotation = rootLocalRot;
                }
            }
            else
            {
                t.position = savedPos + (t.position - baseSamplePos);
                t.rotation = savedRot * (Quaternion.Inverse(baseSampleRot) * t.rotation);
                if (rootBone != null)
                {
                    rootBone.localPosition = rootLocalPos + (rootBone.localPosition - baseRootLocalPos);
                    rootBone.localRotation = rootLocalRot * (Quaternion.Inverse(baseRootLocalRot) * rootBone.localRotation);
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreAnimatorState();
        previewClip = null;
    }

    private void Update()
    {
    }

    public List<string> GetCategories()
    {
        var cats = new List<string> { "All" };
        cats.AddRange(registry.Select(r => r.category).Distinct().OrderBy(c => c));
        return cats;
    }

    private void SaveLibrary()
    {
        string json = JsonUtility.ToJson(new RegistryWrapper { items = registry }, true);
        File.WriteAllText(GetPath(), json);
    }

    private void LoadLibrary()
    {
        string path = GetPath();
        if (File.Exists(path))
        {
            var w = JsonUtility.FromJson<RegistryWrapper>(File.ReadAllText(path));
            if (w != null) registry = w.items ?? new List<AnimationClipData>();
        }
#if UNITY_EDITOR
        var clips = new List<AnimationClip>();
        foreach (var item in registry)
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(item.assetPath);
            if (c != null) clips.Add(c);
        }
        if (clips.Count > 0)
        {
            clipReferences = clips.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    private string GetPath() => Path.Combine(Application.persistentDataPath, "animation_library.json");

    [System.Serializable]
    private class RegistryWrapper { public List<AnimationClipData> items; }
}
