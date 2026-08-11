using UnityEngine;

[System.Serializable]
public class RoomMotifData
{
    public int letterIndex;
    public string letterName;
    public float angle;
    public Vector2 localPosition;
    public float targetRadius;
    public float scaleRatio;
}

[RequireComponent(typeof(RoomKochLayoutSettings))]
public class RoomMotifLayout : MonoBehaviour
{
    [Header("Calculated Child Motifs")]
    [SerializeField]
    private RoomMotifData[] motifs =
        new RoomMotifData[3];

    private RoomKochLayoutSettings layoutSettings;

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
            GetComponent<RoomKochLayoutSettings>();
    }

    private void Start()
    {
        CalculateMotifs();
    }

    //Calculate/store the three child motifs for this room (pos, angle, radius, letter index) based on RoomKochLayoutSettings
    [ContextMenu("Calculate Motifs")]
    public void CalculateMotifs()
    {
        EnsureReferences();

        if(layoutSettings == null)
        {
            Debug.LogError(
                "RoomKochLayoutSettings is missing.",
                this
            );

            return;
        }

        if(motifs == null ||
            motifs.Length != 3)
        {
            motifs =
                new RoomMotifData[3];
        }

        int rotationSteps =
            GetRotationSteps();

        float rotationAngle =
            rotationSteps * 60f
            + layoutSettings.roomRotationOffset;

        Vector2[] basePoints =
            new Vector2[4];

        for(int i = 0; i < basePoints.Length; i++)
        {
            float angle =
                i * 60f;

            basePoints[i] =
                layoutSettings.center
                + layoutSettings.snowflakeRadius
                * new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );
        }

        Vector2[] rotatedPoints =
            new Vector2[4];

        for(int i = 0; i < rotatedPoints.Length; i++)
        {
            rotatedPoints[i] =
                RotatePoint(
                    basePoints[i],
                    rotationAngle
                );
        }

        int[] childLetters =
            FractalNode.GetChildLetters(
                rotationSteps
            );

        float targetRadius =
            layoutSettings.snowflakeRadius
            * layoutSettings.childMotifRadiusRatio;

        for(int i = 0; i < 3; i++)
        {
            Vector2 start =
                rotatedPoints[i];

            Vector2 end =
                rotatedPoints[i + 1];

            Vector2 motifCenter =
                (start + end) * 0.5f;

            Vector2 edgeDirection =
                end - start;

            float edgeAngle =
                Mathf.Atan2(
                    edgeDirection.y,
                    edgeDirection.x
                ) * Mathf.Rad2Deg;

            int letterIndex =
                childLetters[i];

            RoomMotifData motif =
                new RoomMotifData();

            motif.letterIndex =
                letterIndex;

            motif.letterName =
                FractalNode.LetterNames[
                    letterIndex
                ];

            motif.angle =
                edgeAngle;

            motif.localPosition =
                motifCenter;

            motif.targetRadius =
                targetRadius;

            motif.scaleRatio =
                layoutSettings.childMotifRadiusRatio;

            motifs[i] =
                motif;
        }
    }

    //Helper - return RoomMotifData at given motif array index, return null if out of range
    public RoomMotifData GetMotif(
        int motifIndex)
    {
        EnsureLayoutExists();

        if(motifIndex < 0 ||
            motifIndex >= motifs.Length)
        {
            Debug.LogError(
                "Invalid room motif index: " +
                motifIndex,
                this
            );

            return null;
        }

        return motifs[motifIndex];
    }

    //Helper - return motif whose letterIndex matches given letter, or null if no match
    public RoomMotifData GetMotifForLetter(
        int letterIndex)
    {
        EnsureLayoutExists();

        for(int i = 0; i < motifs.Length; i++)
        {
            if(motifs[i].letterIndex ==
                letterIndex)
            {
                return motifs[i];
            }
        }

        return null;
    }

    //Helper - convert motif’s local pos (by motif index) into world space
    public Vector3 GetMotifWorldPosition(
        int motifIndex)
    {
        RoomMotifData motif =
            GetMotif(motifIndex);

        if(motif == null)
            return transform.position;

        return transform.TransformPoint(
            motif.localPosition
        );
    }

    //Helper - convert motif’s local pos (by letter index) into world space
    public Vector3 GetMotifWorldPositionForLetter(
        int letterIndex)
    {
        RoomMotifData motif =
            GetMotifForLetter(letterIndex);

        if(motif == null)
            return transform.position;

        return transform.TransformPoint(
            motif.localPosition
        );
    }

    //Helper - return motif’s radius in world units
    public float GetMotifWorldRadius(
        int motifIndex)
    {
        RoomMotifData motif =
            GetMotif(motifIndex);

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

    //Helper - return world-space radius of room’s snowflake based on layout settings & current transform scale
    public float GetRoomWorldRadius()
    {
        float averageScale =
            (
                Mathf.Abs(transform.lossyScale.x)
                +
                Mathf.Abs(transform.lossyScale.y)
            ) * 0.5f;

        return layoutSettings.snowflakeRadius *
            averageScale;
    }

    //Helper - return room’s snowflake center in world coords
    public Vector3 GetRoomWorldCenter()
    {
        return transform.TransformPoint(
            layoutSettings.center
        );
    }

    //Helper - calculate how many 60° rotation steps this room uses based on config letter index
    private int GetRotationSteps()
    {
        int roomLetterIndex =
            layoutSettings.roomLetterIndex;

        if(roomLetterIndex < 0 ||
            roomLetterIndex >=
            FractalNode.LetterNames.Length)
        {
            Debug.LogError(
                "Invalid room letter index: " +
                roomLetterIndex,
                this
            );

            return 0;
        }

        return roomLetterIndex;
    }

    //Helper -rotate point around room’s layout center by given angle in degs - return rotated pos
    private Vector2 RotatePoint(
        Vector2 point,
        float angleDegrees)
    {
        float radians =
            angleDegrees * Mathf.Deg2Rad;

        float cos =
            Mathf.Cos(radians);

        float sin =
            Mathf.Sin(radians);

        Vector2 offset =
            point - layoutSettings.center;

        Vector2 rotated =
            new Vector2(
                offset.x * cos
                - offset.y * sin,
                offset.x * sin
                + offset.y * cos
            );

        return layoutSettings.center +
            rotated;
    }

    //Helper - fetch & cache RoomKochLayoutSettings component if required
    private void EnsureReferences()
    {
        if(layoutSettings == null)
        {
            layoutSettings =
                GetComponent<RoomKochLayoutSettings>();
        }
    }

    //Helper - ensure motifs array initialized/populated, recalculaete motifs if data missing
    private void EnsureLayoutExists()
    {
        EnsureReferences();

        if(motifs == null ||
            motifs.Length != 3)
        {
            CalculateMotifs();
            return;
        }

        for(int i = 0; i < motifs.Length; i++)
        {
            if(motifs[i] == null)
            {
                CalculateMotifs();
                return;
            }
        }
    }

    //Helper - return room’s snowflake radius in local space from RoomKochLayoutSettings
    public float GetLocalRoomRadius()
    {
        EnsureReferences();

        if(layoutSettings == null)
            return 0f;

        return layoutSettings.snowflakeRadius;
    }
}