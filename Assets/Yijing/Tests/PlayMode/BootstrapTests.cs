using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System;
using System.IO;
using Yijing.Presentation;

namespace Yijing.Tests
{
    public sealed class BootstrapTests
    {
        private string save;
        [SetUp] public void IsolateSave()
        {
            save = Path.Combine(Path.GetTempPath(), "yijing-bootstrap-" + Guid.NewGuid().ToString("N"), "save.json");
            GamePresenter.SavePathForTesting = save;
        }
        [TearDown] public void Cleanup()
        {
            GamePresenter.SavePathForTesting = null;
            if (Directory.Exists(Path.GetDirectoryName(save))) Directory.Delete(Path.GetDirectoryName(save), true);
        }
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
            yield return null;
            Assert.That(UnityEngine.Object.FindFirstObjectByType<GamePresenter>().Session, Is.Not.Null);
        }
    }
}
