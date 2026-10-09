using UnityEngine;

namespace Hortensia.Narrative
{
    public enum GardenCounter
    {
        Blooms,
        LiesTold,
        PatientsFed
    }

    public sealed class GardenState
    {
        public GardenState(int initialBloomCount)
            : this(initialBloomCount, 0, 0)
        {
        }

        public GardenState(int bloomCount, int liesTold, int patientsFed)
        {
            Restore(bloomCount, liesTold, patientsFed);
        }

        public int BloomCount { get; private set; }
        public int LiesTold { get; private set; }
        public int PatientsFed { get; private set; }

        public void Restore(int bloomCount, int liesTold, int patientsFed)
        {
            BloomCount = Mathf.Max(0, bloomCount);
            LiesTold = Mathf.Max(0, liesTold);
            PatientsFed = Mathf.Max(0, patientsFed);
        }

        public void Adjust(GardenCounter counter, int delta)
        {
            switch (counter)
            {
                case GardenCounter.Blooms:
                    BloomCount = Mathf.Max(0, BloomCount + delta);
                    break;
                case GardenCounter.LiesTold:
                    LiesTold = Mathf.Max(0, LiesTold + delta);
                    break;
                case GardenCounter.PatientsFed:
                    PatientsFed = Mathf.Max(0, PatientsFed + delta);
                    break;
            }
        }
    }
}
