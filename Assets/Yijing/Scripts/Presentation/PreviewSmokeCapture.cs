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
            UnityEngine.Application.runInBackground = true;
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
            StartCoroutine(Watchdog());
            yield return Capture("intro.png"); Click("intro_next");
            yield return Capture("arrival.png"); Click("intro_next");
            presenter.Produce("tea"); presenter.Produce("tea");
            yield return Capture("merge-guide.png");
            presenter.SelectCell(0); presenter.SelectCell(1);
            yield return Capture("choice-guide.png"); Click("choice_understood"); Click("choice_quiet");
            yield return Capture("tea-table-start.png");
            for (int order = 0; order < 3; order++) {
                if (order == 2) Click("choice_act");
                foreach (var need in presenter.Session.Order(0).variants[order == 2 ? 1 : 0].requirements) {
                    var item = presenter.Session.Item(need.itemId);
                    while (presenter.Session.Snapshot.board.Count(x => x.itemId == item.id) < need.quantity) MakeTea(presenter, item.chainId, item.tier);
                }
                if (order == 0) yield return Capture("tea-table-ready.png");
                Click("deliver"); yield return null;
                if (order == 0) yield return Capture("delivery.png");
                Click("confirm_delivery"); yield return null;
                yield return Capture("story-response-" + order + ".png"); Click("response_continue"); yield return null;
                if (order == 0) { yield return Capture("lamp-story.png"); Click("repair_lamp"); yield return null; Click("lamp_continue"); }
                if (order < 2) Click("story_begin");
                else { yield return Capture("chapter-ending.png"); Click("ending_continue"); }
            }
            yield return Capture("oracle-qian-15.png");
            File.WriteAllText(Path.Combine(Output, "smoke-result.txt"), "orders=" + presenter.Session.Snapshot.completedStory + "\noracle=" + presenter.Session.Today.oracleKey);
            UnityEngine.Application.Quit(0);
        }
        private static void Click(string name) => GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
        private static void MakeTea(GamePresenter presenter, string chain, int tier)
        {
            for (int i = 0; i < (1 << (tier - 1)); i++) presenter.Produce(chain);
            for (int attempt = 0; attempt < 32; attempt++) {
                var pair = presenter.Session.Snapshot.board.Select((x, i) => new { slot = x, index = i })
                    .Where(x => !x.slot.Empty && x.slot.itemId.StartsWith(chain) && presenter.Session.Item(x.slot.itemId).tier < tier)
                    .GroupBy(x => x.slot.itemId).FirstOrDefault(x => x.Count() >= 2)?.Take(2).ToArray();
                if (pair == null) break;
                long revision = presenter.Session.Snapshot.revision;
                presenter.SelectCell(pair[0].index); presenter.SelectCell(pair[1].index);
                if (presenter.Session.Snapshot.revision == revision) throw new InvalidOperationException("Smoke merge made no progress.");
            }
        }
        private static IEnumerator Watchdog() { yield return new WaitForSecondsRealtime(60); UnityEngine.Application.Quit(4); }
        private static IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.25f);
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(Output, name);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 10;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!File.Exists(path)) { Debug.LogError("Visual smoke capture timed out: " + path); UnityEngine.Application.Quit(3); }
        }
    }
}
#endif
