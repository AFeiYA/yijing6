using UnityEngine;
using UnityEngine.SceneManagement;
using Yijing.Domain.Configuration;
using Yijing.Infrastructure;

namespace Yijing.Application
{
    public sealed class Bootstrap : MonoBehaviour
    {
        [SerializeField] private PrototypeConfigAsset configuration;

        private void Start()
        {
            if (configuration == null)
                throw new System.InvalidOperationException("Bootstrap configuration is not assigned.");
            var snapshot = configuration.CreateSnapshot();
            ConfigValidator.Validate(snapshot);
            UnityEngine.Application.targetFrameRate = 60;
            Debug.Log($"Yijing configuration {snapshot.contentVersion} validated; loading Game.");
            SceneManager.LoadSceneAsync("Game");
        }
    }
}
