using UnityEngine;

[System.Serializable]
public class RootMotifData
{
    public int letterIndex;
    public string letterName;
    public float angle;
    public Vector2 localPosition;
    public float targetRadius;
    public float scaleRatio;
}

[RequireComponent(typeof(RootKochLayoutSettings))]
public class RootMotifLayout : MonoBehaviour
{
    [Header("Calculated Root Motifs")]
    [SerializeField]
    private RootMotifData[] motifs =
        new RootMotifData[6];

    private RootKochLayoutSettings layoutSettings;

    public int MotifCount
    {
        get
        {
            return motifs.Length;
        }
    }

    private void Awake()
    {
        layoutSettings =
            GetComponent<RootKochLayoutSettings>();
    }

    private void Start()
    {
        CalculateMotifs();
    }

    //Calculate & store root-level motif data (positions, angles, radii, scale ratios) around the snowflake based on RootKochLayoutSettings
    [ContextMenu("Calculate Motifs")]
    public void CalculateMotifs()
    {
        EnsureReferences();

        if(layoutSettings == null)
        {
            Debug.LogError(
                "RootKochLayoutSettings is missing.",
                this
            );

            return;
        }

        if(motifs == null ||
            motifs.Length !=
            FractalNode.LetterNames.Length)
        {
            motifs =
                new RootMotifData[
                    FractalNode.LetterNames.Length
                ];
        }

        float rootRadius =
            layoutSettings.snowflakeCircumradius;

        float orbitRadius =
            rootRadius *
            layoutSettings.motifOrbitRadiusRatio;

        float targetRadius =
            rootRadius *
            layoutSettings.motifRadiusRatio;

        for(int i = 0; i < motifs.Length; i++)
        {
            float angle =
                layoutSettings.motifAngleOffset
                + i * 60f;

            Vector2 direction =
                new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

            Vector2 localPosition =
                layoutSettings.center
                + direction * orbitRadius;

            RootMotifData motif =
                new RootMotifData();

            motif.letterIndex =
                i;

            motif.letterName =
                FractalNode.LetterNames[i];

            motif.angle =
                angle;

            motif.localPosition =
                localPosition;

            motif.targetRadius =
                targetRadius;

            motif.scaleRatio =
                layoutSettings.motifRadiusRatio;

            motifs[i] =
                motif;
        }
    }

    //Helper - return RootMotifData for given letter index, return null if index is invalid
    public RootMotifData GetMotif(
        int letterIndex)
    {
        EnsureLayoutExists();

        if(letterIndex < 0 ||
            letterIndex >= motifs.Length)
        {
            Debug.LogError(
                "Invalid root motif index: " +
                letterIndex,
                this
            );

            return null;
        }

        return motifs[letterIndex];
    }

    //Helper - convert a motif’s local pos into world space for specified letter index
    public Vector3 GetMotifWorldPosition(
        int letterIndex)
    {
        RootMotifData motif =
            GetMotif(letterIndex);

        if(motif == null)
            return transform.position;

        return transform.TransformPoint(
            motif.localPosition
        );
    }

    //Helper - returns motif’s radius in world units 
    public float GetMotifWorldRadius(
        int letterIndex)
    {
        RootMotifData motif =
            GetMotif(letterIndex);

        if(motif == null)
            return 0f;

        float averageScale =
            (
                Mathf.Abs(transform.lossyScale.x)
                +
                Mathf.Abs(transform.lossyScale.y)
            ) * 0.5f;

        return motif.targetRadius *
            averageScale;
    }

    //Helper - returns motif’s world-space angle via combining its stored angle with root transform’s Z rotation
    public float GetMotifWorldAngle(
        int letterIndex)
    {
        RootMotifData motif =
            GetMotif(letterIndex);

        if(motif == null)
            return 0f;

        float transformAngle =
            transform.eulerAngles.z;

        return motif.angle +
            transformAngle;
    }

    //Helper - fetch & cache RootKochLayoutSettings component if it has not been assigned yet
    private void EnsureReferences()
    {
        if(layoutSettings == null)
        {
            layoutSettings =
                GetComponent<RootKochLayoutSettings>();
        }
    }

    //Helper - ensure motifs array is initialized/populated, recalculate motifs if data missing or out of sync
    private void EnsureLayoutExists()
    {
        EnsureReferences();

        if(motifs == null ||
            motifs.Length !=
            FractalNode.LetterNames.Length)
        {
            CalculateMotifs();
        }

        if(motifs.Length > 0 &&
            motifs[0] == null)
        {
            CalculateMotifs();
        }
    }
}