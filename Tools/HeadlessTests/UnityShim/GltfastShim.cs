// GLTFast stand-in for the headless test harness.
//
// EngineModelLoader references glTFast to load .glb models at runtime. No edit-mode test loads a
// model (there is no .glb bundled), so this only needs to satisfy the compiler and fail safely if
// it is ever reached: Load() reports failure rather than pretending a model appeared.

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace GLTFast
{
    public class GltfImport : IDisposable
    {
        public bool LoadingDone { get; private set; }
        public bool LoadingError { get; private set; } = true;

        public Task<bool> Load(string url) => Load(url, null);

        public Task<bool> Load(string url, object importSettings)
        {
            LoadingDone = true;
            LoadingError = true;
            return Task.FromResult(false);
        }

        public Task<bool> LoadFile(string path) => Task.FromResult(false);

        public Task<bool> LoadGltfBinary(byte[] data, Uri uri = null) => Task.FromResult(false);

        public Task<bool> InstantiateMainSceneAsync(Transform parent) => Task.FromResult(false);

        public Task<bool> InstantiateSceneAsync(Transform parent, int sceneIndex = 0) => Task.FromResult(false);

        public void Dispose() { }
    }
}
