using UnityEngine;

/// <summary>
/// Procedural "acting" for a Synty character (no animation clips needed): poses the arms down from the T-pose,
/// then layers breathing, idle head movement and emotion-driven body language on top every frame.
/// Offsets are applied in the character's own space, so the odd local axes of the Synty rig don't matter.
/// </summary>
public class SuspectRig
{
    readonly Transform root, spine2, spine3, neck, head, shoulderL, shoulderR, elbowL, elbowR, jaw, brows;
    readonly Quaternion baseSpine2, baseSpine3, baseNeck, baseHead, baseJaw;
    readonly Vector3 baseBrows;
    readonly float seed;

    public SuspectRig(GameObject character, float armDown = 74f, float elbowBend = 14f)
    {
        root = character.transform;
        spine2 = Find("Spine_02");
        spine3 = Find("Spine_03");
        neck = Find("Neck");
        head = Find("Head");
        shoulderL = Find("Shoulder_L");
        shoulderR = Find("Shoulder_R");
        elbowL = Find("Elbow_L");
        elbowR = Find("Elbow_R");
        jaw = Find("Jaw");
        brows = Find("Eyebrows");
        seed = Random.value * 10f;

        // Arms down: rotate each upper arm around the character's forward axis, away from the side it sticks out to.
        PoseArm(shoulderL, elbowL, armDown, elbowBend);
        PoseArm(shoulderR, elbowR, armDown, elbowBend);

        baseSpine2 = spine2 ? spine2.localRotation : Quaternion.identity;
        baseSpine3 = spine3 ? spine3.localRotation : Quaternion.identity;
        baseNeck = neck ? neck.localRotation : Quaternion.identity;
        baseHead = head ? head.localRotation : Quaternion.identity;
        baseJaw = jaw ? jaw.localRotation : Quaternion.identity;
        baseBrows = brows ? brows.localPosition : Vector3.zero;
    }

    Transform Find(string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    void PoseArm(Transform shoulder, Transform elbow, float down, float bend)
    {
        if (shoulder == null) return;
        float side = Mathf.Sign(root.InverseTransformPoint(shoulder.position + (elbow ? elbow.position - shoulder.position : Vector3.right)).x);
        Rotate(shoulder, new Vector3(0, 0, -side * down));
        if (elbow != null) Rotate(elbow, new Vector3(bend, 0, 0));
    }

    /// <summary>Rotates a bone by euler degrees expressed in the character's local frame.</summary>
    void Rotate(Transform bone, Vector3 euler)
    {
        var r = root.rotation;
        bone.rotation = r * Quaternion.Euler(euler) * Quaternion.Inverse(r) * bone.rotation;
    }

    void Reset(Transform bone, Quaternion local)
    {
        if (bone != null) bone.localRotation = local;
    }

    /// <param name="t">time</param>
    /// <param name="emotion">calm | nervous | angry | evasive | broken</param>
    /// <param name="react">0..1 how far into the current reaction (eases in)</param>
    /// <param name="stress">0..100</param>
    /// <param name="recoil">0..1 short flinch after evidence is slammed</param>
    public void Tick(float t, string emotion, float react, int stress, float recoil, float talking = 0f)
    {
        t += seed;
        Reset(jaw, baseJaw);
        Reset(spine2, baseSpine2);
        Reset(spine3, baseSpine3);
        Reset(neck, baseNeck);
        Reset(head, baseHead);

        float k = Mathf.SmoothStep(0, 1, react);
        float breathRate = 1.4f + stress * 0.025f;

        // Breathing + idle sway.
        if (spine3) Rotate(spine3, new Vector3(Mathf.Sin(t * breathRate) * 1.5f, 0, Mathf.Sin(t * 0.5f) * 1.2f));
        if (neck) Rotate(neck, new Vector3(Mathf.Sin(t * 0.37f) * 2.5f, Mathf.Sin(t * 0.23f) * 9f, Mathf.Sin(t * 0.29f) * 2f));

        switch (emotion)
        {
            case "nervous":
                if (head) Rotate(head, new Vector3(-3f * k, Mathf.Sin(t * 13f) * 2.2f * k, 0));
                if (spine2) Rotate(spine2, new Vector3(-4f * k, 0, 0));
                break;
            case "angry":
                if (spine2) Rotate(spine2, new Vector3(9f * k, 0, 0));
                if (head) Rotate(head, new Vector3(7f * k, 0, Mathf.Sin(t * 2f) * 2f * k));
                break;
            case "evasive":
                if (neck) Rotate(neck, new Vector3(4f * k, 24f * k, 0));
                if (spine3) Rotate(spine3, new Vector3(0, 8f * k, 0));
                break;
            case "broken":
                if (spine2) Rotate(spine2, new Vector3(14f * k, 0, 0));
                if (head) Rotate(head, new Vector3(22f * k, 0, 6f * k));
                break;
            default:
                if (head) Rotate(head, new Vector3(0, 0, Mathf.Sin(t * 0.8f) * 3f));
                break;
        }

        // Talking: the jaw flaps in a syllable-ish rhythm while the reply types out.
        if (talking > 0 && jaw != null)
        {
            float open = Mathf.Max(0, Mathf.Sin(t * 17f) * 0.6f + Mathf.Sin(t * 9.3f) * 0.5f);
            float fade = Mathf.Clamp01(talking * 4f);
            Rotate(jaw, new Vector3(open * 22f * fade, 0, 0));
            if (head) Rotate(head, new Vector3(Mathf.Sin(t * 7.5f) * 2.5f * fade, Mathf.Sin(t * 3.1f) * 3f * fade, 0));
        }

        // Eyebrows: raised when nervous/evasive, lowered when angry or broken.
        if (brows != null)
        {
            float up = emotion switch { "nervous" => 0.012f, "evasive" => 0.008f, "angry" => -0.01f, "broken" => -0.006f, _ => 0f };
            brows.position = brows.parent.TransformPoint(baseBrows) + root.up * up * k;
        }

        // Flinch backwards when evidence hits the table.
        if (recoil > 0 && spine2) Rotate(spine2, new Vector3(-12f * recoil, 0, Mathf.Sin(recoil * 30f) * 2f * recoil));
    }
}
