using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public class MixedRealityServiceEditor : EditorWindow
{
    string serviceName = "DefaultService";
    private const string ServiceCode =
@"using Microsoft.MixedReality.Toolkit;

public class {0} : BaseExtensionService, I{0}
{{
    public {0}(IMixedRealityServiceRegistrar registrar, string name = null, uint priority = 10, BaseMixedRealityProfile profile = null) : 
        base(registrar, name, priority, profile)
    {{
    }}
}}";

    private const string InterfaceCode =
@"interface I{0}
{{
}}";

    private const string ProfileCode =
@"using Microsoft.MixedReality.Toolkit;
using UnityEngine;

[MixedRealityServiceProfile(typeof(I{0}))]
public class {0}Profile : BaseMixedRealityProfile
{{
    public int NumberOfThings;
}}";

    [MenuItem("Mixed Reality Toolkit/Custom Service")]
    public static void ShowWindow()
    {
        GetWindow(typeof(MixedRealityServiceEditor));
    }

    void OnGUI()
    {
        // The actual window code goes here - read the service name
        GUILayout.Label(new GUIContent("Create a Custom Mixed Reality Extension Service", "custom service help"), EditorStyles.boldLabel);
        serviceName = EditorGUILayout.TextField("Service Name", serviceName);

        var implementationFilename = $"{serviceName}.cs";
        var interfaceName = $"I{implementationFilename}";
        var profileClassname = $"{serviceName}Profile.cs";
        var assetFilename = $"{serviceName}.asset";
        var folderName = $"{serviceName}Extensions";

        GUI.skin.label.wordWrap = true;

        EditorStyles.label.wordWrap = true;

        GUILayout.Label($"Pressing 'Create' will cause the following files to be created in the folder {folderName} :");
        GUILayout.Label($"{implementationFilename}");
        GUILayout.Label($"{interfaceName}");
        GUILayout.Label($"{profileClassname}");
        GUILayout.Label($"{assetFilename}");

        if (GUILayout.Button("Create"))
        {
            // Create a folder if name doesn't exist...
            string guid = AssetDatabase.CreateFolder("Assets", folderName);
            string newFolderPath = AssetDatabase.GUIDToAssetPath(guid);

            var interfaceCodeFilepath = newFolderPath + Path.DirectorySeparatorChar + interfaceName;
            string interfaceReplacedCode = ReplaceTokens(serviceName, interfaceCodeFilepath, InterfaceCode);

            var serviceCodeFilepath = newFolderPath + Path.DirectorySeparatorChar + implementationFilename;
            ReplaceTokens(serviceName, serviceCodeFilepath, ServiceCode);

            var profileClassCodeFilepath = newFolderPath + Path.DirectorySeparatorChar + profileClassname;
            var replacedCode = ReplaceTokens(serviceName, profileClassCodeFilepath, ProfileCode);
            var assembly = Compile(interfaceReplacedCode, replacedCode);
            var runtimeType = assembly.GetType(serviceName + "Profile");

            var so = CreateInstance(runtimeType);
            AssetDatabase.CreateAsset(so, "Assets" + Path.DirectorySeparatorChar +
                serviceName + "Extensions" + Path.DirectorySeparatorChar + assetFilename);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    public static Assembly Compile(params string[] sources)
    {
        var provider = new CSharpCodeProvider(new Dictionary<string, string> { { "CompilerVersion", "v5.0" } });
        var param = new CompilerParameters();

        param.ReferencedAssemblies.Add(typeof(Microsoft.MixedReality.Toolkit.MixedRealityServiceConfiguration).Assembly.Location);

        // System namespace for common types like collections.
        param.ReferencedAssemblies.Add("System.dll");

        // This contains methods from the Unity namespaces:
        param.ReferencedAssemblies.Add(typeof(GameObject).Assembly.Location);

        // Generate a dll in memory
        param.GenerateExecutable = false;
        param.GenerateInMemory = true;

        // Compile the source
        var result = provider.CompileAssemblyFromSource(param, sources);

        if (result.Errors.Count > 0)
        {
            var msg = new StringBuilder();
            foreach (CompilerError error in result.Errors)
            {
                msg.AppendFormat("Error ({0}): {1}\n", error.ErrorNumber, error.ErrorText);
                Debug.Log(msg);
            }
            throw new Exception(msg.ToString());
        }

        // Return the assembly
        return result.CompiledAssembly;
    }

    private string ReplaceTokens(string serviceName, string filePath, string codeTemplate)
    {
        string replacedCode = string.Format(codeTemplate, serviceName);
        using (FileStream fs = File.Create(filePath))
        using (StreamWriter sw = new StreamWriter(fs))
        {
            sw.Write(replacedCode);
            sw.Flush();
        }
        return replacedCode;
    }
}
