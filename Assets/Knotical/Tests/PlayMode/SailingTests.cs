using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Knotical.PlayTests
{
    public class SailingTests
    {
        private const string ScenePath = "Assets/Knotical/Scenes/Main.unity";

        [UnityTest]
        public IEnumerator FullSailDownwindThenHardTurn()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var motor = Object.FindAnyObjectByType<BoatMotor>();
            Assert.IsNotNull(motor, "no BoatMotor in Main scene");
            var rig = motor.GetComponent<SailRig>();
            Assert.IsNotNull(rig, "no SailRig on the boat");
            var hull = motor.GetComponent<Hull>();
            Rigidbody body = motor.GetComponent<Rigidbody>();

            yield return Settle(3f);

            motor.PlayerControlled = false;
            motor.Throttle = 0f;
            motor.Steer = 0f;
            Vector2 wind = Wind.Velocity;
            var downwind = new Vector3(wind.x, 0f, wind.y).normalized;
            body.rotation = Quaternion.LookRotation(downwind, Vector3.up);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            motor.SetGear(3);

            float elapsed = 0f;
            float nextLog = 5f;
            float peakSpeed = 0f;
            while (elapsed < 25f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                peakSpeed = Mathf.Max(peakSpeed, motor.ForwardSpeed);
                if (elapsed >= nextLog)
                {
                    nextLog += 5f;
                    Debug.Log($"Sail: t={elapsed:F0}s speed={motor.ForwardSpeed:F2} m/s wind={Wind.Speed:F1} m/s offWind={rig.OffWindDeg:F0} deg deploy={rig.Deployment:F2} heel={rig.HeelDeg:F1} deg slip={(hull != null ? hull.Slip : 0f):F2} m/s");
                }
            }
            float straightSpeed = motor.ForwardSpeed;
            Debug.Log($"Sail: downwind full sail peak {peakSpeed:F2} m/s, settled {straightSpeed:F2} m/s at wind {Wind.Speed:F1} m/s");

            motor.Steer = 1f;
            float heading = Heading(body);
            float turned = 0f;
            float minSpeed = straightSpeed;
            float peakHeel = 0f;
            float peakYawRate = 0f;
            float time90 = -1f;
            elapsed = 0f;
            while (elapsed < 15f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float now = Heading(body);
                turned += Mathf.DeltaAngle(heading, now);
                heading = now;
                minSpeed = Mathf.Min(minSpeed, motor.ForwardSpeed);
                peakHeel = Mathf.Max(peakHeel, Mathf.Abs(rig.HeelDeg));
                peakYawRate = Mathf.Max(peakYawRate, Mathf.Abs(body.angularVelocity.y) * Mathf.Rad2Deg);
                if (time90 < 0f && Mathf.Abs(turned) >= 90f) time90 = elapsed;
            }
            Debug.Log($"Sail: hard starboard 15 s: turned {turned:F0} deg, 90 deg at {time90:F1} s, peak yaw {peakYawRate:F1} deg/s, speed {straightSpeed:F2} -> min {minSpeed:F2} -> now {motor.ForwardSpeed:F2} m/s, peak heel {peakHeel:F1} deg, rudder {motor.RudderAngle:F2}");

            motor.Steer = 0f;
            motor.SetRudder(0f);
            heading = Heading(body);
            float drift = 0f;
            elapsed = 0f;
            while (elapsed < 6f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float now = Heading(body);
                drift += Mathf.DeltaAngle(heading, now);
                heading = now;
            }
            Debug.Log($"Sail: rudder centred 6 s: drifted {drift:F0} deg, speed {motor.ForwardSpeed:F2} m/s, offWind {rig.OffWindDeg:F0} deg");

            Assert.Greater(straightSpeed, 2f, "boat did not get going under full sail downwind");
            Assert.Greater(Mathf.Abs(turned), 30f, "boat barely turned with full rudder");
            Assert.Less(peakHeel, 45f, "boat heeled past 45 degrees");
        }

        private static IEnumerator Settle(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }
        }

        private static float Heading(Rigidbody body)
        {
            Vector3 f = body.transform.forward;
            return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        }
    }
}
