using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
namespace RemixTable.Editor
{
    /// <summary>Runs the same validation in an already-open editor without closing the user's project.</summary>
    [InitializeOnLoad]
    public static class RemixZoomValidation
    {
        const string RequestPath="Logs/RemixZoomValidation.request";
        const string ModeKey="RemixZoomValidation.Mode";
        static double nextPoll;
        static RemixZoomValidation()
        {
            TestRunnerApi.RegisterTestCallback(new Results());
            EditorApplication.update+=Poll;
        }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll) return;
            nextPoll=EditorApplication.timeSinceStartup+.5;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            string command=File.ReadAllText(RequestPath).Trim();File.Delete(RequestPath);
            try
            {
                for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                    if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the open scene before validation; unsaved changes have been preserved.");
                if(command=="scene")
                {
                    RemixSeatedLayout.ApplyZoom();
                    File.WriteAllText("Logs/RemixZoomSceneValidation.txt","COMPILATION_AND_SCENE_VALIDATION_PASS");
                }
                else if(command=="edit" || command=="play") Run(command);
                else throw new InvalidOperationException("Expected scene, edit or play.");
            }
            catch(Exception error) { File.WriteAllText("Logs/RemixZoomValidationError.txt",error.ToString());Debug.LogException(error); }
        }
        static void Run(string mode)
        {
            SessionState.SetString(ModeKey,mode);
            var api=ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter{testMode=mode=="edit"?TestMode.EditMode:TestMode.PlayMode,assemblyNames=new[]{mode=="edit"?"RemixTable.EditMode":"RemixTable.PlayMode"}}));
        }
        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string mode=SessionState.GetString(ModeKey,"");if(mode=="") return;
                TestRunnerApi.SaveResultToFile(result,mode=="edit"?"Logs/RemixZoomEditMode.xml":"Logs/RemixZoomPlayMode.xml");
                File.WriteAllText("Logs/RemixZoom"+mode+"Result.txt",result.ResultState+" passed="+result.PassCount+" failed="+result.FailCount);
                SessionState.EraseString(ModeKey);
            }
        }
    }
}
