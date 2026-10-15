using System;
using UnityEngine;

namespace Interstellar.Classes.Menu
{
    public static class MenuSizing
    {
        public const int MinimumPercent = 75;
        public const int MaximumPercent = 125;
        public const int StepPercent = 5;
        private const string PreferenceKey = "Interstellar.Menu.Size";
        private static float scale = 1f;

        public static float Scale
        {
            get => scale;
            set => scale = Normalize(value);
        }

        public static int Percent => (int)Math.Round(scale * 100f);

        public static float Normalize(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return 1f;
            double percent = Math.Max(MinimumPercent, Math.Min(MaximumPercent, (double)value * 100d));
            return (float)(Math.Round(percent / StepPercent, MidpointRounding.AwayFromZero) * StepPercent / 100d);
        }

        public static void Load() => Scale = PlayerPrefs.GetFloat(PreferenceKey, 1f);

        public static void Change(bool increase) => Save(Scale + (increase ? StepPercent : -StepPercent) / 100f);

        public static void Reset() => Save(1f);

        private static void Save(float value)
        {
            Scale = value;
            PlayerPrefs.SetFloat(PreferenceKey, Scale);
            PlayerPrefs.Save();
        }
    }
}
