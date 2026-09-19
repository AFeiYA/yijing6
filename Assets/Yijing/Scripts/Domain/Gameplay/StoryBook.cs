using System;
using System.IO;
using System.Linq;

namespace Yijing.Domain.Gameplay
{
    [Serializable]
    public sealed class StoryBeat
    {
        public string orderId, title, request, quietLabel, actLabel, quietResponse, actResponse, takeaway;
    }

    [Serializable]
    public sealed class StoryBook
    {
        public string chapterTitle, premise, arrival, lampBefore, lampAfter, ending, nextChapter;
        public StoryBeat[] beats;
        public void Validate()
        {
            if (new[] { chapterTitle, premise, arrival, lampBefore, lampAfter, ending, nextChapter }.Any(string.IsNullOrWhiteSpace) ||
                beats == null || beats.Length != 3) throw new InvalidDataException("Incomplete story book.");
            for (int i = 0; i < beats.Length; i++) {
                var b = beats[i];
                if (b == null || b.orderId != "E0" + (i + 1) ||
                    new[] { b.title, b.request, b.quietLabel, b.actLabel, b.quietResponse, b.actResponse, b.takeaway }.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException("Story order mapping is incomplete.");
            }
        }
    }
}
