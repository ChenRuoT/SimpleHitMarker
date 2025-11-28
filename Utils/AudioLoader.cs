using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace SimpleHitMarker
{
    /// <summary>
    /// 音频加载器，用于从文件加载音频资源
    /// </summary>
    public static class AudioLoader
    {
        /// <summary>
        /// 从文件路径加载音频剪辑
        /// 支持 WAV 和 OGG 格式
        /// </summary>
        public static AudioClip LoadAudioFromFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                if (!File.Exists(path))
                {
                    Plugin.Log?.LogDebug($"[SimpleHitMarker] AudioLoader: file not found: {path}");
                    return null;
                }

                // 尝试使用 UnityWebRequestMultimedia.GetAudioClip（Unity 2018+）
                AudioClip clip = LoadAudioClipViaUnityWebRequest(path);
                if (clip != null)
                {
                    Plugin.Log?.LogInfo($"[SimpleHitMarker] AudioLoader: loaded audio from {path}");
                    return clip;
                }

                // 回退到 WWW 方法（旧版 Unity）
                clip = LoadAudioClipViaWWW(path);
                if (clip != null)
                {
                    Plugin.Log?.LogInfo($"[SimpleHitMarker] AudioLoader: loaded audio via WWW from {path}");
                    return clip;
                }

                Plugin.Log?.LogWarning($"[SimpleHitMarker] AudioLoader: failed to load audio from {path}");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SimpleHitMarker] AudioLoader exception loading {path}: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 使用 UnityWebRequestMultimedia 加载音频（推荐方法）
        /// </summary>
        private static AudioClip LoadAudioClipViaUnityWebRequest(string path)
        {
            try
            {
                // 查找 UnityWebRequestMultimedia 类型
                Type wwwType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        wwwType = asm.GetType("UnityEngine.Networking.UnityWebRequestMultimedia", false, false);
                        if (wwwType != null) break;
                    }
                    catch { }
                }

                if (wwwType == null)
                {
                    Plugin.Log?.LogDebug("[SimpleHitMarker] AudioLoader: UnityWebRequestMultimedia type not found");
                    return null;
                }

                // 调用 GetAudioClip 方法
                MethodInfo getAudioClipMethod = wwwType.GetMethod(
                    "GetAudioClip",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string), typeof(AudioType) },
                    null
                );

                if (getAudioClipMethod == null)
                {
                    Plugin.Log?.LogDebug("[SimpleHitMarker] AudioLoader: GetAudioClip overload not found on UnityWebRequestMultimedia");
                    return null;
                }

                // 确定音频类型
                AudioType audioType = AudioType.UNKNOWN;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".wav")
                {
                    audioType = AudioType.WAV;
                }
                else if (ext == ".ogg" || ext == ".ogv")
                {
                    audioType = AudioType.OGGVORBIS;
                }
                else if (ext == ".mp3")
                {
                    audioType = AudioType.MPEG;
                }

                if (audioType == AudioType.UNKNOWN)
                {
                    // 尝试根据文件头判断
                    audioType = DetectAudioType(path);
                }

                // 转换为 file:// URL
                string url = "file://" + path.Replace('\\', '/');

                // 调用静态方法
                var result = getAudioClipMethod.Invoke(null, new object[] { url, audioType });

                if (result == null)
                {
                    Plugin.Log?.LogDebug($"[SimpleHitMarker] AudioLoader: GetAudioClip returned null for {path}");
                    return null;
                }

                // 如果直接是 AudioClip（某些 Unity版本可能返回 AudioClip），直接返回
                if (result is AudioClip directClip)
                {
                    return directClip;
                }

                // 否则，可能返回一个 UnityWebRequest（多数 Unity版本）
                var resultType = result.GetType();
                if (resultType.FullName != null && resultType.FullName.Contains("UnityWebRequest"))
                {
                    Plugin.Log?.LogDebug($"[SimpleHitMarker] AudioLoader: GetAudioClip returned UnityWebRequest-like object: {resultType.FullName}");

                    // 调用 SendWebRequest 或 Send
                    MethodInfo sendMethod = resultType.GetMethod("SendWebRequest") ?? resultType.GetMethod("Send");
                    if (sendMethod != null)
                    {
                        try
                        {
                            var asyncOp = sendMethod.Invoke(result, null);

                            // 等待请求完成（有超时保护）
                            PropertyInfo isDoneProp = resultType.GetProperty("isDone");
                            int waited = 0;
                            const int timeoutMs = 10000; //10s timeout
                            while (true)
                            {
                                bool isDone = false;
                                if (isDoneProp != null)
                                {
                                    var val = isDoneProp.GetValue(result);
                                    if (val is bool b) isDone = b;
                                }

                                if (isDone) break;

                                System.Threading.Thread.Sleep(10);
                                waited += 10;
                                if (waited >= timeoutMs)
                                {
                                    Plugin.Log?.LogWarning($"[SimpleHitMarker] AudioLoader: UnityWebRequest timed out for {path}");
                                    break;
                                }
                            }

                            // 检查是否有下载处理器并尝试读取 audioClip 属性
                            PropertyInfo downloadHandlerProp = resultType.GetProperty("downloadHandler");
                            if (downloadHandlerProp != null)
                            {
                                var dh = downloadHandlerProp.GetValue(result);
                                if (dh != null)
                                {
                                    var dhType = dh.GetType();
                                    Plugin.Log?.LogDebug($"[SimpleHitMarker] AudioLoader: DownloadHandler type: {dhType.FullName}");
                                    
                                    PropertyInfo audioClipProp = dhType.GetProperty("audioClip");
                                    if (audioClipProp != null)
                                    {
                                        var loaded = audioClipProp.GetValue(dh) as AudioClip;
                                        if (loaded != null)
                                        {
                                            Plugin.Log?.LogInfo($"[SimpleHitMarker] AudioLoader: Successfully extracted AudioClip: {loaded.name}, length={loaded.length}");
                                            return loaded;
                                        }
                                        else
                                        {
                                            Plugin.Log?.LogWarning($"[SimpleHitMarker] AudioLoader: audioClip property returned null");
                                        }
                                    }
                                    else
                                    {
                                        Plugin.Log?.LogWarning($"[SimpleHitMarker] AudioLoader: DownloadHandler has no audioClip property");
                                    }

                                    //另外某些版本可能通过 DownloadHandler.GetContent 返回 AudioClip，但那是静态泛型方法在 UnityWebRequest中
                                    // 尝试通过 UnityEngine.Networking.DownloadHandlerAudioClip 类型的属性回退已在上面处理
                                }
                                else
                                {
                                    Plugin.Log?.LogWarning($"[SimpleHitMarker] AudioLoader: downloadHandler is null");
                                }
                            }
                            else
                            {
                                Plugin.Log?.LogWarning($"[SimpleHitMarker] AudioLoader: UnityWebRequest has no downloadHandler property");
                            }

                            // 如果到了这里仍未获取到 AudioClip，尝试读取 "error" 或 "isNetworkError" 来记录信息
                            PropertyInfo errorProp = resultType.GetProperty("error");
                            if (errorProp != null)
                            {
                                var err = errorProp.GetValue(result) as string;
                                if (!string.IsNullOrEmpty(err))
                                {
                                    Plugin.Log?.LogWarning($"[SimpleHitMarker] AudioLoader: UnityWebRequest error for {path}: {err}");
                                }
                            }

                            return null;
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log?.LogDebug($"[SimpleHitMarker] AudioLoader: exception while sending UnityWebRequest: {ex}");
                            return null;
                        }
                    }
                    else
                    {
                        Plugin.Log?.LogDebug("[SimpleHitMarker] AudioLoader: UnityWebRequest has no SendWebRequest/Send method");
                        return null;
                    }
                }

                Plugin.Log?.LogDebug($"[SimpleHitMarker] AudioLoader: GetAudioClip returned unsupported type: {resultType.FullName}");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SimpleHitMarker] AudioLoader: UnityWebRequest method failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 使用 WWW 加载音频（兼容旧版 Unity）
        /// </summary>
        private static AudioClip LoadAudioClipViaWWW(string path)
        {
            try
            {
                Type wwwType = typeof(WWW);
                if (wwwType == null)
                {
                    return null;
                }

                // 转换为 file:// URL
                string url = "file://" + path.Replace('\\', '/');

                // 创建 WWW 实例（需要协程，这里简化处理）
                // 注意：WWW 需要异步加载，这里只是尝试同步方式
                // 实际使用中可能需要预加载音频
                return null; // WWW 需要协程，暂时返回 null
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SimpleHitMarker] AudioLoader: WWW method failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 检测音频文件类型
        /// </summary>
        private static AudioType DetectAudioType(string path)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    byte[] header = new byte[12];
                    int bytesRead = fs.Read(header, 0, 12);
                    if (bytesRead < 12)
                    {
                        return AudioType.UNKNOWN;
                    }

                    // WAV 文件头：RIFF...WAVE
                    if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                        header[8] == 0x57 && header[9] == 0x41 && header[10] == 0x56 && header[11] == 0x45)
                    {
                        return AudioType.WAV;
                    }

                    // OGG 文件头：OggS
                    if (header[0] == 0x4F && header[1] == 0x67 && header[2] == 0x67 && header[3] == 0x53)
                    {
                        return AudioType.OGGVORBIS;
                    }
                }
            }
            catch { }

            return AudioType.UNKNOWN;
        }
    }
}

