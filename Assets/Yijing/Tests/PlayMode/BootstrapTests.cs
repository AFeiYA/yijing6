using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Yijing.Tests
{
    public sealed class BootstrapTests
    {
        [UnityTest]
        public IEnumerator BootstrapLoadsConfiguredGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            var deadline = Time.realtimeSinceStartup + 15;
            while (SceneManager.GetActiveScene().name != "Game" && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Game"));
            Assert.That(Camera.main, Is.Not.Null);
            Assert.That(Camera.main.orthographic, Is.True);
        }
    }
}
