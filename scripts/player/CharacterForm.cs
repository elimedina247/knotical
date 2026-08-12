using Godot;

namespace Knotical.Player;

public sealed class CharacterForm
{
    public float LegLength = 0.95f;
    public float ArmLength = 1.05f;
    public float TorsoHeight = 0.5f;
    public float TorsoRadius = 0.19f;
    public float NeckLength = 0.04f;
    public float HeadRadius = 0.21f;
    public float LimbRadius = 0.055f;
    public float HandRadius = 0.085f;
    public float ShoulderWidth = 0.85f;
    public float HipWidth = 0.55f;
    public float ShoulderDrop = 0.09f;
    public float EyeSpacing = 0.085f;
    public float EyeHeight = 0.03f;
    public float EyeRadius = 0.055f;
    public Vector3 FootSize = new(0.14f, 0.08f, 0.26f);
    public int Segments = 3;

    public float HipY => LegLength;
    public float ShoulderY => HipY + TorsoHeight - ShoulderDrop;
    public float TorsoCenterY => HipY + TorsoHeight * 0.5f;
    public float NeckY => HipY + TorsoHeight;
    public float HeadCenterY => NeckY + NeckLength + HeadRadius;

    public float SpineLowY => HipY + TorsoRadius;
    public float SpineHighY => Mathf.Max(SpineLowY, NeckY - TorsoRadius);

    public Vector3 Shoulder(float side) => new(TorsoRadius * ShoulderWidth * side, ShoulderY, 0f);

    public Vector3 Hip(float side) => new(TorsoRadius * HipWidth * side, HipY, 0f);

    public Vector3 Root(int limb) => limb switch
    {
        0 => Shoulder(-1f),
        1 => Shoulder(1f),
        2 => Hip(-1f),
        _ => Hip(1f),
    };

    public float Reach(int limb) => limb < 2 ? ArmLength : LegLength;

    public bool IsArm(int limb) => limb < 2;
}
