using Interstellar.Managers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using static Interstellar.Utilities.FileUtilities;

namespace Interstellar.Utilities
{
    public class AssetUtilities
    {
        private static AssetBundle assetBundle;
        private static void LoadAssetBundle()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{PluginInfo.ClientResourcePath}.interstellarmenu");
            if (stream != null)
                assetBundle = AssetBundle.LoadFromStream(stream);
            else
                LogManager.LogError("Failed to load assetbundle");
        }

        public static T LoadObject<T>(string assetName) where T : Object
        {
            if (assetBundle == null)
                LoadAssetBundle();

            T gameObject = Object.Instantiate(assetBundle.LoadAsset<T>(assetName));
            return gameObject;
        }

        public static T LoadAsset<T>(string assetName) where T : Object
        {
            if (assetBundle == null)
                LoadAssetBundle();

            T gameObject = assetBundle.LoadAsset(assetName) as T;
            return gameObject;
        }

        public static readonly Dictionary<string, AudioClip> audioFilePool = new Dictionary<string, AudioClip>();

        public static void LoadSoundFromFile(string fileName, System.Action<AudioClip> onLoaded)
        {
            CoroutineManager.instance.StartCoroutine(Load());

            IEnumerator Load()
            {
                if (audioFilePool.TryGetValue(fileName, out var cached) && cached != null)
                {
                    onLoaded?.Invoke(cached);
                    yield break;
                }

                string filePath = GetMenuPath(fileName);
                string url = new System.Uri(Path.GetFullPath(filePath)).AbsoluteUri;
                var handler = new DownloadHandlerAudioClip(url, AudioType.UNKNOWN);

                using UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET, handler, null);

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    LogManager.LogError($"Failed to load audio file '{fileName}': {request.result}");
                    onLoaded?.Invoke(null);
                    yield break;
                }

                AudioClip clip = handler.audioClip;

                if (clip != null)
                    audioFilePool[fileName] = clip;

                onLoaded?.Invoke(clip);
            }
        }

        public static bool ExtractResource(string resourcePath, string fileName)
        {
            string filePath = GetMenuPath(fileName);
            if (File.Exists(filePath))
                return true;

            using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{PluginInfo.ClientResourcePath}.{resourcePath.Replace('/', '.')}");
            if (stream == null)
            {
                LogManager.LogError("Failed to load resource: " + resourcePath);
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            GetMenuPath(fileName);
            using FileStream file = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            stream.CopyTo(file);
            return true;
        }

        public static void LoadSoundFromResource(string resourcePath, System.Action<AudioClip> action = null)
        {
            if (!ExtractResource(resourcePath, resourcePath))
            {
                action?.Invoke(null);
                return;
            }

            if (action != null)
                LoadSoundFromFile(resourcePath, action);
        }

        public static readonly Dictionary<string, Texture2D> textureResourceDictionary = new Dictionary<string, Texture2D>();
        public static Texture2D LoadTextureFromResource(string resourcePath)
        {
            if (textureResourceDictionary.TryGetValue(resourcePath, out Texture2D existingTexture))
                return existingTexture;

            Texture2D texture = new Texture2D(2, 2);

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath);
            if (stream != null)
            {
                byte[] fileData = new byte[stream.Length];
                stream.Read(fileData, 0, (int)stream.Length);
                texture.LoadImage(fileData);
            }
            else
                LogManager.LogError("Failed to load texture from resource: " + resourcePath);

            textureResourceDictionary[resourcePath] = texture;

            return texture;
        }

        public static Rect OpaqueBounds(Texture2D texture)
        {
            try
            {
                Color32[] pixels = texture.GetPixels32();

                int minX = texture.width;
                int minY = texture.height;
                int maxX = -1;
                int maxY = -1;

                for (int y = 0; y < texture.height; y++)
                {
                    int row = y * texture.width;
                    for (int x = 0; x < texture.width; x++)
                    {
                        if (pixels[row + x].a <= 8)
                            continue;

                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }

                if (maxX >= minX && maxY >= minY)
                    return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            catch { }

            return new Rect(0f, 0f, texture.width, texture.height);
        }

    }
}
