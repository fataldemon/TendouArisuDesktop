using System;

[Serializable]
public class AnimationClipData
{
    public string name;
    public string category;
    public float duration;
    public string assetPath;
    public int actionParam;
    public float blendDuration = 0.35f;

    public AnimationClipData() { }

    public AnimationClipData(string name, string category, float duration, string assetPath)
    {
        this.name = name;
        this.category = category;
        this.duration = duration;
        this.assetPath = assetPath;
    }
}
