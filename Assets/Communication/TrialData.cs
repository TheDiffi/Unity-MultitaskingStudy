using UnityEngine;

public class TrialData
{
    public int TrialNumber;
    public int ColorIndex;
    public bool IsTarget;
    public bool ResponseMade;
    public float ReactionTime;
    public string Result;
    public float Timestamp;

    public override string ToString()
    {
        return JsonUtility.ToJson(this);
    }
}
