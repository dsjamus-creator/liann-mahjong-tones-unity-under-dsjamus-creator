using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiANN.MahjongTones
{
    /// <summary>Minimal tween runner for UI Toolkit styles; driven from MahjongTonesApp.Update.</summary>
    public sealed class Tweens
    {
        sealed class Tween
        {
            public float Start, Duration;
            public Action<float> Step;
            public Action Done;
            public Func<float, float> Ease;
        }

        readonly List<Tween> _live = new List<Tween>();
        public bool ReducedMotion;

        public static float EaseOutCubic(float t) => 1 - Mathf.Pow(1 - t, 3);
        public static float EaseInOut(float t) => t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        public static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }

        /// <summary>Animate 0→1 over <paramref name="seconds"/>; with reduced motion the step jumps to the end.</summary>
        public void Run(float seconds, Action<float> step, Action done = null, Func<float, float> ease = null)
        {
            if (ReducedMotion || seconds <= 0) { step(1); done?.Invoke(); return; }
            _live.Add(new Tween { Start = Time.unscaledTime, Duration = seconds, Step = step, Done = done, Ease = ease ?? EaseOutCubic });
        }

        public void Delay(float seconds, Action then) => Run(seconds, _ => { }, then, t => t);

        public void Update()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var tw = _live[i];
                float t = Mathf.Clamp01((Time.unscaledTime - tw.Start) / tw.Duration);
                tw.Step(tw.Ease(t));
                if (t >= 1) { _live.RemoveAt(i); tw.Done?.Invoke(); }
            }
        }

        public void Clear() => _live.Clear();
    }
}
