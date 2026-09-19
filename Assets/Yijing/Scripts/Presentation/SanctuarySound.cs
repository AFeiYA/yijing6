using System;
using UnityEngine;

namespace Yijing.Presentation
{
    // Original synthesized preview sounds; generated once and released with the scene.
    public sealed class SanctuarySound : MonoBehaviour
    {
        private AudioSource rain, effects, water;
        private AudioClip rainClip, cupClip, chimeClip, pourClip;
        public bool Muted { get; private set; }
        private bool focused = true;
        private void Awake()
        {
            rain = Source(true); effects = Source(false); water = Source(false);
            rainClip = Noise("Soft courtyard rain", 12, 61, false);
            pourClip = Noise("Water into porcelain", 2.5f, 23, true);
            cupClip = Tone("Porcelain touch", .55f, 1370, 2190);
            chimeClip = Tone("Small bronze chime", 3.5f, 659, 1071);
            rain.clip = rainClip; Muted = PlayerPrefs.GetInt("Yijing.SoundMuted", 0) == 1;
            ApplyVolume(); rain.Play();
        }
        private AudioSource Source(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0; return source;
        }
        public void SetMuted(bool value) { Muted = value; PlayerPrefs.SetInt("Yijing.SoundMuted", value ? 1 : 0); PlayerPrefs.Save(); ApplyVolume(); }
        private void ApplyVolume() { bool audible = focused && !Muted; rain.volume = audible ? .24f : 0; effects.volume = audible ? .22f : 0; water.volume = audible ? .25f : 0; }
        private void OnApplicationFocus(bool value) { focused = value; if (rain != null) ApplyVolume(); }
        private void OnApplicationPause(bool value) { focused = !value; if (rain != null) ApplyVolume(); }
        public void Cup() { if (!Muted) effects.PlayOneShot(cupClip); }
        public void Chime() { if (!Muted) { effects.Stop(); effects.PlayOneShot(chimeClip); } }
        public void Pour() { water.clip = pourClip; water.Play(); }
        public void StopPour() => water.Stop();
        private static AudioClip Noise(string name, float seconds, int seed, bool pour)
        {
            const int rate = 24000; var data = new float[(int)(seconds * rate)]; var random = new System.Random(seed);
            double low = 0, slow = 0;
            for (int i = 0; i < data.Length; i++) {
                double n = random.NextDouble() * 2 - 1; low = low * .92 + n * .08; slow = slow * .999 + n * .001;
                double time = (double)i / rate;
                double texture = low * 1.9 + n * .10 + slow * 3;
                if (pour) texture += Math.Sin(time * (800 + 370 * time)) * .045;
                double envelope = pour ? Math.Min(1, time * 12) * Math.Min(1, (seconds - time) * 6) : .82 + .13 * Math.Sin(time * Math.PI * 2 / seconds);
                data[i] = (float)(texture * envelope * .65);
            }
            // A short circular blend removes the discontinuity at the ambience loop seam.
            if (!pour) {
                int fade = rate / 4;
                for (int i = 0; i < fade; i++) { float t = (float)i / fade; data[data.Length - fade + i] = data[data.Length - fade + i] * (1 - t) + data[i] * t; }
                var loop = new float[data.Length - fade]; Array.Copy(data, fade, loop, 0, loop.Length); data = loop;
            }
            return Clip(name, data, rate);
        }
        private static AudioClip Tone(string name, float seconds, double f1, double f2)
        {
            const int rate = 24000; var data = new float[(int)(rate * seconds)];
            for (int i = 0; i < data.Length; i++) {
                double t = (double)i / rate;
                data[i] = (float)((Math.Sin(2 * Math.PI * f1 * t) + .27 * Math.Sin(2 * Math.PI * f2 * t)) * Math.Exp(-t * 5 / seconds) * Math.Min(1, t * 180) * .24 * Math.Min(1, (seconds - t) * 80));
            }
            return Clip(name, data, rate);
        }
        private static AudioClip Clip(string name, float[] data, int rate) { var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip; }
        private void OnDestroy() { foreach (var clip in new[] { rainClip, cupClip, chimeClip, pourClip }) if (clip != null) Destroy(clip); }
    }
}
