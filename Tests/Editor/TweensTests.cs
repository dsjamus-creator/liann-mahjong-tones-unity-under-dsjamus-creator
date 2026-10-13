using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LiANN.MahjongTones.Tests
{
    /// <summary>Regression tests for the tween runner (PR #1 DSJ-PC report, bug B). Unity-only: excluded from Tools~/CoreTests.</summary>
    public class TweensTests
    {
        float _now;
        Tweens Make() => new Tweens { Now = () => _now };

        [SetUp] public void ResetClock() => _now = 0f;   // NUnit reuses the fixture instance between tests

        [Test]
        public void StepsThenFinishesAndCallsDone()
        {
            var tw = Make();
            float last = -1; bool done = false;
            tw.Run(1f, t => last = t, () => done = true, t => t);
            _now = 0.5f; tw.Update();
            Assert.AreEqual(0.5f, last, 1e-4f);
            Assert.IsFalse(done);
            _now = 1.2f; tw.Update();
            Assert.AreEqual(1f, last, 1e-4f);
            Assert.IsTrue(done);
            Assert.AreEqual(0, tw.Count);
        }

        [Test]
        public void DoneThatClearsDoesNotBreakTheLoop()
        {
            var tw = Make();
            int otherSteps = 0;
            tw.Run(0.1f, _ => { }, () => tw.Clear(), t => t);   // e.g. win → EndRound → NewScreen
            tw.Run(5f, _ => otherSteps++, null, t => t);
            _now = 0.2f;
            Assert.DoesNotThrow(() => tw.Update());
            Assert.AreEqual(0, otherSteps, "a tween cleared earlier in the same frame must not run");
            Assert.AreEqual(0, tw.Count);
        }

        [Test]
        public void StepThatThrowsIsDroppedOnceAndOthersKeepRunning()
        {
            var tw = Make();
            int good = 0;
            tw.Run(1f, _ => throw new NullReferenceException("picker closed"), null, t => t);
            tw.Run(1f, _ => good++, null, t => t);
            LogAssert.Expect(LogType.Exception, new Regex("picker closed"));
            _now = 0.1f; tw.Update();
            _now = 0.2f; tw.Update();
            Assert.AreEqual(2, good);
            Assert.AreEqual(1, tw.Count, "the failing tween is removed after its first exception");
        }

        [Test]
        public void ReducedMotionJumpsToTheEnd()
        {
            var tw = Make();
            tw.ReducedMotion = true;
            float last = -1; bool done = false;
            tw.Run(1f, t => last = t, () => done = true);
            Assert.AreEqual(1f, last);
            Assert.IsTrue(done);
            Assert.AreEqual(0, tw.Count);
        }
    }
}
