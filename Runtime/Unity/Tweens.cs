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

        /// <summary>Clock in seconds; tests replace it.</summary>
        public Func<float> Now = () => Time.unscaledTime;

        public int Count => _live.Count;

        public static float EaseOutCubic(float t) => 1 - Mathf.Pow(1 - t, 3);
        public static float EaseInOut(float t) => t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        public static float EaseOutBack(float t) { const float c1 = 1.70158f, c3 = c1 + 1; return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2); }

        /// <summary>Animate 0→1 over <paramref name="seconds"/>; with reduced motion the step jumps to the end.</summary>
        public void Run(float seconds, Action<float> step, Action done = null, Func<float, float> ease = null)
        {
            if (ReducedMotion || seconds <= 0) { step(1); done?.Invoke(); return; }
            _live.Add(new Tween { Start = Now(), Duration = seconds, Step = step, Done = done, Ease = ease ?? EaseOutCubic });
        }

        public void Delay(float seconds, Action then) => Run(seconds, _ => { }, then, t => t);

        /// <summary>
        /// Advance every live tween once. Iterates a snapshot, so Run/Clear from inside a callback is safe; a tween whose
        /// step throws is dropped (and logged) instead of throwing again every frame.
        /// </summary>
        public void Update()
        {
            if (_live.Count == 0) return;
            float now = Now();
            foreach (var tw in _live.ToArray())
            {
                if (!_live.Contains(tw)) continue;   // removed by Clear() in an earlier callback this frame
                float t = Mathf.Clamp01((now - tw.Start) / tw.Duration);
                try { tw.Step(tw.Ease(t)); }
                catch (Exception e) { _live.Remove(tw); Debug.LogException(e); continue; }
                if (t >= 1)
                {
                    _live.Remove(tw);
                    try { tw.Done?.Invoke(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        public void Clear() => _live.Clear();
    }
}
