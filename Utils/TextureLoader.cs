using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleHitMarker
{
 // 简单的纹理加载器，支持从给定路径加载单张或多张图片，
 // 使用反射调用 Unity 的 LoadImage（实例或 ImageConversion 静态）以兼容不同运行时。
 public static class TextureLoader
 {
 // 尝试加载单个文件路径为 Texture2D，返回 null 如果失败
 public static Texture2D LoadTextureFromFile(string path)
 {
 if (string.IsNullOrEmpty(path)) return null;
 try
 {
 if (!File.Exists(path))
 {
 Plugin.Log?.LogDebug($"[shm] TextureLoader: file not found: {path}");
 return null;
 }

 byte[] bytes = File.ReadAllBytes(path);
 Texture2D tex = new Texture2D(2,2, TextureFormat.ARGB32, false);

 bool loaded = TryLoadImageInstance(tex, bytes, out bool instanceCalled);
 if (!loaded)
 {
 loaded = TryLoadImageViaImageConversion(tex, bytes, out bool staticCalled);
 }

 if (loaded)
 {
 tex.Apply();
 Plugin.Log?.LogInfo($"[shm] TextureLoader: loaded texture from {path}");
 return tex;
 }
 else
 {
 Plugin.Log?.LogWarning($"[shm] TextureLoader: failed to decode image data: {path}");
 try { UnityEngine.Object.Destroy(tex); } catch { }
 return null;
 }
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogError($"[shm] TextureLoader exception loading {path}: {ex}");
 return null;
 }
 }

 //通过给定的目录和文件名列表批量加载（文件名可以包含子路径）
 public static Dictionary<string, Texture2D> LoadTexturesFromPaths(IEnumerable<string> paths)
 {
 var dict = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
 if (paths == null) return dict;
 foreach (var p in paths)
 {
 try
 {
 var tex = LoadTextureFromFile(p);
 if (tex != null)
 dict[p] = tex;
 }
 catch { }
 }
 return dict;
 }

 // 从插件目录下的子文件夹加载所有图片（png/jpg）并返回字典（文件名->Texture2D）
 public static Dictionary<string, Texture2D> LoadAllTexturesInSubfolder(string subfolder)
 {
 try
 {
 string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
 string folder = Path.Combine(assemblyDir, subfolder ?? string.Empty);
 if (!Directory.Exists(folder))
 {
 Plugin.Log?.LogDebug($"[shm] TextureLoader: subfolder not found: {folder}");
 return new Dictionary<string, Texture2D>();
 }

 var exts = new[] { "*.png", "*.jpg", "*.jpeg" };
 var files = exts.SelectMany(e => Directory.GetFiles(folder, e, SearchOption.TopDirectoryOnly)).ToArray();
 var dict = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
 foreach (var f in files)
 {
 var tex = LoadTextureFromFile(f);
 if (tex != null) dict[Path.GetFileName(f)] = tex;
 }
 return dict;
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogError($"[shm] TextureLoader: error scanning subfolder {subfolder}: {ex}");
 return new Dictionary<string, Texture2D>();
 }
 }

 // internal helpers
 private static bool TryLoadImageInstance(Texture2D tex, byte[] bytes, out bool called)
 {
 called = false;
 try
 {
 // try signatures: LoadImage(byte[]), LoadImage(byte[], bool)
 var instanceMethod = typeof(Texture2D).GetMethod("LoadImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(byte[]) }, null);
 if (instanceMethod != null)
 {
 called = true;
 var res = instanceMethod.Invoke(tex, new object[] { bytes });
 if (res is bool b && b) return true;
 }

 var instanceMethod2 = typeof(Texture2D).GetMethod("LoadImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(byte[]), typeof(bool) }, null);
 if (instanceMethod2 != null)
 {
 called = true;
 var res = instanceMethod2.Invoke(tex, new object[] { bytes, false });
 if (res is bool b && b) return true;
 }
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogDebug($"[shm] TextureLoader: instance LoadImage reflection error: {ex}");
 }
 return false;
 }

 private static bool TryLoadImageViaImageConversion(Texture2D tex, byte[] bytes, out bool called)
 {
 called = false;
 try
 {
 // scan loaded assemblies for UnityEngine.ImageConversion
 Type imgConvType = null;
 foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
 {
 try
 {
 imgConvType = asm.GetType("UnityEngine.ImageConversion", false, false);
 if (imgConvType != null) break;
 }
 catch { }
 }
 if (imgConvType == null)
 {
 Plugin.Log?.LogDebug("[shm] TextureLoader: ImageConversion type not found in loaded assemblies.");
 return false;
 }

 // try signatures: static LoadImage(Texture2D, byte[]), LoadImage(Texture2D, byte[], bool)
 var m1 = imgConvType.GetMethod("LoadImage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Texture2D), typeof(byte[]) }, null);
 if (m1 != null)
 {
 called = true;
 var r = m1.Invoke(null, new object[] { tex, bytes });
 if (r is bool br && br) return true;
 }

 var m2 = imgConvType.GetMethod("LoadImage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Texture2D), typeof(byte[]), typeof(bool) }, null);
 if (m2 != null)
 {
 called = true;
 var r = m2.Invoke(null, new object[] { tex, bytes, false });
 if (r is bool br && br) return true;
 }
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogDebug($"[shm] TextureLoader: ImageConversion reflection error: {ex}");
 }
 return false;
 }
 }
}
