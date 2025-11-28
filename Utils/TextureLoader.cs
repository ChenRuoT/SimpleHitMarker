using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleHitMarker
{
 // �򵥵�������������֧�ִӸ���·�����ص��Ż����ͼƬ��
 //ʹ�÷������ Unity �� LoadImage��ʵ���� ImageConversion ��̬���Լ��ݲ�ͬ����ʱ��
 public static class TextureLoader
 {
 //���Լ��ص����ļ�·��Ϊ Texture2D������ null���ʧ��
 public static Texture2D LoadTextureFromFile(string path)
 {
 if (string.IsNullOrEmpty(path)) return null;
 try
 {
 if (!File.Exists(path))
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: file not found: {path}");
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
 Plugin.Log?.LogInfo($"[SimpleHitMarker] TextureLoader: loaded texture from {path}");
 return tex;
 }
 else
 {
 Plugin.Log?.LogWarning($"[SimpleHitMarker] TextureLoader: failed to decode image data: {path}");
 try { UnityEngine.Object.Destroy(tex); } catch { }
 return null;
 }
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogError($"[SimpleHitMarker] TextureLoader exception loading {path}: {ex}");
 return null;
 }
 }

 //ͨ��������Ŀ¼���ļ����б��������أ��ļ������԰�����·����
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

 // �Ӳ��Ŀ¼�µ����ļ��м�������ͼƬ��png/jpg���������ֵ䣨�ļ���->Texture2D��
 public static Dictionary<string, Texture2D> LoadAllTexturesInSubfolder(string subfolder)
 {
 try
 {
 string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
 string folder = Path.Combine(assemblyDir, subfolder ?? string.Empty);
 if (!Directory.Exists(folder))
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: subfolder not found: {folder}");
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
 Plugin.Log?.LogError($"[SimpleHitMarker] TextureLoader: error scanning subfolder {subfolder}: {ex}");
 return new Dictionary<string, Texture2D>();
 }
 }

 // internal helpers
 private static bool TryLoadImageInstance(Texture2D tex, byte[] bytes, out bool called)
 {
 called = false;
 try
 {
 // More robust: find any instance method named "LoadImage" and try to invoke it with sensible args
 var methods = typeof(Texture2D).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
 .Where(m => string.Equals(m.Name, "LoadImage", StringComparison.OrdinalIgnoreCase)).ToArray();
 if (methods.Length ==0)
 {
 Plugin.Log?.LogDebug("[SimpleHitMarker] TextureLoader: no instance LoadImage methods found on Texture2D.");
 return false;
 }

 foreach (var method in methods)
 {
 try
 {
 var pars = method.GetParameters();
 // We expect first parameter to accept byte[] (or something assignable from it)
 if (pars.Length ==0)
 continue;
 var p0 = pars[0].ParameterType;
 if (!p0.IsAssignableFrom(typeof(byte[])) && p0 != typeof(object))
 {
 // skip unlikely overloads
 continue;
 }
 called = true;
 object[] args = null;
 if (pars.Length ==1)
 {
 args = new object[] { bytes };
 }
 else if (pars.Length >=2)
 {
 // try to supply a sensible second argument if it's a bool (commonly markNonReadable)
 var p1 = pars[1].ParameterType;
 if (p1 == typeof(bool))
 args = new object[] { bytes, false };
 else
 args = new object[] { bytes, GetDefault(p1) };
 }
 else
 {
 args = new object[] { bytes };
 }
 var res = method.Invoke(tex, args);
 if (res is bool b && b)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: instance LoadImage succeeded using method: {method}");
 return true;
 }
 // Some overloads might return void; try checking texture size as a proxy
 if (method.ReturnType == typeof(void))
 {
 if (tex.width >0 && tex.height >0)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: instance LoadImage (void) seemed to succeed using method: {method}");
 return true;
 }
 }
 }
 catch (TargetInvocationException tie)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: instance LoadImage invocation error: {tie.InnerException ?? tie}");
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: instance LoadImage error: {ex}");
 }
 }
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: instance LoadImage reflection error: {ex}");
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
 Plugin.Log?.LogDebug("[SimpleHitMarker] TextureLoader: ImageConversion type not found in loaded assemblies.");
 // As a last-ditch attempt, try direct reference if available at compile time
 try
 {
 // Some Unity versions expose ImageConversion in UnityEngine namespace directly
 var direct = Type.GetType("UnityEngine.ImageConversion, UnityEngine");
 if (direct != null) imgConvType = direct;
 }
 catch { }
 if (imgConvType == null) return false;
 }

 // find any static method named LoadImage that looks like LoadImage(Texture2D, byte[], [bool])
 var methods = imgConvType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
 .Where(m => string.Equals(m.Name, "LoadImage", StringComparison.OrdinalIgnoreCase)).ToArray();
 if (methods.Length ==0)
 {
 Plugin.Log?.LogDebug("[SimpleHitMarker] TextureLoader: no static LoadImage methods found on ImageConversion.");
 return false;
 }

 foreach (var m in methods)
 {
 try
 {
 var pars = m.GetParameters();
 if (pars.Length ==0) continue;
 // first param should be Texture2D or object
 if (!pars[0].ParameterType.IsAssignableFrom(typeof(Texture2D)) && pars[0].ParameterType != typeof(object)) continue;
 // second param should accept byte[]
 if (pars.Length >=2)
 {
 if (!pars[1].ParameterType.IsAssignableFrom(typeof(byte[])) && pars[1].ParameterType != typeof(object)) continue;
 }
 called = true;
 object[] args = null;
 if (pars.Length ==2)
 {
 args = new object[] { tex, bytes };
 }
 else if (pars.Length >=3)
 {
 var p2 = pars[2].ParameterType;
 if (p2 == typeof(bool))
 args = new object[] { tex, bytes, false };
 else
 args = new object[] { tex, bytes, GetDefault(p2) };
 }
 else
 {
 // unusual signature, try passing two args
 args = new object[] { tex, bytes };
 }
 var res = m.Invoke(null, args);
 if (res is bool br && br)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: ImageConversion.LoadImage succeeded using method: {m}");
 return true;
 }
 if (m.ReturnType == typeof(void))
 {
 if (tex.width >0 && tex.height >0)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: ImageConversion.LoadImage (void) seemed to succeed using method: {m}");
 return true;
 }
 }
 }
 catch (TargetInvocationException tie)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: ImageConversion invocation error: {tie.InnerException ?? tie}");
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: ImageConversion reflection error: {ex}");
 }
 }
 }
 catch (Exception ex)
 {
 Plugin.Log?.LogDebug($"[SimpleHitMarker] TextureLoader: ImageConversion reflection error: {ex}");
 }
 return false;
 }

 private static object GetDefault(Type t)
 {
 if (t.IsValueType) return Activator.CreateInstance(t);
 return null;
 }
 }
}
