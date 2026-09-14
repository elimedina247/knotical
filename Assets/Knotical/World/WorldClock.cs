using System;
using UnityEngine;

namespace Knotical
{
    [DefaultExecutionOrder(-1000)]
    public class WorldClock : MonoBehaviour
    {
        [SerializeField] private OceanSettings ocean;
        [SerializeField] private WindSettings wind;

        private void Awake()
        {
            ConfigureOcean();
            Wind.Configure(ApplyCommandLine(wind));
        }

        private void Update()
        {
            Ocean.Advance(UnityEngine.Time.deltaTime);
            Wind.Advance(UnityEngine.Time.deltaTime);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            ConfigureOcean();
            Wind.Configure(wind);
        }

        public void ConfigureOcean()
        {
            Ocean.Configure(ocean != null ? ocean : ScriptableObject.CreateInstance<OceanSettings>());
        }

        private static WindSettings ApplyCommandLine(WindSettings source)
        {
            WindSettings settings = source != null ? Instantiate(source) : ScriptableObject.CreateInstance<WindSettings>();

            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (!arg.StartsWith("--")) continue;
                string[] pair = arg.Substring(2).Split('=');
                if (pair.Length != 2 || !float.TryParse(pair[1], out float value)) continue;

                switch (pair[0])
                {
                    case "wind": settings.BaseSpeed = value; break;
                    case "winddir": settings.BaseDirectionDeg = value; break;
                    case "gust": settings.Gustiness = value; break;
                }
            }

            return settings;
        }
    }
}
