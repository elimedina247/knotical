using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Knotical.PlayTests
{
    public class ShotTests
    {
        private const string ScenePath = "Assets/Knotical/Scenes/Main.unity";

        [UnityTest]
        public IEnumerator MainSceneRendersAndBoatFloats()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;

            var motor = Object.FindAnyObjectByType<BoatMotor>();
            Assert.IsNotNull(motor, "no BoatMotor in Main scene");
            var buoyancy = motor.GetComponent<Buoyancy>();
            Assert.IsNotNull(buoyancy, "no Buoyancy on the boat");
            Rigidbody body = buoyancy.Body;

            float elapsed = 0f;
            float maxSpeed = 0f;
            float maxWetness = 0f;
            while (elapsed < 6f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                if (elapsed > 3f) maxSpeed = Mathf.Max(maxSpeed, body.linearVelocity.magnitude);
                if (elapsed > 3f) maxWetness = Mathf.Max(maxWetness, buoyancy.Wetness);
            }

            Vector3 p = body.position;
            float water = Ocean.GetHeight(p);
            float tilt = Vector3.Angle(body.transform.up, Vector3.up);
            Debug.Log($"Shot: boat at y={p.y:F2} water={water:F2} wetness now={buoyancy.Wetness:F2} max={maxWetness:F2} tilt={tilt:F1} deg maxSpeed(3-6s)={maxSpeed:F2} m/s");

            Assert.Less(Mathf.Abs(p.y - water), 3f, "boat is not near the water surface");
            Assert.Less(tilt, 30f, "boat is not upright");
            Assert.Greater(maxWetness, 0.05f, "no pontoon was wet during seconds 3 to 6");

            Vector3 start = body.position;
            motor.PlayerControlled = false;
            motor.Throttle = 0.6f;
            motor.Steer = 0.12f;
            elapsed = 0f;
            float maxTilt = 0f;
            while (elapsed < 10f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                maxTilt = Mathf.Max(maxTilt, Vector3.Angle(body.transform.up, Vector3.up));
            }
            float travelled = Vector3.Distance(start, body.position);
            Debug.Log($"Shot: drove {travelled:F1} m in 10 s, max tilt {maxTilt:F1} deg, speed now {body.linearVelocity.magnitude:F2} m/s");
            Assert.Greater(travelled, 8f, "boat did not move under power");
            Assert.Less(maxTilt, 45f, "boat heeled past 45 degrees under power");

            Capture(Camera.main, "shot_main.png");

            Transform cam = Camera.main.transform;
            Vector3 savedPosition = cam.position;
            Quaternion savedRotation = cam.rotation;
            cam.position = body.position + body.transform.TransformDirection(new Vector3(19f, 6f, -9f));
            cam.LookAt(body.position + Vector3.up * 2f);
            Capture(Camera.main, "shot_boat.png");
            cam.position = body.position + body.transform.TransformDirection(new Vector3(-13f, 6f, 17f));
            cam.LookAt(body.position + Vector3.up * 2f);
            Capture(Camera.main, "shot_bow.png");
            cam.SetPositionAndRotation(savedPosition, savedRotation);

            var surface = Object.FindAnyObjectByType<OceanSurface>();
            Assert.IsNotNull(surface, "no OceanSurface in Main scene");
            foreach ((OceanDebugView view, string file) in new[] { (OceanDebugView.Grey, "shot_grey.png"), (OceanDebugView.Height, "shot_height.png"), (OceanDebugView.Foam, "shot_foam.png"), (OceanDebugView.Normals, "shot_normals.png"), (OceanDebugView.Depth, "shot_depth.png") })
            {
                surface.DebugView = view;
                yield return null;
                yield return null;
                Capture(Camera.main, file);
            }
            surface.DebugView = OceanDebugView.Off;
            Debug.Log($"Shot: sea significant height {Ocean.SignificantHeight:F2} m, {Ocean.WaveSet.Count} waves, peak wavelength {Ocean.PeakWavelength:F0} m");
        }

        private static void Capture(Camera camera, string fileName)
        {
            Assert.IsNotNull(camera, "no main camera");
            const int width = 1280;
            const int height = 720;

            var rt = new RenderTexture(width, height, 24);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = previous;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            string dir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(dir);
            string path = Path.GetFullPath(Path.Combine(dir, fileName));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"Shot: wrote {path}");

            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
