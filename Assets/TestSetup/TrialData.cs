using UnityEngine;

public class TrialData
{
    public string study_id;
    public int session_number;
    public int stimulus_number;
    public string stimulus_color;
    public bool is_target;
    public bool response_made;
    public bool is_correct;
    public int stimulus_onset_time;
    public int response_time;
    public int reaction_time;
    public int stimulus_end_time;

    public override string ToString()
    {
        return JsonUtility.ToJson(this);
    }
}
