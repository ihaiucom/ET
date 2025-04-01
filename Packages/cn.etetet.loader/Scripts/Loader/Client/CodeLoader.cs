using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HybridCLR;
using UnityEngine;

namespace ET
{
    public class CodeLoader: Singleton<CodeLoader>, ISingletonAwake
    {
        private Assembly modelAssembly;
        private Assembly modelViewAssembly;

        private Dictionary<string, TextAsset> dlls;
        private Dictionary<string, TextAsset> aotDlls;
        private bool enableDll;

        public void Awake()
        {
            if (Define.IsEditor)
            {
                this.enableDll = Resources.Load<GlobalConfig>("GlobalConfig").EnableDll;
            }
            else
            {
                this.enableDll = true;
            }
        }

        private async ETTask DownloadAsync()
        {
            if (!Define.IsEditor && enableDll)
            {
                this.dlls = await ResourcesComponent.Instance.LoadAllAssetsAsync<TextAsset>($"Packages/cn.etetet.loader/Bundles/Code/ET.Model.dll.bytes");
                if (Define.EnableIL2CPP)
                {
                    this.aotDlls = await ResourcesComponent.Instance.LoadAllAssetsAsync<TextAsset>($"Packages/cn.etetet.loader/Bundles/AotDlls/mscorlib.dll.bytes");
                    if (this.aotDlls == null)
                    {
                        Debug.LogError("aotDlls is null");
                    }
                }
            }
        }

        public async ETTask Start()
        {
            Assembly hotfixAssembly = null; Assembly hotfixViewAssembly = null;
            if (enableDll)
            {
                await DownloadAsync();
                LoadModel();
                ( hotfixAssembly,  hotfixViewAssembly) = this.LoadHotfix();
            }
            else
            {
#if !UNITY_WEBGL
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    string assemblyName = assembly.GetName().Name;
                    Debug.Log(assemblyName);
                    if (assemblyName == "ET.Model")
                    {
                        this.modelAssembly = assembly;
                    }
                    if (assemblyName == "ET.ModelView")
                    {
                        this.modelViewAssembly = assembly;
                    }
                    else if (assemblyName == "ET.Hotfix")
                    {
                        hotfixAssembly = assembly;
                    }   
                    else if (assemblyName == "ET.HotfixView")
                    {
                        hotfixViewAssembly = assembly;
                    }
                }
                
#endif
            }

            

            World.Instance.AddSingleton<CodeTypes, Assembly[]>(new[]
            {
                typeof (World).Assembly, typeof (Init).Assembly, this.modelAssembly, this.modelViewAssembly, hotfixAssembly,
                hotfixViewAssembly
            });

            IStaticMethod start = new StaticMethod(this.modelAssembly, "ET.Entry", "Start");
            start.Run();
        }

        private (Assembly, Assembly) LoadModel()
        {
            
            if (!Define.IsEditor)
            {
                byte[] modelAssBytes = this.dlls["ET.Model.dll"].bytes;
                byte[] modelPdbBytes = this.dlls["ET.Model.pdb"].bytes;
                byte[] modelViewAssBytes = this.dlls["ET.ModelView.dll"].bytes;
                byte[] modelViewPdbBytes = this.dlls["ET.ModelView.pdb"].bytes;
                // 如果需要测试，可替换成下面注释的代码直接加载Packages/cn.etetet.loader/Code/ET.Model.dll.bytes，但真正打包时必须使用上面的代码
                //modelAssBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.Model.dll.bytes"));
                //modelPdbBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.Model.pdb.bytes"));
                //modelViewAssBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.ModelView.dll.bytes"));
                //modelViewPdbBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.ModelView.pdb.bytes"));

                if (Define.EnableIL2CPP)
                {
                    if (this.aotDlls != null)
                    {
                        foreach (var kv in this.aotDlls)
                        {
                            if(kv.Value == null) continue;
                            TextAsset textAsset = kv.Value;
                            RuntimeApi.LoadMetadataForAOTAssembly(textAsset.bytes, HomologousImageMode.SuperSet);
                        }
                    }

                }
                this.modelAssembly = Assembly.Load(modelAssBytes, modelPdbBytes);
                this.modelViewAssembly = Assembly.Load(modelViewAssBytes, modelViewPdbBytes);
            }
            else
            {
                byte[] modelAssBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.Model.dll.bytes"));
                byte[] modelPdbBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.Model.pdb.bytes"));
                byte[] modelViewAssBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.ModelView.dll.bytes"));
                byte[] modelViewPdbBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.ModelView.pdb.bytes"));
                this.modelAssembly = Assembly.Load(modelAssBytes, modelPdbBytes);
                this.modelViewAssembly = Assembly.Load(modelViewAssBytes, modelViewPdbBytes);
            }
            return (this.modelAssembly, this.modelViewAssembly);
        }

        private (Assembly, Assembly) LoadHotfix()
        {
            byte[] hotfixAssBytes;
            byte[] hotfixPdbBytes;
            byte[] hotfixViewAssBytes;
            byte[] hotfixViewPdbBytes;
            Assembly hotfixAssembly = null;
            Assembly hotfixViewAssembly = null;
            if (!Define.IsEditor)
            {
                hotfixAssBytes = this.dlls["ET.Hotfix.dll"].bytes;
                hotfixPdbBytes = this.dlls["ET.Hotfix.pdb"].bytes;
                hotfixViewAssBytes = this.dlls["ET.HotfixView.dll"].bytes;
                hotfixViewPdbBytes = this.dlls["ET.HotfixView.pdb"].bytes;
                // 如果需要测试，可替换成下面注释的代码直接加载Packages/cn.etetet.loader/Code/Hotfix.dll.bytes，但真正打包时必须使用上面的代码
                //hotfixAssBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.Hotfix.dll.bytes"));
                //hotfixPdbBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.Hotfix.pdb.bytes"));
                //hotfixViewAssBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.HotfixView.dll.bytes"));
                //hotfixViewPdbBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.HotfixView.pdb.bytes"));
                hotfixAssembly = Assembly.Load(hotfixAssBytes, hotfixPdbBytes);
                hotfixViewAssembly = Assembly.Load(hotfixViewAssBytes, hotfixViewPdbBytes);
            }
            else
            {
                hotfixAssBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.Hotfix.dll.bytes"));
                hotfixPdbBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.Hotfix.pdb.bytes"));
                hotfixViewAssBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.HotfixView.dll.bytes"));
                hotfixViewPdbBytes = File.ReadAllBytes(Path.Combine(Define.CodeDir, "ET.HotfixView.pdb.bytes"));
                hotfixAssembly = Assembly.Load(hotfixAssBytes, hotfixPdbBytes);
                hotfixViewAssembly = Assembly.Load(hotfixViewAssBytes, hotfixViewPdbBytes);
            }
            
            return (hotfixAssembly, hotfixViewAssembly);
        }

        public void Reload()
        {
            (Assembly hotfixAssembly, Assembly hotfixViewAssembly) = this.LoadHotfix();

            CodeTypes codeTypes = World.Instance.AddSingleton<CodeTypes, Assembly[]>(new[]
            {
                typeof (World).Assembly, typeof (Init).Assembly, this.modelAssembly, this.modelViewAssembly, hotfixAssembly,
                hotfixViewAssembly
            });
            codeTypes.CodeProcess();

            Log.Info($"reload dll finish!");
        }
    }
}