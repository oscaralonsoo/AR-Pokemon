using System;
using UnityEngine;

[Serializable]
public class ThrowBallistics
{
    [SerializeField] private float minFlick = 1.0f;
    [SerializeField] private float maxFlick = 6f;
    [SerializeField] private float minRange = 0.8f;
    [SerializeField] private float maxRange = 3.5f;
    [SerializeField] private float powerCurve = 2f;
    [SerializeField, Range(10f, 70f)] private float loftAngle = 25f;
    [SerializeField] private float maxYaw = 35f;
    [SerializeField] private float spin = 6f;

    [Header("Tiro curvo")]
    [SerializeField] private float curveAcceleration = 2.5f;
    [SerializeField, Range(0f, 1f)] private float aimCompensation = 1f;
    [SerializeField] private bool invertCurve = false;

    public float MinFlick => minFlick;

    public struct Result
    {
        public Vector3 velocity;
        public Vector3 curveAccel;
        public Vector3 spinTorque;
        public float curveAmount;
    }

    public Result Compute(Vector2 v, float curve, Vector3 camForward)
    {
        float power = Mathf.InverseLerp(minFlick, maxFlick, v.y);
        power = Mathf.Pow(power, powerCurve);

        float range = Mathf.Lerp(minRange, maxRange, power);
        float speed = SpeedForRange(range);
        float yaw = Mathf.Clamp(Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg, -maxYaw, maxYaw);

        Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * camForward;
        dir.y = 0f;
        dir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        dir = Quaternion.AngleAxis(-loftAngle, right) * dir;

        Vector3 velocity = dir * speed;

        float sideSign = invertCurve ? 1f : -1f;
        float lateral = sideSign * curve * curveAcceleration;

        var result = new Result
        {
            curveAccel = right * lateral,
            spinTorque = right * -spin,
            curveAmount = 0f
        };

        if (Mathf.Abs(curve) > 0f)
        {
            float g = Physics.gravity.magnitude;
            float flightTime = 2f * speed * Mathf.Sin(loftAngle * Mathf.Deg2Rad) / g;
            velocity += right * (-0.5f * lateral * flightTime * aimCompensation);
            result.curveAmount = Mathf.Abs(curve);
        }

        result.velocity = velocity;
        return result;
    }

    private float SpeedForRange(float range)
    {
        float g = Physics.gravity.magnitude;
        float s = Mathf.Sin(2f * loftAngle * Mathf.Deg2Rad);
        return Mathf.Sqrt(range * g / Mathf.Max(s, 0.1f));
    }
}