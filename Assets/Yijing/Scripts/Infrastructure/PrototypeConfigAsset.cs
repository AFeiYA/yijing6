using UnityEngine;
using Yijing.Domain.Configuration;

namespace Yijing.Infrastructure
{
    public sealed class PrototypeConfigAsset : ScriptableObject
    {
        [SerializeField] private string sourceSha256;
        [SerializeField] private PrototypeConfig config;

        public string SourceSha256 => sourceSha256;
        // Runtime callers get an independent snapshot; never mutate the imported asset.
        public PrototypeConfig CreateSnapshot()
        {
            return JsonUtility.FromJson<PrototypeConfig>(JsonUtility.ToJson(config));
        }

#if UNITY_EDITOR
        public void SetImportedData(PrototypeConfig data, string sha256)
        {
            ConfigValidator.Validate(data);
            config = data;
            sourceSha256 = sha256;
        }
#endif
    }
}
