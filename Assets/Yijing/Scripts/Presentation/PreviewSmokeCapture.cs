#if DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Yijing.Presentation
{
    // Explicit CLI smoke check for development builds only. Never touches a player's save.
    public sealed class PreviewSmokeCapture : MonoBehaviour
    {
        public static readonly string Output = Argument("--yijing-smoke-output");
        public static bool Enabled => !string.IsNullOrEmpty(Output);
        public static readonly string IsolatedSavePath = Path.Combine(Path.GetTempPath(), "yijing-visual-" + Guid.NewGuid().ToString("N"), "save.json");
        private static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!Enabled) return;
            var host = new GameObject("CLI Visual Smoke"); DontDestroyOnLoad(host); host.AddComponent<PreviewSmokeCapture>();
        }
        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 30;
            GamePresenter presenter = null;
            while ((presenter == null || presenter.Session == null) && Time.realtimeSinceStartup < deadline) {
                presenter = FindFirstObjectByType<GamePresenter>(); yield return null;
            }
            if (presenter == null || presenter.Session == null) { UnityEngine.Application.Quit(2); yield break; }
            Directory.CreateDirectory(Output);
            yield return Capture("tea-table-start.png");
            for (int order = 0; order < 3; order++) {
                MakeTea(presenter, "tea"); MakeTea(presenter, order == 2 ? "ceramic" : "tea");
                if (order == 0) yield return Capture("tea-table-ready.png");
                if (order == 2) Click("choice_act");
                Click("deliver"); yield return null;
                if (order == 0) yield return Capture("delivery.png");
                Click("confirm_delivery"); yield return null; Click("response_continue"); yield return null;
            }
            yield return Capture("oracle-qian-15.png");
            File.WriteAllText(Path.Combine(Output, "smoke-result.txt"), "orders=" + presenter.Session.Snapshot.completedStory + "\noracle=" + presenter.Session.Today.oracleKey);
            UnityEngine.Application.Quit(0);
        }
        private static void Click(string name) => GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
        private static void MakeTea(GamePresenter presenter, string chain)
        {
            for (int i = 0; i < 4; i++) presenter.Produce(chain);
            while (true) {
                var pair = presenter.Session.Snapshot.board.Select((x, i) => new { slot = x, index = i })
                    .Where(x => !x.slot.Empty && x.slot.itemId.StartsWith(chain) && presenter.Session.Item(x.slot.itemId).tier < 3)
                    .GroupBy(x => x.slot.itemId).FirstOrDefault(x => x.Count() >= 2)?.Take(2).ToArray();
                if (pair == null) break;
                presenter.SelectCell(pair[0].index); presenter.SelectCell(pair[1].index);
            }
        }
        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(Output, name); ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 10;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!File.Exists(path)) { Debug.LogError("Visual smoke capture timed out: " + path); UnityEngine.Application.Quit(3); }
        }
    }
}
#endif
