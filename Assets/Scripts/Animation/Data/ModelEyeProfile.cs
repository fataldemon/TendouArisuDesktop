using System;
using System.Collections.Generic;

[Serializable]
public class ModelEyeProfile
{
    public string modelKey;
    public int blinkIndex = -1;
    public int lookLeftIndex = -1;
    public int lookRightIndex = -1;
    public int lookUpIndex = -1;
    public int lookDownIndex = -1;
    public List<int> blinkConflictIndices = new List<int>();
    public float lookStrength = 120f;
    public float headRotationAmount = 10f;
}
